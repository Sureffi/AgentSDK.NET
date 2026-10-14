namespace Claude.Agent;

/// <summary>One MCP server as the binary sees it.</summary>
public sealed class McpServerStatus
{
    internal McpServerStatus(string name, McpServerConnectionStatus status)
    {
        Name = name;
        Status = status;
    }

    /// <summary>The name the server is configured under, which is the one its tools are called by.</summary>
    public string Name { get; }

    /// <summary>Where the server stands with the binary.</summary>
    public McpServerConnectionStatus Status { get; }

    /// <summary>What the server called itself when it connected.</summary>
    public McpServerInfo? ServerInfo { get; internal init; }

    /// <summary>Why the server failed, when it did.</summary>
    public string? Error { get; internal init; }

    /// <summary>The configuration the binary holds for the server.</summary>
    public McpServerConfiguration? Config { get; internal init; }

    /// <summary>Which configuration the server was read from, named as <see cref="Source"/> names it; null when the binary said none.</summary>
    public McpServerSource? Scope { get; internal init; }

    /// <summary>Where the server's definition came from, as the binary names it; null when the binary said none.</summary>
    public McpServerSource? Source { get; internal init; }

    /// <summary>The tools the server serves, once connected.</summary>
    public IReadOnlyList<McpServerTool> Tools { get; internal init; } = [];
}

/// <summary>What an MCP server calls itself.</summary>
public sealed class McpServerInfo
{
    internal McpServerInfo()
    {
    }

    /// <summary>The name the server reports, which need not be the one it is configured under.</summary>
    public string? Name { get; internal init; }

    /// <summary>The version the server reports.</summary>
    public string? Version { get; internal init; }
}

/// <summary>One tool an MCP server serves.</summary>
public sealed class McpServerTool
{
    internal McpServerTool(string name)
    {
        Name = name;
    }

    /// <summary>The tool's name as the server gives it, without the server's prefix.</summary>
    public string Name { get; }

    /// <summary>What the tool does.</summary>
    public string? Description { get; internal init; }

    /// <summary>What the server says the tool does to the world.</summary>
    public McpToolAnnotations? Annotations { get; internal init; }
}

/// <summary>What an MCP server says one of its tools does to the world. The server's own word, which the binary does not check.</summary>
public sealed class McpToolAnnotations
{
    internal McpToolAnnotations()
    {
    }

    /// <summary>Whether the tool changes nothing.</summary>
    public bool? IsReadOnly { get; internal init; }

    /// <summary>Whether the tool may undo or destroy what it touches.</summary>
    public bool? IsDestructive { get; internal init; }

    /// <summary>Whether the tool reaches outside the machine it runs on.</summary>
    public bool? IsOpenWorld { get; internal init; }
}
