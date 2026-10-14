namespace Claude.Agent;

/// <summary>What a tool answered, as the model is fed it.</summary>
public sealed class ToolResultBlock : ContentBlock
{
    internal ToolResultBlock(string toolUseId, IReadOnlyList<ContentBlock> content, bool? isError)
    {
        ToolUseId = toolUseId;
        Content = content;
        IsError = isError;
    }

    /// <summary>The <see cref="ToolUseBlock.Id"/> of the call this answers.</summary>
    public string ToolUseId { get; }

    /// <summary>What the model is fed; content written as a string is one <see cref="TextBlock"/>, and none written is an empty list.</summary>
    public IReadOnlyList<ContentBlock> Content { get; }

    /// <summary>Whether the call failed; null when the frame did not say.</summary>
    public bool? IsError { get; }

    /// <summary>The family of tools the answered call belongs to; null when it belongs to none.</summary>
    public string? ToolsetName { get; internal init; }

    /// <summary>The cache breakpoint on this block; null when the wire carried none.</summary>
    public CacheControl? CacheControl { get; internal init; }
}
