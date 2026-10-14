namespace Claude.Agent;

// The recorder's seam: every line the binary writes, every line written to it, and the argv it was
// started with, as bytes on the wire and before any parse. Null everywhere else.
internal sealed record TransportTap(
    Action<IReadOnlyList<string>> Arguments,
    Action<string> StdoutLine,
    Action<string> StdinLine);
