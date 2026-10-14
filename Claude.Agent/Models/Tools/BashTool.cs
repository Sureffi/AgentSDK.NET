namespace Claude.Agent;

/// <summary>What the model asked the Bash tool to run.</summary>
/// <param name="command">The command line.</param>
/// <param name="timeout">How long to wait for it; the wire carries it in milliseconds.</param>
/// <param name="description">What the command is for, in the model's words.</param>
/// <param name="runInBackground">Whether the command runs as a background task the turn does not wait for.</param>
/// <param name="dangerouslyDisableSandbox">Whether the command runs outside the sandbox.</param>
public sealed class BashToolInput(
    string command,
    TimeSpan? timeout = null,
    string? description = null,
    bool? runInBackground = null,
    bool? dangerouslyDisableSandbox = null) : ToolInput
{
    /// <summary>The command line.</summary>
    public string Command { get; } = command;

    /// <summary>How long to wait for it; null when the call named nothing. The wire carries it in milliseconds.</summary>
    public TimeSpan? Timeout { get; } = timeout;

    /// <summary>What the command is for, in the model's words; null when the call said nothing.</summary>
    public string? Description { get; } = description;

    /// <summary>Whether the command runs as a background task the turn does not wait for; null when the call said nothing.</summary>
    public bool? RunInBackground { get; } = runInBackground;

    /// <summary>Whether the command runs outside the sandbox; null when the call said nothing.</summary>
    public bool? DangerouslyDisableSandbox { get; } = dangerouslyDisableSandbox;
}

/// <summary>A commit a Bash command made, as the binary read it back off the repository.</summary>
/// <param name="sha">The commit's hash.</param>
/// <param name="kind">What the command did to it.</param>
/// <param name="branch">The branch it landed on.</param>
public sealed class BashGitCommit(string sha, GitCommitAction kind, string? branch = null)
{
    /// <summary>The commit's hash.</summary>
    public string Sha { get; } = sha;

    /// <summary>What the command did to it.</summary>
    public GitCommitAction Kind { get; } = kind;

    /// <summary>The branch it landed on; null when the binary named none.</summary>
    public string? Branch { get; } = branch;
}

/// <summary>A push a Bash command made.</summary>
/// <param name="branch">The branch that was pushed.</param>
public sealed class BashGitPush(string branch)
{
    /// <summary>The branch that was pushed.</summary>
    public string Branch { get; } = branch;
}

/// <summary>What a Bash command did to a branch.</summary>
/// <param name="reference">The branch, by the name git gives it; the wire key is <c>ref</c>.</param>
/// <param name="action">What was done to it.</param>
public sealed class BashGitBranch(string reference, GitBranchAction action)
{
    /// <summary>The branch, by the name git gives it; the wire carries it under <c>ref</c>.</summary>
    public string Reference { get; } = reference;

    /// <summary>What was done to it.</summary>
    public GitBranchAction Action { get; } = action;
}

/// <summary>What a Bash command did to a pull request.</summary>
/// <param name="number">The pull request's number.</param>
/// <param name="action">What was done to it.</param>
/// <param name="url">Where it is.</param>
public sealed class BashGitPullRequest(long number, GitPullRequestAction action, string? url = null)
{
    /// <summary>The pull request's number.</summary>
    public long Number { get; } = number;

    /// <summary>What was done to it.</summary>
    public GitPullRequestAction Action { get; } = action;

    /// <summary>Where it is; null when the binary gave no address.</summary>
    public string? Url { get; } = url;
}

/// <summary>What a Bash command did to the repository it ran in, when the binary recognised the command.</summary>
/// <param name="commit">A commit it made.</param>
/// <param name="push">A push it made.</param>
/// <param name="branch">What it did to a branch.</param>
/// <param name="pullRequest">What it did to a pull request; the wire key is <c>pr</c>.</param>
public sealed class BashGitOperation(
    BashGitCommit? commit = null,
    BashGitPush? push = null,
    BashGitBranch? branch = null,
    BashGitPullRequest? pullRequest = null)
{
    /// <summary>A commit it made; null when it made none.</summary>
    public BashGitCommit? Commit { get; } = commit;

    /// <summary>A push it made; null when it made none.</summary>
    public BashGitPush? Push { get; } = push;

    /// <summary>What it did to a branch; null when it touched none.</summary>
    public BashGitBranch? Branch { get; } = branch;

    /// <summary>What it did to a pull request, under the wire key <c>pr</c>; null when it touched none.</summary>
    public BashGitPullRequest? PullRequest { get; } = pullRequest;
}

/// <summary>What a Bash handed back. Its <c>structuredContent</c> is left untyped by the declaration and is read through the raw output beside this.</summary>
/// <param name="standardOutput">What the command wrote to standard output.</param>
/// <param name="standardError">What it wrote to standard error.</param>
/// <param name="interrupted">Whether it was stopped before it ended.</param>
/// <param name="rawOutputPath">Where the whole output was kept when it was too long to carry.</param>
/// <param name="isImage">Whether the output is an image rather than text.</param>
/// <param name="backgroundTaskId">The task the command became, when it was backgrounded.</param>
/// <param name="backgroundedByUser">Whether it was backgrounded by a person rather than by the call.</param>
/// <param name="timedOutAfter">How long it ran before its timeout cut it; the wire carries it in milliseconds.</param>
/// <param name="backgroundWorkingDirectoryHint">The directory a backgrounded command runs in; the wire key is <c>backgroundCwdHint</c>.</param>
/// <param name="backgroundEndsWithFinalResponse">Whether the backgrounded command ends the turn it belongs to.</param>
/// <param name="dangerouslyDisableSandbox">Whether it ran outside the sandbox.</param>
/// <param name="returnCodeInterpretation">What the binary made of the command's exit code.</param>
/// <param name="noOutputExpected">Whether the command was not expected to write anything.</param>
/// <param name="persistedOutputPath">Where the output was kept for later reading.</param>
/// <param name="persistedOutputSize">How large that kept output is, in bytes.</param>
/// <param name="staleReadFileStateHint">A note that a file the command touched had been read before it changed.</param>
/// <param name="gitHubRateLimitHint">A note that the host refused for rate limiting; the wire key is <c>ghRateLimitHint</c>.</param>
/// <param name="gitOperation">What the command did to the repository it ran in.</param>
public sealed class BashToolOutput(
    string standardOutput,
    string standardError,
    bool interrupted,
    string? rawOutputPath = null,
    bool? isImage = null,
    string? backgroundTaskId = null,
    bool? backgroundedByUser = null,
    TimeSpan? timedOutAfter = null,
    string? backgroundWorkingDirectoryHint = null,
    bool? backgroundEndsWithFinalResponse = null,
    bool? dangerouslyDisableSandbox = null,
    string? returnCodeInterpretation = null,
    bool? noOutputExpected = null,
    string? persistedOutputPath = null,
    long? persistedOutputSize = null,
    string? staleReadFileStateHint = null,
    string? gitHubRateLimitHint = null,
    BashGitOperation? gitOperation = null) : ToolOutput
{
    /// <summary>What the command wrote to standard output; the wire carries it under <c>stdout</c>.</summary>
    public string StandardOutput { get; } = standardOutput;

    /// <summary>What it wrote to standard error; the wire carries it under <c>stderr</c>.</summary>
    public string StandardError { get; } = standardError;

    /// <summary>Whether it was stopped before it ended.</summary>
    public bool Interrupted { get; } = interrupted;

    /// <summary>Where the whole output was kept when it was too long to carry; null when it was not.</summary>
    public string? RawOutputPath { get; } = rawOutputPath;

    /// <summary>Whether the output is an image rather than text; null when the binary said nothing.</summary>
    public bool? IsImage { get; } = isImage;

    /// <summary>The task the command became, when it was backgrounded; null when it ran in the foreground.</summary>
    public string? BackgroundTaskId { get; } = backgroundTaskId;

    /// <summary>Whether it was backgrounded by a person rather than by the call; null when the binary said nothing.</summary>
    public bool? BackgroundedByUser { get; } = backgroundedByUser;

    /// <summary>How long it ran before its timeout cut it; null when none did. The wire carries it in milliseconds under <c>timedOutAfterMs</c>.</summary>
    public TimeSpan? TimedOutAfter { get; } = timedOutAfter;

    /// <summary>The directory a backgrounded command runs in, under the wire key <c>backgroundCwdHint</c>; null when the binary gave none.</summary>
    public string? BackgroundWorkingDirectoryHint { get; } = backgroundWorkingDirectoryHint;

    /// <summary>Whether the backgrounded command ends the turn it belongs to; null when the binary said nothing.</summary>
    public bool? BackgroundEndsWithFinalResponse { get; } = backgroundEndsWithFinalResponse;

    /// <summary>Whether it ran outside the sandbox; null when the binary said nothing.</summary>
    public bool? DangerouslyDisableSandbox { get; } = dangerouslyDisableSandbox;

    /// <summary>What the binary made of the command's exit code; null when it said nothing.</summary>
    public string? ReturnCodeInterpretation { get; } = returnCodeInterpretation;

    /// <summary>Whether the command was not expected to write anything; null when the binary said nothing.</summary>
    public bool? NoOutputExpected { get; } = noOutputExpected;

    /// <summary>Where the output was kept for later reading; null when it was not kept.</summary>
    public string? PersistedOutputPath { get; } = persistedOutputPath;

    /// <summary>How large that kept output is, in bytes; null when nothing was kept.</summary>
    public long? PersistedOutputSize { get; } = persistedOutputSize;

    /// <summary>A note that a file the command touched had been read before it changed; null when there is none.</summary>
    public string? StaleReadFileStateHint { get; } = staleReadFileStateHint;

    /// <summary>A note that the host refused for rate limiting, under the wire key <c>ghRateLimitHint</c>; null when there is none.</summary>
    public string? GitHubRateLimitHint { get; } = gitHubRateLimitHint;

    /// <summary>What the command did to the repository it ran in; null when the binary recognised nothing.</summary>
    public BashGitOperation? GitOperation { get; } = gitOperation;
}
