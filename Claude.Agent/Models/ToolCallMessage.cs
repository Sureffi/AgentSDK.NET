using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// A snapshot of a <see cref="ToolCall"/>, assembled by the session, which pairs every call it reads,
/// and handed out right after the message that caused it, wherever that message goes: a run, or
/// <see cref="ClaudeAgentSession.ReceiveMessagesAsync"/>. Once right after the assistant message
/// that started the call, and once when it closes: right after the user message that answered it,
/// or, for a call still open when the run it started in ends, unanswered, just before that run's
/// result, or after its last message when it ends without one. What no run reads has no run's end:
/// a call started there closes when its answer comes, or unanswered, last, when the session ends.
/// Every call closes once: an answer that comes after its call was closed unanswered is handed out
/// as its message and nothing more. <see cref="AgentMessage.Raw"/> is the line of the message that
/// caused the snapshot; null for an unanswered close.
/// </summary>
public sealed class ToolCallMessage : AgentMessage
{
    internal ToolCallMessage(ToolCall toolCall, string? sessionId, JsonElement? raw)
        : base(null, sessionId, raw)
    {
        ToolCall = toolCall;
    }

    /// <summary>The call as it stands at this point of the run.</summary>
    public ToolCall ToolCall { get; }
}
