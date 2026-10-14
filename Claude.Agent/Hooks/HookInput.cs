using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// What the binary sent a hook. One type per event, over this base: the members here are the ones
/// every event carries, the event's own are on its type. An event this library has no type for, and
/// an event registered under a name it does not name, arrive as <see cref="UnknownHookInput"/>.
/// </summary>
public abstract class HookInput
{
    private protected HookInput(HookEvent hookEvent, HookInputCommon common)
    {
        ArgumentNullException.ThrowIfNull(common);

        Event = hookEvent;
        SessionId = common.SessionId;
        TranscriptPath = common.TranscriptPath;
        WorkingDirectory = common.WorkingDirectory;
        PromptId = common.PromptId;
        PermissionMode = common.PermissionMode;
        AgentId = common.AgentId;
        AgentType = common.AgentType;
        Effort = common.Effort;
        ScratchpadDirectory = common.ScratchpadDirectory;
        Json = common.Raw;
    }

    /// <summary>The event the binary fired, as it named it; one this library does not name is kept as it came.</summary>
    public HookEvent Event { get; }

    /// <summary>The session the hook fired in.</summary>
    public string SessionId { get; }

    /// <summary>The session's transcript file.</summary>
    public string TranscriptPath { get; }

    /// <summary>The directory the binary runs in.</summary>
    public string WorkingDirectory { get; }

    /// <summary>What ties every event of one prompt together, until the next prompt; null before the first prompt of the process.</summary>
    public string? PromptId { get; }

    /// <summary>The permission mode in effect; null when the input carried none.</summary>
    public PermissionMode? PermissionMode { get; }

    /// <summary>The subagent the hook fired in; null on the main thread.</summary>
    public string? AgentId { get; }

    /// <summary>The agent's type, the name it was started by; set inside a subagent, and on the main thread of a session started as one agent.</summary>
    public AgentType? AgentType { get; }

    /// <summary>The effort the turn is running at; null for an event outside a turn, and on a model that takes no effort.</summary>
    public Effort? Effort { get; }

    /// <summary>The directory the binary keeps this session's scratch files in; null when the input carried none.</summary>
    public string? ScratchpadDirectory { get; }

    // The whole input as the binary wrote it, which the codec writes back when it encodes the
    // callback that carried it. A caller reads it on the unknown, where it is the input.
    internal JsonElement Json { get; }
}

// What every hook input carries, gathered so that a type per event does not repeat eleven
// parameters. Built by the parser from one frame and by the model factory from a test's arguments.
internal sealed class HookInputCommon
{
    public required string SessionId { get; init; }

    public required string TranscriptPath { get; init; }

    public required string WorkingDirectory { get; init; }

    public string? PromptId { get; init; }

    public PermissionMode? PermissionMode { get; init; }

    public string? AgentId { get; init; }

    public AgentType? AgentType { get; init; }

    public Effort? Effort { get; init; }

    public string? ScratchpadDirectory { get; init; }

    public required JsonElement Raw { get; init; }
}
