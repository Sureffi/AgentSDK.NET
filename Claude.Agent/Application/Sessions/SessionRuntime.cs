using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Claude.Agent;

// One live process: the read loop, the outbound lock, the runs and what no run reads, the control
// requests in both directions, and the close sequence. Every failure surfaces as the exception the
// caller sees. The close is one awaited sequence: stdin EOF, the process through the transport's
// close sequence, the read loop drained to the end of what the process wrote, then what the
// runtime owns disposed, in that order.
internal sealed class SessionRuntime : IAsyncDisposable
{
    private const string ClosedMessage = "session is closed";
    private const string ClosedBeforeResult = "The session was closed before the run's result.";
    private const string EndedBeforeResult = "The binary's output ended before the run's result.";
    private const string ClosedBeforeStart = "The binary closed the prompt before its turn started.";

    private static readonly TimeSpan ControlTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan TransportCloseTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReadLoopDrainTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ControlDrainTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan WriteDrainTimeout = TimeSpan.FromSeconds(5);

    private readonly ITransport _transport;
    private readonly IPayloadParser _payloads;
    private readonly IMcpBridge _bridge;
    private readonly ControlRequestTracker _tracker = new();
    private readonly ConcurrentDictionary<Task, byte> _controlTasks = new();
    private readonly ILogger _logger;
    private readonly string _session;

    // One prompt of a session in flight: the rest wait in the queue, in the order they were sent, and
    // the next is written when the current run has ended. The current run takes its prompt's
    // stretch — from the frame that says the prompt started to the turn's result — and every frame
    // outside such a stretch goes to what no run reads, as it comes. A one-shot's run takes every
    // frame until its session is closed. Nothing here waits for a reader.
    private readonly Lock _routing = new();
    private readonly LinkedList<PendingPrompt> _queue = new();
    private readonly ToolCallPairing _pairing = new();
    private readonly object _unclaimedOwner = new();
    private RunStream? _current;
    private RunStream? _oneShot;
    private readonly Channel<AgentMessage> _unclaimed = Channel.CreateUnbounded<AgentMessage>(
        new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false,
        });

    private readonly TaskCompletionSource _streamEnded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentDictionary<RequestId, PendingControl> _pending = new();
    private readonly SemaphoreSlim _write = new(1, 1);
    private readonly CancellationTokenSource _readCancellation = new();
    private readonly Lock _state = new();
    private readonly Lock _ending = new();
    private readonly HashSet<string> _inFlightTasks = new(StringComparer.Ordinal);
    private readonly Lock _rejected = new();
    private readonly List<ParseError> _rejectedFrames = [];

    private Task? _readLoop;
    private Error? _fatal;
    private Error? _exitAtClose;
    private string? _id;
    private int _requestCounter;
    private int _closed;
    private bool _inputEnded;
    private Task? _closing;
    private Task? _readingEnded;

    public SessionRuntime(
        ClaudeAgentSessionOptions sessionOptions,
        ITransport transport,
        IPayloadParser payloads,
        IMcpBridge bridge,
        HookTable hooks,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(sessionOptions);

        SessionOptions = sessionOptions;
        Hooks = hooks;
        _id = sessionOptions.SessionId is { Length: > 0 } requested ? requested : null;
        _transport = transport;
        _payloads = payloads;
        _bridge = bridge;
        _logger = logger ?? NullLogger.Instance;
        _session = Guid.NewGuid().ToString();
    }

    public IMcpBridge Bridge => _bridge;

    public IPayloadParser Payloads => _payloads;

    public HookTable Hooks { get; }

    public Func<ToolPermissionRequest, CancellationToken, Task<ToolPermissionResult>>? PermissionCallback => SessionOptions.PermissionCallback;

    public ClaudeAgentSessionOptions SessionOptions { get; }

    // The id the session was asked for, then the first one a frame carries, then the one
    // system/init states, which is authoritative.
    public string? Id
    {
        get
        {
            lock (_state)
            {
                return _id;
            }
        }
    }

    public ServerInfo? ServerInfo { get; private set; }

    public IReadOnlyList<ParseError> RejectedFrames
    {
        get
        {
            lock (_rejected)
            {
                return [.. _rejectedFrames];
            }
        }
    }

    // The close has begun: no prompt, control request or reply is written from here on.
    public bool IsClosed => Volatile.Read(ref _closed) != 0;

    // The background agents and workflows this turn started and the binary has not yet reported done.
    public ImmutableHashSet<string> InFlightTasks
    {
        get
        {
            lock (_state)
            {
                return [.. _inFlightTasks];
            }
        }
    }

    public void Start() => _readLoop ??= Task.Run(() => ReadLoopAsync(_readCancellation.Token));

    public async Task<ServerInfo> InitializeAsync(InitializeRequest request, CancellationToken ct)
    {
        var payload = await SendControlAsync(request, ControlTimeout, ct).ConfigureAwait(false);
        var info = _payloads.ServerInfo(payload).OrThrow();
        ServerInfo = info;
        return info;
    }

    public Task<JsonElement?> SendControlAsync(ControlRequest request, CancellationToken ct) =>
        SendControlAsync(request, ControlTimeout, ct);

    // The read loop has ended, or the session closed: no message reaches a run after this, and every
    // run still waiting for its result has failed.
    public Task Ended => _streamEnded.Task;

    // Why the process went, when it failed.
    public Error? Fatal => Volatile.Read(ref _fatal);

    public Task WriteAsync(OutboundFrame frame, CancellationToken ct) => WriteAsync(frame, null, ct);

    // `onlyIf`, when given, is asked under the write lock, and the frame goes out only when it holds.
    // Nothing is written once stdin has been closed.
    private async Task<bool> WriteAsync(OutboundFrame frame, Func<bool>? onlyIf, CancellationToken ct)
    {
        if (Volatile.Read(ref _fatal) is { } known)
        {
            throw known.ToException();
        }

        await _write.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (onlyIf is not null && !onlyIf())
            {
                return false;
            }

            ThrowWhenInputEnded();
            await _transport.WriteAsync(frame, ct).ConfigureAwait(false);
            return true;
        }
        catch (ClaudeAgentException) when (Volatile.Read(ref _fatal) is { } fatal)
        {
            // The write failed because the process is gone; the reason it went is the better error.
            throw fatal.ToException();
        }
        finally
        {
            _write.Release();
        }
    }

    // Sends a session's prompt for `run`: it joins the queue and is written once the run before it
    // has ended; `ct` takes it out of the queue while it waits there, and stops nothing once it is
    // taken to be written. The part of a write that can complete at once does so before this
    // returns. A failure ends the run.
    public void Send(RunStream run, UserInputFrame frame, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(frame);
        if (run.IsOneShot)
        {
            throw new ArgumentException("A one-shot's run is driven by RunOneShotAsync.", nameof(run));
        }

        run.Pairing = _pairing;
        Enqueue(run, frame, ct);
    }

    // A one-shot's run up to the end of its turn: its prompt written, then the result that ends its
    // turn read, or the session's output ended. Stdin stays open all the while, so the run can be
    // interrupted. The rest of the run's end — the session's close, then Finish — is its owner's.
    // `ct` given up before the turn ended withdraws the run, so nothing more reaches it, and throws.
    public async Task RunOneShotAsync(RunStream run, UserInputFrame frame, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(frame);
        if (!run.IsOneShot)
        {
            throw new ArgumentException("A session's run is sent with Send.", nameof(run));
        }

        run.Pairing = _pairing;
        try
        {
            await WriteOneShotAsync(run, frame, ct).ConfigureAwait(false);

            // The run's completion among them: a turn that ended without a result its run could use
            // has failed the run where that was read, and there is nothing more to wait for.
            await Task.WhenAny(run.Ending, run.Completion, Ended).WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (run.Ending.IsCompleted)
            {
                // The turn ended as the token went: the run ends as it stands.
                return;
            }

            Withdraw(run);
            throw;
        }
    }

    private async Task WriteOneShotAsync(RunStream run, UserInputFrame frame, CancellationToken ct)
    {
        if (Volatile.Read(ref _fatal) is { } known)
        {
            throw known.ToException();
        }

        await _write.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowWhenInputEnded();
            Enlist(run);
            try
            {
                await _transport.WriteAsync(frame, ct).ConfigureAwait(false);
            }
            catch
            {
                Withdraw(run);
                throw;
            }
        }
        catch (ClaudeAgentException) when (Volatile.Read(ref _fatal) is { } fatal)
        {
            throw fatal.ToException();
        }
        finally
        {
            _write.Release();
        }
    }

    // Under the write lock.
    private void ThrowWhenInputEnded()
    {
        if (Volatile.Read(ref _inputEnded))
        {
            throw (Volatile.Read(ref _fatal) ?? new ControlError(ClosedMessage, null)).ToException();
        }
    }

    private void Enlist(RunStream run)
    {
        lock (_routing)
        {
            if (_streamEnded.Task.IsCompleted || IsClosed)
            {
                throw (Volatile.Read(ref _fatal) ?? new ControlError(ClosedMessage, null)).ToException();
            }

            _oneShot = run;
            run.IsStarted = true;
            run.MarkStarted();
        }
    }

    // A session's prompt waits its turn. A token already cancelled ends the run at once; one
    // cancelled while the prompt waits takes it out of the queue.
    private void Enqueue(RunStream run, UserInputFrame frame, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            run.Cancel(ct);
            return;
        }

        var pending = new PendingPrompt(run, frame);
        PendingPrompt? next;
        lock (_routing)
        {
            if (_streamEnded.Task.IsCompleted || IsClosed)
            {
                run.Fail(Volatile.Read(ref _fatal) ?? new ControlError(ClosedMessage, null));
                return;
            }

            pending.Node = _queue.AddLast(pending);
            next = TakeNextLocked();
        }

        if (ct.CanBeCanceled && next != pending)
        {
            // Run at once when the token is already cancelled; kept only while the prompt waits.
            var registration = ct.UnsafeRegister(
                static (state, token) =>
                {
                    var (runtime, waiting) = ((SessionRuntime, PendingPrompt))state!;
                    runtime.Dequeue(waiting, token);
                },
                (this, pending));
            bool waits;
            lock (_routing)
            {
                waits = pending.IsQueued;
                if (waits)
                {
                    pending.Registration = registration;
                }
            }

            if (!waits)
            {
                registration.Unregister();
            }
        }

        WriteNext(next, inline: true);
    }

    // The next prompt, taken off the queue to be written, when no run of the session is current and
    // the session is not closing.
    private PendingPrompt? TakeNextLocked()
    {
        if (IsClosed || _current is not null || _queue.First is not { Value: var next })
        {
            return null;
        }

        Unqueue(next);
        _current = next.Run;
        return next;
    }

    // Under the routing lock. Unregister does not wait for a callback under way, which waits for this
    // lock.
    private void Unqueue(PendingPrompt pending)
    {
        if (pending.Node is { List: not null } node)
        {
            _queue.Remove(node);
        }

        pending.Registration.Unregister();
    }

    // A prompt still waiting whose sender gave up on it: nothing is written, and its run ends
    // cancelled.
    private void Dequeue(PendingPrompt pending, CancellationToken token)
    {
        lock (_routing)
        {
            if (!pending.IsQueued)
            {
                return;
            }

            Unqueue(pending);
        }

        pending.Run.Cancel(token);
    }

    // Written with no token: once a prompt is taken to be written, nothing the caller cancels stops
    // it half-way. A prompt that did not go out fails its run, and the next is written.
    private async Task WriteSessionPromptAsync(PendingPrompt pending)
    {
        var run = pending.Run;
        try
        {
            if (Volatile.Read(ref _fatal) is { } known)
            {
                throw known.ToException();
            }

            if (IsClosed)
            {
                throw new ControlError(ClosedMessage, null).ToException();
            }

            await _write.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                ThrowWhenInputEnded();
                await _transport.WriteAsync(pending.Frame, CancellationToken.None).ConfigureAwait(false);
            }
            catch (ClaudeAgentException) when (Volatile.Read(ref _fatal) is { } fatal)
            {
                throw fatal.ToException();
            }
            finally
            {
                _write.Release();
            }
        }
        catch (Exception ex)
        {
            PendingPrompt? next;
            lock (_routing)
            {
                if (_current == run)
                {
                    _current = null;
                }

                next = TakeNextLocked();
            }

            run.Fail(ex);
            WriteNext(next, inline: false);
        }
    }

    // A sender's own prompt is written on its call. One taken when a run ends is written off the
    // read loop: a write that waits on a full pipe must not stop the reading the binary waits on.
    private void WriteNext(PendingPrompt? next, bool inline)
    {
        if (next is null)
        {
            return;
        }

        _ = inline ? WriteSessionPromptAsync(next) : Task.Run(() => WriteSessionPromptAsync(next));
    }

    // A one-shot's run whose prompt never went out, or whose owner gave up on it: whatever reached it
    // stays its own, and nothing more does.
    private void Withdraw(RunStream run)
    {
        lock (_routing)
        {
            if (_oneShot == run)
            {
                _oneShot = null;
            }
        }
    }

    // A run's own interrupt. A prompt still waiting is taken out of the queue, nothing is written,
    // and its run ends cancelled, since no turn ran. A run whose turn has not begun is waited for.
    // The interrupt goes out only while the run's turn is the one under way, which is checked under
    // the write lock, so it is never written after the run's result was read; a run whose result
    // has come, or that has ended, sends nothing. A one-shot's stdin is open until its turn's
    // ending result, so its interrupt is written as a session's is.
    public async Task InterruptAsync(RunStream run, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        while (true)
        {
            Task begun;
            PendingPrompt? withdrawn = null;
            lock (_routing)
            {
                if (run.IsEnded)
                {
                    return;
                }

                foreach (var pending in _queue)
                {
                    if (pending.Run == run)
                    {
                        withdrawn = pending;
                        break;
                    }
                }

                if (withdrawn is not null)
                {
                    Unqueue(withdrawn);
                }
                else if (IsUnderWay(run))
                {
                    break;
                }
                else if (run.IsStarted)
                {
                    return;
                }

                begun = Task.WhenAny(run.Started, run.Completion);
            }

            if (withdrawn is not null)
            {
                run.Cancel(CancellationToken.None);
                return;
            }

            await begun.WaitAsync(ct).ConfigureAwait(false);
        }

        var written = false;
        var receipt = await SendControlAsync(new InterruptRequest(), ControlTimeout, ct, () =>
        {
            lock (_routing)
            {
                return written = IsUnderWay(run);
            }
        }).ConfigureAwait(false);
        if (written)
        {
            ReadReceipt(receipt);
        }
    }

    // The session's interrupt, written whenever it is called, a turn under way or not.
    public async Task InterruptAsync(CancellationToken ct) =>
        ReadReceipt(await SendControlAsync(new InterruptRequest(), ControlTimeout, ct).ConfigureAwait(false));

    // The interrupt has been answered, so its receipt is read for the log alone: what the binary
    // still holds runs unless cancelled first, and a receipt that does not fit changes nothing the
    // interrupt did.
    private void ReadReceipt(JsonElement? payload)
    {
        if (!_payloads.Interrupt(payload).TryGet(out var receipt, out var error))
        {
            Debug(null, $"the interrupt's receipt does not fit: {error.Message}");
            return;
        }

        if (receipt.StillQueued.Count > 0)
        {
            Debug(null, $"the interrupt leaves {string.Join(", ", receipt.StillQueued)} queued");
        }

        if (receipt.Cancelled is { Count: > 0 } cancelled)
        {
            Debug(null, $"the interrupt cancelled {string.Join(", ", cancelled)}");
        }
    }

    // Under the routing lock: the run's turn is the one under way.
    private bool IsUnderWay(RunStream run) =>
        run.IsOneShot ? _oneShot == run && !run.HasEnding && !run.IsEnded : _current == run && run.IsOpen;

    // What no run reads, until the session ends; the process's failure is thrown at the end, with
    // what this enumeration handed out.
    public async IAsyncEnumerable<AgentMessage> ReadMessagesAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var handed = new List<AgentMessage>();
        var reader = _unclaimed.Reader;
        while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            while (reader.TryRead(out var message))
            {
                handed.Add(message);
                yield return message;
            }
        }

        if (Volatile.Read(ref _fatal) is { } fatal)
        {
            throw fatal.ToException(handed);
        }
    }

    public ValueTask DisposeAsync() => CloseAsync(CancellationToken.None);

    // Closes the session once; every call awaits the same close. The sequence is bounded by the
    // transport's grace and its own timeouts, not by `ct`.
    public async ValueTask CloseAsync(CancellationToken ct)
    {
        TaskCompletionSource? mine = null;
        Task closing;
        lock (_routing)
        {
            if (_closing is null)
            {
                Volatile.Write(ref _closed, 1);
                mine = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _closing = mine.Task;
            }

            closing = _closing;
        }

        if (mine is not null)
        {
            try
            {
                await CloseCoreAsync().ConfigureAwait(false);
            }
            finally
            {
                mine.TrySetResult();
            }
        }

        await closing.ConfigureAwait(false);
    }

    // Nothing here throws: each step that fails is logged and the next goes on.
    private async Task CloseCoreAsync()
    {
        // A handler still out is told to stop: nothing it answers can be written once stdin closes.
        _tracker.CancelAll();

        // Stdin EOF, behind a write under way; nothing is written after it.
        await EndInputAsync().ConfigureAwait(false);

        // The process, awaited through the transport's close sequence: its exit within the grace,
        // else SIGTERM and the grace again, else SIGKILL.
        try
        {
            await _transport.CloseAsync(CancellationToken.None)
                .AsTask()
                .WaitAsync(TransportCloseTimeout, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug(ex, "closing the transport failed");
        }

        // The read loop reads what the process wrote before it went, to the end, and ends reading:
        // the runs still waiting are ended.
        await DrainReadLoopAsync().ConfigureAwait(false);
        await EndReadingAsync().ConfigureAwait(false);

        try
        {
            await Task.WhenAll(_controlTasks.Keys).WaitAsync(ControlDrainTimeout, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug(ex, "a control request handler did not settle");
        }

        try
        {
            await _bridge.CloseAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug(ex, "closing the mcp bridge failed");
        }

        try
        {
            await _bridge.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug(ex, "disposing the mcp bridge failed");
        }

        try
        {
            await _transport.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug(ex, "disposing the transport failed");
        }

        _readCancellation.Dispose();
    }

    // Under the write lock, so it goes after a write under way and before any other. A write that
    // holds the lock past the bound (a pipe the process stopped reading) is left to the transport's
    // close, which ends stdin itself.
    private async Task EndInputAsync()
    {
        var held = await _write.WaitAsync(WriteDrainTimeout, CancellationToken.None).ConfigureAwait(false);
        try
        {
            Volatile.Write(ref _inputEnded, true);
            if (held)
            {
                await _transport.EndInputAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Debug(ex, "closing stdin failed");
        }
        finally
        {
            if (held)
            {
                _write.Release();
            }
        }
    }

    // The loop ends at the end of the output, which the transport's close has brought about; a loop
    // that does not get there within the bound is stopped.
    private async Task DrainReadLoopAsync()
    {
        if (_readLoop is not { } loop)
        {
            return;
        }

        try
        {
            await loop.WaitAsync(ReadLoopDrainTimeout, CancellationToken.None).ConfigureAwait(false);
            return;
        }
        catch (Exception ex)
        {
            Debug(ex, "the read loop did not reach the end of the output, so it is stopped");
        }

        try
        {
            await _readCancellation.CancelAsync().ConfigureAwait(false);
            await loop.WaitAsync(ReadLoopDrainTimeout, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug(ex, "the read loop did not settle");
        }
    }

    // Once, when the read loop is done or the close goes on without it; every caller awaits the same
    // end.
    private async Task EndReadingAsync()
    {
        TaskCompletionSource? mine = null;
        Task ending;
        lock (_ending)
        {
            if (_readingEnded is null)
            {
                mine = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _readingEnded = mine.Task;
            }

            ending = _readingEnded;
        }

        if (mine is not null)
        {
            try
            {
                SettlePending(Volatile.Read(ref _fatal) ?? new ControlError(ClosedMessage, null));
                EndStream();
            }
            finally
            {
                mine.TrySetResult();
            }
        }

        await ending.ConfigureAwait(false);
    }

    // `onlyIf`, when given, is asked under the write lock: the request goes out only when it holds,
    // and when it does not, nothing is sent and nothing is answered.
    private async Task<JsonElement?> SendControlAsync(ControlRequest request, TimeSpan timeout, CancellationToken ct, Func<bool>? onlyIf = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (IsClosed)
        {
            throw new ControlError(ClosedMessage, request.Subtype).ToException();
        }

        var requestId = NextRequestId();
        var pending = new PendingControl(
            request.Subtype,
            new TaskCompletionSource<Result<JsonElement?, Error>>(TaskCreationOptions.RunContinuationsAsynchronously));
        _pending[requestId] = pending;

        try
        {
            if (!await WriteAsync(new OutboundControlRequest(requestId, request), onlyIf, ct).ConfigureAwait(false))
            {
                return null;
            }

            var reply = await pending.Completion.Task.WaitAsync(timeout, ct).ConfigureAwait(false);
            return reply.OrThrow();
        }
        catch (TimeoutException)
        {
            throw new ControlError($"Control request timeout: {request.Subtype}", request.Subtype).ToException();
        }
        finally
        {
            _pending.TryRemove(requestId, out _);
        }
    }

    private RequestId NextRequestId()
    {
        var counter = Interlocked.Increment(ref _requestCounter);
        Span<byte> entropy = stackalloc byte[4];
        RandomNumberGenerator.Fill(entropy);
        return new RequestId($"req_{counter}_{Convert.ToHexString(entropy).ToLowerInvariant()}");
    }

    // Reads to the end of the output, through the close too: what the process writes as it shuts
    // down is routed like anything else.
    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var frame in _transport.ReadAsync(ct).ConfigureAwait(false))
            {
                if (!frame.TryGet(out var inbound, out var parseError))
                {
                    Reject(parseError);
                    continue;
                }

                await RouteAsync(inbound, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (TransportException ex)
        {
            SetFatal(ex.Error);
            Debug(ex, "the process ended the message stream");
        }
        catch (Exception ex)
        {
            if (IsClosed)
            {
                Debug(ex, "the message reader ended while closing");
            }
            else
            {
                SetFatal(new ProcessError(ex.Message, null));
                _logger.LogError(ex, "Session {Session}: the message reader failed.", _session);
            }
        }
        finally
        {
            await EndReadingAsync().ConfigureAwait(false);
        }
    }

    // Once, when reading has ended. A session's run whose result came before its prompt's lifecycle
    // closed completes with its trace, the result last. Every other run of the session, and every
    // prompt still waiting, fails: for the close when the caller began one, else for the process's
    // failure or for the result that never came. What no run reads closes its open calls and ends.
    // A one-shot's run is its owner's to end, once the session is closed.
    private void EndStream()
    {
        List<RunStream> waiting = [];
        RunStream? answered = null;
        lock (_routing)
        {
            if (_streamEnded.Task.IsCompleted)
            {
                return;
            }

            if (_current is { } current)
            {
                _current = null;
                if (current.HeldResult is { } result)
                {
                    Deliver(current, result, endsTurn: true);
                    answered = current;
                }
                else
                {
                    waiting.Add(current);
                }
            }

            foreach (var pending in _queue.ToArray())
            {
                Unqueue(pending);
                waiting.Add(pending.Run);
            }

            var handed = new List<AgentMessage>();
            _pairing.CloseUnanswered(_unclaimedOwner, handed);
            Unclaimed(handed);

            _unclaimed.Writer.TryComplete();
            _streamEnded.TrySetResult();
        }

        answered?.Complete(new ProcessError(EndedBeforeResult, null));

        // A process that failed once the close had begun is no failure of a run's: the caller asked
        // for the end, so a run still waiting fails for the close. Such a failure is never in
        // `_fatal` (SetFatal keeps it apart), so the order here says it.
        var fatal = Volatile.Read(ref _fatal);
        foreach (var run in waiting)
        {
            run.Fail(fatal ?? (IsClosed ? new ControlError(ClosedBeforeResult, null) : new ProcessError(EndedBeforeResult, null)));
        }
    }

    // A one-shot's run ends once its session is closed, in one step: what followed its turn's ending
    // result, then its calls still open closed unanswered, then that result, last. A run whose
    // ending result came completes with its trace, however the process went after it. One without
    // it fails, for the process's failure or for the result that never came, with the messages it
    // has.
    //
    // Decided 2026-09-20: the result decides what an exit means. What the binary does, as
    // the orchestrator probed it on 2.1.276 with haiku through stream-json (a probe, not a
    // recording in this tree): a success result, then exit 0; an interrupted turn's
    // error_during_execution with terminal reason aborted_streaming, then exit 1; error_max_turns,
    // then exit 1; stdin ended mid-turn, and the binary finishes the turn and exits 0. So an exit
    // after an error result says nothing the result does not, and one after a success result
    // disagrees with it — which no probe has seen, so it is logged as a warning.
    public void Finish(RunStream run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var fatal = Volatile.Read(ref _fatal);
        if (fatal is not null && !run.HasEnding)
        {
            run.Fail(fatal);
            return;
        }

        if ((fatal ?? Volatile.Read(ref _exitAtClose)) is { } after)
        {
            LogExitAfterResult(run, after);
        }

        run.Complete(new ProcessError(EndedBeforeResult, null));
    }

    private void LogExitAfterResult(RunStream run, Error after)
    {
        if (run.EndingResult is { IsError: false })
        {
            _logger.LogWarning(
                "Session {Session}: the process failed after a successful result, which disagrees with it — {Detail}.",
                _session,
                after.Message);
            return;
        }

        Debug(null, $"the process failed after the run's result: {after.Message}");
    }

    // Read through the close too: a process that fails as it shuts down failed — unless the caller
    // began that close, when the end was asked for: the exit is then no failure of the session's,
    // and is kept for the log alone. One the close sequence had to end is not reported by the
    // transport as a failure at all.
    private void SetFatal(Error error)
    {
        if (IsClosed)
        {
            Volatile.Write(ref _exitAtClose, error);
            Debug(null, $"the process failed once the close had begun: {error.Message}");
            return;
        }

        Volatile.Write(ref _fatal, error);
    }

    private async Task RouteAsync(InboundFrame frame, CancellationToken ct)
    {
        switch (frame)
        {
            case MessageFrame message:
                OnMessage(message.Message);
                break;

            case ControlRequestFrame request:
                await OnControlRequestAsync(request, ct).ConfigureAwait(false);
                break;

            case ControlResponseFrame response:
                OnControlResponse(response.Response);
                break;

            case ControlCancelFrame cancel:
                _tracker.Cancel(cancel.RequestId);
                break;

            case KeepAliveFrame:
                break;

            default:
                break;
        }
    }

    // Hands `message` where it goes, and writes the prompt that is next when a run ends here.
    private void Route(AgentMessage message, bool tasksInFlight)
    {
        PendingPrompt? next;
        lock (_routing)
        {
            next = RouteLocked(message, tasksInFlight);
        }

        WriteNext(next, inline: false);
    }

    // A one-shot's run takes every message; a result written with no background task in flight
    // ends its turn, and is held by the run until it ends. A session's current run takes the lifecycle frames that name its prompt,
    // and, from the one that says the prompt started, every other message up to and including the
    // next result; it ends at that result once the prompt's lifecycle has closed, the frame that
    // closes it placed before the result. A lifecycle frame that names another prompt, and
    // everything else, goes to what no run reads, as it comes.
    private PendingPrompt? RouteLocked(AgentMessage message, bool tasksInFlight)
    {
        if (_streamEnded.Task.IsCompleted)
        {
            return null;
        }

        if (_oneShot is { } oneShot)
        {
            if (!tasksInFlight && MisfitResult(message) is { } misfitOneShot)
            {
                Deliver(oneShot, message, endsTurn: false);
                oneShot.Fail(misfitOneShot);
                return null;
            }

            Deliver(oneShot, message, endsTurn: message is ResultMessage && !tasksInFlight);
            return null;
        }

        if (_current is { } run)
        {
            if (message is CommandLifecycleMessage lifecycle)
            {
                if (Names(run, lifecycle))
                {
                    return OnLifecycleLocked(run, lifecycle);
                }
            }
            else if (run.IsOpen)
            {
                if (MisfitResult(message) is { } misfit)
                {
                    Deliver(run, message, endsTurn: false);
                    _current = null;
                    run.Fail(misfit);
                    return TakeNextLocked();
                }

                if (message is not ResultMessage result)
                {
                    Deliver(run, message, endsTurn: false);
                    return null;
                }

                if (!run.IsLifecycleClosed)
                {
                    run.HeldResult = result;
                    return null;
                }

                Deliver(run, result, endsTurn: true);
                return EndLocked(run);
            }
        }

        Unclaimed(message);
        return null;
    }

    private PendingPrompt? OnLifecycleLocked(RunStream run, CommandLifecycleMessage lifecycle)
    {
        Deliver(run, lifecycle, endsTurn: false);
        if (lifecycle.State == CommandLifecycleState.Started && !run.IsStarted)
        {
            run.IsStarted = true;
            run.MarkStarted();
            return null;
        }

        if (lifecycle.State != CommandLifecycleState.Completed && lifecycle.State != CommandLifecycleState.Cancelled)
        {
            return null;
        }

        run.IsLifecycleClosed = true;
        if (run.HeldResult is { } result)
        {
            Deliver(run, result, endsTurn: true);
            return EndLocked(run);
        }

        if (!run.IsStarted)
        {
            _current = null;
            run.Fail(new ControlError(ClosedBeforeStart, null));
            return TakeNextLocked();
        }

        return null;
    }

    // A result line whose members do not fit ResultMessage is the unknown of the message set, as any
    // misfit frame is (the page's models row), and it is handed out as one. The turn it was written
    // for has no result, so nothing else would end the run: it ends here, failed for the line, which
    // is what a reader can act on — a query whose stdin would otherwise stay open on a binary that
    // waits, a session that would otherwise write no further prompt. The page's methods row keeps
    // its word the only way it can: a trace's result is never null, so there is no trace.
    // A query's result written while a background task is in flight is not the one that ends its
    // run, so a misfit there ends nothing either: it goes out as the unknown and the run waits for
    // the result that does end it.
    private static ParseError? MisfitResult(AgentMessage message) =>
        message is UnknownMessage { Type: FrameTypes.Result } unknown
            ? new ParseError(
                ParseErrorKind.InvalidValue,
                unknown.Reason is { Length: > 0 } reason
                    ? $"The turn's result did not fit its type, so the turn has no result. {reason}"
                    : "The turn's result did not fit its type, so the turn has no result.",
                unknown.Raw?.GetRawText() ?? string.Empty)
            : null;

    private static bool Names(RunStream run, CommandLifecycleMessage lifecycle) =>
        Guid.TryParse(lifecycle.CommandUuid, out var named) && named == run.Prompt;

    // The run has its result, last: it completes with its trace, and the next prompt is taken.
    private PendingPrompt? EndLocked(RunStream run)
    {
        _current = null;
        run.Complete(new ProcessError(EndedBeforeResult, null));
        return TakeNextLocked();
    }

    // A session's result that ends its run closes the run's open calls just before it. A one-shot's
    // closes them at its end, after what follows its ending result.
    private void Deliver(RunStream run, AgentMessage message, bool endsTurn)
    {
        var handed = new List<AgentMessage>(2);
        var elsewhere = new List<ToolCallPairing.Owned>();
        _pairing.Read(message, run, endsTurn && !run.IsOneShot, handed, elsewhere);
        run.Add(message, handed, endsTurn);
        HandElsewhere(elsewhere);
    }

    // What no run reads has no run's end: a result there closes nothing, and a call started there
    // closes when its answer comes, or unanswered when the session ends.
    private void Unclaimed(AgentMessage message)
    {
        if (_streamEnded.Task.IsCompleted)
        {
            return;
        }

        var handed = new List<AgentMessage>(2);
        var elsewhere = new List<ToolCallPairing.Owned>();
        _pairing.Read(message, _unclaimedOwner, endsTurn: false, handed, elsewhere);
        Unclaimed(handed);
        HandElsewhere(elsewhere);
    }

    // A call belongs to where its start went: its closing snapshot goes to that owner even when the
    // message answering it went elsewhere — a call a run started, answered once the run was
    // withdrawn or once its stretch had ended, still closes in that run.
    private void HandElsewhere(List<ToolCallPairing.Owned> elsewhere)
    {
        foreach (var (owner, message) in elsewhere)
        {
            if (owner is RunStream run)
            {
                run.Add(message, [message], endsTurn: false);
            }
            else
            {
                _unclaimed.Writer.TryWrite(message);
            }
        }
    }

    // Messages already paired.
    private void Unclaimed(IEnumerable<AgentMessage> handed)
    {
        foreach (var each in handed)
        {
            _unclaimed.Writer.TryWrite(each);
        }
    }

    private void OnMessage(AgentMessage message)
    {
        bool tasksInFlight;
        lock (_state)
        {
            AdoptId(message);
            TrackTasks(message);
            tasksInFlight = _inFlightTasks.Count != 0;
        }

        Route(message, tasksInFlight);
    }

    private void AdoptId(AgentMessage message)
    {
        switch (message)
        {
            case SystemInitMessage { SessionId: { Length: > 0 } authoritative }:
                _id = authoritative;
                break;

            default:
                if (_id is null && message.SessionId is { Length: > 0 } observed)
                {
                    _id = observed;
                }

                break;
        }
    }

    // A background agent or workflow started in this turn keeps a one-shot's run, and so its stdin,
    // open past the result, so the binary can deliver its notification and the turn it opens then.
    private void TrackTasks(AgentMessage message)
    {
        switch (message)
        {
            case (LocalAgentTaskStartedMessage or LocalWorkflowTaskStartedMessage) and TaskStartedMessage { TaskId: { Length: > 0 } started }:
                _inFlightTasks.Add(started);
                break;

            case TaskNotificationMessage { TaskId: { Length: > 0 } notified }:
                _inFlightTasks.Remove(notified);
                break;

            case TaskUpdatedMessage { TaskId: { Length: > 0 } updated, Patch.IsTerminal: true }:
                _inFlightTasks.Remove(updated);
                break;

            default:
                break;
        }
    }

    private async Task OnControlRequestAsync(ControlRequestFrame frame, CancellationToken ct)
    {
        switch (frame.Request)
        {
            case HookCallbackRequest hook:
                Spawn(frame.RequestId, scope => ControlRequestHandlers.HookAsync(frame.RequestId, hook, scope, this, _logger));
                break;

            case CanUseToolRequest permission:
                Spawn(frame.RequestId, scope => ControlRequestHandlers.PermissionAsync(frame.RequestId, permission, scope, this, _logger));
                break;

            case McpMessageRequest mcp:
                Spawn(frame.RequestId, scope => ControlRequestHandlers.McpAsync(frame.RequestId, mcp, scope, this, _logger));
                break;

            case UnparsedControlRequest unparsed:
                await WriteAsync(new OutboundControlError(frame.RequestId, unparsed.Reason.Message), ct)
                    .ConfigureAwait(false);
                break;

            default:
                await WriteAsync(
                        new OutboundControlError(
                            frame.RequestId,
                            $"This session answers no control request of subtype '{frame.Request.Subtype}'."),
                        ct)
                    .ConfigureAwait(false);
                break;
        }
    }

    // The scope begins here, on the read loop, so the cancel frame that may be next finds it. The
    // handler runs off the loop: a hook, a permission callback or a tool is host code, and one that
    // blocks before it first suspends would otherwise stop every frame behind it, its own
    // control_cancel_request included.
    private void Spawn(RequestId requestId, Func<ControlRequestScope, Task> handle)
    {
        var scope = _tracker.Begin(requestId);
        var handling = Task.Run(() => handle(scope));
        _controlTasks[handling] = 0;
        _ = handling.ContinueWith(
            done => _controlTasks.TryRemove(done, out _),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void OnControlResponse(ControlResponse response)
    {
        if (!_pending.TryRemove(response.RequestId, out var pending))
        {
            return;
        }

        switch (response)
        {
            case ControlSuccess success:
                pending.Completion.TrySetResult(Result<JsonElement?, Error>.Ok(success.Response));
                break;

            case ControlFailure failure:
                pending.Completion.TrySetResult(
                    Result<JsonElement?, Error>.Fail(new ControlError(failure.Error, pending.Subtype)));
                break;

            default:
                break;
        }
    }

    private void SettlePending(Error error)
    {
        foreach (var requestId in _pending.Keys)
        {
            if (_pending.TryRemove(requestId, out var pending))
            {
                pending.Completion.TrySetResult(Result<JsonElement?, Error>.Fail(error));
            }
        }
    }

    private void Reject(ParseError error)
    {
        lock (_rejected)
        {
            _rejectedFrames.Add(error);
        }

        _logger.LogWarning("Session {Session}: rejected a frame — {Problem}", _session, error.Message);
    }

    private void Debug(Exception? failure, string detail)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(failure, "Session {Session}: {Detail}.", _session, detail);
        }
    }

    private sealed record PendingControl(
        string Subtype,
        TaskCompletionSource<Result<JsonElement?, Error>> Completion);

    // A session's prompt and its run; its node and its sender's registration are touched under the
    // routing lock only.
    private sealed class PendingPrompt(RunStream run, UserInputFrame frame)
    {
        public RunStream Run { get; } = run;

        public UserInputFrame Frame { get; } = frame;

        public LinkedListNode<PendingPrompt>? Node { get; set; }

        public CancellationTokenRegistration Registration { get; set; }

        public bool IsQueued => Node is { List: not null };
    }
}
