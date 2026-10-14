namespace Claude.Agent;

// The binary's stdio, or in tests a socket replaying what the binary once wrote. Connect and Write
// throw the exception the caller sees; Read yields each frame or the reason it was refused, and ends
// with a TransportException when the process did.
internal interface ITransport : IAsyncDisposable
{
    bool IsReady { get; }

    ValueTask ConnectAsync(CancellationToken ct);

    IAsyncEnumerable<Result<InboundFrame, ParseError>> ReadAsync(CancellationToken ct);

    ValueTask WriteAsync(OutboundFrame frame, CancellationToken ct);

    ValueTask EndInputAsync(CancellationToken ct);

    ValueTask CloseAsync(CancellationToken ct);
}

internal interface ITransportFactory
{
    ITransport Create(ClaudeAgentClientOptions options, ClaudeAgentSessionOptions session, TransportNeeds needs);
}

internal sealed record TransportNeeds(bool RoutePermissionsOverStdio);

// Carries the typed error across the read loop to the runtime, which fails the session with it.
internal sealed class TransportException : Exception
{
    public TransportException(Error error)
        : base((error ?? throw new ArgumentNullException(nameof(error))).Message) => Error = error;

    public Error Error { get; }
}
