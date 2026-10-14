using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// One message of a session, typed by the frame's <c>type</c>: a line the binary wrote on stdout,
/// or one this library assembled itself (a <see cref="ToolCallMessage"/>). <see cref="Raw"/> is the
/// line it was read from.
/// </summary>
public abstract class AgentMessage
{
    private protected AgentMessage(string? uuid, string? sessionId, JsonElement? raw)
    {
        Uuid = uuid;
        SessionId = sessionId;
        Raw = raw;
    }

    /// <summary>The frame's own id, when it carries one.</summary>
    public string? Uuid { get; }

    /// <summary>The session the frame belongs to, when it names one.</summary>
    public string? SessionId { get; }

    /// <summary>The whole line as the binary wrote it; null for a message this library assembled without one.</summary>
    public JsonElement? Raw { get; }
}
