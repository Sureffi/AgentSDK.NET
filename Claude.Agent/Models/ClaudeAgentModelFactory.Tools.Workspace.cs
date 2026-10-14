namespace Claude.Agent;

public static partial class ClaudeAgentModelFactory
{
    /// <summary>Builds a <see cref="Claude.Agent.EnterWorktreeToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static EnterWorktreeToolInput EnterWorktreeToolInput(string? name = null, string? path = null) =>
        new(name, path);

    /// <summary>Builds a <see cref="Claude.Agent.EnterWorktreeToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static EnterWorktreeToolOutput EnterWorktreeToolOutput(
        string worktreePath = "",
        string message = "",
        string? worktreeBranch = null) =>
        new(worktreePath, message, worktreeBranch);

    /// <summary>Builds a <see cref="Claude.Agent.ExitWorktreeKeepToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static ExitWorktreeKeepToolInput ExitWorktreeKeepToolInput() =>
        new();

    /// <summary>Builds a <see cref="Claude.Agent.ExitWorktreeRemoveToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static ExitWorktreeRemoveToolInput ExitWorktreeRemoveToolInput(bool? discardChanges = null) =>
        new(discardChanges);

    /// <summary>Builds a <see cref="Claude.Agent.ExitWorktreeKeepToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ExitWorktreeKeepToolOutput ExitWorktreeKeepToolOutput(
        string originalWorkingDirectory = "",
        string worktreePath = "",
        string message = "",
        string? worktreeBranch = null,
        string? tmuxSessionName = null) =>
        new(originalWorkingDirectory, worktreePath, message, worktreeBranch, tmuxSessionName);

    /// <summary>Builds a <see cref="Claude.Agent.ExitWorktreeRemoveToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ExitWorktreeRemoveToolOutput ExitWorktreeRemoveToolOutput(
        string originalWorkingDirectory = "",
        string worktreePath = "",
        string message = "",
        string? worktreeBranch = null,
        long? discardedFileCount = null,
        long? discardedCommitCount = null) =>
        new(originalWorkingDirectory, worktreePath, message, worktreeBranch, discardedFileCount, discardedCommitCount);

    /// <summary>Builds a <see cref="Claude.Agent.PushNotificationToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static PushNotificationToolInput PushNotificationToolInput(
        string message = "",
        PushNotificationStatus status = default) =>
        new(message, status);

    /// <summary>Builds a <see cref="Claude.Agent.PushNotificationToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static PushNotificationToolOutput PushNotificationToolOutput(
        string message = "",
        bool? pushSent = null,
        bool? localSent = null,
        PushNotificationDisabledReason? disabledReason = null,
        string? sentAt = null) =>
        new(message, pushSent, localSent, disabledReason, sentAt);

    /// <summary>Builds a <see cref="Claude.Agent.ReportFindingsToolFinding"/> for a test. Each parameter is the property of its name.</summary>
    public static ReportFindingsToolFinding ReportFindingsToolFinding(
        string file = "",
        string summary = "",
        string failureScenario = "",
        long? line = null,
        string? shortSummary = null,
        string? category = null,
        ReportFindingsVerdict? verdict = null,
        ReportFindingsOutcome? outcome = null) =>
        new(file, summary, failureScenario, line, shortSummary, category, verdict, outcome);

    /// <summary>Builds a <see cref="Claude.Agent.ReportFindingsToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static ReportFindingsToolInput ReportFindingsToolInput(
        IEnumerable<ReportFindingsToolFinding>? findings = null,
        Effort? level = null) =>
        new([.. findings ?? []], level);

    /// <summary>Builds a <see cref="Claude.Agent.ReportFindingsToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ReportFindingsToolOutput ReportFindingsToolOutput(
        long count = 0,
        IEnumerable<ReportFindingsToolFinding>? findings = null,
        Effort? level = null) =>
        new(count, [.. findings ?? []], level);
}
