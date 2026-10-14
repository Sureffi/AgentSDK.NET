namespace Claude.Agent;

/// <summary>A tool has run and answered. The hook may add to what the model reads, or replace the tool's output.</summary>
public sealed class PostToolUseHookInput : HookInput
{
    internal PostToolUseHookInput(HookInputCommon common,
        ToolCall toolCall,
        TimeSpan? duration,
        McpServerOrigin? mcpServer)
        : base(HookEvent.PostToolUse, common)
    {
        ToolCall = toolCall;
        Duration = duration;
        McpServer = mcpServer;
    }

    /// <summary>The call as it was answered, the tool's own output on it. A hook's own snapshot, not the pairing a run keeps.</summary>
    public ToolCall ToolCall { get; }

    /// <summary>How long the tool took; null when the binary did not say.</summary>
    public TimeSpan? Duration { get; }

    /// <summary>The MCP server the tool belongs to; null for a tool the binary runs itself.</summary>
    public McpServerOrigin? McpServer { get; }
}
