namespace Claude.Agent;

/// <summary>The binary is about to ask a person whether a tool may run. The hook may answer in their place.</summary>
public sealed class PermissionRequestHookInput : HookInput
{
    internal PermissionRequestHookInput(HookInputCommon common,
        ToolCall toolCall,
        IReadOnlyList<PermissionUpdate> permissionSuggestions,
        McpServerOrigin? mcpServer)
        : base(HookEvent.PermissionRequest, common)
    {
        ToolCall = toolCall;
        PermissionSuggestions = permissionSuggestions;
        McpServer = mcpServer;
    }

    /// <summary>The call being asked about. Its id is empty: the input the binary sends this event carries none.</summary>
    public ToolCall ToolCall { get; }

    /// <summary>What the binary would remember if the person chose to stop being asked; empty when it would remember nothing.</summary>
    public IReadOnlyList<PermissionUpdate> PermissionSuggestions { get; }

    /// <summary>The MCP server the tool belongs to; null for a tool the binary runs itself.</summary>
    public McpServerOrigin? McpServer { get; }
}
