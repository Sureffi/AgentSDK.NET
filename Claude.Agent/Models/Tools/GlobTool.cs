namespace Claude.Agent;

/// <summary>What the model asked the Glob tool to match.</summary>
/// <param name="pattern">The glob pattern.</param>
/// <param name="path">The directory to match under; absent means the working directory.</param>
public sealed class GlobToolInput(string pattern, string? path = null) : ToolInput
{
    /// <summary>The glob pattern.</summary>
    public string Pattern { get; } = pattern;

    /// <summary>The directory to match under; null when the call named none.</summary>
    public string? Path { get; } = path;
}

/// <summary>What a Glob handed back.</summary>
/// <param name="duration">How long the match took; the wire carries it in milliseconds.</param>
/// <param name="fileCount">How many files are named here.</param>
/// <param name="fileNames">The paths that matched.</param>
/// <param name="truncated">Whether more matched than are named here.</param>
/// <param name="totalMatchCount">How many matched in all.</param>
/// <param name="countIsComplete">Whether the total is the whole count rather than a floor.</param>
public sealed class GlobToolOutput(
    TimeSpan duration,
    long fileCount,
    IReadOnlyList<string> fileNames,
    bool truncated,
    long? totalMatchCount = null,
    bool? countIsComplete = null) : ToolOutput
{
    /// <summary>How long the match took; the wire carries it in milliseconds under <c>durationMs</c>.</summary>
    public TimeSpan Duration { get; } = duration;

    /// <summary>How many files are named here; the wire carries it under <c>numFiles</c>.</summary>
    public long FileCount { get; } = fileCount;

    /// <summary>The paths that matched.</summary>
    public IReadOnlyList<string> FileNames { get; } = fileNames;

    /// <summary>Whether more matched than are named here.</summary>
    public bool Truncated { get; } = truncated;

    /// <summary>How many matched in all; null when the binary gave no total.</summary>
    public long? TotalMatchCount { get; } = totalMatchCount;

    /// <summary>Whether the total is the whole count rather than a floor; null when the binary said nothing.</summary>
    public bool? CountIsComplete { get; } = countIsComplete;
}
