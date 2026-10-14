using System.Text.Json;

namespace Claude.Agent;

/// <summary>One API stream event, sent when <see cref="ClaudeAgentSessionOptions.IncludePartialMessages"/> is on.</summary>
public sealed class StreamEventMessage : AgentMessage
{
    internal StreamEventMessage(JsonElement @event, string? parentToolUseId, string uuid, string sessionId, JsonElement? raw)
        : base(uuid, sessionId, raw)
    {
        Event = @event;
        ParentToolUseId = parentToolUseId;
    }

    /// <summary>The event as the API streamed it. A partial message is not typed here; the finished <see cref="AssistantMessage"/> is.</summary>
    public JsonElement Event { get; }

    /// <summary>The Agent tool use that started the subagent this event belongs to; null on the main thread.</summary>
    public string? ParentToolUseId { get; }

    /// <summary>How long until the model's first token; set on the event that opens a message.</summary>
    public TimeSpan? TimeToFirstToken { get; internal init; }

    /// <summary>The prompt this event belongs to, when the binary stamps one.</summary>
    public string? UserMessageUuid { get; internal init; }

    /// <summary>The prompts this event belongs to, when the turn answered more than one.</summary>
    public IReadOnlyList<string> UserMessageUuids { get; internal init; } = [];

    /// <summary>Why the session was resumed, when this event belongs to a resumed turn.</summary>
    public string? ResumeReason { get; internal init; }

    /// <summary>What the event is about, read off its <c>type</c>; empty when the event carries none.</summary>
    public StreamEventType EventType =>
        new(Event.ValueKind == JsonValueKind.Object
            && Event.TryGetProperty("type", out var type)
            && type.ValueKind == JsonValueKind.String
                ? type.GetString() ?? string.Empty
                : string.Empty);
}
