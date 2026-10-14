namespace Claude.Agent;

/// <summary>A tool has run and failed. The hook may add to what the model reads about it.</summary>
public sealed class PostToolUseFailureHookInput : HookInput
{
    internal PostToolUseFailureHookInput(HookInputCommon common,
        ToolCall toolCall,
        bool? isInterrupt,
        TimeSpan? duration,
        McpServerOrigin? mcpServer)
        : base(HookEvent.PostToolUseFailure, common)
    {
        ToolCall = toolCall;
        IsInterrupt = isInterrupt;
        Duration = duration;
        McpServer = mcpServer;
    }

    /// <summary>The call as it failed, the error text on it. A hook's own snapshot, not the pairing a run keeps.</summary>
    public ToolCall ToolCall { get; }

    /// <summary>Whether the tool was cut short rather than failing of itself; null when the binary did not say.</summary>
    public bool? IsInterrupt { get; }

    /// <summary>How long the tool ran before it failed; null when the binary did not say.</summary>
    public TimeSpan? Duration { get; }

    /// <summary>The MCP server the tool belongs to; null for a tool the binary runs itself.</summary>
    public McpServerOrigin? McpServer { get; }
}
