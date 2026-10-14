using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// What the API's search over the tools on offer handed back: the tools it found, or why it did not
/// answer. Each kind the wire names is a type of its own; a kind this library has no type for, or a
/// part that does not fit its kind's type, is an <see cref="UnknownToolSearchToolResultContent"/>.
/// </summary>
public abstract class ToolSearchToolResultContent
{
    private protected ToolSearchToolResultContent()
    {
    }
}

/// <summary>The tools the search found.</summary>
public sealed class ToolSearchToolSearchResult : ToolSearchToolResultContent
{
    internal ToolSearchToolSearchResult(IReadOnlyList<ToolReferenceBlock> toolReferences)
    {
        ToolReferences = toolReferences;
    }

    /// <summary>The tools found, each named as the model would call it.</summary>
    public IReadOnlyList<ToolReferenceBlock> ToolReferences { get; }
}

/// <summary>Why the search did not answer.</summary>
public sealed class ToolSearchToolResultErrorContent : ToolSearchToolResultContent
{
    internal ToolSearchToolResultErrorContent(ToolSearchErrorCode errorCode)
    {
        ErrorCode = errorCode;
    }

    /// <summary>What went wrong.</summary>
    public ToolSearchErrorCode ErrorCode { get; }

    /// <summary>What went wrong, in words; null when the wire did not say.</summary>
    public string? ErrorMessage { get; internal init; }
}

/// <summary>A part of a kind this library has no type for, or one that does not fit its kind's type: its kind's name and the part as written.</summary>
public sealed class UnknownToolSearchToolResultContent : ToolSearchToolResultContent
{
    internal UnknownToolSearchToolResultContent(string type, JsonElement raw)
    {
        Type = type;
        Raw = raw;
    }

    /// <summary>The part's <c>type</c>.</summary>
    public string Type { get; }

    /// <summary>The part as the binary wrote it.</summary>
    public JsonElement Raw { get; }
}
