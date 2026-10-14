using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace Claude.Agent.Tests.Replay;

// The fixture server's end of a session, closed as a process is: stdin EOF, then the server's
// hang-up awaited within the grace, the connection cut when it does not come, as a process that
// stays is killed. The script's exit code is the process's, unless the close had to cut it. What
// the server sends is read as the binary's stdout is, into lines by StandardOutputLines, and every
// line goes to the codec as it came; no line is too long.
internal sealed class SocketTransport : ITransport
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IPEndPoint _endPoint;
    private readonly FrameCodec _codec;
    private readonly int _exitCode;
    private readonly TimeSpan _grace;
    private readonly SemaphoreSlim _write = new(1, 1);
    private readonly TaskCompletionSource _hungUp = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private TcpClient? _client;
    private NetworkStream? _stream;
    private bool _forced;

    public SocketTransport(IPEndPoint endPoint, FrameCodec codec, int exitCode = 0, TimeSpan? grace = null)
    {
        _endPoint = endPoint;
        _codec = codec;
        _exitCode = exitCode;
        _grace = grace ?? TimeSpan.FromSeconds(5);
    }

    public bool IsReady { get; private set; }

    public async ValueTask ConnectAsync(CancellationToken ct)
    {
        var client = new TcpClient();
        try
        {
            await client.ConnectAsync(_endPoint, ct).ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            client.Dispose();
            throw new ConnectionError(ex.Message).ToException();
        }

        _client = client;
        _stream = client.GetStream();
        IsReady = true;
    }

    public async IAsyncEnumerable<Result<InboundFrame, ParseError>> ReadAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var stream = _stream ?? throw new InvalidOperationException("the socket transport is not connected.");

        try
        {
            await foreach (var line in StandardOutputLines.ReadAsync(stream, int.MaxValue, ct).ConfigureAwait(false))
            {
                yield return _codec.Decode(line);
            }
        }
        finally
        {
            _hungUp.TrySetResult();
        }

        if (_exitCode != 0 && !Volatile.Read(ref _forced))
        {
            throw new TransportException(new ProcessError(
                string.Create(CultureInfo.InvariantCulture, $"The binary exited with code {_exitCode}"),
                _exitCode));
        }
    }

    public async ValueTask WriteAsync(OutboundFrame frame, CancellationToken ct)
    {
        var stream = _stream;
        if (stream is null)
        {
            throw new ConnectionError("the socket transport is not connected.").ToException();
        }

        var line = Utf8.GetBytes(_codec.Encode(frame) + "\n");
        await _write.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await stream.WriteAsync(line, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
        {
            throw new ConnectionError(ex.Message).ToException();
        }
        finally
        {
            _write.Release();
        }
    }

    public ValueTask EndInputAsync(CancellationToken ct)
    {
        try
        {
            _client?.Client.Shutdown(SocketShutdown.Send);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask CloseAsync(CancellationToken ct)
    {
        IsReady = false;
        if (_client is null)
        {
            return;
        }

        await EndInputAsync(ct).ConfigureAwait(false);
        try
        {
            await _hungUp.Task.WaitAsync(_grace, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Volatile.Write(ref _forced, true);
            Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        IsReady = false;
        Release();
        _write.Dispose();
        return ValueTask.CompletedTask;
    }

    private void Release()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
    }
}
