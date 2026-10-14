namespace Claude.Agent;

/// <summary>One run of changed lines in the patch an Edit or a Write reports.</summary>
/// <param name="oldStart">The first line of the run in the file as it was.</param>
/// <param name="oldLines">How many lines the run covered in the file as it was.</param>
/// <param name="newStart">The first line of the run in the file as it now is.</param>
/// <param name="newLines">How many lines the run covers in the file as it now is.</param>
/// <param name="lines">The run's lines, each led by the character that says whether it was kept, removed or added.</param>
public sealed class FilePatchHunk(long oldStart, long oldLines, long newStart, long newLines, IReadOnlyList<string> lines)
{
    /// <summary>The first line of the run in the file as it was.</summary>
    public long OldStart { get; } = oldStart;

    /// <summary>How many lines the run covered in the file as it was.</summary>
    public long OldLines { get; } = oldLines;

    /// <summary>The first line of the run in the file as it now is.</summary>
    public long NewStart { get; } = newStart;

    /// <summary>How many lines the run covers in the file as it now is.</summary>
    public long NewLines { get; } = newLines;

    /// <summary>The run's lines, each led by the character that says whether it was kept, removed or added.</summary>
    public IReadOnlyList<string> Lines { get; } = lines;
}

/// <summary>How an edited file stands in the repository it belongs to, as an Edit or a Write reports it.</summary>
/// <param name="fileName">The file's path as the repository names it.</param>
/// <param name="status">Whether the repository already tracked the file.</param>
/// <param name="additions">Lines added.</param>
/// <param name="deletions">Lines removed.</param>
/// <param name="changes">Lines touched in all.</param>
/// <param name="patch">The diff itself.</param>
/// <param name="repository">The repository the file is in; absent when the binary named none.</param>
public sealed class FileGitDiff(
    string fileName,
    GitDiffStatus status,
    long additions,
    long deletions,
    long changes,
    string patch,
    string? repository = null)
{
    /// <summary>The file's path as the repository names it.</summary>
    public string FileName { get; } = fileName;

    /// <summary>Whether the repository already tracked the file.</summary>
    public GitDiffStatus Status { get; } = status;

    /// <summary>Lines added.</summary>
    public long Additions { get; } = additions;

    /// <summary>Lines removed.</summary>
    public long Deletions { get; } = deletions;

    /// <summary>Lines touched in all.</summary>
    public long Changes { get; } = changes;

    /// <summary>The diff itself.</summary>
    public string Patch { get; } = patch;

    /// <summary>The repository the file is in; null when the binary named none.</summary>
    public string? Repository { get; } = repository;
}
