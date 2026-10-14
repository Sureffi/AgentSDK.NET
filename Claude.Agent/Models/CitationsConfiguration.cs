namespace Claude.Agent;

/// <summary>Whether the model may cite the block this sits on.</summary>
public sealed class CitationsConfiguration
{
    internal CitationsConfiguration()
    {
    }

    /// <summary>Whether citing is on; null when the wire did not say.</summary>
    public bool? Enabled { get; internal init; }
}
