using System.Text.Json;

namespace Claude.Agent;

/// <summary>The goal the session is working towards, as the binary now holds it; cleared when there is none.</summary>
public sealed class ActiveGoalMessage : AgentMessage
{
    internal ActiveGoalMessage(ActiveGoal? value, string? uuid, string? sessionId, JsonElement? raw)
        : base(uuid, sessionId, raw)
    {
        Value = value;
    }

    /// <summary>The goal; null when the session no longer has one.</summary>
    public ActiveGoal? Value { get; }
}

/// <summary>A goal the session is working towards until it is met.</summary>
public sealed class ActiveGoal
{
    internal ActiveGoal(string condition, long iterations, DateTimeOffset? setAt, long tokensAtStart)
    {
        Condition = condition;
        Iterations = iterations;
        SetAt = setAt;
        TokensAtStart = tokensAtStart;
    }

    /// <summary>What has to hold for the goal to be met.</summary>
    public string Condition { get; }

    /// <summary>The turns spent on it so far.</summary>
    public long Iterations { get; }

    /// <summary>When it was set.</summary>
    public DateTimeOffset? SetAt { get; }

    /// <summary>The tokens the conversation took when it was set.</summary>
    public long TokensAtStart { get; }

    /// <summary>Why the last turn did not meet it.</summary>
    public string? LastReason { get; internal init; }
}
