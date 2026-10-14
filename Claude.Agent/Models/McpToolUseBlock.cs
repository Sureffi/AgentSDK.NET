using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// The model calling a tool of an MCP server the API is connected to. Not the binary's own MCP
/// tools, which reach the wire as a <see cref="ToolUseBlock"/> named <c>mcp__server__tool</c>.
/// </summary>
public sealed class McpToolUseBlock : ContentBlock
{
    internal McpToolUseBlock(string id, string name, string serverName, JsonElement rawInput)
    {
        Id = id;
        Name = name;
        ServerName = serverName;
        RawInput = rawInput;
    }

    /// <summary>The call's id; the <see cref="McpToolResultBlock"/> that answers it carries it as <see cref="McpToolResultBlock.ToolUseId"/>.</summary>
    public string Id { get; }

    /// <summary>The tool called, by the name the server gives it.</summary>
    public string Name { get; }

    /// <summary>The server the tool belongs to.</summary>
    public string ServerName { get; }

    /// <summary>The arguments as the model wrote them. An MCP server's shapes are its own, so this library has no typed view of them.</summary>
    public JsonElement RawInput { get; }
}
