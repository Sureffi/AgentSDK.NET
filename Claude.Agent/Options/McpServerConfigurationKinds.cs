using System.Text.Json;

namespace Claude.Agent;

/// <summary>A server the binary spawns and talks to over stdio.</summary>
public sealed class McpStdioServerConfiguration : McpServerConfiguration
{
    internal McpStdioServerConfiguration(string command)
    {
        Command = command;
    }

    /// <summary>The command the binary runs.</summary>
    public string Command { get; }

    /// <summary>The command's arguments; null when the binary is left to run it bare.</summary>
    public IReadOnlyList<string>? Arguments { get; internal init; }

    /// <summary>The environment the command is started with; null when the binary is left to pass its own.</summary>
    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; internal init; }

    /// <summary>Whether the server's tools are always in the prompt rather than deferred behind tool search; setting it also makes the binary wait for the server at startup.</summary>
    public bool? AlwaysLoad { get; internal init; }

    internal override string? Problem(string name) =>
        string.IsNullOrWhiteSpace(Command) ? $"MCP server '{name}' needs a command." : base.Problem(name);
}

/// <summary>A server the binary reaches over SSE.</summary>
public sealed class McpSseServerConfiguration : McpServerConfiguration
{
    internal McpSseServerConfiguration(Uri url)
    {
        Url = url;
    }

    /// <summary>The server's endpoint.</summary>
    public Uri Url { get; }

    /// <summary>Headers sent with every request to the server; null when none are.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; internal init; }

    /// <summary>What the binary does when each named tool of the server is called; null when it decides for itself.</summary>
    public IReadOnlyList<McpServerToolPolicy>? Tools { get; internal init; }

    /// <summary>Whether the server's tools are always in the prompt rather than deferred behind tool search; setting it also makes the binary wait for the server at startup.</summary>
    public bool? AlwaysLoad { get; internal init; }

    internal override string? Problem(string name) =>
        Url is null ? $"MCP server '{name}' needs a url." : McpServerToolPolicy.Problem(Tools, name) ?? base.Problem(name);
}

/// <summary>A server the binary reaches over streamable HTTP.</summary>
public sealed class McpHttpServerConfiguration : McpServerConfiguration
{
    internal McpHttpServerConfiguration(Uri url)
    {
        Url = url;
    }

    /// <summary>The server's endpoint.</summary>
    public Uri Url { get; }

    /// <summary>Headers sent with every request to the server; null when none are.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; internal init; }

    /// <summary>What the binary does when each named tool of the server is called; null when it decides for itself.</summary>
    public IReadOnlyList<McpServerToolPolicy>? Tools { get; internal init; }

    /// <summary>Whether the server's tools are always in the prompt rather than deferred behind tool search; setting it also makes the binary wait for the server at startup.</summary>
    public bool? AlwaysLoad { get; internal init; }

    internal override string? Problem(string name) =>
        Url is null ? $"MCP server '{name}' needs a url." : McpServerToolPolicy.Problem(Tools, name) ?? base.Problem(name);
}

/// <summary>A server that is an object in this process: the binary calls its tools back through the client, and nothing is spawned.</summary>
public sealed class McpSdkServerConfiguration : McpServerConfiguration
{
    internal McpSdkServerConfiguration()
    {
    }

    /// <summary>The name the binary holds the server under; null on one this library registered, which takes its key in the options instead.</summary>
    public string? Name { get; internal init; }

    /// <summary>The object served in-process; its tools are the methods marked <c>[McpTool]</c>. Null on a server this library did not register.</summary>
    public object? Tools { get; internal init; }

    /// <summary>The version the server reports; null on a server this library did not register.</summary>
    public string? Version { get; internal init; }

    internal ReflectionToolServer? ToolServer { get; init; }

    // One read off the wire, as an MCP status reply carries it, names an object this process does
    // not hold: there is nothing to serve its tools from, so it cannot be sent back out.
    internal override string? Problem(string name) =>
        ToolServer is null
            ? $"MCP server '{name}' is an in-process server this library did not register; AddClaudeTools registers one."
            : base.Problem(name);
}

/// <summary>A server reached through the claude.ai proxy. The binary holds these; this library does not configure one.</summary>
public sealed class McpClaudeAIProxyServerConfiguration : McpServerConfiguration
{
    internal McpClaudeAIProxyServerConfiguration()
    {
    }

    /// <summary>The proxy endpoint.</summary>
    public Uri? Url { get; internal init; }

    /// <summary>What the proxy calls the server.</summary>
    public string? Id { get; internal init; }

    internal override string? Problem(string name) =>
        $"MCP server '{name}' is of a kind this library cannot send.";
}

/// <summary>A configuration of a kind this library has no type for, or one that does not fit its kind's type: its kind's name and the configuration as written.</summary>
public sealed class UnknownMcpServerConfiguration : McpServerConfiguration
{
    internal UnknownMcpServerConfiguration(string type, JsonElement raw)
    {
        Type = type;
        Raw = raw;
    }

    /// <summary>The configuration's <c>type</c>; the empty string when it carried none.</summary>
    public string Type { get; }

    /// <summary>The configuration as the binary wrote it.</summary>
    public JsonElement Raw { get; }

    internal override string? Problem(string name) =>
        $"MCP server '{name}' is of a kind this library cannot send.";
}
