using System.Text.Json;

namespace Claude.Agent;

/// <summary>An MCP server's ask for input has been answered. The hook may change the answer before the server sees it.</summary>
public sealed class ElicitationResultHookInput : HookInput
{
    internal ElicitationResultHookInput(HookInputCommon common,
        string mcpServerName,
        string? elicitationId,
        ElicitationMode? mode,
        ElicitationAction action,
        JsonElement? content)
        : base(HookEvent.ElicitationResult, common)
    {
        McpServerName = mcpServerName;
        ElicitationId = elicitationId;
        Mode = mode;
        Action = action;
        Content = content;
    }

    /// <summary>The server that asked.</summary>
    public string McpServerName { get; }

    /// <summary>What ties this result to the ask it answers; null when the server sent none.</summary>
    public string? ElicitationId { get; }

    /// <summary>How the server asked; null when it did not say.</summary>
    public ElicitationMode? Mode { get; }

    /// <summary>What was answered.</summary>
    public ElicitationAction Action { get; }

    /// <summary>The input given, as the binary wrote it. An MCP server's shape is someone else's, so it is kept raw; null when nothing was given.</summary>
    public JsonElement? Content { get; }
}
