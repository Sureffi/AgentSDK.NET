namespace Claude.Agent;

/// <summary>
/// How much of the model's context window the turn is using, as an assistant frame reports it,
/// broken down by what fills it. It is not the shape <see cref="ContextUsage"/> reads, which is the
/// binary's answer to a caller asking.
/// </summary>
public sealed class MessageContextUsage
{
    internal MessageContextUsage(string model, long totalTokens, long rawMaxTokens, double percentage)
    {
        Model = model;
        TotalTokens = totalTokens;
        RawMaxTokens = rawMaxTokens;
        Percentage = percentage;
    }

    /// <summary>The model the window belongs to.</summary>
    public string Model { get; }

    /// <summary>The tokens the conversation takes.</summary>
    public long TotalTokens { get; }

    /// <summary>The window's size before anything is held back.</summary>
    public long RawMaxTokens { get; }

    /// <summary>How full the window is, as a percentage: the tokens taken over <see cref="RawMaxTokens"/>, rounded, and past 100 when the conversation is over the window.</summary>
    public double Percentage { get; }

    /// <summary>Set when the conversation is over a limit: which one, and by how much.</summary>
    public ContextUsageLimit? OverLimit { get; internal init; }

    /// <summary>What fills the window, category by category.</summary>
    public IReadOnlyList<MessageContextUsageCategory> Categories { get; internal init; } = [];

    /// <summary>What each MCP tool's declaration costs.</summary>
    public IReadOnlyList<MessageContextUsageMcpTool> McpTools { get; internal init; } = [];

    /// <summary>What each memory file loaded costs.</summary>
    public IReadOnlyList<MessageContextUsageMemoryFile> MemoryFiles { get; internal init; } = [];

    /// <summary>What each subagent's declaration costs.</summary>
    public IReadOnlyList<MessageContextUsageAgent> Agents { get; internal init; } = [];

    /// <summary>What each skill loaded costs.</summary>
    public IReadOnlyList<MessageContextUsageSkill> Skills { get; internal init; } = [];
}

/// <summary>A limit the conversation has gone over, and by how much.</summary>
public sealed class ContextUsageLimit
{
    internal ContextUsageLimit(long tokensOver, ContextUsageLimitKind kind)
    {
        TokensOver = tokensOver;
        Kind = kind;
    }

    /// <summary>The tokens over the limit.</summary>
    public long TokensOver { get; }

    /// <summary>Which limit.</summary>
    public ContextUsageLimitKind Kind { get; }
}

/// <summary>One share of the context window, named by the binary.</summary>
public sealed class MessageContextUsageCategory
{
    internal MessageContextUsageCategory(string name, long tokens, ContextSpace kind)
    {
        Name = name;
        Tokens = tokens;
        Kind = kind;
    }

    /// <summary>What the share is, in the binary's own words.</summary>
    public string Name { get; }

    /// <summary>The tokens it takes.</summary>
    public long Tokens { get; }

    /// <summary>What the share is doing.</summary>
    public ContextSpace Kind { get; }
}

/// <summary>What one MCP tool's declaration costs in the window.</summary>
public sealed class MessageContextUsageMcpTool
{
    internal MessageContextUsageMcpTool(string name, string serverName, long tokens)
    {
        Name = name;
        ServerName = serverName;
        Tokens = tokens;
    }

    /// <summary>The tool, by the name the model calls it with.</summary>
    public string Name { get; }

    /// <summary>The server that serves it.</summary>
    public string ServerName { get; }

    /// <summary>The tokens its declaration takes.</summary>
    public long Tokens { get; }
}

/// <summary>What one memory file costs in the window.</summary>
public sealed class MessageContextUsageMemoryFile
{
    internal MessageContextUsageMemoryFile(string path, string type, long tokens)
    {
        Path = path;
        Type = type;
        Tokens = tokens;
    }

    /// <summary>Where the file is.</summary>
    public string Path { get; }

    /// <summary>What kind of memory it is, in the binary's own words.</summary>
    public string Type { get; }

    /// <summary>The tokens it takes.</summary>
    public long Tokens { get; }
}

/// <summary>What one subagent's declaration costs in the window.</summary>
public sealed class MessageContextUsageAgent
{
    internal MessageContextUsageAgent(string agentType, string source, long tokens)
    {
        AgentType = agentType;
        Source = source;
        Tokens = tokens;
    }

    /// <summary>The subagent, by name.</summary>
    public string AgentType { get; }

    /// <summary>Where it was declared, in the binary's own words.</summary>
    public string Source { get; }

    /// <summary>The tokens its declaration takes.</summary>
    public long Tokens { get; }
}

/// <summary>What one skill costs in the window.</summary>
public sealed class MessageContextUsageSkill
{
    internal MessageContextUsageSkill(string name, string source, long tokens)
    {
        Name = name;
        Source = source;
        Tokens = tokens;
    }

    /// <summary>The skill, by name.</summary>
    public string Name { get; }

    /// <summary>Where it was loaded from, in the binary's own words.</summary>
    public string Source { get; }

    /// <summary>The plugin that ships it, when one does.</summary>
    public string? PluginName { get; internal init; }

    /// <summary>The tokens it takes.</summary>
    public long Tokens { get; }
}
