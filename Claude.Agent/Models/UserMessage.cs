using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// A user turn the binary echoed: a tool result it fed the model, or, under
/// <see cref="ClaudeAgentSessionOptions.ReplayUserMessages"/>, the prompt itself with the id
/// <see cref="ClaudeAgentSession.RewindFilesAsync"/> takes. <see cref="IsReplay"/> says which of the
/// two this is.
/// </summary>
public sealed class UserMessage : AgentMessage
{
    private ToolOutput? _toolOutput;
    private int _toolOutputRead;

    internal UserMessage(IReadOnlyList<ContentBlock> content, string? uuid, string? sessionId, JsonElement? raw)
        : base(uuid, sessionId, raw)
    {
        Content = content;
    }

    /// <summary>The blocks; a prompt written as a string is one <see cref="TextBlock"/>.</summary>
    public IReadOnlyList<ContentBlock> Content { get; }

    /// <summary>Who the API message is from; the binary writes "user" on this frame.</summary>
    public MessageRole? Role { get; internal init; }

    /// <summary>Whether this is a prompt played back, rather than a turn the binary is feeding the model.</summary>
    public bool IsReplay { get; internal init; }

    /// <summary>The Agent tool use that started the subagent this message belongs to; null on the main thread.</summary>
    public string? ParentToolUseId { get; internal init; }

    /// <summary>
    /// What the tool reported beside the content the model was fed, as the tool's typed output. The
    /// frame does not name its tool, so the name is the one of the call its tool result answers; null
    /// when the run never saw that call start, when the call had already been closed unanswered, when
    /// the frame answers more than one call at once, when this library has no type for the tool, or
    /// when the output does not fit it. <see cref="RawToolOutput"/> is there in every one of those
    /// cases.
    /// </summary>
    public ToolOutput? ToolOutput
    {
        get => _toolOutput;
        internal init
        {
            _toolOutput = value;
            _toolOutputRead = 1;
        }
    }

    /// <summary>The frame's <c>tool_use_result</c> as the binary wrote it: what the tool reported, in the tool's own shape.</summary>
    public JsonElement? RawToolOutput { get; internal init; }

    /// <summary>Whether the binary wrote the turn itself, rather than a person or a tool.</summary>
    public bool? IsSynthetic { get; internal init; }

    /// <summary>Where the turn goes in the binary's queue, when the frame says.</summary>
    public UserPriority? Priority { get; internal init; }

    /// <summary>Where the turn came from, when the frame says.</summary>
    public MessageOrigin? Origin { get; internal init; }

    /// <summary>Whether the turn is to be answered by the model, when the frame says.</summary>
    public bool? ShouldQuery { get; internal init; }

    /// <summary>When the binary wrote the frame.</summary>
    public DateTimeOffset? Timestamp { get; internal init; }

    /// <summary>The subagent the turn belongs to, by name; set on a subagent's messages.</summary>
    public string? SubagentType { get; internal init; }

    /// <summary>What the subagent was asked for; set on a subagent's messages.</summary>
    public string? TaskDescription { get; internal init; }

    /// <summary>Why a tool result exists for a tool that never ran, one entry per tool use.</summary>
    public IReadOnlyList<ToolResultNote> ToolResultNotes { get; internal init; } = [];

    /// <summary>The text blocks, joined.</summary>
    public string Text => string.Concat(Content.OfType<TextBlock>().Select(block => block.Text));

    // The pairing types the output once, before anyone is handed the message, and never again.
    internal void AdoptToolOutput(ToolOutput? output)
    {
        if (Interlocked.Exchange(ref _toolOutputRead, 1) == 0)
        {
            _toolOutput = output;
        }
    }
}

/// <summary>Why one tool use ended with a result the tool did not produce.</summary>
public sealed class ToolResultNote
{
    internal ToolResultNote(string id, string nonExecutionKind)
    {
        Id = id;
        NonExecutionKind = nonExecutionKind;
    }

    /// <summary>The tool use this is about.</summary>
    public string Id { get; }

    /// <summary>Why the tool did not run, in the binary's own words.</summary>
    public string NonExecutionKind { get; }
}
