using System.Text.Json;

namespace Claude.Agent;

public static partial class ClaudeAgentModelFactory
{
    /// <summary>Builds a <see cref="Claude.Agent.ServerInfo"/> for a test. Each parameter is the property of its name.</summary>
    public static ServerInfo ServerInfo(
        IEnumerable<SlashCommandInfo>? commands = null,
        IEnumerable<AgentInfo>? agents = null,
        string? outputStyle = null,
        IEnumerable<string>? availableOutputStyles = null,
        string? userOutputStylesDirectory = null,
        IEnumerable<ModelInfo>? models = null,
        AccountInfo? account = null,
        bool? hooksApplied = null,
        bool? pluginsApplied = null,
        FastModeState? fastModeState = null,
        FastModeDisabledReason? fastModeDisabledReason = null,
        long? processId = null,
        PermissionMode? permissionMode = null,
        SessionState? sessionState = null,
        bool? analyticsDisabled = null,
        bool? remoteControlAvailable = null,
        bool? remoteControlAutoEnable = null,
        bool? remoteControlAutoOnByDefault = null,
        bool? remoteControlAutoConnectDefault = null,
        bool? ideRemoteControlAutoEnableGate = null,
        IEnumerable<string>? capabilities = null,
        FeedbackMode? feedbackMode = null) =>
        new()
        {
            Commands = [.. commands ?? []],
            Agents = [.. agents ?? []],
            OutputStyle = outputStyle,
            AvailableOutputStyles = [.. availableOutputStyles ?? []],
            UserOutputStylesDirectory = userOutputStylesDirectory,
            Models = [.. models ?? []],
            Account = account,
            HooksApplied = hooksApplied,
            PluginsApplied = pluginsApplied,
            FastModeState = fastModeState,
            FastModeDisabledReason = fastModeDisabledReason,
            ProcessId = processId,
            PermissionMode = permissionMode,
            SessionState = sessionState,
            AnalyticsDisabled = analyticsDisabled,
            RemoteControlAvailable = remoteControlAvailable,
            RemoteControlAutoEnable = remoteControlAutoEnable,
            RemoteControlAutoOnByDefault = remoteControlAutoOnByDefault,
            RemoteControlAutoConnectDefault = remoteControlAutoConnectDefault,
            IdeRemoteControlAutoEnableGate = ideRemoteControlAutoEnableGate,
            Capabilities = [.. capabilities ?? []],
            FeedbackMode = feedbackMode,
        };

    /// <summary>Builds a <see cref="Claude.Agent.FeedbackMode"/> for a test. Each parameter is the property of its name.</summary>
    public static FeedbackMode FeedbackMode(FeedbackModeKind? kind = null) =>
        new() { Kind = kind };

        /// <summary>Builds an <see cref="Claude.Agent.AgentInfo"/> for a test. Each parameter is the property of its name.</summary>
    public static AgentInfo AgentInfo(string? name = null, string? description = null, string? model = null) =>
        new(name ?? string.Empty) { Description = description, Model = model };

    /// <summary>Builds a <see cref="Claude.Agent.ModelInfo"/> for a test. Each parameter is the property of its name.</summary>
    public static ModelInfo ModelInfo(
        string? value = null,
        string? resolvedModel = null,
        string? displayName = null,
        string? description = null,
        bool? supportsEffort = null,
        IEnumerable<Effort>? supportedEffortLevels = null,
        bool? supportsAdaptiveThinking = null,
        bool? supportsFastMode = null,
        bool? supportsAutoMode = null) =>
        new(value ?? string.Empty)
        {
            ResolvedModel = resolvedModel,
            DisplayName = displayName,
            Description = description,
            SupportsEffort = supportsEffort,
            SupportedEffortLevels = [.. supportedEffortLevels ?? []],
            SupportsAdaptiveThinking = supportsAdaptiveThinking,
            SupportsFastMode = supportsFastMode,
            SupportsAutoMode = supportsAutoMode,
        };

    /// <summary>Builds an <see cref="Claude.Agent.AccountInfo"/> for a test. Each parameter is the property of its name.</summary>
    public static AccountInfo AccountInfo(
        string? email = null,
        string? organization = null,
        string? subscriptionType = null,
        string? tokenSource = null,
        ApiKeySource? apiKeySource = null,
        ApiProvider? apiProvider = null) =>
        new()
        {
            Email = email,
            Organization = organization,
            SubscriptionType = subscriptionType,
            TokenSource = tokenSource,
            ApiKeySource = apiKeySource,
            ApiProvider = apiProvider,
        };

    /// <summary>Builds a <see cref="Claude.Agent.ContextUsage"/> for a test. Each parameter is the property of its name.</summary>
    public static ContextUsage ContextUsage(
        string? model = null,
        IEnumerable<ContextUsageCategory>? categories = null,
        long totalTokens = 0,
        long maxTokens = 0,
        long? rawMaxTokens = null,
        double percentage = 0,
        IEnumerable<IReadOnlyList<ContextUsageGridSquare>>? gridRows = null,
        IEnumerable<ContextUsageMemoryFile>? memoryFiles = null,
        IEnumerable<ContextUsageMcpTool>? mcpTools = null,
        IEnumerable<ContextUsageDeferredTool>? deferredBuiltInTools = null,
        IEnumerable<ContextUsageSystemTool>? systemTools = null,
        IEnumerable<ContextUsageSystemPromptSection>? systemPromptSections = null,
        IEnumerable<ContextUsageAgent>? agents = null,
        ContextUsageSlashCommands? slashCommands = null,
        ContextUsageSkills? skills = null,
        long? autoCompactThreshold = null,
        bool isAutoCompactEnabled = false,
        string? autoCompactSource = null,
        ContextUsageMessageBreakdown? messageBreakdown = null,
        ContextUsageApiTokens? apiUsage = null) =>
        new(model ?? string.Empty)
        {
            Categories = [.. categories ?? []],
            TotalTokens = totalTokens,
            MaxTokens = maxTokens,
            RawMaxTokens = rawMaxTokens,
            Percentage = percentage,
            GridRows = [.. gridRows ?? []],
            MemoryFiles = [.. memoryFiles ?? []],
            McpTools = [.. mcpTools ?? []],
            DeferredBuiltInTools = deferredBuiltInTools is null ? null : [.. deferredBuiltInTools],
            SystemTools = systemTools is null ? null : [.. systemTools],
            SystemPromptSections = systemPromptSections is null ? null : [.. systemPromptSections],
            Agents = [.. agents ?? []],
            SlashCommands = slashCommands,
            Skills = skills,
            AutoCompactThreshold = autoCompactThreshold,
            IsAutoCompactEnabled = isAutoCompactEnabled,
            AutoCompactSource = autoCompactSource,
            MessageBreakdown = messageBreakdown,
            ApiUsage = apiUsage,
        };

    /// <summary>Builds a <see cref="Claude.Agent.ContextUsageCategory"/> for a test. Each parameter is the property of its name.</summary>
    public static ContextUsageCategory ContextUsageCategory(
        string? name = null,
        long? tokens = null,
        string? color = null,
        bool? isDeferred = null,
        ContextSpace? kind = null) =>
        new(name ?? string.Empty) { Tokens = tokens, Color = color, IsDeferred = isDeferred, Kind = kind };

    /// <summary>Builds a <see cref="Claude.Agent.ContextUsageGridSquare"/> for a test. Each parameter is the property of its name.</summary>
    public static ContextUsageGridSquare ContextUsageGridSquare(
        string? color = null,
        bool? isFilled = null,
        string? categoryName = null,
        long? tokens = null,
        double? percentage = null,
        double? squareFullness = null) =>
        new()
        {
            Color = color,
            IsFilled = isFilled,
            CategoryName = categoryName,
            Tokens = tokens,
            Percentage = percentage,
            SquareFullness = squareFullness,
        };

    /// <summary>Builds a <see cref="Claude.Agent.ContextUsageMemoryFile"/> for a test. Each parameter is the property of its name.</summary>
    public static ContextUsageMemoryFile ContextUsageMemoryFile(string? path = null, string? type = null, long? tokens = null) =>
        new() { Path = path, Type = type, Tokens = tokens };

    /// <summary>Builds a <see cref="Claude.Agent.ContextUsageMcpTool"/> for a test. Each parameter is the property of its name.</summary>
    public static ContextUsageMcpTool ContextUsageMcpTool(
        string? name = null,
        string? serverName = null,
        long? tokens = null,
        bool? isLoaded = null) =>
        new() { Name = name, ServerName = serverName, Tokens = tokens, IsLoaded = isLoaded };

    /// <summary>Builds a <see cref="Claude.Agent.ContextUsageDeferredTool"/> for a test. Each parameter is the property of its name.</summary>
    public static ContextUsageDeferredTool ContextUsageDeferredTool(string? name = null, long? tokens = null, bool? isLoaded = null) =>
        new() { Name = name, Tokens = tokens, IsLoaded = isLoaded };

    /// <summary>Builds a <see cref="Claude.Agent.ContextUsageSystemTool"/> for a test. Each parameter is the property of its name.</summary>
    public static ContextUsageSystemTool ContextUsageSystemTool(string? name = null, long? tokens = null) =>
        new() { Name = name, Tokens = tokens };

    /// <summary>Builds a <see cref="Claude.Agent.ContextUsageSystemPromptSection"/> for a test. Each parameter is the property of its name.</summary>
    public static ContextUsageSystemPromptSection ContextUsageSystemPromptSection(string? name = null, long? tokens = null) =>
        new() { Name = name, Tokens = tokens };

    /// <summary>Builds a <see cref="Claude.Agent.ContextUsageAgent"/> for a test. Each parameter is the property of its name.</summary>
    public static ContextUsageAgent ContextUsageAgent(string? agentType = null, string? source = null, long? tokens = null) =>
        new() { AgentType = agentType, Source = source, Tokens = tokens };

    /// <summary>Builds a <see cref="Claude.Agent.ContextUsageSlashCommands"/> for a test. Each parameter is the property of its name.</summary>
    public static ContextUsageSlashCommands ContextUsageSlashCommands(
        long? totalCommands = null,
        long? includedCommands = null,
        long? tokens = null) =>
        new() { TotalCommands = totalCommands, IncludedCommands = includedCommands, Tokens = tokens };

    /// <summary>Builds a <see cref="Claude.Agent.ContextUsageSkills"/> for a test. Each parameter is the property of its name.</summary>
    public static ContextUsageSkills ContextUsageSkills(
        long? totalSkills = null,
        long? includedSkills = null,
        long? tokens = null,
        IEnumerable<ContextUsageSkill>? skillFrontmatter = null) =>
        new()
        {
            TotalSkills = totalSkills,
            IncludedSkills = includedSkills,
            Tokens = tokens,
            SkillFrontmatter = [.. skillFrontmatter ?? []],
        };

    /// <summary>Builds a <see cref="Claude.Agent.ContextUsageSkill"/> for a test. Each parameter is the property of its name.</summary>
    public static ContextUsageSkill ContextUsageSkill(string? name = null, string? source = null, long? tokens = null) =>
        new() { Name = name, Source = source, Tokens = tokens };

    /// <summary>Builds a <see cref="Claude.Agent.ContextUsageMessageBreakdown"/> for a test. Each parameter is the property of its name.</summary>
    public static ContextUsageMessageBreakdown ContextUsageMessageBreakdown(
        long? toolCallTokens = null,
        long? toolResultTokens = null,
        long? attachmentTokens = null,
        long? assistantMessageTokens = null,
        long? userMessageTokens = null,
        long? redirectedContextTokens = null,
        long? unattributedTokens = null,
        IEnumerable<ContextUsageToolCallTokens>? toolCallsByType = null,
        IEnumerable<ContextUsageAttachmentTokens>? attachmentsByType = null) =>
        new()
        {
            ToolCallTokens = toolCallTokens,
            ToolResultTokens = toolResultTokens,
            AttachmentTokens = attachmentTokens,
            AssistantMessageTokens = assistantMessageTokens,
            UserMessageTokens = userMessageTokens,
            RedirectedContextTokens = redirectedContextTokens,
            UnattributedTokens = unattributedTokens,
            ToolCallsByType = [.. toolCallsByType ?? []],
            AttachmentsByType = [.. attachmentsByType ?? []],
        };

    /// <summary>Builds a <see cref="Claude.Agent.ContextUsageToolCallTokens"/> for a test. Each parameter is the property of its name.</summary>
    public static ContextUsageToolCallTokens ContextUsageToolCallTokens(
        string? name = null,
        long? callTokens = null,
        long? resultTokens = null) =>
        new() { Name = name, CallTokens = callTokens, ResultTokens = resultTokens };

    /// <summary>Builds a <see cref="Claude.Agent.ContextUsageAttachmentTokens"/> for a test. Each parameter is the property of its name.</summary>
    public static ContextUsageAttachmentTokens ContextUsageAttachmentTokens(string? name = null, long? tokens = null) =>
        new() { Name = name, Tokens = tokens };

    /// <summary>Builds a <see cref="Claude.Agent.ContextUsageApiTokens"/> for a test. Each parameter is the property of its name.</summary>
    public static ContextUsageApiTokens ContextUsageApiTokens(
        long? inputTokens = null,
        long? outputTokens = null,
        long? cacheCreationInputTokens = null,
        long? cacheReadInputTokens = null) =>
        new()
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CacheCreationInputTokens = cacheCreationInputTokens,
            CacheReadInputTokens = cacheReadInputTokens,
        };

    /// <summary>Builds a <see cref="Claude.Agent.McpServerStatus"/> for a test. Each parameter is the property of its name.</summary>
    public static McpServerStatus McpServerStatus(
        string? name = null,
        McpServerConnectionStatus status = default,
        McpServerInfo? serverInfo = null,
        string? error = null,
        McpServerConfiguration? config = null,
        McpServerSource? scope = null,
        McpServerSource? source = null,
        IEnumerable<McpServerTool>? tools = null) =>
        new(name ?? string.Empty, status)
        {
            ServerInfo = serverInfo,
            Error = error,
            Config = config,
            Scope = scope,
            Source = source,
            Tools = [.. tools ?? []],
        };

    /// <summary>Builds an <see cref="Claude.Agent.McpServerInfo"/> for a test. Each parameter is the property of its name.</summary>
    public static McpServerInfo McpServerInfo(string? name = null, string? version = null) =>
        new() { Name = name, Version = version };

    /// <summary>Builds an <see cref="Claude.Agent.McpServerTool"/> for a test. Each parameter is the property of its name.</summary>
    public static McpServerTool McpServerTool(
        string? name = null,
        string? description = null,
        McpToolAnnotations? annotations = null) =>
        new(name ?? string.Empty) { Description = description, Annotations = annotations };

    /// <summary>Builds an <see cref="Claude.Agent.McpToolAnnotations"/> for a test. Each parameter is the property of its name.</summary>
    public static McpToolAnnotations McpToolAnnotations(
        bool? isReadOnly = null,
        bool? isDestructive = null,
        bool? isOpenWorld = null) =>
        new() { IsReadOnly = isReadOnly, IsDestructive = isDestructive, IsOpenWorld = isOpenWorld };

    /// <summary>Builds an <see cref="Claude.Agent.McpClaudeAIProxyServerConfiguration"/> for a test. Each parameter is the property of its name.</summary>
    public static McpClaudeAIProxyServerConfiguration McpClaudeAIProxyServerConfiguration(
        Uri? url = null,
        string? id = null,
        TimeSpan? timeout = null) =>
        new() { Url = url, Id = id, Timeout = timeout };

    /// <summary>Builds an <see cref="Claude.Agent.UnknownMcpServerConfiguration"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownMcpServerConfiguration UnknownMcpServerConfiguration(
        string? type = null,
        JsonElement raw = default,
        TimeSpan? timeout = null) =>
        new(type ?? string.Empty, Object(raw)) { Timeout = timeout };
}
