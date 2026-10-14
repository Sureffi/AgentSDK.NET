namespace Claude.Agent;

/// <summary>A moment the binary calls a hook at. An event this library does not name may be registered for, and its input arrives as the binary wrote it.</summary>
public readonly struct HookEvent : IEquatable<HookEvent>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public HookEvent(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary>Before a tool runs; the hook may allow, deny, ask, defer or change the input.</summary>
    public static HookEvent PreToolUse { get; } = new("PreToolUse");

    /// <summary>After a tool ran.</summary>
    public static HookEvent PostToolUse { get; } = new("PostToolUse");

    /// <summary>After a tool failed.</summary>
    public static HookEvent PostToolUseFailure { get; } = new("PostToolUseFailure");

    /// <summary>After a batch of tool uses ran together.</summary>
    public static HookEvent PostToolBatch { get; } = new("PostToolBatch");

    /// <summary>When a prompt arrives, before the model sees it.</summary>
    public static HookEvent UserPromptSubmit { get; } = new("UserPromptSubmit");

    /// <summary>When the binary expands a prompt before the model sees it.</summary>
    public static HookEvent UserPromptExpansion { get; } = new("UserPromptExpansion");

    /// <summary>When the model is about to end its turn.</summary>
    public static HookEvent Stop { get; } = new("Stop");

    /// <summary>When a turn ends in a failure.</summary>
    public static HookEvent StopFailure { get; } = new("StopFailure");

    /// <summary>When a subagent starts.</summary>
    public static HookEvent SubagentStart { get; } = new("SubagentStart");

    /// <summary>When a subagent is about to end.</summary>
    public static HookEvent SubagentStop { get; } = new("SubagentStop");

    /// <summary>Before the conversation is compacted.</summary>
    public static HookEvent PreCompact { get; } = new("PreCompact");

    /// <summary>After the conversation was compacted.</summary>
    public static HookEvent PostCompact { get; } = new("PostCompact");

    /// <summary>When the binary notifies the user.</summary>
    public static HookEvent Notification { get; } = new("Notification");

    /// <summary>When the binary is about to show a message.</summary>
    public static HookEvent MessageDisplay { get; } = new("MessageDisplay");

    /// <summary>When the binary is about to ask for permission; the hook may answer instead.</summary>
    public static HookEvent PermissionRequest { get; } = new("PermissionRequest");

    /// <summary>After a tool use was refused.</summary>
    public static HookEvent PermissionDenied { get; } = new("PermissionDenied");

    /// <summary>When a session starts.</summary>
    public static HookEvent SessionStart { get; } = new("SessionStart");

    /// <summary>When a session ends.</summary>
    public static HookEvent SessionEnd { get; } = new("SessionEnd");

    /// <summary>When the binary runs its setup for a working directory.</summary>
    public static HookEvent Setup { get; } = new("Setup");

    /// <summary>When a setting the binary reads changes.</summary>
    public static HookEvent ConfigChange { get; } = new("ConfigChange");

    /// <summary>When the directory the binary runs in changes.</summary>
    public static HookEvent WorkingDirectoryChanged { get; } = new("CwdChanged");

    /// <summary>When a directory is added to what the tools may reach.</summary>
    public static HookEvent DirectoryAdded { get; } = new("DirectoryAdded");

    /// <summary>When a file the session is watching changes.</summary>
    public static HookEvent FileChanged { get; } = new("FileChanged");

    /// <summary>When the binary loads instructions for the session.</summary>
    public static HookEvent InstructionsLoaded { get; } = new("InstructionsLoaded");

    /// <summary>Before the binary switches model.</summary>
    public static HookEvent PreModelSwitch { get; } = new("PreModelSwitch");

    /// <summary>After the binary switched model.</summary>
    public static HookEvent PostModelSwitch { get; } = new("PostModelSwitch");

    /// <summary>When an MCP server asks the user something.</summary>
    public static HookEvent Elicitation { get; } = new("Elicitation");

    /// <summary>When an MCP server's question has an answer.</summary>
    public static HookEvent ElicitationResult { get; } = new("ElicitationResult");

    /// <summary>When a background task is created.</summary>
    public static HookEvent TaskCreated { get; } = new("TaskCreated");

    /// <summary>When a background task is done.</summary>
    public static HookEvent TaskCompleted { get; } = new("TaskCompleted");

    /// <summary>When a teammate session has nothing to do.</summary>
    public static HookEvent TeammateIdle { get; } = new("TeammateIdle");

    /// <summary>When a git worktree is created for the session.</summary>
    public static HookEvent WorktreeCreate { get; } = new("WorktreeCreate");

    /// <summary>When a git worktree of the session is removed.</summary>
    public static HookEvent WorktreeRemove { get; } = new("WorktreeRemove");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "PreToolUse" or "PostToolUse" or "PostToolUseFailure" or "PostToolBatch" or "UserPromptSubmit" or
        "UserPromptExpansion" or "Stop" or "StopFailure" or "SubagentStart" or "SubagentStop" or "PreCompact" or
        "PostCompact" or "Notification" or "MessageDisplay" or "PermissionRequest" or "PermissionDenied" or
        "SessionStart" or "SessionEnd" or "Setup" or "ConfigChange" or "CwdChanged" or "DirectoryAdded" or
        "FileChanged" or "InstructionsLoaded" or "PreModelSwitch" or "PostModelSwitch" or "Elicitation" or
        "ElicitationResult" or "TaskCreated" or "TaskCompleted" or "TeammateIdle" or "WorktreeCreate" or
        "WorktreeRemove";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(HookEvent left, HookEvent right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(HookEvent left, HookEvent right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator HookEvent(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(HookEvent other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="HookEvent"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is HookEvent other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
