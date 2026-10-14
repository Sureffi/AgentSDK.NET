using System.Text.Json;

namespace Claude.Agent;

/// <summary>The first message of a session: what the binary loaded and the id it runs under.</summary>
public sealed class SystemInitMessage : SystemMessage
{
    internal SystemInitMessage(string? uuid, string? sessionId, JsonElement? raw)
        : base("init", uuid, sessionId, raw)
    {
    }

    /// <summary>The directory the binary runs in.</summary>
    public string? WorkingDirectory { get; internal init; }

    /// <summary>The tools the model is offered, MCP tools included.</summary>
    public IReadOnlyList<string> Tools { get; internal init; } = [];

    /// <summary>Each configured MCP server and where it stands.</summary>
    public IReadOnlyList<McpServerSummary> McpServers { get; internal init; } = [];

    /// <summary>The model the session runs on, as a full id.</summary>
    public string? Model { get; internal init; }

    /// <summary>The mode the session runs under; null when the frame carried none.</summary>
    public PermissionMode? PermissionMode { get; internal init; }

    /// <summary>The slash commands a prompt may start with.</summary>
    public IReadOnlyList<string> SlashCommands { get; internal init; } = [];

    /// <summary>The slash commands the binary answers only at a terminal.</summary>
    public IReadOnlyList<string> TerminalSlashCommands { get; internal init; } = [];

    /// <summary>Where the binary found the credential it runs on; <see cref="Claude.Agent.ApiKeySource.None"/> on a login.</summary>
    public ApiKeySource? ApiKeySource { get; internal init; }

    /// <summary>The binary's version.</summary>
    public string? ClaudeCodeVersion { get; internal init; }

    /// <summary>The output style in effect.</summary>
    public string? OutputStyle { get; internal init; }

    /// <summary>The subagents the Agent tool may start, by name: the binary's own and those in <see cref="ClaudeAgentSessionOptions.Agents"/>.</summary>
    public IReadOnlyList<string> Agents { get; internal init; } = [];

    /// <summary>The skills loaded, by name.</summary>
    public IReadOnlyList<string> Skills { get; internal init; } = [];

    /// <summary>Protocol capabilities the binary announces, by name.</summary>
    public IReadOnlyList<string> Capabilities { get; internal init; } = [];

    /// <summary>The API betas the session asks for, by name.</summary>
    public IReadOnlyList<string> Betas { get; internal init; } = [];

    /// <summary>The plugins loaded.</summary>
    public IReadOnlyList<PluginInfo> Plugins { get; internal init; } = [];

    /// <summary>How hard the model is asked to think, when the session sets it.</summary>
    public Effort? Effort { get; internal init; }

    /// <summary>Whether the binary's fast mode is in effect.</summary>
    public FastModeState? FastModeState { get; internal init; }

    /// <summary>Why fast mode is off, when it is.</summary>
    public FastModeDisabledReason? FastModeDisabledReason { get; internal init; }

    /// <summary>Whether the binary was told not to report analytics.</summary>
    public bool? AnalyticsDisabled { get; internal init; }

    /// <summary>Whether the binary was told not to ask for product feedback.</summary>
    public bool? ProductFeedbackDisabled { get; internal init; }

    /// <summary>Where the binary keeps the session's memory.</summary>
    public MemoryPaths? MemoryPaths { get; internal init; }

    /// <summary>The socket the binary listens on for messages from its peers.</summary>
    public string? MessagingSocketPath { get; internal init; }

    /// <summary>The directory the binary keeps this session's scratch files in; null when the frame carried none, as it carries none while the scratchpad is off.</summary>
    public string? ScratchpadDirectory { get; internal init; }

    /// <summary>Whether effort goes out per turn for <see cref="Model"/>: true when a change of effort is sent inside the conversation at the turn it changes and keeps the prompt cache, false when it is sent only as the request's parameter and a change rewrites the cached prefix; null when the frame carried none.</summary>
    public bool? IsPerTurnEffortActive { get; internal init; }

    /// <summary>The view the binary shows the session's transcript in; null when the frame carried none.</summary>
    public ViewMode? ViewMode { get; internal init; }
}

/// <summary>An MCP server the session was started with, and where it stands.</summary>
public sealed class McpServerSummary
{
    internal McpServerSummary(string name, McpServerConnectionStatus status)
    {
        Name = name;
        Status = status;
    }

    /// <summary>The server, by the name the tools are prefixed with.</summary>
    public string Name { get; }

    /// <summary>Whether the binary reached it.</summary>
    public McpServerConnectionStatus Status { get; }

    /// <summary>Where the server's definition came from, as <see cref="McpServerStatus.Source"/> says it; null when the binary said none.</summary>
    public McpServerSource? Source { get; internal init; }
}

/// <summary>A plugin the binary loaded.</summary>
public sealed class PluginInfo
{
    internal PluginInfo(string name, string path)
    {
        Name = name;
        Path = path;
    }

    /// <summary>The plugin, by name.</summary>
    public string Name { get; }

    /// <summary>Where it was loaded from.</summary>
    public string Path { get; }

    /// <summary>The version loaded, when the plugin declares one.</summary>
    public string? Version { get; internal init; }
}

/// <summary>Where the binary keeps a session's memory.</summary>
public sealed class MemoryPaths
{
    internal MemoryPaths(string? auto) => Auto = auto;

    /// <summary>The directory the binary writes memory to by itself.</summary>
    public string? Auto { get; }
}
