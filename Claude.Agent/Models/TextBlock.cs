namespace Claude.Agent;

/// <summary>Text the model or the user wrote. Where the wire gives a string in place of blocks, it is read as one of these.</summary>
public sealed class TextBlock : ContentBlock
{
    internal TextBlock(string text)
        : this(text, [])
    {
    }

    internal TextBlock(string text, IReadOnlyList<Citation> citations)
    {
        Text = text;
        Citations = citations;
    }

    /// <summary>The text.</summary>
    public string Text { get; }

    /// <summary>What the text is cited from; empty when the wire carried none, which is every recording to date.</summary>
    public IReadOnlyList<Citation> Citations { get; }

    /// <summary>The cache breakpoint on this block; null when the wire carried none.</summary>
    public CacheControl? CacheControl { get; internal init; }
}
