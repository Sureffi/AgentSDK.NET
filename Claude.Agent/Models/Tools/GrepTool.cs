namespace Claude.Agent;

/// <summary>What the model asked the Grep tool to search for. Six of its keys are the search program's own flags, spelt as the wire spells them.</summary>
/// <param name="pattern">The regular expression to search for.</param>
/// <param name="path">The file or directory to search; absent means the working directory.</param>
/// <param name="glob">A glob the searched paths must match.</param>
/// <param name="outputMode">What to hand back: the matching lines, the file names, or a count.</param>
/// <param name="beforeContextLines">Lines of context before each match; the wire key is <c>-B</c>.</param>
/// <param name="afterContextLines">Lines of context after each match; the wire key is <c>-A</c>.</param>
/// <param name="surroundingContextLines">Lines of context on both sides of each match; the wire key is <c>-C</c>.</param>
/// <param name="contextLines">Lines of context on both sides of each match, under the wire key <c>context</c>.</param>
/// <param name="showLineNumbers">Whether each line carries its number; the wire key is <c>-n</c>.</param>
/// <param name="caseInsensitive">Whether case is ignored; the wire key is <c>-i</c>.</param>
/// <param name="matchesOnly">Whether only the matching part of a line is returned; the wire key is <c>-o</c>.</param>
/// <param name="fileType">A file type of the search program's own list; the wire key is <c>type</c>.</param>
/// <param name="headLimit">How many results to keep from the front.</param>
/// <param name="offset">How many results to drop from the front.</param>
/// <param name="multiline">Whether a pattern may match across line ends.</param>
public sealed class GrepToolInput(
    string pattern,
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
    bool? multiline = null) : ToolInput
{
    /// <summary>The regular expression to search for.</summary>
    public string Pattern { get; } = pattern;

    /// <summary>The file or directory to search; null when the call named none.</summary>
    public string? Path { get; } = path;

    /// <summary>A glob the searched paths must match; null when the call named none.</summary>
    public string? Glob { get; } = glob;

    /// <summary>What to hand back; null when the call named nothing.</summary>
    public GrepOutputMode? OutputMode { get; } = outputMode;

    /// <summary>Lines of context before each match, under the wire key <c>-B</c>; null when the call named none.</summary>
    public long? BeforeContextLines { get; } = beforeContextLines;

    /// <summary>Lines of context after each match, under the wire key <c>-A</c>; null when the call named none.</summary>
    public long? AfterContextLines { get; } = afterContextLines;

    /// <summary>Lines of context on both sides of each match, under the wire key <c>-C</c>; null when the call named none.</summary>
    public long? SurroundingContextLines { get; } = surroundingContextLines;

    /// <summary>Lines of context on both sides of each match, under the wire key <c>context</c>; null when the call named none.</summary>
    public long? ContextLines { get; } = contextLines;

    /// <summary>Whether each line carries its number, under the wire key <c>-n</c>; null when the call said nothing.</summary>
    public bool? ShowLineNumbers { get; } = showLineNumbers;

    /// <summary>Whether case is ignored, under the wire key <c>-i</c>; null when the call said nothing.</summary>
    public bool? CaseInsensitive { get; } = caseInsensitive;

    /// <summary>Whether only the matching part of a line is returned, under the wire key <c>-o</c>; null when the call said nothing.</summary>
    public bool? MatchesOnly { get; } = matchesOnly;

    /// <summary>A file type of the search program's own list, under the wire key <c>type</c>; null when the call named none.</summary>
    public string? FileType { get; } = fileType;

    /// <summary>How many results to keep from the front; null when the call named none.</summary>
    public long? HeadLimit { get; } = headLimit;

    /// <summary>How many results to drop from the front; null when the call named none.</summary>
    public long? Offset { get; } = offset;

    /// <summary>Whether a pattern may match across line ends; null when the call said nothing.</summary>
    public bool? Multiline { get; } = multiline;
}

/// <summary>What a Grep handed back. The mode it ran in is which type this is: file names, matching lines or counts, one type each, over the members all three carry.</summary>
public abstract class GrepToolOutput : ToolOutput
{
    private protected GrepToolOutput(long? appliedLimit, long? appliedOffset)
    {
        AppliedLimit = appliedLimit;
        AppliedOffset = appliedOffset;
    }

    /// <summary>The limit that cut the results, the call's own or the binary's default of 250; null when every result fit, and always null under a limit of 0, which keeps everything.</summary>
    public long? AppliedLimit { get; }

    /// <summary>The offset the call asked for; null when it asked for none or for 0.</summary>
    public long? AppliedOffset { get; }
}

/// <summary>A Grep that handed back the files that matched, newest first. An output that names no mode is this one, as the binary reads it.</summary>
/// <param name="fileNames">The files that matched, after any limit and offset.</param>
/// <param name="fileCount">How many files are named here; the wire carries it under <c>numFiles</c>.</param>
/// <param name="totalFileCount">How many files matched before any limit and offset.</param>
/// <param name="appliedLimit">The limit that cut the results, the call's own or the binary's default of 250; null when every result fit, and always null under a limit of 0, which keeps everything.</param>
/// <param name="appliedOffset">The offset the call asked for; null when it asked for none or for 0.</param>
public sealed class GrepFilesWithMatchesToolOutput(
    IReadOnlyList<string> fileNames,
    long fileCount,
    long totalFileCount,
    long? appliedLimit = null,
    long? appliedOffset = null) : GrepToolOutput(appliedLimit, appliedOffset)
{
    /// <summary>The files that matched, after any limit and offset.</summary>
    public IReadOnlyList<string> FileNames { get; } = fileNames;

    /// <summary>How many files are named here; the wire carries it under <c>numFiles</c>.</summary>
    public long FileCount { get; } = fileCount;

    /// <summary>How many files matched before any limit and offset.</summary>
    public long TotalFileCount { get; } = totalFileCount;
}

/// <summary>A Grep that handed back the matching lines. The binary writes no file names and a file count of 0 beside them, so this carries neither.</summary>
/// <param name="content">The matching lines and the context asked for around them, one to a line, after any limit and offset.</param>
/// <param name="lineCount">How many lines the content holds; the wire carries it under <c>numLines</c>.</param>
/// <param name="totalLineCount">How many lines the search wrote before any limit and offset: the matches, the context asked for, and the <c>--</c> lines between groups; the wire carries it under <c>totalLines</c>.</param>
/// <param name="appliedLimit">The limit that cut the results, the call's own or the binary's default of 250; null when every result fit, and always null under a limit of 0, which keeps everything.</param>
/// <param name="appliedOffset">The offset the call asked for; null when it asked for none or for 0.</param>
public sealed class GrepContentToolOutput(
    string content,
    long lineCount,
    long totalLineCount,
    long? appliedLimit = null,
    long? appliedOffset = null) : GrepToolOutput(appliedLimit, appliedOffset)
{
    /// <summary>The matching lines and the context asked for around them, one to a line, after any limit and offset.</summary>
    public string Content { get; } = content;

    /// <summary>How many lines the content holds; the wire carries it under <c>numLines</c>.</summary>
    public long LineCount { get; } = lineCount;

    /// <summary>How many lines the search wrote before any limit and offset: the matches, the context asked for, and the <c>--</c> lines between groups; the wire carries it under <c>totalLines</c>.</summary>
    public long TotalLineCount { get; } = totalLineCount;
}

/// <summary>A Grep that handed back how many matches each file holds. The binary writes no file names beside them, so this carries none.</summary>
/// <param name="content">One <c>file:count</c> line a counted file, after any limit and offset.</param>
/// <param name="fileCount">How many files were counted, before any limit and offset; the wire carries it under <c>numFiles</c>.</param>
/// <param name="matchCount">How many matches the counted files hold together, before any limit and offset; the wire carries it under <c>numMatches</c>.</param>
/// <param name="appliedLimit">The limit that cut the results, the call's own or the binary's default of 250; null when every result fit, and always null under a limit of 0, which keeps everything.</param>
/// <param name="appliedOffset">The offset the call asked for; null when it asked for none or for 0.</param>
public sealed class GrepCountToolOutput(
    string content,
    long fileCount,
    long matchCount,
    long? appliedLimit = null,
    long? appliedOffset = null) : GrepToolOutput(appliedLimit, appliedOffset)
{
    /// <summary>One <c>file:count</c> line a counted file, after any limit and offset.</summary>
    public string Content { get; } = content;

    /// <summary>How many files were counted, before any limit and offset; the wire carries it under <c>numFiles</c>.</summary>
    public long FileCount { get; } = fileCount;

    /// <summary>How many matches the counted files hold together, before any limit and offset; the wire carries it under <c>numMatches</c>.</summary>
    public long MatchCount { get; } = matchCount;
}
