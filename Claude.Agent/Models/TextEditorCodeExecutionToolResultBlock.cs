namespace Claude.Agent;

/// <summary>What the API's text editor answered a <see cref="ServerToolUseBlock"/> with.</summary>
public sealed class TextEditorCodeExecutionToolResultBlock : ContentBlock
{
    internal TextEditorCodeExecutionToolResultBlock(string toolUseId, IReadOnlyList<TextEditorCodeExecutionToolResultContent> content)
    {
        ToolUseId = toolUseId;
        Content = content;
    }

    /// <summary>The <see cref="ServerToolUseBlock.Id"/> of the call this answers.</summary>
    public string ToolUseId { get; }

    /// <summary>What the editor did, or the one part saying why it did not run.</summary>
    public IReadOnlyList<TextEditorCodeExecutionToolResultContent> Content { get; }

    /// <summary>The cache breakpoint on this block; null when the wire carried none.</summary>
    public CacheControl? CacheControl { get; internal init; }
}
