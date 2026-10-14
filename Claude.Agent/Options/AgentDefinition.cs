namespace Claude.Agent;

/// <summary>A subagent the binary may delegate to, declared by this process.</summary>
public sealed class AgentDefinition
{
    /// <summary>Declares a subagent.</summary>
    /// <param name="description">When to use it; the model reads this to choose.</param>
    /// <param name="prompt">The subagent's system prompt.</param>
    public AgentDefinition(string description, string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(prompt);
        Description = description;
        Prompt = prompt;
    }

    /// <summary>When to use the agent; the model reads this to choose.</summary>
    public string Description { get; }

    /// <summary>The agent's system prompt.</summary>
    public string Prompt { get; }

    /// <summary>Null leaves the parent's tools.</summary>
    public IList<string>? Tools { get; set; }

    /// <summary>Tools taken away from the agent.</summary>
    public IList<string>? DisallowedTools { get; set; }

    /// <summary>The agent's model, by alias or id. Null is the parent's.</summary>
    public string? Model { get; set; }

    /// <summary>Skills the agent has, by name.</summary>
    public IList<string>? Skills { get; set; }

    /// <summary>The scope of the agent's memory, written as <c>memory</c>. Null leaves the parent's.</summary>
    public AgentMemory? Memory { get; set; }

    /// <summary>The MCP servers this agent may use, by the name each has in <see cref="ClaudeAgentSessionOptions.McpServers"/>.</summary>
    public IList<string>? McpServers { get; set; }

    /// <summary>A first prompt of the agent's own, written as <c>initialPrompt</c>.</summary>
    public string? InitialPrompt { get; set; }

    /// <summary>The most turns the agent may take.</summary>
    public int? MaxTurns { get; set; }

    /// <summary>Start the agent as a background task.</summary>
    public bool? Background { get; set; }

    /// <summary>An effort level. Exclusive with <see cref="EffortTokens"/>.</summary>
    public Effort? Effort { get; set; }

    /// <summary>An effort as a token budget. Exclusive with <see cref="Effort"/>.</summary>
    public int? EffortTokens { get; set; }

    /// <summary>The permission mode the agent runs in. Null is the parent's.</summary>
    public PermissionMode? PermissionMode { get; set; }

    internal AgentDefinition Clone() => new(Description, Prompt)
    {
        Tools = Copy(Tools),
        DisallowedTools = Copy(DisallowedTools),
        Model = Model,
        Skills = Copy(Skills),
        Memory = Memory,
        McpServers = Copy(McpServers),
        InitialPrompt = InitialPrompt,
        MaxTurns = MaxTurns,
        Background = Background,
        Effort = Effort,
        EffortTokens = EffortTokens,
        PermissionMode = PermissionMode,
    };

    private static List<string>? Copy(IList<string>? list) => list is null ? null : [.. list];

    internal string? Problem(string name)
    {
        if (Effort is not null && EffortTokens is not null)
        {
            return $"Agent '{name}' sets both Effort and EffortTokens; set one.";
        }

        if (Effort is { } effort && effort.ToString().Length == 0)
        {
            return $"Agent '{name}' sets Effort to no value; set an effort level or leave it null.";
        }

        if (PermissionMode is { } permissionMode && permissionMode.ToString().Length == 0)
        {
            return $"Agent '{name}' sets PermissionMode to no value; set a mode or leave it null.";
        }

        return Memory is { } memory && memory.ToString().Length == 0
            ? $"Agent '{name}' sets Memory to no value; set a scope or leave it null."
            : null;
    }
}
