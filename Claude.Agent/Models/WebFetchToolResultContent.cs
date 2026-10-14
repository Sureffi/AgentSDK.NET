using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// What the API's web fetch handed back: the page, or why it did not answer. Each kind the wire
/// names is a type of its own; a kind this library has no type for, or a part that does not fit its
/// kind's type, is an <see cref="UnknownWebFetchToolResultContent"/>.
/// </summary>
public abstract class WebFetchToolResultContent
{
    private protected WebFetchToolResultContent()
    {
    }
}

/// <summary>The page the fetch retrieved, as a document.</summary>
public sealed class WebFetchResult : WebFetchToolResultContent
{
    internal WebFetchResult(DocumentBlock content, string url)
    {
        Content = content;
        Url = url;
    }

    /// <summary>The page as a document the model reads.</summary>
    public DocumentBlock Content { get; }

    /// <summary>The address fetched.</summary>
    public string Url { get; }

    /// <summary>When the page was retrieved; null when the wire did not say, or said something no instant holds.</summary>
    public DateTimeOffset? RetrievedAt { get; internal init; }
}

/// <summary>Why the fetch did not answer.</summary>
public sealed class WebFetchToolResultErrorContent : WebFetchToolResultContent
{
    internal WebFetchToolResultErrorContent(WebFetchErrorCode errorCode)
    {
        ErrorCode = errorCode;
    }

    /// <summary>What went wrong.</summary>
    public WebFetchErrorCode ErrorCode { get; }
}

/// <summary>A part of a kind this library has no type for, or one that does not fit its kind's type: its kind's name and the part as written.</summary>
public sealed class UnknownWebFetchToolResultContent : WebFetchToolResultContent
{
    internal UnknownWebFetchToolResultContent(string type, JsonElement raw)
    {
        Type = type;
        Raw = raw;
    }

    /// <summary>The part's <c>type</c>.</summary>
    public string Type { get; }

    /// <summary>The part as the binary wrote it.</summary>
    public JsonElement Raw { get; }
}
