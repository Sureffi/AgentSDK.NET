using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// What the API's web search handed back: the results, or why it did not answer. Each kind the wire
/// names is a type of its own; a kind this library has no type for, or a part that does not fit its
/// kind's type, is an <see cref="UnknownWebSearchToolResultContent"/>.
/// </summary>
public abstract class WebSearchToolResultContent
{
    private protected WebSearchToolResultContent()
    {
    }
}

/// <summary>One page the search found.</summary>
public sealed class WebSearchResult : WebSearchToolResultContent
{
    internal WebSearchResult(string encryptedContent, string title, string url)
    {
        EncryptedContent = encryptedContent;
        Title = title;
        Url = url;
    }

    /// <summary>The page as the API kept it, opaque, and sent back unchanged when the turn is sent again.</summary>
    public string EncryptedContent { get; }

    /// <summary>The page's title.</summary>
    public string Title { get; }

    /// <summary>The page's address.</summary>
    public string Url { get; }

    /// <summary>How old the page is, as the search reported it; null when it did not.</summary>
    public string? PageAge { get; internal init; }
}

/// <summary>Why the search did not answer.</summary>
public sealed class WebSearchToolResultErrorContent : WebSearchToolResultContent
{
    internal WebSearchToolResultErrorContent(WebSearchErrorCode errorCode)
    {
        ErrorCode = errorCode;
    }

    /// <summary>What went wrong.</summary>
    public WebSearchErrorCode ErrorCode { get; }
}

/// <summary>A part of a kind this library has no type for, or one that does not fit its kind's type: its kind's name and the part as written.</summary>
public sealed class UnknownWebSearchToolResultContent : WebSearchToolResultContent
{
    internal UnknownWebSearchToolResultContent(string type, JsonElement raw)
    {
        Type = type;
        Raw = raw;
    }

    /// <summary>The part's <c>type</c>.</summary>
    public string Type { get; }

    /// <summary>The part as the binary wrote it.</summary>
    public JsonElement Raw { get; }
}
