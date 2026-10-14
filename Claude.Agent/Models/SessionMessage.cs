namespace Claude.Agent;

/// <summary>One turn read back from a transcript.</summary>
public sealed class SessionMessage
{
    internal SessionMessage(SessionMessageRole role, string uuid, string sessionId, IReadOnlyList<ContentBlock> content)
    {
        Role = role;
        Uuid = uuid;
        SessionId = sessionId;
        Content = content;
    }

    /// <summary>Who wrote the turn.</summary>
    public SessionMessageRole Role { get; }

    /// <summary>The transcript entry's id.</summary>
    public string Uuid { get; }

    /// <summary>The session the entry belongs to.</summary>
    public string SessionId { get; }

    /// <summary>The blocks of the turn; a prompt written as a string is one <see cref="TextBlock"/>.</summary>
    public IReadOnlyList<ContentBlock> Content { get; }

    /// <summary>The API's message on a turn the model wrote, whole; null on a person's turn, which carries only content.</summary>
    public ApiMessage? Message { get; internal init; }

    /// <summary>Set on a subagent's turns: the tool use that started the subagent.</summary>
    public string? ParentToolUseId { get; internal init; }

    /// <summary>Set on a subagent's turns: the agent that started it, when one did.</summary>
    public string? ParentAgentId { get; internal init; }

    /// <summary>The text blocks, joined.</summary>
    public string Text => string.Concat(Content.OfType<TextBlock>().Select(block => block.Text));
}
