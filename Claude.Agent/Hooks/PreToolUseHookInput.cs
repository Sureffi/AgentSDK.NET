namespace Claude.Agent;

/// <summary>A tool is about to run. The hook may let it, stop it, or hand back a different input for it.</summary>
public sealed class PreToolUseHookInput : HookInput
{
    internal PreToolUseHookInput(HookInputCommon common,
        ToolCall toolCall,
        McpServerOrigin? mcpServer)
        : base(HookEvent.PreToolUse, common)
    {
        ToolCall = toolCall;
        McpServer = mcpServer;
    }

    /// <summary>The call as the binary is about to make it: the tool, its input and its id. A hook's own snapshot, not the pairing a run keeps.</summary>
    public ToolCall ToolCall { get; }

    /// <summary>The MCP server the tool belongs to; null for a tool the binary runs itself.</summary>
    public McpServerOrigin? McpServer { get; }
}
