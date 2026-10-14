using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// A hook's reply. Leave everything null and the binary carries on as if the hook had said nothing.
/// The event-specific members are written under the event the hook was registered for.
/// </summary>
public sealed class HookOutput
{
    // A JsonElement handed in is copied, so a hook may dispose the document it parsed it from.
    private PermissionDecision? _decision;
    private JsonElement? _rawUpdatedInput;
    private JsonElement? _rawUpdatedToolOutput;
    private JsonElement? _updatedMcpToolOutput;

    /// <summary>Reply now and let the hook keep running; the binary waits at most <see cref="AsyncTimeout"/> for its side effects.</summary>
    public bool IsAsync { get; set; }

    /// <summary>How long the binary waits for a hook that replied with <see cref="IsAsync"/>; it goes out as whole milliseconds.</summary>
    public TimeSpan? AsyncTimeout { get; set; }

    /// <summary>False stops the binary's processing outright.</summary>
    public bool? Continue { get; set; }

    /// <summary>Keep the hook's output out of the transcript.</summary>
    public bool? SuppressOutput { get; set; }

    /// <summary>What to show when <see cref="Continue"/> is false.</summary>
    public string? StopReason { get; set; }

    /// <summary>Block the action the hook is about; <see cref="Reason"/> says why.</summary>
    public bool Block { get; set; }

    /// <summary>A message shown to the user.</summary>
    public string? SystemMessage { get; set; }

    /// <summary>Why the hook blocked; fed back to the model.</summary>
    public string? Reason { get; set; }

    // A closed set is open to a value this library has not named and closed against no value at
    // all: the default names nothing, so there would be nothing for the binary to read.
    /// <summary>PreToolUse: allow, deny or ask instead of the binary's own decision.</summary>
    /// <exception cref="ArgumentException">The value names nothing.</exception>
    public PermissionDecision? Decision
    {
        get => _decision;
        set
        {
            if (value is { } decision && decision.ToString().Length == 0)
            {
                throw new ArgumentException("A hook's output names a decision; the value is empty.", nameof(value));
            }

            _decision = value;
        }
    }

    /// <summary>PreToolUse: why <see cref="Decision"/> was made.</summary>
    public string? DecisionReason { get; set; }

    /// <summary>PreToolUse: the tool input to use instead, as the tool's own type. <see cref="RawUpdatedInput"/> wins when both are set.</summary>
    public ToolInput? UpdatedInput { get; set; }

    /// <summary>PreToolUse: the tool input to use instead, as JSON. Set it for a tool this library has no type for, and for an MCP server's tool, whose shape is someone else's. Set beside <see cref="UpdatedInput"/> it is the one that goes out.</summary>
    public JsonElement? RawUpdatedInput
    {
        get => _rawUpdatedInput;
        set => _rawUpdatedInput = value?.Clone();
    }

    /// <summary>Text the model sees in addition to the tool's own output or the prompt.</summary>
    public string? AdditionalContext { get; set; }

    /// <summary>PostToolUse: what the model sees instead of the tool's output, as the tool's own type. <see cref="RawUpdatedToolOutput"/> wins when both are set.</summary>
    public ToolOutput? UpdatedToolOutput { get; set; }

    /// <summary>PostToolUse: what the model sees instead of the tool's output, as JSON. Set it for a tool this library has no type for. Set beside <see cref="UpdatedToolOutput"/> it is the one that goes out.</summary>
    public JsonElement? RawUpdatedToolOutput
    {
        get => _rawUpdatedToolOutput;
        set => _rawUpdatedToolOutput = value?.Clone();
    }

    /// <summary>PostToolUse: what the model sees instead of an MCP tool's output. An MCP server's shape is someone else's, so this one is JSON and has no typed partner.</summary>
    public JsonElement? UpdatedMcpToolOutput
    {
        get => _updatedMcpToolOutput;
        set => _updatedMcpToolOutput = value?.Clone();
    }

    /// <summary>PermissionRequest: the decision, in place of asking a person.</summary>
    public ToolPermissionResult? PermissionRequestDecision { get; set; }

    internal bool HasEventSpecificOutput =>
        Decision is not null || DecisionReason is not null || UpdatedInput is not null || RawUpdatedInput is not null
        || AdditionalContext is not null || UpdatedToolOutput is not null || RawUpdatedToolOutput is not null
        || UpdatedMcpToolOutput is not null || PermissionRequestDecision is not null;
}
