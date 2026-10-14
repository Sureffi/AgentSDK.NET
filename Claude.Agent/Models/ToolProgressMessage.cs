using System.Text.Json;

namespace Claude.Agent;

/// <summary>A tool the model called is still running, and this is how long it has been.</summary>
public sealed class ToolProgressMessage : AgentMessage
{
    internal ToolProgressMessage(
        string toolUseId,
        string toolName,
        TimeSpan elapsedTime,
        string? uuid,
        string? sessionId,
        JsonElement? raw)
        : base(uuid, sessionId, raw)
    {
        ToolUseId = toolUseId;
        ToolName = toolName;
        ElapsedTime = elapsedTime;
    }

    /// <summary>The tool use that is running.</summary>
    public string ToolUseId { get; }

    /// <summary>The tool the model called.</summary>
    public string ToolName { get; }

    /// <summary>How long it has been running.</summary>
    public TimeSpan ElapsedTime { get; }

    /// <summary>The Agent tool use that started the subagent this belongs to; null on the main thread.</summary>
    public string? ParentToolUseId { get; internal init; }

    /// <summary>The background task running it, when one is.</summary>
    public string? TaskId { get; internal init; }

    /// <summary>Whether the notice is only to say the tool is alive.</summary>
    public bool? Heartbeat { get; internal init; }

    /// <summary>The subagent running it, by name.</summary>
    public string? SubagentType { get; internal init; }

    /// <summary>Set when the subagent is being started again after a failure.</summary>
    public SubagentRetry? SubagentRetry { get; internal init; }
}

/// <summary>A subagent being started again after a failure, and what failed.</summary>
public sealed class SubagentRetry
{
    internal SubagentRetry(string agentId, long attempt, long maxRetries, TimeSpan retryDelay, string errorCategory)
    {
        AgentId = agentId;
        Attempt = attempt;
        MaxRetries = maxRetries;
        RetryDelay = retryDelay;
        ErrorCategory = errorCategory;
    }

    /// <summary>The subagent being started again.</summary>
    public string AgentId { get; }

    /// <summary>Which attempt this is.</summary>
    public long Attempt { get; }

    /// <summary>How many attempts the binary will make.</summary>
    public long MaxRetries { get; }

    /// <summary>How long it waits before the next one.</summary>
    public TimeSpan RetryDelay { get; }

    /// <summary>What kind of failure it was, in the binary's own words.</summary>
    public string ErrorCategory { get; }

    /// <summary>The HTTP status that failed, when there was one.</summary>
    public long? ErrorStatus { get; internal init; }
}
