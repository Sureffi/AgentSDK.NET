using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// Where a document block's document comes from. Each kind the wire names is a type of its own; a
/// kind this library has no type for, or a source that does not fit its kind's type, is an
/// <see cref="UnknownDocumentSource"/>.
/// </summary>
public abstract class DocumentSource
{
    private protected DocumentSource()
    {
    }
}

/// <summary>The document itself, base64 encoded.</summary>
public sealed class Base64DocumentSource : DocumentSource
{
    internal Base64DocumentSource(string data, DocumentMediaType mediaType)
    {
        Data = data;
        MediaType = mediaType;
    }

    /// <summary>The document's bytes, base64 encoded.</summary>
    public string Data { get; }

    /// <summary>The document's format.</summary>
    public DocumentMediaType MediaType { get; }
}

/// <summary>The document as plain text.</summary>
public sealed class TextDocumentSource : DocumentSource
{
    internal TextDocumentSource(string data, DocumentMediaType mediaType)
    {
        Data = data;
        MediaType = mediaType;
    }

    /// <summary>The document's text.</summary>
    public string Data { get; }

    /// <summary>The document's format.</summary>
    public DocumentMediaType MediaType { get; }
}

/// <summary>The document as content blocks, which a citation can point into block by block.</summary>
public sealed class ContentDocumentSource : DocumentSource
{
    internal ContentDocumentSource(IReadOnlyList<ContentBlock> content)
    {
        Content = content;
    }

    /// <summary>The document's blocks; content written as a string is one <see cref="TextBlock"/>.</summary>
    public IReadOnlyList<ContentBlock> Content { get; }
}

/// <summary>A document the API fetches from an address.</summary>
public sealed class UrlDocumentSource : DocumentSource
{
    internal UrlDocumentSource(string url)
    {
        Url = url;
    }

    /// <summary>The document's address.</summary>
    public string Url { get; }
}

/// <summary>A document already uploaded to the account as a file.</summary>
public sealed class FileDocumentSource : DocumentSource
{
    internal FileDocumentSource(string fileId)
    {
        FileId = fileId;
    }

    /// <summary>The uploaded file.</summary>
    public string FileId { get; }
}

/// <summary>A source of a kind this library has no type for, or one that does not fit its kind's type: its kind's name and the source as written.</summary>
public sealed class UnknownDocumentSource : DocumentSource
{
    internal UnknownDocumentSource(string type, JsonElement raw)
    {
        Type = type;
        Raw = raw;
    }

    /// <summary>The source's <c>type</c>.</summary>
    public string Type { get; }

    /// <summary>The source as the binary wrote it.</summary>
    public JsonElement Raw { get; }
}
