namespace Claude.Agent;

/// <summary>What the subagents of a turn did, counted by the binary on the result.</summary>
public sealed class SubagentStatistics
{
    internal SubagentStatistics(
        long spawned,
        long spawnedBySubagents,
        long startedInBackground,
        long completed,
        long failed,
        long maxDepth)
    {
        Spawned = spawned;
        SpawnedBySubagents = spawnedBySubagents;
        StartedInBackground = startedInBackground;
        Completed = completed;
        Failed = failed;
        MaxDepth = maxDepth;
    }

    /// <summary>The subagents started.</summary>
    public long Spawned { get; }

    /// <summary>How many of them another subagent started.</summary>
    public long SpawnedBySubagents { get; }

    /// <summary>How many were started in the background.</summary>
    public long StartedInBackground { get; }

    /// <summary>How many finished their work.</summary>
    public long Completed { get; }

    /// <summary>How many ended in an error.</summary>
    public long Failed { get; }

    /// <summary>The deepest a subagent's own subagents went.</summary>
    public long MaxDepth { get; }

    /// <summary>How many were asked for, by whether the caller asked for the background.</summary>
    public SubagentRequestCounts? Requested { get; internal init; }

    /// <summary>How many were stopped, by who stopped them.</summary>
    public SubagentKillCounts? Killed { get; internal init; }

    /// <summary>How many were refused, by what refused them.</summary>
    public SubagentRefusalCounts? Refused { get; internal init; }

    /// <summary>How many were started, by subagent name.</summary>
    public IReadOnlyDictionary<string, long> ByType { get; internal init; } = EmptyCounts;

    private static readonly IReadOnlyDictionary<string, long> EmptyCounts = new Dictionary<string, long>();
}

/// <summary>Subagents asked for, by whether the caller asked for the background.</summary>
public sealed class SubagentRequestCounts
{
    internal SubagentRequestCounts(long background, long foreground, long unset)
    {
        Background = background;
        Foreground = foreground;
        Unset = unset;
    }

    /// <summary>Asked for in the background.</summary>
    public long Background { get; }

    /// <summary>Asked for in the foreground.</summary>
    public long Foreground { get; }

    /// <summary>Asked for without saying which.</summary>
    public long Unset { get; }
}

/// <summary>Subagents stopped, by who stopped them.</summary>
public sealed class SubagentKillCounts
{
    internal SubagentKillCounts(long parent, long user, long system)
    {
        Parent = parent;
        User = user;
        System = system;
    }

    /// <summary>Stopped by the agent that started them.</summary>
    public long Parent { get; }

    /// <summary>Stopped by the person.</summary>
    public long User { get; }

    /// <summary>Stopped by the binary.</summary>
    public long System { get; }
}

/// <summary>Subagents refused, by what refused them.</summary>
public sealed class SubagentRefusalCounts
{
    internal SubagentRefusalCounts(long budget, long concurrencyLimit, long depthLimit)
    {
        Budget = budget;
        ConcurrencyLimit = concurrencyLimit;
        DepthLimit = depthLimit;
    }

    /// <summary>Refused because the budget was spent.</summary>
    public long Budget { get; }

    /// <summary>Refused because too many were already running.</summary>
    public long ConcurrencyLimit { get; }

    /// <summary>Refused because they would have gone too deep.</summary>
    public long DepthLimit { get; }
}
