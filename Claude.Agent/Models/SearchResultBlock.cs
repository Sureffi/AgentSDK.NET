namespace Claude.Agent;

/// <summary>One result of a search a tool ran, which the model may cite block by block.</summary>
public sealed class SearchResultBlock : ContentBlock
{
    internal SearchResultBlock(IReadOnlyList<ContentBlock> content, string source, string title)
    {
        Content = content;
        Source = source;
        Title = title;
    }

    /// <summary>The result's text, block by block, which a citation points into.</summary>
    public IReadOnlyList<ContentBlock> Content { get; }

    /// <summary>Where the result came from.</summary>
    public string Source { get; }

    /// <summary>The result's title.</summary>
    public string Title { get; }

    /// <summary>Whether the model may cite the result; null when the wire carried nothing.</summary>
    public CitationsConfiguration? Citations { get; internal init; }

    /// <summary>The cache breakpoint on this block; null when the wire carried none.</summary>
    public CacheControl? CacheControl { get; internal init; }
}
