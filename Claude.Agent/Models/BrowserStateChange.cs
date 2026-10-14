using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// What changed in the browser during a call. Each kind the wire names is a type of its own; a kind
/// this library has no type for, or a change that does not fit its kind's type, is an
/// <see cref="UnknownBrowserStateChange"/>.
/// </summary>
public abstract class BrowserStateChange
{
    private protected BrowserStateChange()
    {
    }
}

/// <summary>A tab the call opened that is still open at its end; the tab itself is in the block's inventory.</summary>
public sealed class TabOpenedBrowserStateChange : BrowserStateChange
{
    internal TabOpenedBrowserStateChange(string tabId)
    {
        TabId = tabId;
    }

    /// <summary>The tab that was opened, by the id its inventory entry carries.</summary>
    public string TabId { get; }
}

/// <summary>A download the call started.</summary>
public sealed class DownloadStartedBrowserStateChange : BrowserStateChange
{
    internal DownloadStartedBrowserStateChange(string downloadId, string url)
    {
        DownloadId = downloadId;
        Url = url;
    }

    /// <summary>The download, by an id that holds across the changes reporting it.</summary>
    public string DownloadId { get; }

    /// <summary>The address the download was served from, after any redirect.</summary>
    public string Url { get; }
}

/// <summary>A download that finished during the call.</summary>
public sealed class DownloadCompletedBrowserStateChange : BrowserStateChange
{
    internal DownloadCompletedBrowserStateChange(string downloadId, string url)
    {
        DownloadId = downloadId;
        Url = url;
    }

    /// <summary>The download, by an id that holds across the changes reporting it.</summary>
    public string DownloadId { get; }

    /// <summary>The address the download was served from, after any redirect.</summary>
    public string Url { get; }

    /// <summary>Where the file was saved, on the machine that ran the browser; null when nothing else there can read it.</summary>
    public string? Path { get; internal init; }

    /// <summary>The file's size; null when the wire did not say.</summary>
    public long? SizeBytes { get; internal init; }
}

/// <summary>A download that failed or was cancelled during the call.</summary>
public sealed class DownloadFailedBrowserStateChange : BrowserStateChange
{
    internal DownloadFailedBrowserStateChange(string downloadId, string url)
    {
        DownloadId = downloadId;
        Url = url;
    }

    /// <summary>The download, by an id that holds across the changes reporting it.</summary>
    public string DownloadId { get; }

    /// <summary>The address the download was served from, after any redirect.</summary>
    public string Url { get; }

    /// <summary>What went wrong, when it is known.</summary>
    public string? Error { get; internal init; }
}

/// <summary>A change of a kind this library has no type for, or one that does not fit its kind's type: its kind's name and the change as written.</summary>
public sealed class UnknownBrowserStateChange : BrowserStateChange
{
    internal UnknownBrowserStateChange(string type, JsonElement raw)
    {
        Type = type;
        Raw = raw;
    }

    /// <summary>The change's <c>type</c>.</summary>
    public string Type { get; }

    /// <summary>The change as the binary wrote it.</summary>
    public JsonElement Raw { get; }
}
