namespace Claude.Agent;

/// <summary>What the model asked the Write tool to put on disk.</summary>
/// <param name="filePath">The file to write, as an absolute path.</param>
/// <param name="content">What to put in it.</param>
public sealed class WriteToolInput(string filePath, string content) : ToolInput
{
    /// <summary>The file to write, as an absolute path.</summary>
    public string FilePath { get; } = filePath;

    /// <summary>What to put in it.</summary>
    public string Content { get; } = content;
}

/// <summary>What a Write handed back. Whether it made the file or replaced one is which type this is, over the members both carry.</summary>
public abstract class WriteToolOutput : ToolOutput
{
    private protected WriteToolOutput(string filePath, string content, FileGitDiff? gitDiff, bool? userModified, bool? staged)
    {
        FilePath = filePath;
        Content = content;
        GitDiff = gitDiff;
        UserModified = userModified;
        Staged = staged;
    }

    /// <summary>The file that was written.</summary>
    public string FilePath { get; }

    /// <summary>What was put in it.</summary>
    public string Content { get; }

    /// <summary>How the file stands in the repository it belongs to; null when it is in none or the binary said nothing.</summary>
    public FileGitDiff? GitDiff { get; }

    /// <summary>Whether the user edited the proposed content in the permission dialog before accepting it; null when the binary said nothing.</summary>
    public bool? UserModified { get; }

    /// <summary>Whether the write was held for the machine's owner to review instead of written, the file left unchanged; null when the binary said nothing.</summary>
    public bool? Staged { get; }
}

/// <summary>A Write that made the file: <c>create</c>. The binary writes no patch and no earlier contents beside it, so this carries neither.</summary>
/// <param name="filePath">The file that was written.</param>
/// <param name="content">What was put in it.</param>
/// <param name="gitDiff">How the file stands in the repository it belongs to.</param>
/// <param name="userModified">Whether the user edited the proposed content in the permission dialog before accepting it.</param>
/// <param name="staged">Whether the write was held for the machine's owner to review instead of written, the file left unchanged.</param>
public sealed class WriteCreateToolOutput(
    string filePath,
    string content,
    FileGitDiff? gitDiff = null,
    bool? userModified = null,
    bool? staged = null) : WriteToolOutput(filePath, content, gitDiff, userModified, staged);

/// <summary>A Write that replaced the file: <c>update</c>.</summary>
/// <param name="filePath">The file that was written.</param>
/// <param name="content">What was put in it.</param>
/// <param name="structuredPatch">The runs of changed lines; empty when none changed, or when the binary did not diff the file.</param>
/// <param name="originalFile">The file's contents before the write; null when the binary left them out, as it does for contents too large to carry.</param>
/// <param name="gitDiff">How the file stands in the repository it belongs to.</param>
/// <param name="userModified">Whether the user edited the proposed content in the permission dialog before accepting it.</param>
/// <param name="staged">Whether the write was held for the machine's owner to review instead of written, the file left unchanged.</param>
public sealed class WriteUpdateToolOutput(
    string filePath,
    string content,
    IReadOnlyList<FilePatchHunk> structuredPatch,
    string? originalFile = null,
    FileGitDiff? gitDiff = null,
    bool? userModified = null,
    bool? staged = null) : WriteToolOutput(filePath, content, gitDiff, userModified, staged)
{
    /// <summary>The runs of changed lines; empty when none changed, or when the binary did not diff the file.</summary>
    public IReadOnlyList<FilePatchHunk> StructuredPatch { get; } = structuredPatch;

    /// <summary>The file's contents before the write; null when the binary left them out, as it does for contents too large to carry.</summary>
    public string? OriginalFile { get; } = originalFile;
}
