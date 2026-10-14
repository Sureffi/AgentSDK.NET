using System.Text.Json;

namespace Claude.Agent;

/// <summary>An MCP server is asking for input. The hook may answer in the person's place instead of a dialog being shown.</summary>
public sealed class ElicitationHookInput : HookInput
{
    internal ElicitationHookInput(HookInputCommon common,
        string mcpServerName,
        string message,
        ElicitationMode? mode,
        string? url,
        string? elicitationId,
        JsonElement? requestedSchema)
        : base(HookEvent.Elicitation, common)
    {
        McpServerName = mcpServerName;
        Message = message;
        Mode = mode;
        Url = url;
        ElicitationId = elicitationId;
        RequestedSchema = requestedSchema;
    }

    /// <summary>The server asking.</summary>
    public string McpServerName { get; }

    /// <summary>What it is asking for, in its own words.</summary>
    public string Message { get; }

    /// <summary>How it is asking; null when the server did not say.</summary>
    public ElicitationMode? Mode { get; }

    /// <summary>The page to open when it asks that way; null otherwise.</summary>
    public string? Url { get; }

    /// <summary>What ties this ask to the result that answers it; null when the server sent none.</summary>
    public string? ElicitationId { get; }

    /// <summary>The shape of the input the server wants, as JSON schema, as the server wrote it. An MCP server's shape is someone else's, so it is kept raw; null when the server sent none.</summary>
    public JsonElement? RequestedSchema { get; }
}
