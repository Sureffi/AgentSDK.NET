namespace Claude.Agent;

/// <summary>A document the model reads and may cite.</summary>
public sealed class DocumentBlock : ContentBlock
{
    internal DocumentBlock(DocumentSource source)
    {
        Source = source;
    }

    /// <summary>Where the document comes from.</summary>
    public DocumentSource Source { get; }

    /// <summary>The document's title; null when the wire carried none.</summary>
    public string? Title { get; internal init; }

    /// <summary>What the document is, in words, for the model; null when the wire carried none.</summary>
    public string? Context { get; internal init; }

    /// <summary>Whether the model may cite the document; null when the wire carried nothing.</summary>
    public CitationsConfiguration? Citations { get; internal init; }

    /// <summary>The cache breakpoint on this block; null when the wire carried none.</summary>
    public CacheControl? CacheControl { get; internal init; }
}
