namespace Claude.Agent;

/// <summary>One finding of a code review, as it goes in and as it comes back.</summary>
/// <param name="file">The file the finding is in, relative to the repository.</param>
/// <param name="summary">The defect in one sentence.</param>
/// <param name="failureScenario">What input or state makes it go wrong, and how.</param>
/// <param name="line">The line it is anchored to, counted from one; null when it is anchored to none.</param>
/// <param name="shortSummary">The claim alone, for a narrow view; null when none was given.</param>
/// <param name="category">What kind of finding it is, as a short slug; null when none was given.</param>
/// <param name="verdict">How sure the review is; null where no verify pass ran.</param>
/// <param name="outcome">What became of it; null unless the findings were re-reported after the fixes.</param>
public sealed class ReportFindingsToolFinding(
    string file,
    string summary,
    string failureScenario,
    long? line = null,
    string? shortSummary = null,
    string? category = null,
    ReportFindingsVerdict? verdict = null,
    ReportFindingsOutcome? outcome = null)
{
    /// <summary>The file the finding is in, relative to the repository.</summary>
    public string File { get; } = file;

    /// <summary>The defect in one sentence.</summary>
    public string Summary { get; } = summary;

    /// <summary>What input or state makes it go wrong, and how.</summary>
    public string FailureScenario { get; } = failureScenario;

    /// <summary>The line it is anchored to, counted from one; null when it is anchored to none.</summary>
    public long? Line { get; } = line;

    /// <summary>The claim alone, for a narrow view; null when none was given.</summary>
    public string? ShortSummary { get; } = shortSummary;

    /// <summary>What kind of finding it is, as a short slug; null when none was given.</summary>
    public string? Category { get; } = category;

    /// <summary>How sure the review is; null where no verify pass ran.</summary>
    public ReportFindingsVerdict? Verdict { get; } = verdict;

    /// <summary>What became of it; null unless the findings were re-reported after the fixes.</summary>
    public ReportFindingsOutcome? Outcome { get; } = outcome;
}

/// <summary>What the model asked the ReportFindings tool to report. The binary takes at most thirty-two findings in one call.</summary>
/// <param name="findings">The findings, the worst first; empty when none survived.</param>
/// <param name="level">How much effort the review ran at; null when the call said nothing.</param>
public sealed class ReportFindingsToolInput(
    IReadOnlyList<ReportFindingsToolFinding> findings,
    Effort? level = null) : ToolInput
{
    /// <summary>The findings, the worst first; empty when none survived.</summary>
    public IReadOnlyList<ReportFindingsToolFinding> Findings { get; } = findings;

    /// <summary>How much effort the review ran at; null when the call said nothing.</summary>
    public Effort? Level { get; } = level;
}

/// <summary>What a ReportFindings handed back: the findings as they were taken.</summary>
/// <param name="count">How many findings were reported.</param>
/// <param name="findings">The findings as they were taken.</param>
/// <param name="level">How much effort the review ran at; null when the binary said nothing.</param>
public sealed class ReportFindingsToolOutput(
    long count,
    IReadOnlyList<ReportFindingsToolFinding> findings,
    Effort? level = null) : ToolOutput
{
    /// <summary>How many findings were reported.</summary>
    public long Count { get; } = count;

    /// <summary>The findings as they were taken.</summary>
    public IReadOnlyList<ReportFindingsToolFinding> Findings { get; } = findings;

    /// <summary>How much effort the review ran at; null when the binary said nothing.</summary>
    public Effort? Level { get; } = level;
}
