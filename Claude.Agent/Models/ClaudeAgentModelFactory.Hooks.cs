using System.Text.Json;

namespace Claude.Agent;

public static partial class ClaudeAgentModelFactory
{
    /// <summary>Builds a <see cref="Claude.Agent.PreToolUseHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static PreToolUseHookInput PreToolUseHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        ToolCall? toolCall = null,
        McpServerOrigin? mcpServer = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            toolCall ?? ToolCall(),
            mcpServer);

    /// <summary>Builds a <see cref="Claude.Agent.PostToolUseHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static PostToolUseHookInput PostToolUseHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        ToolCall? toolCall = null,
        TimeSpan? duration = null,
        McpServerOrigin? mcpServer = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            toolCall ?? ToolCall(),
            duration,
            mcpServer);

    /// <summary>Builds a <see cref="Claude.Agent.PostToolUseFailureHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static PostToolUseFailureHookInput PostToolUseFailureHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        ToolCall? toolCall = null,
        bool? isInterrupt = null,
        TimeSpan? duration = null,
        McpServerOrigin? mcpServer = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            toolCall ?? ToolCall(),
            isInterrupt,
            duration,
            mcpServer);

    /// <summary>Builds a <see cref="Claude.Agent.PostToolBatchHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static PostToolBatchHookInput PostToolBatchHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        IEnumerable<ToolCall>? toolCalls = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            [.. toolCalls ?? []]);

    /// <summary>Builds a <see cref="Claude.Agent.NotificationHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static NotificationHookInput NotificationHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        string? message = null,
        string? title = null,
        NotificationType notificationType = default) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            message ?? string.Empty,
            title,
            notificationType);

    /// <summary>Builds a <see cref="Claude.Agent.UserPromptSubmitHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static UserPromptSubmitHookInput UserPromptSubmitHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        string? prompt = null,
        UserPromptSubmitSource? source = null,
        string? sessionTitle = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            prompt ?? string.Empty,
            source,
            sessionTitle);

    /// <summary>Builds a <see cref="Claude.Agent.UserPromptExpansionHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static UserPromptExpansionHookInput UserPromptExpansionHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        UserPromptExpansionType expansionType = default,
        string? commandName = null,
        string? commandArguments = null,
        string? commandSource = null,
        string? prompt = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            expansionType,
            commandName ?? string.Empty,
            commandArguments ?? string.Empty,
            commandSource,
            prompt ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.SessionStartHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static SessionStartHookInput SessionStartHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        SessionStartSource source = default,
        string? model = null,
        string? sessionTitle = null,
        TimeSpan? timeSinceLastResponse = null,
        long? contextTokens = null,
        bool? isPromptCacheLikelyExpired = null,
        decimal? estimatedCacheWriteCost = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            source,
            model,
            sessionTitle,
            timeSinceLastResponse,
            contextTokens,
            isPromptCacheLikelyExpired,
            estimatedCacheWriteCost);

    /// <summary>Builds a <see cref="Claude.Agent.SessionEndHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static SessionEndHookInput SessionEndHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        SessionEndReason reason = default) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            reason);

    /// <summary>Builds a <see cref="Claude.Agent.StopHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static StopHookInput StopHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        bool isStopHookActive = false,
        string? lastAssistantMessage = null,
        IEnumerable<StopHookBackgroundTask>? backgroundTasks = null,
        IEnumerable<SessionCron>? sessionCrons = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            isStopHookActive,
            lastAssistantMessage,
            [.. backgroundTasks ?? []],
            [.. sessionCrons ?? []]);

    /// <summary>Builds a <see cref="Claude.Agent.StopFailureHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static StopFailureHookInput StopFailureHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        AssistantErrorCode errorCode = default,
        string? errorDetails = null,
        string? lastAssistantMessage = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            errorCode,
            errorDetails,
            lastAssistantMessage);

    /// <summary>Builds a <see cref="Claude.Agent.SubagentStartHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static SubagentStartHookInput SubagentStartHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory));

    /// <summary>Builds a <see cref="Claude.Agent.SubagentStopHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static SubagentStopHookInput SubagentStopHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        bool isStopHookActive = false,
        string? agentTranscriptPath = null,
        string? lastAssistantMessage = null,
        IEnumerable<StopHookBackgroundTask>? backgroundTasks = null,
        IEnumerable<SessionCron>? sessionCrons = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            isStopHookActive,
            agentTranscriptPath ?? string.Empty,
            lastAssistantMessage,
            [.. backgroundTasks ?? []],
            [.. sessionCrons ?? []]);

    /// <summary>Builds a <see cref="Claude.Agent.PreCompactHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static PreCompactHookInput PreCompactHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        CompactTrigger trigger = default,
        string? customInstructions = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            trigger,
            customInstructions);

    /// <summary>Builds a <see cref="Claude.Agent.PostCompactHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static PostCompactHookInput PostCompactHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        CompactTrigger trigger = default,
        string? compactSummary = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            trigger,
            compactSummary ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.PreModelSwitchHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static PreModelSwitchHookInput PreModelSwitchHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        string? fromModel = null,
        string? toModel = null,
        string? requestedModel = null,
        PreModelSwitchSource source = default,
        long contextTokens = 0,
        bool isPromptCacheWarm = false,
        ModelSwitchCacheTimeToLive cacheTimeToLive = default,
        decimal estimatedCacheWriteCost = 0,
        ModelSwitchPricing pricing = default) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            fromModel ?? string.Empty,
            toModel ?? string.Empty,
            requestedModel,
            source,
            contextTokens,
            isPromptCacheWarm,
            cacheTimeToLive,
            estimatedCacheWriteCost,
            pricing);

    /// <summary>Builds a <see cref="Claude.Agent.PostModelSwitchHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static PostModelSwitchHookInput PostModelSwitchHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        string? fromModel = null,
        string? toModel = null,
        string? requestedModel = null,
        PostModelSwitchSource source = default,
        long contextTokens = 0,
        bool isPromptCacheWarm = false,
        ModelSwitchCacheTimeToLive cacheTimeToLive = default,
        decimal estimatedCacheWriteCost = 0,
        ModelSwitchPricing pricing = default) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            fromModel ?? string.Empty,
            toModel ?? string.Empty,
            requestedModel,
            source,
            contextTokens,
            isPromptCacheWarm,
            cacheTimeToLive,
            estimatedCacheWriteCost,
            pricing);

    /// <summary>Builds a <see cref="Claude.Agent.PermissionRequestHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static PermissionRequestHookInput PermissionRequestHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        ToolCall? toolCall = null,
        IEnumerable<PermissionUpdate>? permissionSuggestions = null,
        McpServerOrigin? mcpServer = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            toolCall ?? ToolCall(),
            [.. permissionSuggestions ?? []],
            mcpServer);

    /// <summary>Builds a <see cref="Claude.Agent.PermissionDeniedHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static PermissionDeniedHookInput PermissionDeniedHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        ToolCall? toolCall = null,
        McpServerOrigin? mcpServer = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            toolCall ?? ToolCall(),
            mcpServer);

    /// <summary>Builds a <see cref="Claude.Agent.SetupHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static SetupHookInput SetupHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        SetupTrigger trigger = default) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            trigger);

    /// <summary>Builds a <see cref="Claude.Agent.TeammateIdleHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static TeammateIdleHookInput TeammateIdleHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        string? teammateName = null,
        string? teamName = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            teammateName ?? string.Empty,
            teamName ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.TaskCreatedHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskCreatedHookInput TaskCreatedHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        string? taskId = null,
        string? taskSubject = null,
        string? taskDescription = null,
        string? teammateName = null,
        string? teamName = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            taskId ?? string.Empty,
            taskSubject ?? string.Empty,
            taskDescription,
            teammateName,
            teamName);

    /// <summary>Builds a <see cref="Claude.Agent.TaskCompletedHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskCompletedHookInput TaskCompletedHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        string? taskId = null,
        string? taskSubject = null,
        string? taskDescription = null,
        string? teammateName = null,
        string? teamName = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            taskId ?? string.Empty,
            taskSubject ?? string.Empty,
            taskDescription,
            teammateName,
            teamName);

    /// <summary>Builds a <see cref="Claude.Agent.ElicitationHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static ElicitationHookInput ElicitationHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        string? mcpServerName = null,
        string? message = null,
        ElicitationMode? mode = null,
        string? url = null,
        string? elicitationId = null,
        JsonElement? requestedSchema = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            mcpServerName ?? string.Empty,
            message ?? string.Empty,
            mode,
            url,
            elicitationId,
            requestedSchema);

    /// <summary>Builds a <see cref="Claude.Agent.ElicitationResultHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static ElicitationResultHookInput ElicitationResultHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        string? mcpServerName = null,
        string? elicitationId = null,
        ElicitationMode? mode = null,
        ElicitationAction action = default,
        JsonElement? content = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            mcpServerName ?? string.Empty,
            elicitationId,
            mode,
            action,
            content);

    /// <summary>Builds a <see cref="Claude.Agent.ConfigChangeHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static ConfigChangeHookInput ConfigChangeHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        ConfigChangeSource source = default,
        string? filePath = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            source,
            filePath);

    /// <summary>Builds a <see cref="Claude.Agent.WorktreeCreateHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static WorktreeCreateHookInput WorktreeCreateHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        string? name = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            name ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.WorktreeRemoveHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static WorktreeRemoveHookInput WorktreeRemoveHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        string? worktreePath = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            worktreePath ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.InstructionsLoadedHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static InstructionsLoadedHookInput InstructionsLoadedHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        string? filePath = null,
        InstructionsMemoryType memoryType = default,
        InstructionsLoadReason loadReason = default,
        IEnumerable<string>? globs = null,
        string? triggerFilePath = null,
        string? parentFilePath = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            filePath ?? string.Empty,
            memoryType,
            loadReason,
            [.. globs ?? []],
            triggerFilePath,
            parentFilePath);

    /// <summary>Builds a <see cref="Claude.Agent.WorkingDirectoryChangedHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static WorkingDirectoryChangedHookInput WorkingDirectoryChangedHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        string? oldWorkingDirectory = null,
        string? newWorkingDirectory = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            oldWorkingDirectory ?? string.Empty,
            newWorkingDirectory ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.FileChangedHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static FileChangedHookInput FileChangedHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        string? filePath = null,
        FileChangedEvent change = default) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            filePath ?? string.Empty,
            change);

    /// <summary>Builds a <see cref="Claude.Agent.DirectoryAddedHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static DirectoryAddedHookInput DirectoryAddedHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        string? directory = null,
        DirectoryAddedSource source = default) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            directory ?? string.Empty,
            source);

    /// <summary>Builds a <see cref="Claude.Agent.MessageDisplayHookInput"/> for a test. Each parameter is the property of its name.</summary>
    public static MessageDisplayHookInput MessageDisplayHookInput(
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        string? turnId = null,
        string? messageId = null,
        long index = 0,
        bool isFinal = false,
        string? delta = null) =>
        new(Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory),
            turnId ?? string.Empty,
            messageId ?? string.Empty,
            index,
            isFinal,
            delta ?? string.Empty);

    /// <summary>Builds an <see cref="Claude.Agent.UnknownHookInput"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownHookInput UnknownHookInput(
        HookEvent hookEvent = default,
        string? sessionId = null,
        string? transcriptPath = null,
        string? workingDirectory = null,
        string? promptId = null,
        PermissionMode? permissionMode = null,
        string? agentId = null,
        AgentType? agentType = null,
        Effort? effort = null,
        string? scratchpadDirectory = null,
        JsonElement raw = default) =>
        new(hookEvent, Common(sessionId, transcriptPath, workingDirectory, promptId, permissionMode, agentId, agentType, effort, scratchpadDirectory, raw));

    /// <summary>Builds a <see cref="Claude.Agent.ShellStopHookBackgroundTask"/> for a test. Each parameter is the property of its name.</summary>
    public static ShellStopHookBackgroundTask ShellStopHookBackgroundTask(
        string? id = null,
        BackgroundTaskStatus status = default,
        string? description = null,
        string? command = null) =>
        new(id ?? string.Empty, status, description ?? string.Empty) { Command = command };

    /// <summary>Builds a <see cref="Claude.Agent.SubagentStopHookBackgroundTask"/> for a test. Each parameter is the property of its name.</summary>
    public static SubagentStopHookBackgroundTask SubagentStopHookBackgroundTask(
        string? id = null,
        BackgroundTaskStatus status = default,
        string? description = null,
        AgentType? agentType = null) =>
        new(id ?? string.Empty, status, description ?? string.Empty) { AgentType = agentType };

    /// <summary>Builds an <see cref="Claude.Agent.UnknownStopHookBackgroundTask"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownStopHookBackgroundTask UnknownStopHookBackgroundTask(
        StopHookBackgroundTaskType taskType = default,
        string? id = null,
        BackgroundTaskStatus status = default,
        string? description = null,
        JsonElement raw = default) =>
        new(taskType, id ?? string.Empty, status, description ?? string.Empty, Object(raw));

    /// <summary>Builds a <see cref="Claude.Agent.SessionCron"/> for a test. Each parameter is the property of its name.</summary>
    public static SessionCron SessionCron(
        string? id = null,
        string? schedule = null,
        bool isRecurring = false,
        string? prompt = null) =>
        new(id ?? string.Empty, schedule ?? string.Empty, isRecurring, prompt ?? string.Empty);

    /// <summary>Builds an <see cref="Claude.Agent.McpServerOrigin"/> for a test. Each parameter is the property of its name.</summary>
    public static McpServerOrigin McpServerOrigin(string? name = null, McpServerSource source = default) =>
        new(name ?? string.Empty, source);

    private static HookInputCommon Common(
        string? sessionId,
        string? transcriptPath,
        string? workingDirectory,
        string? promptId,
        PermissionMode? permissionMode,
        string? agentId,
        AgentType? agentType,
        Effort? effort,
        string? scratchpadDirectory,
        JsonElement raw = default) =>
        new()
        {
            SessionId = sessionId ?? string.Empty,
            TranscriptPath = transcriptPath ?? string.Empty,
            WorkingDirectory = workingDirectory ?? string.Empty,
            PromptId = promptId,
            PermissionMode = permissionMode,
            AgentId = agentId,
            AgentType = agentType,
            Effort = effort,
            ScratchpadDirectory = scratchpadDirectory,
            Raw = Object(raw),
        };
}
