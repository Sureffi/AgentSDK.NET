using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// A line the binary wrote that this library has no type for, or one whose members do not fit the
/// type of its kind: its <c>type</c>, its <c>subtype</c> when it has one, and the line itself in
/// <see cref="AgentMessage.Raw"/>. It is handed out, never thrown and never skipped.
/// </summary>
public sealed class UnknownMessage : AgentMessage
{
    internal UnknownMessage(string type, string? subtype, string? uuid, string? sessionId, JsonElement? raw)
        : base(uuid, sessionId, raw)
    {
        Type = type;
        Subtype = subtype;
    }

    /// <summary>The line's <c>type</c>.</summary>
    public string Type { get; }

    /// <summary>The line's <c>subtype</c>, when it carries a string one.</summary>
    public string? Subtype { get; }

    // Why the line was not read as its kind, for the log; null when its kind has no type at all.
    internal string? Reason { get; init; }
}
