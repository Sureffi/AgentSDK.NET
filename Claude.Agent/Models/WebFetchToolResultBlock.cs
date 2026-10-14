namespace Claude.Agent;

/// <summary>What the API's web fetch answered a <see cref="ServerToolUseBlock"/> with.</summary>
public sealed class WebFetchToolResultBlock : ContentBlock
{
    internal WebFetchToolResultBlock(string toolUseId, IReadOnlyList<WebFetchToolResultContent> content)
    {
        ToolUseId = toolUseId;
        Content = content;
    }

    /// <summary>The <see cref="ServerToolUseBlock.Id"/> of the call this answers.</summary>
    public string ToolUseId { get; }

    /// <summary>The page fetched, or the one part saying why the fetch did not answer.</summary>
    public IReadOnlyList<WebFetchToolResultContent> Content { get; }

    /// <summary>What made the call; null when the wire did not say.</summary>
    public ToolCaller? Caller { get; internal init; }

    /// <summary>The cache breakpoint on this block; null when the wire carried none.</summary>
    public CacheControl? CacheControl { get; internal init; }
}
