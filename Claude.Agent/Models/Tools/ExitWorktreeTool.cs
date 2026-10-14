namespace Claude.Agent;

/// <summary>What the model asked the ExitWorktree tool to do on the way out. The action is which type this is: the worktree kept or removed, one type each.</summary>
public abstract class ExitWorktreeToolInput : ToolInput
{
    private protected ExitWorktreeToolInput()
    {
    }
}

/// <summary>An ExitWorktree that leaves the worktree and its branch on disk. It takes nothing else.</summary>
public sealed class ExitWorktreeKeepToolInput : ExitWorktreeToolInput
{
}

/// <summary>An ExitWorktree that deletes the worktree and its branch.</summary>
/// <param name="discardChanges">Whether work not committed or not merged may be thrown away; null when the call said nothing, which the binary refuses where there is any.</param>
public sealed class ExitWorktreeRemoveToolInput(bool? discardChanges = null) : ExitWorktreeToolInput
{
    /// <summary>Whether work not committed or not merged may be thrown away; null when the call said nothing, which the binary refuses where there is any.</summary>
    public bool? DiscardChanges { get; } = discardChanges;
}

/// <summary>What an ExitWorktree handed back. The action is which type this is: the worktree kept or removed, one type each, over the members both carry.</summary>
public abstract class ExitWorktreeToolOutput : ToolOutput
{
    private protected ExitWorktreeToolOutput(string originalWorkingDirectory, string worktreePath, string message, string? worktreeBranch)
    {
        OriginalWorkingDirectory = originalWorkingDirectory;
        WorktreePath = worktreePath;
        Message = message;
        WorktreeBranch = worktreeBranch;
    }

    /// <summary>The directory the session works in again.</summary>
    public string OriginalWorkingDirectory { get; }

    /// <summary>The worktree that was left.</summary>
    public string WorktreePath { get; }

    /// <summary>What happened, in the binary's own words.</summary>
    public string Message { get; }

    /// <summary>The branch that was checked out there; null when the binary named none.</summary>
    public string? WorktreeBranch { get; }
}

/// <summary>An ExitWorktree that left the worktree and its branch on disk.</summary>
/// <param name="originalWorkingDirectory">The directory the session works in again.</param>
/// <param name="worktreePath">The worktree that was left.</param>
/// <param name="message">What happened, in the binary's own words.</param>
/// <param name="worktreeBranch">The branch that was checked out there; null when the binary named none.</param>
/// <param name="tmuxSessionName">The terminal session that was left with it; null when there was none.</param>
public sealed class ExitWorktreeKeepToolOutput(
    string originalWorkingDirectory,
    string worktreePath,
    string message,
    string? worktreeBranch = null,
    string? tmuxSessionName = null) : ExitWorktreeToolOutput(originalWorkingDirectory, worktreePath, message, worktreeBranch)
{
    /// <summary>The terminal session that was left with it; null when there was none.</summary>
    public string? TmuxSessionName { get; } = tmuxSessionName;
}

/// <summary>An ExitWorktree that deleted the worktree and its branch.</summary>
/// <param name="originalWorkingDirectory">The directory the session works in again.</param>
/// <param name="worktreePath">The worktree that was deleted.</param>
/// <param name="message">What happened, in the binary's own words.</param>
/// <param name="worktreeBranch">The branch that was checked out there; null when the binary named none.</param>
/// <param name="discardedFileCount">How many files were thrown away; null when the binary said nothing.</param>
/// <param name="discardedCommitCount">How many commits were thrown away; null when the binary said nothing.</param>
public sealed class ExitWorktreeRemoveToolOutput(
    string originalWorkingDirectory,
    string worktreePath,
    string message,
    string? worktreeBranch = null,
    long? discardedFileCount = null,
    long? discardedCommitCount = null) : ExitWorktreeToolOutput(originalWorkingDirectory, worktreePath, message, worktreeBranch)
{
    /// <summary>How many files were thrown away; null when the binary said nothing.</summary>
    public long? DiscardedFileCount { get; } = discardedFileCount;

    /// <summary>How many commits were thrown away; null when the binary said nothing.</summary>
    public long? DiscardedCommitCount { get; } = discardedCommitCount;
}
