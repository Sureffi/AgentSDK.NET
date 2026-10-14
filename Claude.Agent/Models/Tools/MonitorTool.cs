namespace Claude.Agent;

/// <summary>The socket a Monitor watches, where it watches one rather than a command.</summary>
/// <param name="url">The socket to open.</param>
/// <param name="protocols">The subprotocols to ask for.</param>
public sealed class MonitorToolWebSocket(string url, IReadOnlyList<string>? protocols = null)
{
    /// <summary>The socket to open.</summary>
    public string Url { get; } = url;

    /// <summary>The subprotocols to ask for.</summary>
    public IReadOnlyList<string> Protocols { get; } = protocols ?? [];
}

/// <summary>What the model asked the Monitor tool to watch. A call names a command or a socket, never both.</summary>
/// <param name="description">What is being watched, in the words the notifications use.</param>
/// <param name="timeout">How long the watch may run before the binary kills it; the wire carries it in milliseconds.</param>
/// <param name="command">The command whose every output line is an event; null when the call watches a socket.</param>
/// <param name="webSocket">The socket whose every text frame is an event; null when the call watches a command.</param>
public sealed class MonitorToolInput(
    string description,
    TimeSpan timeout,
    string? command = null,
    MonitorToolWebSocket? webSocket = null) : ToolInput
{
    /// <summary>What is being watched, in the words the notifications use.</summary>
    public string Description { get; } = description;

    /// <summary>How long the watch may run before the binary kills it; the wire carries it in milliseconds.</summary>
    public TimeSpan Timeout { get; } = timeout;

    /// <summary>The command whose every output line is an event; null when the call watches a socket.</summary>
    public string? Command { get; } = command;

    /// <summary>The socket whose every text frame is an event; null when the call watches a command.</summary>
    public MonitorToolWebSocket? WebSocket { get; } = webSocket;
}

/// <summary>What a Monitor handed back.</summary>
/// <param name="taskId">The background task the watch runs as.</param>
/// <param name="timeout">When the watch is killed; the wire carries it in milliseconds, and zero where the watch has no deadline.</param>
/// <param name="persistent">Whether the watch runs until it is stopped rather than to a deadline; null when the binary said nothing.</param>
public sealed class MonitorToolOutput(string taskId, TimeSpan timeout, bool? persistent = null) : ToolOutput
{
    /// <summary>The background task the watch runs as.</summary>
    public string TaskId { get; } = taskId;

    /// <summary>When the watch is killed; the wire carries it in milliseconds, and zero where the watch has no deadline.</summary>
    public TimeSpan Timeout { get; } = timeout;

    /// <summary>Whether the watch runs until it is stopped rather than to a deadline; null when the binary said nothing.</summary>
    public bool? Persistent { get; } = persistent;
}
