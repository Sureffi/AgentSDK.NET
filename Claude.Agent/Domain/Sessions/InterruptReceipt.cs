namespace Claude.Agent;

// The binary's answer to an interrupt: the prompts it still holds, which run unless cancelled
// first, and the ones it cancelled, which it lists only when the interrupt asked it to cancel them.
// A binary older than the receipt answers with an empty body, which lists nothing.
internal sealed record InterruptReceipt(IReadOnlyList<string> StillQueued, IReadOnlyList<string>? Cancelled);
