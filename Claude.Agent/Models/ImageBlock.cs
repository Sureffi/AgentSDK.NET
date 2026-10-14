namespace Claude.Agent;

/// <summary>An image the model is shown.</summary>
public sealed class ImageBlock : ContentBlock
{
    internal ImageBlock(ImageSource source)
    {
        Source = source;
    }

    /// <summary>Where the image comes from.</summary>
    public ImageSource Source { get; }

    /// <summary>What the API does to the image before the model sees it; null when the wire did not say.</summary>
    public ImageTransformations? Transformations { get; internal init; }

    /// <summary>The cache breakpoint on this block; null when the wire carried none.</summary>
    public CacheControl? CacheControl { get; internal init; }
}
