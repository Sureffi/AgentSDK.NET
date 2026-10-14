namespace Claude.Agent;

/// <summary>What the API's search over the tools on offer answered a <see cref="ServerToolUseBlock"/> with.</summary>
public sealed class ToolSearchToolResultBlock : ContentBlock
{
    internal ToolSearchToolResultBlock(string toolUseId, IReadOnlyList<ToolSearchToolResultContent> content)
    {
        ToolUseId = toolUseId;
        Content = content;
    }

    /// <summary>The <see cref="ServerToolUseBlock.Id"/> of the call this answers.</summary>
    public string ToolUseId { get; }

    /// <summary>The tools found, or the one part saying why the search did not answer.</summary>
    public IReadOnlyList<ToolSearchToolResultContent> Content { get; }

    /// <summary>The cache breakpoint on this block; null when the wire carried none.</summary>
    public CacheControl? CacheControl { get; internal init; }
}
