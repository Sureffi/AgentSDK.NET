using System.Diagnostics.CodeAnalysis;

namespace Claude.Agent;

/// <summary>
/// One MCP server: a command the binary spawns, an SSE or HTTP endpoint it reaches, an object in
/// this process whose <c>[McpTool]</c> methods it calls back through the client, or one the binary
/// holds that this library did not configure. Which of those it is, is the type.
/// </summary>
public abstract class McpServerConfiguration
{
    private protected McpServerConfiguration()
    {
    }

    /// <summary>How long one call to a tool of this server may take before the binary gives up on it; the binary ignores anything under a second.</summary>
    public TimeSpan? Timeout { get; internal init; }

    /// <summary>A server the binary spawns and talks to over stdio.</summary>
    /// <param name="command">The command to run.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <returns>The configuration.</returns>
    /// <exception cref="ArgumentException"><paramref name="command"/> is null, empty or blank.</exception>
    public static McpStdioServerConfiguration Stdio(string command, params string[] arguments) =>
        Stdio(command, arguments, null);

    /// <summary>A server the binary spawns and talks to over stdio, with its environment.</summary>
    /// <param name="command">The command to run.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <param name="environmentVariables">The environment it is started with.</param>
    /// <param name="timeout">How long one of its tool calls may take.</param>
    /// <param name="alwaysLoad">Whether its tools are always in the prompt rather than deferred behind tool search.</param>
    /// <returns>The configuration.</returns>
    /// <exception cref="ArgumentException"><paramref name="command"/> is null, empty or blank.</exception>
    public static McpStdioServerConfiguration Stdio(
        string command,
        IEnumerable<string>? arguments,
        IReadOnlyDictionary<string, string>? environmentVariables,
        TimeSpan? timeout = null,
        bool? alwaysLoad = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return new McpStdioServerConfiguration(command)
        {
            Arguments = arguments is null ? null : [.. arguments],
            EnvironmentVariables = Copy(environmentVariables),
            Timeout = timeout,
            AlwaysLoad = alwaysLoad,
        };
    }

    /// <summary>A server the binary reaches over SSE.</summary>
    /// <param name="url">Its endpoint.</param>
    /// <param name="headers">Headers sent with every request to it.</param>
    /// <param name="tools">What the binary does when each named tool of it is called.</param>
    /// <param name="timeout">How long one of its tool calls may take.</param>
    /// <param name="alwaysLoad">Whether its tools are always in the prompt rather than deferred behind tool search.</param>
    /// <returns>The configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url"/> is null.</exception>
    public static McpSseServerConfiguration Sse(
        Uri url,
        IReadOnlyDictionary<string, string>? headers = null,
        IEnumerable<McpServerToolPolicy>? tools = null,
        TimeSpan? timeout = null,
        bool? alwaysLoad = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        return new McpSseServerConfiguration(url)
        {
            Headers = Copy(headers),
            Tools = tools is null ? null : [.. tools],
            Timeout = timeout,
            AlwaysLoad = alwaysLoad,
        };
    }

    /// <summary>A server the binary reaches over streamable HTTP.</summary>
    /// <param name="url">Its endpoint.</param>
    /// <param name="headers">Headers sent with every request to it.</param>
    /// <param name="tools">What the binary does when each named tool of it is called.</param>
    /// <param name="timeout">How long one of its tool calls may take.</param>
    /// <param name="alwaysLoad">Whether its tools are always in the prompt rather than deferred behind tool search.</param>
    /// <returns>The configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url"/> is null.</exception>
    public static McpHttpServerConfiguration Http(
        Uri url,
        IReadOnlyDictionary<string, string>? headers = null,
        IEnumerable<McpServerToolPolicy>? tools = null,
        TimeSpan? timeout = null,
        bool? alwaysLoad = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        return new McpHttpServerConfiguration(url)
        {
            Headers = Copy(headers),
            Tools = tools is null ? null : [.. tools],
            Timeout = timeout,
            AlwaysLoad = alwaysLoad,
        };
    }

    /// <summary>The methods of <paramref name="tools"/> marked <c>[McpTool]</c> are read here, once; the server takes the name it is registered under.</summary>
    /// <param name="tools">The object whose marked methods are served.</param>
    /// <param name="version">The version the server reports.</param>
    /// <param name="timeout">How long one of its tool calls may take.</param>
    /// <returns>The configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tools"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="version"/> is null, empty or blank.</exception>
    [RequiresUnreferencedCode("Tool discovery reflects over the object's methods and their parameter types.")]
    public static McpSdkServerConfiguration InProcess(object tools, string version = "1.0.0", TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        return new McpSdkServerConfiguration
        {
            Tools = tools,
            Version = version,
            Timeout = timeout,
            ToolServer = new ReflectionToolServer(tools, version),
        };
    }

    // What the options refuse before a process starts, under the name the server is registered as.
    internal virtual string? Problem(string name) =>
        Timeout is { } timeout && timeout < TimeSpan.Zero
            ? $"MCP server '{name}' has a negative timeout."
            : null;

    // A configuration is immutable once made, which is what lets the options share one between the
    // defaults and every call; the caller's dictionary is the one thing that could still move under it.
    private static OrderedDictionary<string, string>? Copy(IReadOnlyDictionary<string, string>? source)
    {
        if (source is null)
        {
            return null;
        }

        var copy = new OrderedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in source)
        {
            copy[name] = value;
        }

        return copy;
    }
}
