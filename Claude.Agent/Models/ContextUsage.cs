namespace Claude.Agent;

/// <summary>How full the context window is, as the binary counts it.</summary>
public sealed class ContextUsage
{
    internal ContextUsage(string model)
    {
        Model = model;
    }

    /// <summary>The model the window belongs to.</summary>
    public string Model { get; }

    /// <summary>What is in the window, row by row, in the binary's order.</summary>
    public IReadOnlyList<ContextUsageCategory> Categories { get; internal init; } = [];

    /// <summary>Tokens in the window now.</summary>
    public long TotalTokens { get; internal init; }

    /// <summary>The window's size in tokens, as the session may use it.</summary>
    public long MaxTokens { get; internal init; }

    /// <summary>The window's size in tokens before any limit the session was started with.</summary>
    public long? RawMaxTokens { get; internal init; }

    /// <summary><see cref="TotalTokens"/> over <see cref="MaxTokens"/>, as a percentage.</summary>
    public double Percentage { get; internal init; }

    /// <summary>The window drawn as a grid of squares, row by row, for a terminal to paint.</summary>
    public IReadOnlyList<IReadOnlyList<ContextUsageGridSquare>> GridRows { get; internal init; } = [];

    /// <summary>The memory files loaded, and what each costs.</summary>
    public IReadOnlyList<ContextUsageMemoryFile> MemoryFiles { get; internal init; } = [];

    /// <summary>The MCP tools in the window, and what each costs.</summary>
    public IReadOnlyList<ContextUsageMcpTool> McpTools { get; internal init; } = [];

    /// <summary>The built-in tools held outside the window until they are needed; null when the binary sent none.</summary>
    public IReadOnlyList<ContextUsageDeferredTool>? DeferredBuiltInTools { get; internal init; }

    /// <summary>The binary's own tools in the window; null when the binary sent none.</summary>
    public IReadOnlyList<ContextUsageSystemTool>? SystemTools { get; internal init; }

    /// <summary>The system prompt's sections; null when the binary sent none.</summary>
    public IReadOnlyList<ContextUsageSystemPromptSection>? SystemPromptSections { get; internal init; }

    /// <summary>The subagents in the window, and what each costs.</summary>
    public IReadOnlyList<ContextUsageAgent> Agents { get; internal init; } = [];

    /// <summary>What the slash commands cost together.</summary>
    public ContextUsageSlashCommands? SlashCommands { get; internal init; }

    /// <summary>What the skills cost together.</summary>
    public ContextUsageSkills? Skills { get; internal init; }

    /// <summary>The token count at which the binary compacts the conversation on its own.</summary>
    public long? AutoCompactThreshold { get; internal init; }

    /// <summary>Whether the binary compacts the conversation on its own when the window fills.</summary>
    public bool IsAutoCompactEnabled { get; internal init; }

    /// <summary>Where <see cref="AutoCompactThreshold"/> came from.</summary>
    public string? AutoCompactSource { get; internal init; }

    /// <summary>The messages' own tokens, split by what carried them.</summary>
    public ContextUsageMessageBreakdown? MessageBreakdown { get; internal init; }

    /// <summary>What the last request to the API spent; null when the binary sent none.</summary>
    public ContextUsageApiTokens? ApiUsage { get; internal init; }
}

/// <summary>One row of the context breakdown.</summary>
public sealed class ContextUsageCategory
{
    internal ContextUsageCategory(string name)
    {
        Name = name;
    }

    /// <summary>The row's name, for a person to read.</summary>
    public string Name { get; }

    /// <summary>The row's tokens.</summary>
    public long? Tokens { get; internal init; }

    /// <summary>The colour the binary's own display paints the row in.</summary>
    public string? Color { get; internal init; }

    /// <summary>Whether the row counts what is held outside the window until it is needed.</summary>
    public bool? IsDeferred { get; internal init; }

    /// <summary>What the row counts; null on a binary old enough not to say, which is where the row must be read by its name instead.</summary>
    public ContextSpace? Kind { get; internal init; }
}

/// <summary>One square of the grid the binary's own display paints the window as.</summary>
public sealed class ContextUsageGridSquare
{
    internal ContextUsageGridSquare()
    {
    }

    /// <summary>The colour the square is painted in.</summary>
    public string? Color { get; internal init; }

    /// <summary>Whether the square is painted at all.</summary>
    public bool? IsFilled { get; internal init; }

    /// <summary>The name of the row the square belongs to.</summary>
    public string? CategoryName { get; internal init; }

    /// <summary>The row's tokens.</summary>
    public long? Tokens { get; internal init; }

    /// <summary>The row's share of the window, as a percentage.</summary>
    public double? Percentage { get; internal init; }

    /// <summary>How much of the square the row fills, from none to all.</summary>
    public double? SquareFullness { get; internal init; }
}

/// <summary>One memory file in the window.</summary>
public sealed class ContextUsageMemoryFile
{
    internal ContextUsageMemoryFile()
    {
    }

    /// <summary>Where the file is.</summary>
    public string? Path { get; internal init; }

    /// <summary>Which kind of memory the file is.</summary>
    public string? Type { get; internal init; }

    /// <summary>The file's tokens.</summary>
    public long? Tokens { get; internal init; }
}

/// <summary>One MCP tool in the window.</summary>
public sealed class ContextUsageMcpTool
{
    internal ContextUsageMcpTool()
    {
    }

    /// <summary>The tool's name, as it is called.</summary>
    public string? Name { get; internal init; }

    /// <summary>The server that serves the tool.</summary>
    public string? ServerName { get; internal init; }

    /// <summary>The tool's tokens.</summary>
    public long? Tokens { get; internal init; }

    /// <summary>Whether the tool's schema is in the window now.</summary>
    public bool? IsLoaded { get; internal init; }
}

/// <summary>One built-in tool held outside the window until it is needed.</summary>
public sealed class ContextUsageDeferredTool
{
    internal ContextUsageDeferredTool()
    {
    }

    /// <summary>The tool's name.</summary>
    public string? Name { get; internal init; }

    /// <summary>The tool's tokens.</summary>
    public long? Tokens { get; internal init; }

    /// <summary>Whether the tool's schema is in the window now.</summary>
    public bool? IsLoaded { get; internal init; }
}

/// <summary>One of the binary's own tools in the window.</summary>
public sealed class ContextUsageSystemTool
{
    internal ContextUsageSystemTool()
    {
    }

    /// <summary>The tool's name.</summary>
    public string? Name { get; internal init; }

    /// <summary>The tool's tokens.</summary>
    public long? Tokens { get; internal init; }
}

/// <summary>One section of the system prompt.</summary>
public sealed class ContextUsageSystemPromptSection
{
    internal ContextUsageSystemPromptSection()
    {
    }

    /// <summary>The section's name.</summary>
    public string? Name { get; internal init; }

    /// <summary>The section's tokens.</summary>
    public long? Tokens { get; internal init; }
}

/// <summary>One subagent in the window.</summary>
public sealed class ContextUsageAgent
{
    internal ContextUsageAgent()
    {
    }

    /// <summary>The agent's type.</summary>
    public string? AgentType { get; internal init; }

    /// <summary>Where the agent was defined.</summary>
    public string? Source { get; internal init; }

    /// <summary>The agent's tokens.</summary>
    public long? Tokens { get; internal init; }
}

/// <summary>What the slash commands cost together.</summary>
public sealed class ContextUsageSlashCommands
{
    internal ContextUsageSlashCommands()
    {
    }

    /// <summary>How many commands the binary loaded.</summary>
    public long? TotalCommands { get; internal init; }

    /// <summary>How many of them are in the window.</summary>
    public long? IncludedCommands { get; internal init; }

    /// <summary>Their tokens together.</summary>
    public long? Tokens { get; internal init; }
}

/// <summary>What the skills cost together.</summary>
public sealed class ContextUsageSkills
{
    internal ContextUsageSkills()
    {
    }

    /// <summary>How many skills the binary loaded.</summary>
    public long? TotalSkills { get; internal init; }

    /// <summary>How many of them are in the window.</summary>
    public long? IncludedSkills { get; internal init; }

    /// <summary>Their tokens together.</summary>
    public long? Tokens { get; internal init; }

    /// <summary>The heading of each skill that is in the window, and what it costs.</summary>
    public IReadOnlyList<ContextUsageSkill> SkillFrontmatter { get; internal init; } = [];
}

/// <summary>One skill's heading in the window.</summary>
public sealed class ContextUsageSkill
{
    internal ContextUsageSkill()
    {
    }

    /// <summary>The skill's name.</summary>
    public string? Name { get; internal init; }

    /// <summary>Where the skill was defined.</summary>
    public string? Source { get; internal init; }

    /// <summary>The heading's tokens.</summary>
    public long? Tokens { get; internal init; }
}

/// <summary>The messages' own tokens, split by what carried them.</summary>
public sealed class ContextUsageMessageBreakdown
{
    internal ContextUsageMessageBreakdown()
    {
    }

    /// <summary>Tokens spent on tool calls.</summary>
    public long? ToolCallTokens { get; internal init; }

    /// <summary>Tokens spent on what the tools answered.</summary>
    public long? ToolResultTokens { get; internal init; }

    /// <summary>Tokens spent on what was attached to the prompts.</summary>
    public long? AttachmentTokens { get; internal init; }

    /// <summary>Tokens spent on the assistant's messages.</summary>
    public long? AssistantMessageTokens { get; internal init; }

    /// <summary>Tokens spent on the user's messages.</summary>
    public long? UserMessageTokens { get; internal init; }

    /// <summary>Tokens spent on context moved in from elsewhere.</summary>
    public long? RedirectedContextTokens { get; internal init; }

    /// <summary>Tokens the binary could not put under any of the others.</summary>
    public long? UnattributedTokens { get; internal init; }

    /// <summary>The tool calls, by tool.</summary>
    public IReadOnlyList<ContextUsageToolCallTokens> ToolCallsByType { get; internal init; } = [];

    /// <summary>The attachments, by kind.</summary>
    public IReadOnlyList<ContextUsageAttachmentTokens> AttachmentsByType { get; internal init; } = [];
}

/// <summary>What one tool's calls and answers cost in the window.</summary>
public sealed class ContextUsageToolCallTokens
{
    internal ContextUsageToolCallTokens()
    {
    }

    /// <summary>The tool's name.</summary>
    public string? Name { get; internal init; }

    /// <summary>Tokens its calls cost.</summary>
    public long? CallTokens { get; internal init; }

    /// <summary>Tokens its answers cost.</summary>
    public long? ResultTokens { get; internal init; }
}

/// <summary>What one kind of attachment costs in the window.</summary>
public sealed class ContextUsageAttachmentTokens
{
    internal ContextUsageAttachmentTokens()
    {
    }

    /// <summary>The attachment's kind.</summary>
    public string? Name { get; internal init; }

    /// <summary>Its tokens.</summary>
    public long? Tokens { get; internal init; }
}

/// <summary>What the session's last request to the API spent, as the API counted it.</summary>
public sealed class ContextUsageApiTokens
{
    internal ContextUsageApiTokens()
    {
    }

    /// <summary>Input tokens neither read from nor written to the prompt cache.</summary>
    public long? InputTokens { get; internal init; }

    /// <summary>Output tokens.</summary>
    public long? OutputTokens { get; internal init; }

    /// <summary>Input tokens written to the prompt cache.</summary>
    public long? CacheCreationInputTokens { get; internal init; }

    /// <summary>Input tokens read from the prompt cache.</summary>
    public long? CacheReadInputTokens { get; internal init; }
}
