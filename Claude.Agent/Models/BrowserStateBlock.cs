namespace Claude.Agent;

/// <summary>Where the browser stands after a call: every tab open, and what changed during the call.</summary>
public sealed class BrowserStateBlock : ContentBlock
{
    internal BrowserStateBlock(IReadOnlyList<BrowserTab> tabs, IReadOnlyList<BrowserStateChange> stateChanges)
    {
        Tabs = tabs;
        StateChanges = stateChanges;
    }

    /// <summary>Every tab open after the call, not only the ones it opened; when any is open, one of them is in front.</summary>
    public IReadOnlyList<BrowserTab> Tabs { get; }

    /// <summary>What the call opened and downloaded; empty when it had nothing to report.</summary>
    public IReadOnlyList<BrowserStateChange> StateChanges { get; }

    /// <summary>The cache breakpoint on this block; null when the wire carried none.</summary>
    public CacheControl? CacheControl { get; internal init; }
}
