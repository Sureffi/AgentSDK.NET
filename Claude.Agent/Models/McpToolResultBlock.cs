namespace Claude.Agent;

/// <summary>What a tool of an MCP server the API is connected to answered a <see cref="McpToolUseBlock"/> with.</summary>
public sealed class McpToolResultBlock : ContentBlock
{
    internal McpToolResultBlock(string toolUseId, IReadOnlyList<ContentBlock> content, bool isError)
    {
        ToolUseId = toolUseId;
        Content = content;
        IsError = isError;
    }

    /// <summary>The <see cref="McpToolUseBlock.Id"/> of the call this answers.</summary>
    public string ToolUseId { get; }

    /// <summary>What the model is fed; content written as a string is one <see cref="TextBlock"/>.</summary>
    public IReadOnlyList<ContentBlock> Content { get; }

    /// <summary>Whether the call failed.</summary>
    public bool IsError { get; }
}
