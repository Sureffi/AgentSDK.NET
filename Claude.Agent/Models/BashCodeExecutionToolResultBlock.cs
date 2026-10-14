namespace Claude.Agent;

/// <summary>What the API's shell execution answered a <see cref="ServerToolUseBlock"/> with.</summary>
public sealed class BashCodeExecutionToolResultBlock : ContentBlock
{
    internal BashCodeExecutionToolResultBlock(string toolUseId, IReadOnlyList<BashCodeExecutionToolResultContent> content)
    {
        ToolUseId = toolUseId;
        Content = content;
    }

    /// <summary>The <see cref="ServerToolUseBlock.Id"/> of the call this answers.</summary>
    public string ToolUseId { get; }

    /// <summary>What the command wrote, or the one part saying why it did not run.</summary>
    public IReadOnlyList<BashCodeExecutionToolResultContent> Content { get; }

    /// <summary>The cache breakpoint on this block; null when the wire carried none.</summary>
    public CacheControl? CacheControl { get; internal init; }
}
