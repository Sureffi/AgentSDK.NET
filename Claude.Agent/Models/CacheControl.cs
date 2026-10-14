namespace Claude.Agent;

/// <summary>A cache breakpoint on a block: what a request asks the API to keep cached up to that block.</summary>
public sealed class CacheControl
{
    internal CacheControl()
    {
    }

    /// <summary>How long the breakpoint lasts; null when the wire did not say, which the API takes as five minutes.</summary>
    public CacheControlTimeToLive? TimeToLive { get; internal init; }
}
