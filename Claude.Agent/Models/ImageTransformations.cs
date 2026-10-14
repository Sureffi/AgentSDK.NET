namespace Claude.Agent;

/// <summary>What the API does to an image before the model sees it. A key the wire leaves out keeps the API's default.</summary>
public sealed class ImageTransformations
{
    internal ImageTransformations()
    {
    }

    /// <summary>What happens to an image larger than the model takes; null when the wire did not say.</summary>
    public ImageOversizedHandling? OversizedImage { get; internal init; }
}
