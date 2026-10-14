using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// A hook the binary itself runs has started. These arrive only when
/// <see cref="ClaudeAgentSessionOptions.IncludeHookEvents"/> is on; a hook this library runs is
/// called over the control channel instead.
/// </summary>
public sealed class HookStartedMessage : SystemMessage
{
    internal HookStartedMessage(string hookId, string hookName, HookEvent hookEvent, string? uuid, string? sessionId, JsonElement? raw)
        : base("hook_started", uuid, sessionId, raw)
    {
        HookId = hookId;
        HookName = hookName;
        HookEvent = hookEvent;
    }

    /// <summary>The run of the hook, by the id the binary gives it.</summary>
    public string HookId { get; }

    /// <summary>The hook, as the binary names it: the event and the matcher it was registered under.</summary>
    public string HookName { get; }

    /// <summary>The event that fired it.</summary>
    public HookEvent HookEvent { get; }
}

/// <summary>A hook the binary runs has written something while running.</summary>
public sealed class HookProgressMessage : SystemMessage
{
    internal HookProgressMessage(
        string hookId,
        string hookName,
        HookEvent hookEvent,
        string standardOutput,
        string standardError,
        string output,
        string? uuid,
        string? sessionId,
        JsonElement? raw)
        : base("hook_progress", uuid, sessionId, raw)
    {
        HookId = hookId;
        HookName = hookName;
        HookEvent = hookEvent;
        StandardOutput = standardOutput;
        StandardError = standardError;
        Output = output;
    }

    /// <summary>The run of the hook, by the id the binary gives it.</summary>
    public string HookId { get; }

    /// <summary>The hook, as the binary names it.</summary>
    public string HookName { get; }

    /// <summary>The event that fired it.</summary>
    public HookEvent HookEvent { get; }

    /// <summary>What it has written to stdout.</summary>
    public string StandardOutput { get; }

    /// <summary>What it has written to stderr.</summary>
    public string StandardError { get; }

    /// <summary>What the binary takes as its answer so far.</summary>
    public string Output { get; }
}

/// <summary>A hook the binary runs has ended, and what it said.</summary>
public sealed class HookResponseMessage : SystemMessage
{
    internal HookResponseMessage(
        string hookId,
        string hookName,
        HookEvent hookEvent,
        string output,
        string standardOutput,
        string standardError,
        HookOutcome outcome,
        string? uuid,
        string? sessionId,
        JsonElement? raw)
        : base("hook_response", uuid, sessionId, raw)
    {
        HookId = hookId;
        HookName = hookName;
        HookEvent = hookEvent;
        Output = output;
        StandardOutput = standardOutput;
        StandardError = standardError;
        Outcome = outcome;
    }

    /// <summary>The run of the hook, by the id the binary gives it.</summary>
    public string HookId { get; }

    /// <summary>The hook, as the binary names it.</summary>
    public string HookName { get; }

    /// <summary>The event that fired it.</summary>
    public HookEvent HookEvent { get; }

    /// <summary>What the binary took as its answer.</summary>
    public string Output { get; }

    /// <summary>What it wrote to stdout.</summary>
    public string StandardOutput { get; }

    /// <summary>What it wrote to stderr.</summary>
    public string StandardError { get; }

    /// <summary>How it ended.</summary>
    public HookOutcome Outcome { get; }

    /// <summary>The code it exited with, when the binary reports one.</summary>
    public long? ExitCode { get; internal init; }
}

/// <summary>The binary refused a tool use, and why.</summary>
public sealed class PermissionDeniedMessage : SystemMessage
{
    internal PermissionDeniedMessage(
        string toolName,
        string toolUseId,
        string message,
        string? uuid,
        string? sessionId,
        JsonElement? raw)
        : base("permission_denied", uuid, sessionId, raw)
    {
        ToolName = toolName;
        ToolUseId = toolUseId;
        Message = message;
    }

    /// <summary>The tool the model asked for.</summary>
    public string ToolName { get; }

    /// <summary>The refused tool use.</summary>
    public string ToolUseId { get; }

    /// <summary>What the binary says about the refusal.</summary>
    public string Message { get; }

    /// <summary>The subagent whose tool use was refused, when it was a subagent's.</summary>
    public string? AgentId { get; internal init; }

    /// <summary>What decided the refusal, in the binary's own words.</summary>
    public string? DecisionReasonType { get; internal init; }

    /// <summary>What that decision was about, in the binary's own words.</summary>
    public string? DecisionReason { get; internal init; }
}
