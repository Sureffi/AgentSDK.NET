namespace Claude.Agent;

/// <summary>
/// The API's summary of the context it compacted away, which goes back unchanged when the turn is
/// sent again. Not the binary's own compaction, which arrives as system frames and text.
/// </summary>
public sealed class CompactionBlock : ContentBlock
{
    internal CompactionBlock()
    {
    }

    /// <summary>The summary; null when the compaction failed, which the API takes as nothing to carry.</summary>
    public string? Content { get; internal init; }

    /// <summary>What the API kept beside the summary, opaque, to go back unchanged; null when it kept nothing.</summary>
    public string? EncryptedContent { get; internal init; }

    /// <summary>The API's signature over the summary, to go back with it; null when the wire carried none.</summary>
    public string? Signature { get; internal init; }
}
