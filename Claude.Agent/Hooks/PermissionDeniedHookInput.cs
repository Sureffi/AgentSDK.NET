namespace Claude.Agent;

/// <summary>A tool use was refused. The hook may ask for it to be tried again.</summary>
public sealed class PermissionDeniedHookInput : HookInput
{
    internal PermissionDeniedHookInput(HookInputCommon common,
        ToolCall toolCall,
        McpServerOrigin? mcpServer)
        : base(HookEvent.PermissionDenied, common)
    {
        ToolCall = toolCall;
        McpServer = mcpServer;
    }

    /// <summary>The call that was refused, failed, the refusal's reason as its error.</summary>
    public ToolCall ToolCall { get; }

    /// <summary>The MCP server the tool belongs to; null for a tool the binary runs itself.</summary>
    public McpServerOrigin? McpServer { get; }
}
