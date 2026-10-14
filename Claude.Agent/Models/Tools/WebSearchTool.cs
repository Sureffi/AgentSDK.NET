namespace Claude.Agent;

/// <summary>What the model asked the WebSearch tool to search for.</summary>
/// <param name="query">The search terms.</param>
/// <param name="allowedDomains">The only domains results may come from; empty when the call named none.</param>
/// <param name="blockedDomains">Domains results may not come from; empty when the call named none.</param>
public sealed class WebSearchToolInput(
    string query,
    IReadOnlyList<string>? allowedDomains = null,
    IReadOnlyList<string>? blockedDomains = null) : ToolInput
{
    /// <summary>The search terms.</summary>
    public string Query { get; } = query;

    /// <summary>The only domains results may come from; empty when the call named none.</summary>
    public IReadOnlyList<string> AllowedDomains { get; } = allowedDomains ?? [];

    /// <summary>Domains results may not come from; empty when the call named none.</summary>
    public IReadOnlyList<string> BlockedDomains { get; } = blockedDomains ?? [];
}

/// <summary>One result of a WebSearch. The wire tells the two apart by their JSON kind, not by a key.</summary>
public abstract class WebSearchToolResultItem
{
    private protected WebSearchToolResultItem()
    {
    }
}

/// <summary>One page a WebSearch hit names.</summary>
/// <param name="title">The page's title.</param>
/// <param name="url">The page's address.</param>
public sealed class WebSearchLink(string title, string url)
{
    /// <summary>The page's title.</summary>
    public string Title { get; } = title;

    /// <summary>The page's address, as the wire spells it.</summary>
    public string Url { get; } = url;
}

/// <summary>A result the search engine returned as an object: the pages it found, under the id of the server-side search that found them.</summary>
/// <param name="toolUseId">The id of the server-side search that found them.</param>
/// <param name="content">The pages themselves.</param>
public sealed class WebSearchHitResult(string toolUseId, IReadOnlyList<WebSearchLink> content) : WebSearchToolResultItem
{
    /// <summary>The id of the server-side search that found them.</summary>
    public string ToolUseId { get; } = toolUseId;

    /// <summary>The pages themselves.</summary>
    public IReadOnlyList<WebSearchLink> Content { get; } = content;
}

/// <summary>A result the search engine returned as a plain string.</summary>
/// <param name="text">The string itself.</param>
public sealed class WebSearchTextResult(string text) : WebSearchToolResultItem
{
    /// <summary>The string itself.</summary>
    public string Text { get; } = text;
}

/// <summary>What a WebSearch handed back.</summary>
/// <param name="query">The search terms, as the search ran them.</param>
/// <param name="results">What came back, in order.</param>
/// <param name="duration">How long the search took; the wire carries it in seconds.</param>
/// <param name="searchCount">How many searches were run.</param>
public sealed class WebSearchToolOutput(
    string query,
    IReadOnlyList<WebSearchToolResultItem> results,
    TimeSpan duration,
    long? searchCount = null) : ToolOutput
{
    /// <summary>The search terms, as the search ran them.</summary>
    public string Query { get; } = query;

    /// <summary>What came back, in order.</summary>
    public IReadOnlyList<WebSearchToolResultItem> Results { get; } = results;

    /// <summary>How long the search took; the wire carries it in seconds under <c>durationSeconds</c>.</summary>
    public TimeSpan Duration { get; } = duration;

    /// <summary>How many searches were run; null when the binary gave no count.</summary>
    public long? SearchCount { get; } = searchCount;
}
