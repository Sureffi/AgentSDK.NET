namespace Claude.Agent;

public static partial class ClaudeAgentModelFactory
{
    /// <summary>Builds a <see cref="Claude.Agent.FilePatchHunk"/> for a test. Each parameter is the property of its name.</summary>
    public static FilePatchHunk FilePatchHunk(
        long oldStart = 0,
        long oldLines = 0,
        long newStart = 0,
        long newLines = 0,
        IEnumerable<string>? lines = null) =>
        new(oldStart, oldLines, newStart, newLines, [.. lines ?? []]);

    /// <summary>Builds a <see cref="Claude.Agent.FileGitDiff"/> for a test. Each parameter is the property of its name.</summary>
    public static FileGitDiff FileGitDiff(
        string fileName = "",
        GitDiffStatus status = default,
        long additions = 0,
        long deletions = 0,
        long changes = 0,
        string patch = "",
        string? repository = null) =>
        new(fileName, status, additions, deletions, changes, patch, repository);

    /// <summary>Builds a <see cref="Claude.Agent.GlobToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static GlobToolInput GlobToolInput(string pattern = "", string? path = null) =>
        new(pattern, path);

    /// <summary>Builds a <see cref="Claude.Agent.GlobToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static GlobToolOutput GlobToolOutput(
        TimeSpan duration = default,
        long fileCount = 0,
        IEnumerable<string>? fileNames = null,
        bool truncated = false,
        long? totalMatchCount = null,
        bool? countIsComplete = null) =>
        new(duration, fileCount, [.. fileNames ?? []], truncated, totalMatchCount, countIsComplete);

    /// <summary>Builds a <see cref="Claude.Agent.GrepToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static GrepToolInput GrepToolInput(
        string pattern = "",
        string? path = null,
        string? glob = null,
        GrepOutputMode? outputMode = null,
        long? beforeContextLines = null,
        long? afterContextLines = null,
        long? surroundingContextLines = null,
        long? contextLines = null,
        bool? showLineNumbers = null,
        bool? caseInsensitive = null,
        bool? matchesOnly = null,
        string? fileType = null,
        long? headLimit = null,
        long? offset = null,
        bool? multiline = null) =>
        new(
            pattern,
            path,
            glob,
            outputMode,
            beforeContextLines,
            afterContextLines,
            surroundingContextLines,
            contextLines,
            showLineNumbers,
            caseInsensitive,
            matchesOnly,
            fileType,
            headLimit,
            offset,
            multiline);

    /// <summary>Builds a <see cref="Claude.Agent.GrepFilesWithMatchesToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static GrepFilesWithMatchesToolOutput GrepFilesWithMatchesToolOutput(
        IEnumerable<string>? fileNames = null,
        long fileCount = 0,
        long totalFileCount = 0,
        long? appliedLimit = null,
        long? appliedOffset = null) =>
        new([.. fileNames ?? []], fileCount, totalFileCount, appliedLimit, appliedOffset);

    /// <summary>Builds a <see cref="Claude.Agent.GrepContentToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static GrepContentToolOutput GrepContentToolOutput(
        string content = "",
        long lineCount = 0,
        long totalLineCount = 0,
        long? appliedLimit = null,
        long? appliedOffset = null) =>
        new(content, lineCount, totalLineCount, appliedLimit, appliedOffset);

    /// <summary>Builds a <see cref="Claude.Agent.GrepCountToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static GrepCountToolOutput GrepCountToolOutput(
        string content = "",
        long fileCount = 0,
        long matchCount = 0,
        long? appliedLimit = null,
        long? appliedOffset = null) =>
        new(content, fileCount, matchCount, appliedLimit, appliedOffset);

    /// <summary>Builds a <see cref="Claude.Agent.WriteToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static WriteToolInput WriteToolInput(string filePath = "", string content = "") =>
        new(filePath, content);

    /// <summary>Builds a <see cref="Claude.Agent.WriteCreateToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static WriteCreateToolOutput WriteCreateToolOutput(
        string filePath = "",
        string content = "",
        FileGitDiff? gitDiff = null,
        bool? userModified = null,
        bool? staged = null) =>
        new(filePath, content, gitDiff, userModified, staged);

    /// <summary>Builds a <see cref="Claude.Agent.WriteUpdateToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static WriteUpdateToolOutput WriteUpdateToolOutput(
        string filePath = "",
        string content = "",
        IEnumerable<FilePatchHunk>? structuredPatch = null,
        string? originalFile = null,
        FileGitDiff? gitDiff = null,
        bool? userModified = null,
        bool? staged = null) =>
        new(filePath, content, [.. structuredPatch ?? []], originalFile, gitDiff, userModified, staged);

    /// <summary>Builds an <see cref="Claude.Agent.EditToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static EditToolInput EditToolInput(
        string filePath = "",
        string oldString = "",
        string newString = "",
        bool? replaceAll = null) =>
        new(filePath, oldString, newString, replaceAll);

    /// <summary>Builds an <see cref="Claude.Agent.EditToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static EditToolOutput EditToolOutput(
        string filePath = "",
        string oldString = "",
        string newString = "",
        IEnumerable<FilePatchHunk>? structuredPatch = null,
        bool userModified = false,
        bool replaceAll = false,
        string? originalFile = null,
        FileGitDiff? gitDiff = null,
        bool? staged = null) =>
        new(
            filePath,
            oldString,
            newString,
            [.. structuredPatch ?? []],
            userModified,
            replaceAll,
            originalFile,
            gitDiff,
            staged);

    /// <summary>Builds a <see cref="Claude.Agent.BashToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static BashToolInput BashToolInput(
        string command = "",
        TimeSpan? timeout = null,
        string? description = null,
        bool? runInBackground = null,
        bool? dangerouslyDisableSandbox = null) =>
        new(command, timeout, description, runInBackground, dangerouslyDisableSandbox);

    /// <summary>Builds a <see cref="Claude.Agent.BashToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static BashToolOutput BashToolOutput(
        string standardOutput = "",
        string standardError = "",
        bool interrupted = false,
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
        BashGitOperation? gitOperation = null) =>
        new(
            standardOutput,
            standardError,
            interrupted,
            rawOutputPath,
            isImage,
            backgroundTaskId,
            backgroundedByUser,
            timedOutAfter,
            backgroundWorkingDirectoryHint,
            backgroundEndsWithFinalResponse,
            dangerouslyDisableSandbox,
            returnCodeInterpretation,
            noOutputExpected,
            persistedOutputPath,
            persistedOutputSize,
            staleReadFileStateHint,
            gitHubRateLimitHint,
            gitOperation);

    /// <summary>Builds a <see cref="Claude.Agent.BashGitOperation"/> for a test. Each parameter is the property of its name.</summary>
    public static BashGitOperation BashGitOperation(
        BashGitCommit? commit = null,
        BashGitPush? push = null,
        BashGitBranch? branch = null,
        BashGitPullRequest? pullRequest = null) =>
        new(commit, push, branch, pullRequest);

    /// <summary>Builds a <see cref="Claude.Agent.BashGitCommit"/> for a test. Each parameter is the property of its name.</summary>
    public static BashGitCommit BashGitCommit(string sha = "", GitCommitAction kind = default, string? branch = null) =>
        new(sha, kind, branch);

    /// <summary>Builds a <see cref="Claude.Agent.BashGitPush"/> for a test. Each parameter is the property of its name.</summary>
    public static BashGitPush BashGitPush(string branch = "") => new(branch);

    /// <summary>Builds a <see cref="Claude.Agent.BashGitBranch"/> for a test. Each parameter is the property of its name.</summary>
    public static BashGitBranch BashGitBranch(string reference = "", GitBranchAction action = default) =>
        new(reference, action);

    /// <summary>Builds a <see cref="Claude.Agent.BashGitPullRequest"/> for a test. Each parameter is the property of its name.</summary>
    public static BashGitPullRequest BashGitPullRequest(long number = 0, GitPullRequestAction action = default, string? url = null) =>
        new(number, action, url);

    /// <summary>Builds a <see cref="Claude.Agent.TaskOutputToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskOutputToolInput TaskOutputToolInput(string taskId = "", bool block = false, TimeSpan timeout = default) =>
        new(taskId, block, timeout);

    /// <summary>Builds a <see cref="Claude.Agent.TaskOutputToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskOutputToolOutput TaskOutputToolOutput(TaskRetrievalStatus retrievalStatus = default, TaskOutputToolTask? task = null) =>
        new(retrievalStatus, task ?? TaskOutputToolTask());

    /// <summary>Builds a <see cref="Claude.Agent.TaskOutputToolTask"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskOutputToolTask TaskOutputToolTask(
        string taskId = "",
        TaskType taskType = default,
        string status = "",
        string description = "",
        string output = "",
        long? exitCode = null) =>
        new(taskId, taskType, status, description, output, exitCode);

    /// <summary>Builds a <see cref="Claude.Agent.TaskStopToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskStopToolInput TaskStopToolInput(string? taskId = null, string? shellId = null) =>
        new(taskId, shellId);

    /// <summary>Builds a <see cref="Claude.Agent.TaskStopToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskStopToolOutput TaskStopToolOutput(
        string message = "",
        string taskId = "",
        TaskType taskType = default,
        string? command = null) =>
        new(message, taskId, taskType, command);

    /// <summary>Builds a <see cref="Claude.Agent.NotebookEditToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static NotebookEditToolInput NotebookEditToolInput(
        string notebookPath = "",
        string newSource = "",
        string? cellId = null,
        NotebookCellType? cellType = null,
        NotebookEditMode? editMode = null) =>
        new(notebookPath, newSource, cellId, cellType, editMode);

    /// <summary>Builds a <see cref="Claude.Agent.NotebookEditToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static NotebookEditToolOutput NotebookEditToolOutput(
        string newSource = "",
        NotebookCellType cellType = default,
        string language = "",
        NotebookEditMode editMode = default,
        string notebookPath = "",
        string originalFile = "",
        string updatedFile = "",
        string? oldSource = null,
        string? cellId = null,
        string? error = null) =>
        new(newSource, cellType, language, editMode, notebookPath, originalFile, updatedFile, oldSource, cellId, error);

    /// <summary>Builds a <see cref="Claude.Agent.WebFetchToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static WebFetchToolInput WebFetchToolInput(string url = "", string prompt = "") => new(url, prompt);

    /// <summary>Builds a <see cref="Claude.Agent.WebFetchToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static WebFetchToolOutput WebFetchToolOutput(
        long bytes = 0,
        long code = 0,
        string codeText = "",
        string result = "",
        TimeSpan duration = default,
        string url = "",
        WebFetchArtifactReference? artifactRead = null) =>
        new(bytes, code, codeText, result, duration, url, artifactRead);

    /// <summary>Builds a <see cref="Claude.Agent.WebFetchArtifactReference"/> for a test. Each parameter is the property of its name.</summary>
    public static WebFetchArtifactReference WebFetchArtifactReference(string slug = "", string? version = null, bool? seeded = null) =>
        new(slug, version, seeded);

    /// <summary>Builds a <see cref="Claude.Agent.WebSearchToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static WebSearchToolInput WebSearchToolInput(
        string query = "",
        IEnumerable<string>? allowedDomains = null,
        IEnumerable<string>? blockedDomains = null) =>
        new(query, [.. allowedDomains ?? []], [.. blockedDomains ?? []]);

    /// <summary>Builds a <see cref="Claude.Agent.WebSearchToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static WebSearchToolOutput WebSearchToolOutput(
        string query = "",
        IEnumerable<WebSearchToolResultItem>? results = null,
        TimeSpan duration = default,
        long? searchCount = null) =>
        new(query, [.. results ?? []], duration, searchCount);

    /// <summary>Builds a <see cref="Claude.Agent.WebSearchHitResult"/> for a test. Each parameter is the property of its name.</summary>
    public static WebSearchHitResult WebSearchHitResult(string toolUseId = "", IEnumerable<WebSearchLink>? content = null) =>
        new(toolUseId, [.. content ?? []]);

    /// <summary>Builds a <see cref="Claude.Agent.WebSearchTextResult"/> for a test. Each parameter is the property of its name.</summary>
    public static WebSearchTextResult WebSearchTextResult(string text = "") => new(text);

    /// <summary>Builds a <see cref="Claude.Agent.WebSearchLink"/> for a test. Each parameter is the property of its name.</summary>
    public static WebSearchLink WebSearchLink(string title = "", string url = "") => new(title, url);
}
