using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// Where an image block's image comes from. Each kind the wire names is a type of its own; a kind
/// this library has no type for, or a source that does not fit its kind's type, is an
/// <see cref="UnknownImageSource"/>.
/// </summary>
public abstract class ImageSource
{
    private protected ImageSource()
    {
    }
}

/// <summary>The image itself, base64 encoded.</summary>
public sealed class Base64ImageSource : ImageSource
{
    internal Base64ImageSource(string data, ImageMediaType mediaType)
    {
        Data = data;
        MediaType = mediaType;
    }

    /// <summary>The image's bytes, base64 encoded.</summary>
    public string Data { get; }

    /// <summary>The image's format.</summary>
    public ImageMediaType MediaType { get; }
}

/// <summary>An image the API fetches from an address.</summary>
public sealed class UrlImageSource : ImageSource
{
    internal UrlImageSource(string url)
    {
        Url = url;
    }

    /// <summary>The image's address.</summary>
    public string Url { get; }
}

/// <summary>An image already uploaded to the account as a file.</summary>
public sealed class FileImageSource : ImageSource
{
    internal FileImageSource(string fileId)
    {
        FileId = fileId;
    }

    /// <summary>The uploaded file.</summary>
    public string FileId { get; }
}

/// <summary>A source of a kind this library has no type for, or one that does not fit its kind's type: its kind's name and the source as written.</summary>
public sealed class UnknownImageSource : ImageSource
{
    internal UnknownImageSource(string type, JsonElement raw)
    {
        Type = type;
        Raw = raw;
    }

    /// <summary>The source's <c>type</c>.</summary>
    public string Type { get; }

    /// <summary>The source as the binary wrote it.</summary>
    public JsonElement Raw { get; }
}
