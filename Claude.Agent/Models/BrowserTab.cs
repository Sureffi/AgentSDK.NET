namespace Claude.Agent;

/// <summary>One tab of the browser's inventory after a call.</summary>
public sealed class BrowserTab
{
    internal BrowserTab(string tabId, string title, string url)
    {
        TabId = tabId;
        Title = title;
        Url = url;
    }

    /// <summary>The tab, by an id unique within the inventory.</summary>
    public string TabId { get; }

    /// <summary>The title of the page the tab shows; may be empty.</summary>
    public string Title { get; }

    /// <summary>The address of the page the tab shows; may be empty.</summary>
    public string Url { get; }

    /// <summary>Whether this is the tab in front after the call; null when the wire did not say.</summary>
    public bool? Active { get; internal init; }
}
