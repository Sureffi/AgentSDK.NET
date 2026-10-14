namespace Claude.Agent;

/// <summary>What the session has spent so far and where the account stands, as an assistant frame reports it.</summary>
public sealed class UsageReport
{
    internal UsageReport(UsageReportSession session) => Session = session;

    /// <summary>The session's own totals.</summary>
    public UsageReportSession Session { get; }

    /// <summary>The account's limits, when the binary knows them.</summary>
    public UsageReportRateLimits? RateLimits { get; internal init; }
}

/// <summary>What a session has spent since it started.</summary>
public sealed class UsageReportSession
{
    internal UsageReportSession(
        decimal totalCostUsd,
        TimeSpan totalApiDuration,
        TimeSpan totalDuration,
        long totalLinesAdded,
        long totalLinesRemoved)
    {
        TotalCostUsd = totalCostUsd;
        TotalApiDuration = totalApiDuration;
        TotalDuration = totalDuration;
        TotalLinesAdded = totalLinesAdded;
        TotalLinesRemoved = totalLinesRemoved;
    }

    /// <summary>What the session has cost, in US dollars.</summary>
    public decimal TotalCostUsd { get; }

    /// <summary>How long the session has spent waiting on the API.</summary>
    public TimeSpan TotalApiDuration { get; }

    /// <summary>How long the session has run.</summary>
    public TimeSpan TotalDuration { get; }

    /// <summary>The lines the session's edits added.</summary>
    public long TotalLinesAdded { get; }

    /// <summary>The lines the session's edits removed.</summary>
    public long TotalLinesRemoved { get; }

    /// <summary>What each model spent, by model id.</summary>
    public IReadOnlyDictionary<string, ModelUsage> ModelUsage { get; internal init; } = EmptyModelUsage;

    private static readonly IReadOnlyDictionary<string, ModelUsage> EmptyModelUsage = new Dictionary<string, ModelUsage>();
}

/// <summary>Where the account stands against its limits, as a usage report gives it.</summary>
public sealed class UsageReportRateLimits
{
    internal UsageReportRateLimits()
    {
    }

    /// <summary>One entry per limit the account is under.</summary>
    public IReadOnlyList<UsageReportLimit> Limits { get; internal init; } = [];

    /// <summary>What extra usage beyond the limits stands at, when the account has it.</summary>
    public UsageReportExtraUsage? ExtraUsage { get; internal init; }
}

/// <summary>One limit the account is under, and how much of it is used.</summary>
public sealed class UsageReportLimit
{
    internal UsageReportLimit(string kind, string group, double percent)
    {
        Kind = kind;
        Group = group;
        Percent = percent;
    }

    /// <summary>Which limit it is, in the binary's own words.</summary>
    public string Kind { get; }

    /// <summary>The group the limit is counted over, in the binary's own words.</summary>
    public string Group { get; }

    /// <summary>How much of it is used, from 0 to 1.</summary>
    public double Percent { get; }

    /// <summary>When it resets, as the binary wrote it.</summary>
    public string? ResetsAt { get; internal init; }

    /// <summary>What the limit is about: a model, a surface, or both.</summary>
    public UsageReportLimitScope? Scope { get; internal init; }

    /// <summary>How pressing the limit is, in the binary's own words.</summary>
    public string? Severity { get; internal init; }

    /// <summary>Whether the limit is biting now.</summary>
    public bool? IsActive { get; internal init; }
}

/// <summary>What a limit is counted over.</summary>
public sealed class UsageReportLimitScope
{
    internal UsageReportLimitScope()
    {
    }

    /// <summary>The model it is about, when it is about one.</summary>
    public UsageReportTarget? Model { get; internal init; }

    /// <summary>The surface it is about, when it is about one.</summary>
    public UsageReportTarget? Surface { get; internal init; }
}

/// <summary>What a limit's scope names, as the binary would show it.</summary>
public sealed class UsageReportTarget
{
    internal UsageReportTarget(string displayName) => DisplayName = displayName;

    /// <summary>The name to show.</summary>
    public string DisplayName { get; }
}

/// <summary>What the account may spend past its limits, and what it has spent.</summary>
public sealed class UsageReportExtraUsage
{
    internal UsageReportExtraUsage(bool isEnabled) => IsEnabled = isEnabled;

    /// <summary>Whether extra usage is on.</summary>
    public bool IsEnabled { get; }

    /// <summary>What may be spent in a month, when there is a ceiling.</summary>
    public double? MonthlyLimit { get; internal init; }

    /// <summary>The credits spent so far.</summary>
    public double? UsedCredits { get; internal init; }

    /// <summary>How much of the ceiling is used, from 0 to 1.</summary>
    public double? Utilization { get; internal init; }

    /// <summary>The currency the credits are counted in.</summary>
    public string? Currency { get; internal init; }
}
