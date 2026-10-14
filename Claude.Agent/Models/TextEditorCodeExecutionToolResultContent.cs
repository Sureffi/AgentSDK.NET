using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// What the API's text editor handed back: a view, a file it created, an edit it made, or why it did
/// not run. Each kind the wire names is a type of its own; a kind this library has no type for, or a
/// part that does not fit its kind's type, is an
/// <see cref="UnknownTextEditorCodeExecutionToolResultContent"/>.
/// </summary>
public abstract class TextEditorCodeExecutionToolResultContent
{
    private protected TextEditorCodeExecutionToolResultContent()
    {
    }
}

/// <summary>What the editor saw at the path it was pointed at.</summary>
public sealed class TextEditorCodeExecutionViewResult : TextEditorCodeExecutionToolResultContent
{
    internal TextEditorCodeExecutionViewResult(string content, TextEditorFileType fileType)
    {
        Content = content;
        FileType = fileType;
    }

    /// <summary>What was read.</summary>
    public string Content { get; }

    /// <summary>What kind of file it was.</summary>
    public TextEditorFileType FileType { get; }

    /// <summary>How many lines the view holds; null when the wire did not say.</summary>
    public long? LineCount { get; internal init; }

    /// <summary>The first line the view starts at; null when the wire did not say.</summary>
    public long? StartLine { get; internal init; }

    /// <summary>How many lines the whole file holds; null when the wire did not say.</summary>
    public long? TotalLines { get; internal init; }
}

/// <summary>A file the editor wrote.</summary>
public sealed class TextEditorCodeExecutionCreateResult : TextEditorCodeExecutionToolResultContent
{
    internal TextEditorCodeExecutionCreateResult(bool isFileUpdate)
    {
        IsFileUpdate = isFileUpdate;
    }

    /// <summary>Whether a file was already there and was overwritten.</summary>
    public bool IsFileUpdate { get; }
}

/// <summary>An edit the editor made by replacing one run of text with another.</summary>
public sealed class TextEditorCodeExecutionStringReplaceResult : TextEditorCodeExecutionToolResultContent
{
    internal TextEditorCodeExecutionStringReplaceResult(IReadOnlyList<string> lines)
    {
        Lines = lines;
    }

    /// <summary>The lines around the edit, as the editor showed them; empty when the wire carried none.</summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>How many lines stand where the edit landed; null when the wire did not say.</summary>
    public long? NewLineCount { get; internal init; }

    /// <summary>The first line the edit landed on; null when the wire did not say.</summary>
    public long? NewStartLine { get; internal init; }

    /// <summary>How many lines the edit replaced; null when the wire did not say.</summary>
    public long? OldLineCount { get; internal init; }

    /// <summary>The first line the edit replaced; null when the wire did not say.</summary>
    public long? OldStartLine { get; internal init; }
}

/// <summary>Why the editor did not run.</summary>
public sealed class TextEditorCodeExecutionToolResultErrorContent : TextEditorCodeExecutionToolResultContent
{
    internal TextEditorCodeExecutionToolResultErrorContent(TextEditorCodeExecutionErrorCode errorCode)
    {
        ErrorCode = errorCode;
    }

    /// <summary>What went wrong.</summary>
    public TextEditorCodeExecutionErrorCode ErrorCode { get; }

    /// <summary>What went wrong, in words; null when the wire did not say.</summary>
    public string? ErrorMessage { get; internal init; }
}

/// <summary>A part of a kind this library has no type for, or one that does not fit its kind's type: its kind's name and the part as written.</summary>
public sealed class UnknownTextEditorCodeExecutionToolResultContent : TextEditorCodeExecutionToolResultContent
{
    internal UnknownTextEditorCodeExecutionToolResultContent(string type, JsonElement raw)
    {
        Type = type;
        Raw = raw;
    }

    /// <summary>The part's <c>type</c>.</summary>
    public string Type { get; }

    /// <summary>The part as the binary wrote it.</summary>
    public JsonElement Raw { get; }
}
