namespace Claude.Agent;

/// <summary>What kind of work a background task is, as a stop hook is told: the label the binary gives the task's kind, or the kind's own name when the binary has no label for it. A value this library does not name is read as it is.</summary>
public readonly struct StopHookBackgroundTaskType : IEquatable<StopHookBackgroundTaskType>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public StopHookBackgroundTaskType(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>subagent</c>: a subagent on this machine.</summary>
    public static StopHookBackgroundTaskType Subagent { get; } = new("subagent");

    /// <summary><c>workflow</c>: a workflow on this machine.</summary>
    public static StopHookBackgroundTaskType Workflow { get; } = new("workflow");

    /// <summary><c>shell</c>: a shell command, or a monitor the binary runs as one.</summary>
    public static StopHookBackgroundTaskType Shell { get; } = new("shell");

    /// <summary><c>monitor</c>: a monitor watching an MCP server's tool or a web socket.</summary>
    public static StopHookBackgroundTaskType Monitor { get; } = new("monitor");

    /// <summary><c>MCP task</c>: a task an MCP server runs for a tool call.</summary>
    public static StopHookBackgroundTaskType McpTask { get; } = new("MCP task");

    /// <summary><c>teammate</c>: a teammate running in this process.</summary>
    public static StopHookBackgroundTaskType Teammate { get; } = new("teammate");

    /// <summary><c>dream</c>: the binary consolidating its memories.</summary>
    public static StopHookBackgroundTaskType Dream { get; } = new("dream");

    /// <summary><c>auto-mode scan</c>: a scan auto mode runs.</summary>
    public static StopHookBackgroundTaskType AutoModeScan { get; } = new("auto-mode scan");

    /// <summary><c>cloud session</c>: an agent running in a cloud session.</summary>
    public static StopHookBackgroundTaskType CloudSession { get; } = new("cloud session");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "subagent" or "workflow" or "shell" or "monitor" or "MCP task" or "teammate" or "dream" or
        "auto-mode scan" or "cloud session";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(StopHookBackgroundTaskType left, StopHookBackgroundTaskType right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(StopHookBackgroundTaskType left, StopHookBackgroundTaskType right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator StopHookBackgroundTaskType(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(StopHookBackgroundTaskType other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="StopHookBackgroundTaskType"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is StopHookBackgroundTaskType other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
