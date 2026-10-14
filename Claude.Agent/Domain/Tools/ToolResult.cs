using System.Collections.Immutable;

namespace Claude.Agent;

/// <summary>
/// What an in-process tool hands back. A method may return a string or any value, which becomes
/// text; it returns a <see cref="ToolResult"/> to mark an error or to send an image.
/// </summary>
public sealed class ToolResult
{
    internal ToolResult(ImmutableArray<ToolContent> content, bool isError)
    {
        Content = content;
        IsError = isError;
    }

    internal ImmutableArray<ToolContent> Content { get; }

    /// <summary>Whether the result reports a failure.</summary>
    public bool IsError { get; }

    /// <summary>The text parts, joined.</summary>
    public string Text => string.Concat(Content.OfType<ToolText>().Select(text => text.Text));

    /// <summary>A result of one text part.</summary>
    public static ToolResult FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new ToolResult([new ToolText(text)], false);
    }

    /// <summary>A failed result; the model reads <paramref name="message"/>.</summary>
    public static ToolResult FromError(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new ToolResult([new ToolText(message)], true);
    }

    /// <summary>A result of one image, as base64 with its media type.</summary>
    public static ToolResult FromImage(string base64Data, string mediaType)
    {
        ArgumentException.ThrowIfNullOrEmpty(base64Data);
        ArgumentException.ThrowIfNullOrEmpty(mediaType);
        return new ToolResult([new ToolImage(base64Data, mediaType)], false);
    }

    internal static ToolResult Empty { get; } = new([], false);
}
