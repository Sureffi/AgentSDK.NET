namespace Claude.Agent;

/// <summary>What the model asked the Edit tool to change.</summary>
/// <param name="filePath">The file to edit, as an absolute path.</param>
/// <param name="oldString">The text to find.</param>
/// <param name="newString">The text to put in its place.</param>
/// <param name="replaceAll">Whether every occurrence is replaced rather than the one.</param>
public sealed class EditToolInput(string filePath, string oldString, string newString, bool? replaceAll = null) : ToolInput
{
    /// <summary>The file to edit, as an absolute path.</summary>
    public string FilePath { get; } = filePath;

    /// <summary>The text to find.</summary>
    public string OldString { get; } = oldString;

    /// <summary>The text to put in its place.</summary>
    public string NewString { get; } = newString;

    /// <summary>Whether every occurrence is replaced rather than the one; null when the call said nothing.</summary>
    public bool? ReplaceAll { get; } = replaceAll;
}

/// <summary>What an Edit handed back.</summary>
/// <param name="filePath">The file that was edited.</param>
/// <param name="oldString">The text that was found.</param>
/// <param name="newString">The text put in its place.</param>
/// <param name="structuredPatch">The runs of changed lines.</param>
/// <param name="userModified">Whether the user edited the proposed change in the permission dialog before accepting it.</param>
/// <param name="replaceAll">Whether every occurrence was replaced.</param>
/// <param name="originalFile">The file's contents before the edit; null when the binary sent none.</param>
/// <param name="gitDiff">How the file stands in the repository it belongs to.</param>
/// <param name="staged">Whether the edit was held for the machine's owner to review instead of written, the file left unchanged.</param>
public sealed class EditToolOutput(
    string filePath,
    string oldString,
    string newString,
    IReadOnlyList<FilePatchHunk> structuredPatch,
    bool userModified,
    bool replaceAll,
    string? originalFile = null,
    FileGitDiff? gitDiff = null,
    bool? staged = null) : ToolOutput
{
    /// <summary>The file that was edited.</summary>
    public string FilePath { get; } = filePath;

    /// <summary>The text that was found.</summary>
    public string OldString { get; } = oldString;

    /// <summary>The text put in its place.</summary>
    public string NewString { get; } = newString;

    /// <summary>The runs of changed lines.</summary>
    public IReadOnlyList<FilePatchHunk> StructuredPatch { get; } = structuredPatch;

    /// <summary>Whether the user edited the proposed change in the permission dialog before accepting it.</summary>
    public bool UserModified { get; } = userModified;

    /// <summary>Whether every occurrence was replaced.</summary>
    public bool ReplaceAll { get; } = replaceAll;

    /// <summary>The file's contents before the edit; null when the binary sent none.</summary>
    public string? OriginalFile { get; } = originalFile;

    /// <summary>How the file stands in the repository it belongs to; null when it is in none or the binary said nothing.</summary>
    public FileGitDiff? GitDiff { get; } = gitDiff;

    /// <summary>Whether the edit was held for the machine's owner to review instead of written, the file left unchanged; null when the binary said nothing.</summary>
    public bool? Staged { get; } = staged;
}
