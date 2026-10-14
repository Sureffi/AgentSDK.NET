namespace Claude.Agent;

/// <summary>The MCP server a tool belongs to, and where that server's definition came from.</summary>
public sealed class McpServerOrigin
{
    internal McpServerOrigin(string name, McpServerSource source)
    {
        Name = name;
        Source = source;
    }

    /// <summary>The server, by the name it has in the configuration that declares it, or the name this library registered it under.</summary>
    public string Name { get; }

    /// <summary>Where the server's definition came from, as the binary names it.</summary>
    public McpServerSource Source { get; }
}
