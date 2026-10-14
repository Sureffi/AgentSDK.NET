using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// What the model answered: the API's message in <see cref="Message"/>, and what the binary says
/// about it. The binary writes one of these per content block the model finishes, so a turn's text
/// arrives over several of them and the totals arrive on the <see cref="ResultMessage"/>.
/// </summary>
public sealed class AssistantMessage : AgentMessage
{
    internal AssistantMessage(ApiMessage message, string? uuid, string? sessionId, JsonElement? raw)
        : base(uuid, sessionId, raw)
    {
        Message = message;
    }

    /// <summary>The API's message, whole.</summary>
    public ApiMessage Message { get; }

    /// <summary>The blocks, in the order the model wrote them: <see cref="ApiMessage.Content"/>, at hand.</summary>
    public IReadOnlyList<ContentBlock> Content => Message.Content;

    /// <summary>The model that answered: <see cref="ApiMessage.Model"/>, at hand.</summary>
    public string Model => Message.Model;

    /// <summary>What the message spent: <see cref="ApiMessage.Usage"/>, at hand.</summary>
    public Usage? Usage => Message.Usage;

    /// <summary>The API's own id of the message, distinct from <see cref="AgentMessage.Uuid"/>: <see cref="ApiMessage.Id"/>, at hand.</summary>
    public string? ApiMessageId => Message.Id;

    /// <summary>Why the model stopped writing: <see cref="ApiMessage.StopReason"/>, at hand.</summary>
    public StopReason? StopReason => Message.StopReason;

    /// <summary>The Agent tool use that started the subagent this message belongs to; null on the main thread.</summary>
    public string? ParentToolUseId { get; internal init; }

    /// <summary>Set when the turn failed before the model answered: why it failed.</summary>
    public AssistantErrorCode? Error { get; internal init; }

    /// <summary>The API request that carried the turn, as the binary names it.</summary>
    public string? RequestId { get; internal init; }

    /// <summary>The prompt this answer is to, when the binary stamps one.</summary>
    public string? UserMessageUuid { get; internal init; }

    /// <summary>The prompts this answer is to, when the turn answered more than one.</summary>
    public IReadOnlyList<string> UserMessageUuids { get; internal init; } = [];

    /// <summary>Why the session was resumed, when this message belongs to a resumed turn.</summary>
    public string? ResumeReason { get; internal init; }

    /// <summary>Whether the turn was resumed from thinking the model had not finished.</summary>
    public bool ResumedFromIncompleteThinking { get; internal init; }

    /// <summary>The messages already delivered that this one replaces, by their ids.</summary>
    public IReadOnlyList<string> Supersedes { get; internal init; } = [];

    /// <summary>Whether the message was cut short.</summary>
    public bool Aborted { get; internal init; }

    /// <summary>The subagent that wrote it, by name; set on a subagent's messages.</summary>
    public string? SubagentType { get; internal init; }

    /// <summary>What the subagent was asked for; set on a subagent's messages.</summary>
    public string? TaskDescription { get; internal init; }

    /// <summary>When the binary wrote the frame.</summary>
    public DateTimeOffset? Timestamp { get; internal init; }

    /// <summary>How full the context window is, when the frame reports it.</summary>
    public MessageContextUsage? ContextUsage { get; internal init; }

    /// <summary>What the session has spent and where the account stands, when the frame reports it.</summary>
    public UsageReport? UsageReport { get; internal init; }

    /// <summary>
    /// The arguments as they went to the model, by tool-use id, as the binary wrote them. The typed
    /// input of each is on the <see cref="ToolUseBlock"/> of the same id in <see cref="Content"/>.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> RawWireToolInputs { get; internal init; } = EmptyInputs;

    /// <summary>What to call the tool uses of this message, for an MCP tool that has a name of its own.</summary>
    public IReadOnlyList<ToolUseDisplay> ToolUseDisplays { get; internal init; } = [];

    /// <summary>Whether the binary wrote the message itself, rather than the model: a local command's answer is one.</summary>
    public bool IsMeta { get; internal init; }

    /// <summary>The local command the binary answered with this message, when one did.</summary>
    public string? LocalCommandSource { get; internal init; }

    /// <summary>The text blocks, joined.</summary>
    public string Text => string.Concat(Content.OfType<TextBlock>().Select(block => block.Text));

    /// <summary>The tool-use blocks of <see cref="Content"/>.</summary>
    public IEnumerable<ToolUseBlock> ToolUses => Content.OfType<ToolUseBlock>();

    private static readonly IReadOnlyDictionary<string, JsonElement> EmptyInputs = new Dictionary<string, JsonElement>();
}

/// <summary>What to call one tool use, when the tool has a name of its own to show.</summary>
public sealed class ToolUseDisplay
{
    internal ToolUseDisplay(string id, string displayName, string serverDisplayName)
    {
        Id = id;
        DisplayName = displayName;
        ServerDisplayName = serverDisplayName;
    }

    /// <summary>The tool use this is about.</summary>
    public string Id { get; }

    /// <summary>The tool's name to show.</summary>
    public string DisplayName { get; }

    /// <summary>The server's name to show.</summary>
    public string ServerDisplayName { get; }
}
