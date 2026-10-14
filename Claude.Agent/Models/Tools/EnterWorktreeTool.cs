namespace Claude.Agent;

/// <summary>What the model asked the EnterWorktree tool to move into. A call names a new worktree or an existing path, never both, and a call that names neither gets a worktree under a name of the binary's own.</summary>
/// <param name="name">The name for a worktree to make; null when the call named a path or left the naming to the binary.</param>
/// <param name="path">An existing worktree of this repository to move into; null when the call asked for a new one.</param>
public sealed class EnterWorktreeToolInput(string? name = null, string? path = null) : ToolInput
{
    /// <summary>The name for a worktree to make; null when the call named a path or left the naming to the binary.</summary>
    public string? Name { get; } = name;

    /// <summary>An existing worktree of this repository to move into; null when the call asked for a new one.</summary>
    public string? Path { get; } = path;
}

/// <summary>What an EnterWorktree handed back.</summary>
/// <param name="worktreePath">The worktree the session now works in.</param>
/// <param name="message">What happened, in the binary's own words.</param>
/// <param name="worktreeBranch">The branch checked out there; null when the binary named none.</param>
public sealed class EnterWorktreeToolOutput(
    string worktreePath,
    string message,
    string? worktreeBranch = null) : ToolOutput
{
    /// <summary>The worktree the session now works in.</summary>
    public string WorktreePath { get; } = worktreePath;

    /// <summary>What happened, in the binary's own words.</summary>
    public string Message { get; } = message;

    /// <summary>The branch checked out there; null when the binary named none.</summary>
    public string? WorktreeBranch { get; } = worktreeBranch;
}
