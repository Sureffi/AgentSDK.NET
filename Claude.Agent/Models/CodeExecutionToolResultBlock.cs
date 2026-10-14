namespace Claude.Agent;

/// <summary>What the API's code execution answered a <see cref="ServerToolUseBlock"/> with.</summary>
public sealed class CodeExecutionToolResultBlock : ContentBlock
{
    internal CodeExecutionToolResultBlock(string toolUseId, IReadOnlyList<CodeExecutionToolResultContent> content)
    {
        ToolUseId = toolUseId;
        Content = content;
    }

    /// <summary>The <see cref="ServerToolUseBlock.Id"/> of the call this answers.</summary>
    public string ToolUseId { get; }

    /// <summary>What the code wrote, or the one part saying why it did not run.</summary>
    public IReadOnlyList<CodeExecutionToolResultContent> Content { get; }

    /// <summary>The cache breakpoint on this block; null when the wire carried none.</summary>
    public CacheControl? CacheControl { get; internal init; }
}
