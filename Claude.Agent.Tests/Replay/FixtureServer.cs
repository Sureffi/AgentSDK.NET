using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Claude.Agent.Tests.Replay;

internal sealed class FixtureServer : IAsyncDisposable
{
    // The fixtures some replay of this run wrote the whole recorded stdin of, line for line.
    // Architecture/Tripwire.cs reads it when the run ends.
    public static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> HeldToTheirStdin = new(StringComparer.Ordinal);

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan EofGrace = TimeSpan.FromSeconds(2);
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly TcpListener _listener;
    private readonly Script _script;
    private readonly Lock _gate = new();
    private readonly List<string> _written = [];
    private readonly TaskCompletionSource _stdinEnded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _paused = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _resumed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _hungUp = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _stopping = new();

    private TaskCompletionSource _wrote = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _answeredRequests;
    private bool _disconnected;
    private Task _play = Task.CompletedTask;
    private (string Name, string[] Lines)? _recorded;

    private FixtureServer(Script script)
    {
        _script = script;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        EndPoint = (IPEndPoint)_listener.LocalEndpoint;
    }

    public IPEndPoint EndPoint { get; }

    public Task Completed => _completed.Task;

    public Exception? Failure { get; private set; }

    public int ExitCode => _script.ExitCode;

    public bool StdinEnded => _stdinEnded.Task.IsCompleted;

    // Completes when the client's stdin ends.
    public Task StdinEnd => _stdinEnded.Task;

    // Completes when the play has ended the connection: the process's exit, as the client sees it.
    public Task HungUp => _hungUp.Task;

    // Completes when the play reaches its script's pause: every line before it is on the wire, and
    // none after it is until Resume.
    public Task Paused => _paused.Task;

    public ImmutableArray<string> Written
    {
        get
        {
            lock (_gate)
            {
                return [.. _written];
            }
        }
    }

    // A replay by name is held to the recording on the way out: every line the client wrote is the
    // recorded stdin's line at that place, request ids and the prompts' uuids apart. `stdinDiffers`
    // is for the replays that drive a fixture another way than it was recorded, and says so at the
    // call.
    public static FixtureServer Start(
        string fixtureName,
        bool endAfterLastLine = true,
        bool promptFirst = true,
        bool stdinDiffers = false,
        int pauseAfterResult = 0,
        int pauseAfterLine = 0)
    {
        var fixture = Fixture.Load(fixtureName);
        return Play(
            Script.OfFixture(fixture, awaitStdinEnd: !endAfterLastLine, promptFirst, pauseAfterResult, pauseAfterLine),
            stdinDiffers ? null : fixtureName);
    }

    // A script of a test's own plays a fixture's frames in its own order; naming the fixture holds
    // what the client writes to that fixture's stdin all the same.
    public static FixtureServer Play(Script script, string? heldTo = null)
    {
        var server = new FixtureServer(script);
        if (heldTo is not null)
        {
            server._recorded = (heldTo, Fixture.Load(heldTo).Stdin.Anonymised());
        }

        server._play = Task.Run(() => server.PlayAsync(server._stopping.Token));
        return server;
    }

    public void Resume() => _resumed.TrySetResult();

    public async Task WaitForWrites(int count, TimeSpan timeout)
    {
        var deadline = Task.Delay(timeout);
        while (true)
        {
            Task signalled;
            lock (_gate)
            {
                if (_written.Count >= count)
                {
                    return;
                }

                signalled = _wrote.Task;
            }

            if (await Task.WhenAny(signalled, deadline).ConfigureAwait(false) == deadline)
            {
                lock (_gate)
                {
                    if (_written.Count >= count)
                    {
                        return;
                    }

                    throw new TimeoutException(
                        $"the client wrote {_written.Count} lines, not {count}, within {timeout}.");
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        _listener.Stop();

        try
        {
            await _play.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _stopping.Dispose();

        if (_recorded is { } recorded)
        {
            var written = Written.Anonymised();
            for (var line = 0; line < written.Length; line++)
            {
                if (line >= recorded.Lines.Length || written[line] != recorded.Lines[line])
                {
                    throw new AssertFailedException(
                        $"{recorded.Name}: line {line + 1} written is not the recorded stdin.\n  wrote:    {written[line]}\n  recorded: {(line < recorded.Lines.Length ? recorded.Lines[line] : "(nothing)")}");
                }
            }

            if (written.Length == recorded.Lines.Length)
            {
                HeldToTheirStdin[recorded.Name] = 0;
            }
        }
    }

    private async Task PlayAsync(CancellationToken ct)
    {
        try
        {
            using var client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            _listener.Stop();

            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Utf8, leaveOpen: true);
            var receiving = Task.Run(() => ReceiveAsync(reader, ct), ct);

            try
            {
                await RunAsync(stream, client, ct).ConfigureAwait(false);
            }
            catch (StdinEndedException)
            {
                // The client closed its stdin with the script still waiting on it: as the binary
                // does at EOF, the play ends there.
            }

            if (!_disconnected)
            {
                await SettleAsync(ct).ConfigureAwait(false);
                Disconnect(client);
            }

            await receiving.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Failure = ex;
        }
        finally
        {
            _completed.TrySetResult();
        }
    }

    // HungUp is set before the socket is shut, so a client that has seen the connection end always
    // finds it set.
    private void Disconnect(TcpClient client)
    {
        _disconnected = true;
        _hungUp.TrySetResult();

        try
        {
            client.Client.Shutdown(SocketShutdown.Both);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
        }
    }

    // Nothing is played before the client's first line. Every recorded stdin opens with the
    // initialize request, and the client reads before it writes it: a line played at accept is on
    // the read loop while the initialize is still on its way to the write lock, and a control
    // request played there — the in-process server's initialize mcp_message — is answered on a
    // handler that can take the lock first.
    private async Task RunAsync(NetworkStream stream, TcpClient client, CancellationToken ct)
    {
        await AwaitWrittenAsync(written => written.Count > 0 ? written[0] : null, ct).ConfigureAwait(false);

        foreach (var step in _script.Steps)
        {
            switch (step)
            {
                case AnswerControlRequest answer:
                    await SendAsync(
                            stream,
                            Readdress(Rename(answer.Line), await NextClientRequestIdAsync(ct).ConfigureAwait(false)),
                            ct)
                        .ConfigureAwait(false);
                    break;

                case AwaitPrompt prompt:
                    await AwaitPromptAsync(prompt.Ordinal, ct).ConfigureAwait(false);
                    break;

                case AwaitClientRequest request:
                    await AwaitClientRequestAsync(request.Ordinal, ct).ConfigureAwait(false);
                    break;

                case AwaitWrites writes:
                    if (await AwaitWrittenAsync(written => written.Count >= writes.Count ? string.Empty : null, ct).ConfigureAwait(false) is null)
                    {
                        throw new InvalidOperationException(
                            $"the client wrote fewer than {writes.Count} lines, which the script's next line follows.");
                    }

                    break;

                case AwaitClientStdinEnd:
                    await _stdinEnded.Task.WaitAsync(Patience, ct).ConfigureAwait(false);
                    break;

                case EmitLine emit:
                    await SendAsync(stream, Rename(emit.Line), ct).ConfigureAwait(false);
                    break;

                case AwaitClientResponse expected:
                    await AwaitClientResponseAsync(expected.RequestId, ct).ConfigureAwait(false);
                    break;

                case HangUp:
                    Disconnect(client);
                    return;

                case Pause:
                    _paused.TrySetResult();
                    await Task.WhenAny(_resumed.Task, _stdinEnded.Task).WaitAsync(Patience, ct).ConfigureAwait(false);
                    if (!_resumed.Task.IsCompleted)
                    {
                        throw new StdinEndedException();
                    }

                    break;

                default:
                    throw new InvalidOperationException($"the script step {step} has no play.");
            }
        }
    }

    private static async Task SendAsync(NetworkStream stream, string line, CancellationToken ct)
    {
        await stream.WriteAsync(Utf8.GetBytes(line + "\n"), ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private async Task AwaitPromptAsync(int ordinal, CancellationToken ct)
    {
        var prompt = await AwaitWrittenAsync(
            written => written.Where(line => Fixture.TypeOf(line) == "user").ElementAtOrDefault(ordinal - 1),
            ct).ConfigureAwait(false);

        if (prompt is null)
        {
            throw new InvalidOperationException(
                $"the client never sent user message #{ordinal}, which the script's next line answers.");
        }
    }

    private async Task AwaitClientRequestAsync(int ordinal, CancellationToken ct)
    {
        var request = await AwaitWrittenAsync(
            written => written.Where(line => Fixture.TypeOf(line) == "control_request").ElementAtOrDefault(ordinal - 1),
            ct).ConfigureAwait(false);

        if (request is null)
        {
            throw new InvalidOperationException(
                $"the client never sent control request #{ordinal}, which the script's next line follows.");
        }
    }

    // Every recorded prompt's uuid in `line` becomes the uuid the client wrote for the prompt at the
    // same place; a prompt the client wrote without one keeps the recorded uuid.
    private string Rename(string line)
    {
        if (_script.Prompts.IsDefaultOrEmpty)
        {
            return line;
        }

        ImmutableArray<string?> written;
        lock (_gate)
        {
            written = Fixture.PromptUuids(_written);
        }

        for (var ordinal = 0; ordinal < _script.Prompts.Length; ordinal++)
        {
            if (_script.Prompts[ordinal] is not { } recorded || !line.Contains(recorded, StringComparison.Ordinal))
            {
                continue;
            }

            if (ordinal >= written.Length)
            {
                throw new InvalidOperationException(
                    $"the line names prompt #{ordinal + 1}, which the client has not written: {line}");
            }

            if (written[ordinal] is { } uuid)
            {
                line = line.Replace(recorded, uuid, StringComparison.Ordinal);
            }
        }

        return line;
    }

    private async Task SettleAsync(CancellationToken ct)
    {
        if (_script.AwaitStdinEnd)
        {
            await _stdinEnded.Task.WaitAsync(Patience, ct).ConfigureAwait(false);
            return;
        }

        await Task.WhenAny(_stdinEnded.Task, Task.Delay(EofGrace, ct)).ConfigureAwait(false);
    }

    private async Task ReceiveAsync(StreamReader reader, CancellationToken ct)
    {
        try
        {
            while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
            {
                TaskCompletionSource stale;
                lock (_gate)
                {
                    _written.Add(line);
                    stale = _wrote;
                    _wrote = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                }

                stale.TrySetResult();
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
        }
        finally
        {
            TaskCompletionSource stale;
            lock (_gate)
            {
                _stdinEnded.TrySetResult();
                stale = _wrote;
            }

            stale.TrySetResult();
        }
    }

    private async Task<string> NextClientRequestIdAsync(CancellationToken ct)
    {
        var wanted = _answeredRequests;
        var id = await AwaitWrittenAsync(
            written => written.Where(line => Fixture.TypeOf(line) == "control_request")
                .Select(Fixture.RequestIdOf)
                .ElementAtOrDefault(wanted),
            ct).ConfigureAwait(false);

        _answeredRequests++;
        return id ?? throw new InvalidOperationException(
            $"the client never sent control request #{wanted + 1}, which the script answers next.");
    }

    private async Task AwaitClientResponseAsync(string requestId, CancellationToken ct)
    {
        var answered = await AwaitWrittenAsync(
            written => written.FirstOrDefault(
                line => Fixture.TypeOf(line) == "control_response" && Fixture.ResponseRequestIdOf(line) == requestId),
            ct).ConfigureAwait(false);

        if (answered is null)
        {
            throw new InvalidOperationException($"the client never answered control request {requestId}.");
        }
    }

    // What `probe` finds among the client's lines, waited for; null when the patience ran out. A
    // client whose stdin ended without it will write nothing more, and the play ends.
    private async Task<string?> AwaitWrittenAsync(Func<IReadOnlyList<string>, string?> probe, CancellationToken ct)
    {
        var deadline = Task.Delay(Patience, ct);
        while (true)
        {
            Task signalled;
            lock (_gate)
            {
                if (probe(_written) is { } hit)
                {
                    return hit;
                }

                if (_stdinEnded.Task.IsCompleted)
                {
                    throw new StdinEndedException();
                }

                signalled = _wrote.Task;
            }

            if (await Task.WhenAny(signalled, deadline).ConfigureAwait(false) == deadline)
            {
                ct.ThrowIfCancellationRequested();

                lock (_gate)
                {
                    return probe(_written);
                }
            }
        }
    }

    private sealed class StdinEndedException : Exception
    {
    }

    private static string Readdress(string line, string requestId)
    {
        using var document = JsonDocument.Parse(line);
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!property.NameEquals("response"))
                {
                    property.WriteTo(writer);
                    continue;
                }

                writer.WritePropertyName("response");
                writer.WriteStartObject();
                foreach (var inner in property.Value.EnumerateObject())
                {
                    if (inner.NameEquals("request_id"))
                    {
                        writer.WriteString("request_id", requestId);
                    }
                    else
                    {
                        inner.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return Utf8.GetString(buffer.ToArray());
    }
}
