namespace Claude.Agent;

/// <summary>A file put into the container the API's code execution runs in.</summary>
public sealed class ContainerUploadBlock : ContentBlock
{
    internal ContainerUploadBlock(string fileId)
    {
        FileId = fileId;
    }

    /// <summary>The file, by the id it was uploaded under.</summary>
    public string FileId { get; }

    /// <summary>The cache breakpoint on this block; null when the wire carried none.</summary>
    public CacheControl? CacheControl { get; internal init; }
}
