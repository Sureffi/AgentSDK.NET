namespace Claude.Agent;

// The shape a configuration section binds to: strings where the options hold enums or JSON, so a
// misspelling is reported under its key rather than swallowed by the binder.
internal sealed class ClaudeAgentClientOptionsDto
{
    public string? ExecutablePath { get; set; }

    public Dictionary<string, string>? EnvironmentVariables { get; set; }

    public TimeSpan? ProcessGracePeriod { get; set; }

    public int? MaxMessageSize { get; set; }

    public string? ConfigurationDirectory { get; set; }

    public ClaudeAgentSessionOptionsDto? Defaults { get; set; }
}

// What every call asks unless it says otherwise. Which session a call starts is not here: a default
// names no session.
internal sealed class ClaudeAgentSessionOptionsDto
{
    public string? WorkingDirectory { get; set; }

    public SystemPromptDto? SystemPrompt { get; set; }

    public string? Model { get; set; }

    public string? FallbackModel { get; set; }

    public string? Effort { get; set; }

    public ThinkingDto? Thinking { get; set; }

    public List<string>? Betas { get; set; }

    public int? MaxTurns { get; set; }

    public decimal? MaxBudgetUsd { get; set; }

    public int? TaskBudget { get; set; }

    public List<string>? Tools { get; set; }

    public List<string>? AllowedTools { get; set; }

    public List<string>? DisallowedTools { get; set; }

    public string? PermissionMode { get; set; }

    public string? PermissionPromptTool { get; set; }

    public Dictionary<string, McpServerDto>? McpServers { get; set; }

    public bool? OnlyConfiguredMcpServers { get; set; }

    public Dictionary<string, AgentDto>? Agents { get; set; }

    public List<string>? Skills { get; set; }

    public bool? AllSkills { get; set; }

    public List<string>? PluginDirectories { get; set; }

    public string? Settings { get; set; }

    public List<string>? SettingSources { get; set; }

    public SandboxDto? Sandbox { get; set; }

    public List<string>? AdditionalDirectories { get; set; }

    public bool? IncludePartialMessages { get; set; }

    public bool? IncludeHookEvents { get; set; }

    public bool? ForwardSubagentText { get; set; }

    public bool? ReplayUserMessages { get; set; }

    /// <summary>The schema as JSON text.</summary>
    public string? OutputSchema { get; set; }

    public bool? EnableFileCheckpointing { get; set; }
}

internal sealed class SystemPromptDto
{
    public string? Text { get; set; }

    public string? File { get; set; }

    public bool? Preset { get; set; }

    public string? Append { get; set; }

    public bool? ExcludeDynamicSections { get; set; }
}

internal sealed class ThinkingDto
{
    /// <summary>adaptive, budget or disabled.</summary>
    public string? Mode { get; set; }

    public int? BudgetTokens { get; set; }

    public bool? OmitThinking { get; set; }
}

internal sealed class McpServerDto
{
    /// <summary>stdio, sse or http; an in-process server is code, registered with AddClaudeTools.</summary>
    public string? Type { get; set; }

    public string? Command { get; set; }

    public List<string>? Args { get; set; }

    public Dictionary<string, string>? Env { get; set; }

    public string? Url { get; set; }

    public Dictionary<string, string>? Headers { get; set; }

    public List<McpToolPolicyDto>? Tools { get; set; }

    /// <summary>Milliseconds, as the binary counts a server's tool-call timeout.</summary>
    public int? Timeout { get; set; }

    public bool? AlwaysLoad { get; set; }
}

internal sealed class McpToolPolicyDto
{
    public string? Name { get; set; }

    public string? PermissionPolicy { get; set; }

    public string? OrganizationMaxPermission { get; set; }
}

internal sealed class AgentDto
{
    public string? Description { get; set; }

    public string? Prompt { get; set; }

    public List<string>? Tools { get; set; }

    public List<string>? DisallowedTools { get; set; }

    public string? Model { get; set; }

    public List<string>? Skills { get; set; }

    public string? Memory { get; set; }

    /// <summary>One JSON object each.</summary>
    public List<string>? McpServers { get; set; }

    public string? InitialPrompt { get; set; }

    public int? MaxTurns { get; set; }

    public bool? Background { get; set; }

    /// <summary>An effort level, or a token count.</summary>
    public string? Effort { get; set; }

    public string? PermissionMode { get; set; }
}

internal sealed class SandboxDto
{
    public bool? Enabled { get; set; }

    public bool? AutoAllowBashIfSandboxed { get; set; }

    public List<string>? ExcludedCommands { get; set; }

    public bool? AllowUnsandboxedCommands { get; set; }

    public bool? EnableWeakerNestedSandbox { get; set; }

    public List<string>? AllowedDomains { get; set; }

    public List<string>? DeniedDomains { get; set; }

    public bool? AllowManagedDomainsOnly { get; set; }

    public List<string>? AllowUnixSockets { get; set; }

    public bool? AllowAllUnixSockets { get; set; }

    public bool? AllowLocalBinding { get; set; }

    public List<string>? AllowMachLookup { get; set; }

    public int? HttpProxyPort { get; set; }

    public int? SocksProxyPort { get; set; }

    public List<string>? IgnoredFileViolations { get; set; }

    public List<string>? IgnoredNetworkViolations { get; set; }
}
