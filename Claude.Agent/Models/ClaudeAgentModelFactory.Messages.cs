using System.Text.Json;

namespace Claude.Agent;

public static partial class ClaudeAgentModelFactory
{
    /// <summary>Builds an <see cref="Claude.Agent.AssistantMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static AssistantMessage AssistantMessage(
        ApiMessage? message = null,
        string? parentToolUseId = null,
        AssistantErrorCode? error = null,
        string? requestId = null,
        string? userMessageUuid = null,
        IEnumerable<string>? userMessageUuids = null,
        string? resumeReason = null,
        bool resumedFromIncompleteThinking = false,
        IEnumerable<string>? supersedes = null,
        bool aborted = false,
        string? subagentType = null,
        string? taskDescription = null,
        DateTimeOffset? timestamp = null,
        MessageContextUsage? contextUsage = null,
        UsageReport? usageReport = null,
        IReadOnlyDictionary<string, JsonElement>? rawWireToolInputs = null,
        IEnumerable<ToolUseDisplay>? toolUseDisplays = null,
        bool isMeta = false,
        string? localCommandSource = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(message ?? ApiMessage(), uuid, sessionId, raw)
        {
            ParentToolUseId = parentToolUseId,
            Error = error,
            RequestId = requestId,
            UserMessageUuid = userMessageUuid,
            UserMessageUuids = [.. userMessageUuids ?? []],
            ResumeReason = resumeReason,
            ResumedFromIncompleteThinking = resumedFromIncompleteThinking,
            Supersedes = [.. supersedes ?? []],
            Aborted = aborted,
            SubagentType = subagentType,
            TaskDescription = taskDescription,
            Timestamp = timestamp,
            ContextUsage = contextUsage,
            UsageReport = usageReport,
            RawWireToolInputs = rawWireToolInputs ?? new Dictionary<string, JsonElement>(),
            ToolUseDisplays = [.. toolUseDisplays ?? []],
            IsMeta = isMeta,
            LocalCommandSource = localCommandSource,
        };

    /// <summary>Builds an <see cref="Claude.Agent.ApiMessage"/> for a test. Each parameter is the property of its name.</summary>
    public static ApiMessage ApiMessage(
        IEnumerable<ContentBlock>? content = null,
        string? model = null,
        string? id = null,
        StopReason? stopReason = null,
        string? stopSequence = null,
        StopDetails? stopDetails = null,
        Usage? usage = null,
        MessageContainer? container = null,
        ContextManagement? contextManagement = null,
        MessageDiagnostics? diagnostics = null,
        IEnumerable<InputTransformation>? inputTransformations = null) =>
        new([.. content ?? []], model ?? string.Empty)
        {
            Id = id,
            StopReason = stopReason,
            StopSequence = stopSequence,
            StopDetails = stopDetails,
            Usage = usage,
            Container = container,
            ContextManagement = contextManagement,
            Diagnostics = diagnostics,
            InputTransformations = [.. inputTransformations ?? []],
        };

    /// <summary>Builds a <see cref="Claude.Agent.StopDetails"/> for a test. Each parameter is the property of its name.</summary>
    public static StopDetails StopDetails(
        StopDetailsType type = default,
        RefusalCategory? category = null,
        string? explanation = null,
        string? fallbackCreditToken = null,
        bool? fallbackHasPrefillClaim = null,
        string? recommendedModel = null) =>
        new(type)
        {
            Category = category,
            Explanation = explanation,
            FallbackCreditToken = fallbackCreditToken,
            FallbackHasPrefillClaim = fallbackHasPrefillClaim,
            RecommendedModel = recommendedModel,
        };

    /// <summary>Builds a <see cref="Claude.Agent.MessageContainer"/> for a test. Each parameter is the property of its name.</summary>
    public static MessageContainer MessageContainer(
        string? id = null,
        string? expiresAt = null,
        IEnumerable<ContainerSkill>? skills = null) =>
        new(id ?? string.Empty)
        {
            ExpiresAt = expiresAt,
            Skills = [.. skills ?? []],
        };

    /// <summary>Builds a <see cref="Claude.Agent.ContainerSkill"/> for a test. Each parameter is the property of its name.</summary>
    public static ContainerSkill ContainerSkill(string? skillId = null, ContainerSkillType type = default, string? version = null) =>
        new(skillId ?? string.Empty, type, version ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.ContextManagement"/> for a test. Each parameter is the property of its name.</summary>
    public static ContextManagement ContextManagement(IEnumerable<ContextEdit>? appliedEdits = null) =>
        new([.. appliedEdits ?? []]);

    /// <summary>Builds a <see cref="Claude.Agent.ContextEdit"/> for a test. Each parameter is the property of its name.</summary>
    public static ContextEdit ContextEdit(
        ContextEditType type = default,
        long clearedInputTokens = 0,
        long? clearedToolUses = null,
        long? clearedThinkingTurns = null) =>
        new(type, clearedInputTokens)
        {
            ClearedToolUses = clearedToolUses,
            ClearedThinkingTurns = clearedThinkingTurns,
        };

    /// <summary>Builds a <see cref="Claude.Agent.MessageDiagnostics"/> for a test. Each parameter is the property of its name.</summary>
    public static MessageDiagnostics MessageDiagnostics(CacheMissReason? cacheMissReason = null) => new(cacheMissReason);

    /// <summary>Builds a <see cref="Claude.Agent.CacheMissReason"/> for a test. Each parameter is the property of its name.</summary>
    public static CacheMissReason CacheMissReason(CacheMissReasonType type = default, long? cacheMissedInputTokens = null) =>
        new(type) { CacheMissedInputTokens = cacheMissedInputTokens };

    /// <summary>Builds an <see cref="Claude.Agent.InputTransformation"/> for a test. Each parameter is the property of its name.</summary>
    public static InputTransformation InputTransformation(
        InputTransformationType type = default,
        string? path = null,
        InputTransformationReason reason = default) =>
        new(type, path ?? string.Empty, reason);

    /// <summary>Builds a <see cref="Claude.Agent.ToolUseDisplay"/> for a test. Each parameter is the property of its name.</summary>
    public static ToolUseDisplay ToolUseDisplay(string? id = null, string? displayName = null, string? serverDisplayName = null) =>
        new(id ?? string.Empty, displayName ?? string.Empty, serverDisplayName ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.UserMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static UserMessage UserMessage(
        IEnumerable<ContentBlock>? content = null,
        MessageRole? role = null,
        bool isReplay = false,
        string? parentToolUseId = null,
        ToolOutput? toolOutput = null,
        JsonElement? rawToolOutput = null,
        bool? isSynthetic = null,
        UserPriority? priority = null,
        MessageOrigin? origin = null,
        bool? shouldQuery = null,
        DateTimeOffset? timestamp = null,
        string? subagentType = null,
        string? taskDescription = null,
        IEnumerable<ToolResultNote>? toolResultNotes = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new([.. content ?? []], uuid, sessionId, raw)
        {
            Role = role,
            IsReplay = isReplay,
            ParentToolUseId = parentToolUseId,
            ToolOutput = toolOutput,
            RawToolOutput = rawToolOutput,
            IsSynthetic = isSynthetic,
            Priority = priority,
            Origin = origin,
            ShouldQuery = shouldQuery,
            Timestamp = timestamp,
            SubagentType = subagentType,
            TaskDescription = taskDescription,
            ToolResultNotes = [.. toolResultNotes ?? []],
        };

    /// <summary>Builds a <see cref="Claude.Agent.ToolResultNote"/> for a test. Each parameter is the property of its name.</summary>
    public static ToolResultNote ToolResultNote(string? id = null, string? nonExecutionKind = null) =>
        new(id ?? string.Empty, nonExecutionKind ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.HumanOrigin"/> for a test.</summary>
    public static HumanOrigin HumanOrigin() => new();

    /// <summary>Builds a <see cref="Claude.Agent.ChannelOrigin"/> for a test. Each parameter is the property of its name.</summary>
    public static ChannelOrigin ChannelOrigin(string? server = null) => new(server ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.PeerOrigin"/> for a test. Each parameter is the property of its name.</summary>
    public static PeerOrigin PeerOrigin(
        string? from = null,
        PeerOriginMode? fromMode = null,
        string? name = null,
        string? fromSession = null,
        string? senderTaskId = null,
        string? body = null,
        long? verifiedPeerProcessId = null) =>
        new(from ?? string.Empty)
        {
            FromMode = fromMode,
            Name = name,
            FromSession = fromSession,
            SenderTaskId = senderTaskId,
            Body = body,
            VerifiedPeerProcessId = verifiedPeerProcessId,
        };

    /// <summary>Builds a <see cref="Claude.Agent.TaskNotificationOrigin"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskNotificationOrigin TaskNotificationOrigin(TaskNotificationSource? subkind = null) =>
        new() { Subkind = subkind };

    /// <summary>Builds a <see cref="Claude.Agent.CoordinatorOrigin"/> for a test.</summary>
    public static CoordinatorOrigin CoordinatorOrigin() => new();

    /// <summary>Builds an <see cref="Claude.Agent.UnclassifiedOrigin"/> for a test.</summary>
    public static UnclassifiedOrigin UnclassifiedOrigin() => new();

    /// <summary>Builds an <see cref="Claude.Agent.ObserverOrigin"/> for a test. Each parameter is the property of its name.</summary>
    public static ObserverOrigin ObserverOrigin(string? from = null, string? senderTaskId = null) =>
        new(from ?? string.Empty, senderTaskId ?? string.Empty);

    /// <summary>Builds an <see cref="Claude.Agent.AutoContinuationOrigin"/> for a test.</summary>
    public static AutoContinuationOrigin AutoContinuationOrigin() => new();

    /// <summary>Builds an <see cref="Claude.Agent.ObserverActivityOrigin"/> for a test.</summary>
    public static ObserverActivityOrigin ObserverActivityOrigin() => new();

    /// <summary>Builds an <see cref="Claude.Agent.UnknownMessageOrigin"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownMessageOrigin UnknownMessageOrigin(string? type = null, JsonElement raw = default) =>
        new(type ?? string.Empty, Object(raw));

    /// <summary>Builds a <see cref="Claude.Agent.MessageContextUsage"/> for a test. Each parameter is the property of its name.</summary>
    public static MessageContextUsage MessageContextUsage(
        string? model = null,
        long totalTokens = 0,
        long rawMaxTokens = 0,
        double percentage = 0,
        ContextUsageLimit? overLimit = null,
        IEnumerable<MessageContextUsageCategory>? categories = null,
        IEnumerable<MessageContextUsageMcpTool>? mcpTools = null,
        IEnumerable<MessageContextUsageMemoryFile>? memoryFiles = null,
        IEnumerable<MessageContextUsageAgent>? agents = null,
        IEnumerable<MessageContextUsageSkill>? skills = null) =>
        new(model ?? string.Empty, totalTokens, rawMaxTokens, percentage)
        {
            OverLimit = overLimit,
            Categories = [.. categories ?? []],
            McpTools = [.. mcpTools ?? []],
            MemoryFiles = [.. memoryFiles ?? []],
            Agents = [.. agents ?? []],
            Skills = [.. skills ?? []],
        };

    /// <summary>Builds a <see cref="Claude.Agent.ContextUsageLimit"/> for a test. Each parameter is the property of its name.</summary>
    public static ContextUsageLimit ContextUsageLimit(long tokensOver = 0, ContextUsageLimitKind kind = default) =>
        new(tokensOver, kind);

    /// <summary>Builds a <see cref="Claude.Agent.MessageContextUsageCategory"/> for a test. Each parameter is the property of its name.</summary>
    public static MessageContextUsageCategory MessageContextUsageCategory(string? name = null, long tokens = 0, ContextSpace kind = default) =>
        new(name ?? string.Empty, tokens, kind);

    /// <summary>Builds a <see cref="Claude.Agent.MessageContextUsageMcpTool"/> for a test. Each parameter is the property of its name.</summary>
    public static MessageContextUsageMcpTool MessageContextUsageMcpTool(string? name = null, string? serverName = null, long tokens = 0) =>
        new(name ?? string.Empty, serverName ?? string.Empty, tokens);

    /// <summary>Builds a <see cref="Claude.Agent.MessageContextUsageMemoryFile"/> for a test. Each parameter is the property of its name.</summary>
    public static MessageContextUsageMemoryFile MessageContextUsageMemoryFile(string? path = null, string? type = null, long tokens = 0) =>
        new(path ?? string.Empty, type ?? string.Empty, tokens);

    /// <summary>Builds a <see cref="Claude.Agent.MessageContextUsageAgent"/> for a test. Each parameter is the property of its name.</summary>
    public static MessageContextUsageAgent MessageContextUsageAgent(string? agentType = null, string? source = null, long tokens = 0) =>
        new(agentType ?? string.Empty, source ?? string.Empty, tokens);

    /// <summary>Builds a <see cref="Claude.Agent.MessageContextUsageSkill"/> for a test. Each parameter is the property of its name.</summary>
    public static MessageContextUsageSkill MessageContextUsageSkill(
        string? name = null,
        string? source = null,
        long tokens = 0,
        string? pluginName = null) =>
        new(name ?? string.Empty, source ?? string.Empty, tokens) { PluginName = pluginName };

    /// <summary>Builds a <see cref="Claude.Agent.UsageReport"/> for a test. Each parameter is the property of its name.</summary>
    public static UsageReport UsageReport(UsageReportSession? session = null, UsageReportRateLimits? rateLimits = null) =>
        new(session ?? UsageReportSession()) { RateLimits = rateLimits };

    /// <summary>Builds a <see cref="Claude.Agent.UsageReportSession"/> for a test. Each parameter is the property of its name.</summary>
    public static UsageReportSession UsageReportSession(
        decimal totalCostUsd = 0,
        TimeSpan totalApiDuration = default,
        TimeSpan totalDuration = default,
        long totalLinesAdded = 0,
        long totalLinesRemoved = 0,
        IReadOnlyDictionary<string, ModelUsage>? modelUsage = null) =>
        new(totalCostUsd, totalApiDuration, totalDuration, totalLinesAdded, totalLinesRemoved)
        {
            ModelUsage = modelUsage ?? new Dictionary<string, ModelUsage>(),
        };

    /// <summary>Builds a <see cref="Claude.Agent.UsageReportRateLimits"/> for a test. Each parameter is the property of its name.</summary>
    public static UsageReportRateLimits UsageReportRateLimits(
        IEnumerable<UsageReportLimit>? limits = null,
        UsageReportExtraUsage? extraUsage = null) =>
        new()
        {
            Limits = [.. limits ?? []],
            ExtraUsage = extraUsage,
        };

    /// <summary>Builds a <see cref="Claude.Agent.UsageReportLimit"/> for a test. Each parameter is the property of its name.</summary>
    public static UsageReportLimit UsageReportLimit(
        string? kind = null,
        string? group = null,
        double percent = 0,
        string? resetsAt = null,
        UsageReportLimitScope? scope = null,
        string? severity = null,
        bool? isActive = null) =>
        new(kind ?? string.Empty, group ?? string.Empty, percent)
        {
            ResetsAt = resetsAt,
            Scope = scope,
            Severity = severity,
            IsActive = isActive,
        };

    /// <summary>Builds a <see cref="Claude.Agent.UsageReportLimitScope"/> for a test. Each parameter is the property of its name.</summary>
    public static UsageReportLimitScope UsageReportLimitScope(UsageReportTarget? model = null, UsageReportTarget? surface = null) =>
        new() { Model = model, Surface = surface };

    /// <summary>Builds a <see cref="Claude.Agent.UsageReportTarget"/> for a test. Each parameter is the property of its name.</summary>
    public static UsageReportTarget UsageReportTarget(string? displayName = null) => new(displayName ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.UsageReportExtraUsage"/> for a test. Each parameter is the property of its name.</summary>
    public static UsageReportExtraUsage UsageReportExtraUsage(
        bool isEnabled = false,
        double? monthlyLimit = null,
        double? usedCredits = null,
        double? utilization = null,
        string? currency = null) =>
        new(isEnabled)
        {
            MonthlyLimit = monthlyLimit,
            UsedCredits = usedCredits,
            Utilization = utilization,
            Currency = currency,
        };

    /// <summary>Builds a <see cref="Claude.Agent.ResultMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static ResultMessage ResultMessage(
        ResultMessageSubtype? subtype = null,
        TimeSpan duration = default,
        TimeSpan apiDuration = default,
        bool isError = false,
        int turnCount = 0,
        StopReason? stopReason = null,
        decimal? totalCostUsd = null,
        Usage? usage = null,
        string? result = null,
        IReadOnlyDictionary<string, ModelUsage>? modelUsage = null,
        IEnumerable<PermissionDenial>? permissionDenials = null,
        DeferredToolUse? deferredToolUse = null,
        IEnumerable<string>? errors = null,
        StartupFailureReason? startupFailureReason = null,
        int? apiErrorStatus = null,
        TerminalReason? terminalReason = null,
        long? queuedTurnCount = null,
        string? userMessageUuid = null,
        IEnumerable<string>? userMessageUuids = null,
        string? resumeReason = null,
        long? resultIndex = null,
        FastModeState? fastModeState = null,
        FastModeDisabledReason? fastModeDisabledReason = null,
        MessageOrigin? origin = null,
        SubagentStatistics? subagentStatistics = null,
        string? localCommand = null,
        bool? warmSpareClaimed = null,
        TimeSpan? timeToFirstToken = null,
        TimeSpan? timeToFirstStreamedToken = null,
        TimeSpan? timeToRequest = null,
        TimeSpan? timeToRequestFromSpawn = null,
        TimeSpan? timeToFirstContentFrame = null,
        TimeSpan? requestSentWallTime = null,
        TimeSpan? firstStreamPost = null,
        TimeSpan? firstStreamPostAcknowledgement = null,
        TimeSpan? firstStreamPostWallTime = null,
        DateTimeOffset? timeOrigin = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(subtype ?? ResultMessageSubtype.Success, duration, apiDuration, isError, turnCount, sessionId ?? string.Empty, uuid, raw)
        {
            StopReason = stopReason,
            TotalCostUsd = totalCostUsd,
            Usage = usage,
            Result = result,
            ModelUsage = modelUsage ?? new Dictionary<string, ModelUsage>(),
            PermissionDenials = [.. permissionDenials ?? []],
            DeferredToolUse = deferredToolUse,
            Errors = [.. errors ?? []],
            StartupFailureReason = startupFailureReason,
            ApiErrorStatus = apiErrorStatus,
            TerminalReason = terminalReason,
            QueuedTurnCount = queuedTurnCount,
            UserMessageUuid = userMessageUuid,
            UserMessageUuids = [.. userMessageUuids ?? []],
            ResumeReason = resumeReason,
            ResultIndex = resultIndex,
            FastModeState = fastModeState,
            FastModeDisabledReason = fastModeDisabledReason,
            Origin = origin,
            SubagentStatistics = subagentStatistics,
            LocalCommand = localCommand,
            WarmSpareClaimed = warmSpareClaimed,
            TimeToFirstToken = timeToFirstToken,
            TimeToFirstStreamedToken = timeToFirstStreamedToken,
            TimeToRequest = timeToRequest,
            TimeToRequestFromSpawn = timeToRequestFromSpawn,
            TimeToFirstContentFrame = timeToFirstContentFrame,
            RequestSentWallTime = requestSentWallTime,
            FirstStreamPost = firstStreamPost,
            FirstStreamPostAcknowledgement = firstStreamPostAcknowledgement,
            FirstStreamPostWallTime = firstStreamPostWallTime,
            TimeOrigin = timeOrigin,
        };

    /// <summary>Builds a <see cref="Claude.Agent.SubagentStatistics"/> for a test. Each parameter is the property of its name.</summary>
    public static SubagentStatistics SubagentStatistics(
        long spawned = 0,
        long spawnedBySubagents = 0,
        long startedInBackground = 0,
        long completed = 0,
        long failed = 0,
        long maxDepth = 0,
        SubagentRequestCounts? requested = null,
        SubagentKillCounts? killed = null,
        SubagentRefusalCounts? refused = null,
        IReadOnlyDictionary<string, long>? byType = null) =>
        new(spawned, spawnedBySubagents, startedInBackground, completed, failed, maxDepth)
        {
            Requested = requested,
            Killed = killed,
            Refused = refused,
            ByType = byType ?? new Dictionary<string, long>(),
        };

    /// <summary>Builds a <see cref="Claude.Agent.SubagentRequestCounts"/> for a test. Each parameter is the property of its name.</summary>
    public static SubagentRequestCounts SubagentRequestCounts(long background = 0, long foreground = 0, long unset = 0) =>
        new(background, foreground, unset);

    /// <summary>Builds a <see cref="Claude.Agent.SubagentKillCounts"/> for a test. Each parameter is the property of its name.</summary>
    public static SubagentKillCounts SubagentKillCounts(long parent = 0, long user = 0, long system = 0) =>
        new(parent, user, system);

    /// <summary>Builds a <see cref="Claude.Agent.SubagentRefusalCounts"/> for a test. Each parameter is the property of its name.</summary>
    public static SubagentRefusalCounts SubagentRefusalCounts(long budget = 0, long concurrencyLimit = 0, long depthLimit = 0) =>
        new(budget, concurrencyLimit, depthLimit);

    /// <summary>Builds a <see cref="Claude.Agent.StreamEventMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static StreamEventMessage StreamEventMessage(
        JsonElement @event = default,
        string? parentToolUseId = null,
        TimeSpan? timeToFirstToken = null,
        string? userMessageUuid = null,
        IEnumerable<string>? userMessageUuids = null,
        string? resumeReason = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(Object(@event), parentToolUseId, uuid ?? string.Empty, sessionId ?? string.Empty, raw)
        {
            TimeToFirstToken = timeToFirstToken,
            UserMessageUuid = userMessageUuid,
            UserMessageUuids = [.. userMessageUuids ?? []],
            ResumeReason = resumeReason,
        };

    /// <summary>Builds a <see cref="Claude.Agent.RateLimitMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static RateLimitMessage RateLimitMessage(
        RateLimitStatus? status = null,
        DateTimeOffset? resetsAt = null,
        RateLimitType? limitType = null,
        double? utilization = null,
        RateLimitStatus? overageStatus = null,
        DateTimeOffset? overageResetsAt = null,
        OverageDisabledReason? overageDisabledReason = null,
        bool? isUsingOverage = null,
        bool? overageInUse = null,
        double? surpassedThreshold = null,
        RateLimitScope? limitScope = null,
        RateLimitErrorCode? errorCode = null,
        bool? canUserPurchaseCredits = null,
        bool? hasChargeableSavedPaymentMethod = null,
        IReadOnlyDictionary<string, RateLimitWindow>? unifiedWindows = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(status ?? RateLimitStatus.Allowed, uuid ?? string.Empty, sessionId ?? string.Empty, raw)
        {
            ResetsAt = resetsAt,
            LimitType = limitType,
            Utilization = utilization,
            OverageStatus = overageStatus,
            OverageResetsAt = overageResetsAt,
            OverageDisabledReason = overageDisabledReason,
            IsUsingOverage = isUsingOverage,
            OverageInUse = overageInUse,
            SurpassedThreshold = surpassedThreshold,
            LimitScope = limitScope,
            ErrorCode = errorCode,
            CanUserPurchaseCredits = canUserPurchaseCredits,
            HasChargeableSavedPaymentMethod = hasChargeableSavedPaymentMethod,
            UnifiedWindows = unifiedWindows ?? new Dictionary<string, RateLimitWindow>(),
        };

    /// <summary>Builds a <see cref="Claude.Agent.RateLimitWindow"/> for a test. Each parameter is the property of its name.</summary>
    public static RateLimitWindow RateLimitWindow(double? utilization = null, DateTimeOffset? resetsAt = null) =>
        new() { Utilization = utilization, ResetsAt = resetsAt };

    /// <summary>Builds a <see cref="Claude.Agent.ConversationResetMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static ConversationResetMessage ConversationResetMessage(
        string? newConversationId = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(newConversationId ?? string.Empty, uuid ?? string.Empty, sessionId ?? string.Empty, raw);

    /// <summary>Builds a <see cref="Claude.Agent.CommandLifecycleMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static CommandLifecycleMessage CommandLifecycleMessage(
        string? commandUuid = null,
        CommandLifecycleState state = default,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(commandUuid ?? string.Empty, state, uuid, sessionId, raw);

    /// <summary>Builds a <see cref="Claude.Agent.ToolProgressMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static ToolProgressMessage ToolProgressMessage(
        string? toolUseId = null,
        string? toolName = null,
        TimeSpan elapsedTime = default,
        string? parentToolUseId = null,
        string? taskId = null,
        bool? heartbeat = null,
        string? subagentType = null,
        SubagentRetry? subagentRetry = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(toolUseId ?? string.Empty, toolName ?? string.Empty, elapsedTime, uuid, sessionId, raw)
        {
            ParentToolUseId = parentToolUseId,
            TaskId = taskId,
            Heartbeat = heartbeat,
            SubagentType = subagentType,
            SubagentRetry = subagentRetry,
        };

    /// <summary>Builds a <see cref="Claude.Agent.SubagentRetry"/> for a test. Each parameter is the property of its name.</summary>
    public static SubagentRetry SubagentRetry(
        string? agentId = null,
        long attempt = 0,
        long maxRetries = 0,
        TimeSpan retryDelay = default,
        string? errorCategory = null,
        long? errorStatus = null) =>
        new(agentId ?? string.Empty, attempt, maxRetries, retryDelay, errorCategory ?? string.Empty)
        {
            ErrorStatus = errorStatus,
        };

    /// <summary>Builds a <see cref="Claude.Agent.ToolUseSummaryMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static ToolUseSummaryMessage ToolUseSummaryMessage(
        string? summary = null,
        IEnumerable<string>? precedingToolUseIds = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(summary ?? string.Empty, [.. precedingToolUseIds ?? []], uuid, sessionId, raw);

    /// <summary>Builds an <see cref="Claude.Agent.AuthStatusMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static AuthStatusMessage AuthStatusMessage(
        bool isAuthenticating = false,
        IEnumerable<string>? output = null,
        string? error = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(isAuthenticating, [.. output ?? []], uuid, sessionId, raw) { Error = error };

    /// <summary>Builds a <see cref="Claude.Agent.PromptSuggestionMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static PromptSuggestionMessage PromptSuggestionMessage(
        string? suggestion = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(suggestion ?? string.Empty, uuid, sessionId, raw);

    /// <summary>Builds an <see cref="Claude.Agent.ActiveGoalMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static ActiveGoalMessage ActiveGoalMessage(
        ActiveGoal? value = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(value, uuid, sessionId, raw);

    /// <summary>Builds an <see cref="Claude.Agent.ActiveGoal"/> for a test. Each parameter is the property of its name.</summary>
    public static ActiveGoal ActiveGoal(
        string? condition = null,
        long iterations = 0,
        DateTimeOffset? setAt = null,
        long tokensAtStart = 0,
        string? lastReason = null) =>
        new(condition ?? string.Empty, iterations, setAt, tokensAtStart) { LastReason = lastReason };

    /// <summary>Builds a <see cref="Claude.Agent.SessionMessage"/> for a test. Each parameter is the property of its name.</summary>
    public static SessionMessage SessionMessage(
        SessionMessageRole? role = null,
        string? uuid = null,
        string? sessionId = null,
        IEnumerable<ContentBlock>? content = null,
        ApiMessage? message = null,
        string? parentToolUseId = null,
        string? parentAgentId = null) =>
        new(role ?? SessionMessageRole.User, uuid ?? string.Empty, sessionId ?? string.Empty, [.. content ?? []])
        {
            Message = message,
            ParentToolUseId = parentToolUseId,
            ParentAgentId = parentAgentId,
        };

    /// <summary>Builds a <see cref="Claude.Agent.SystemInitMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static SystemInitMessage SystemInitMessage(
        string? workingDirectory = null,
        IEnumerable<string>? tools = null,
        IEnumerable<McpServerSummary>? mcpServers = null,
        string? model = null,
        PermissionMode? permissionMode = null,
        IEnumerable<string>? slashCommands = null,
        IEnumerable<string>? terminalSlashCommands = null,
        ApiKeySource? apiKeySource = null,
        string? claudeCodeVersion = null,
        string? outputStyle = null,
        IEnumerable<string>? agents = null,
        IEnumerable<string>? skills = null,
        IEnumerable<string>? capabilities = null,
        IEnumerable<string>? betas = null,
        IEnumerable<PluginInfo>? plugins = null,
        Effort? effort = null,
        FastModeState? fastModeState = null,
        FastModeDisabledReason? fastModeDisabledReason = null,
        bool? analyticsDisabled = null,
        bool? productFeedbackDisabled = null,
        MemoryPaths? memoryPaths = null,
        string? messagingSocketPath = null,
        string? scratchpadDirectory = null,
        bool? isPerTurnEffortActive = null,
        ViewMode? viewMode = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(uuid, sessionId, raw)
        {
            WorkingDirectory = workingDirectory,
            Tools = [.. tools ?? []],
            McpServers = [.. mcpServers ?? []],
            Model = model,
            PermissionMode = permissionMode,
            SlashCommands = [.. slashCommands ?? []],
            TerminalSlashCommands = [.. terminalSlashCommands ?? []],
            ApiKeySource = apiKeySource,
            ClaudeCodeVersion = claudeCodeVersion,
            OutputStyle = outputStyle,
            Agents = [.. agents ?? []],
            Skills = [.. skills ?? []],
            Capabilities = [.. capabilities ?? []],
            Betas = [.. betas ?? []],
            Plugins = [.. plugins ?? []],
            Effort = effort,
            FastModeState = fastModeState,
            FastModeDisabledReason = fastModeDisabledReason,
            AnalyticsDisabled = analyticsDisabled,
            ProductFeedbackDisabled = productFeedbackDisabled,
            MemoryPaths = memoryPaths,
            MessagingSocketPath = messagingSocketPath,
            ScratchpadDirectory = scratchpadDirectory,
            IsPerTurnEffortActive = isPerTurnEffortActive,
            ViewMode = viewMode,
        };

    /// <summary>Builds an <see cref="Claude.Agent.McpServerSummary"/> for a test. Each parameter is the property of its name.</summary>
    public static McpServerSummary McpServerSummary(
        string? name = null,
        McpServerConnectionStatus status = default,
        McpServerSource? source = null) =>
        new(name ?? string.Empty, status) { Source = source };

    /// <summary>Builds a <see cref="Claude.Agent.PluginInfo"/> for a test. Each parameter is the property of its name.</summary>
    public static PluginInfo PluginInfo(string? name = null, string? path = null, string? version = null) =>
        new(name ?? string.Empty, path ?? string.Empty) { Version = version };

    /// <summary>Builds a <see cref="Claude.Agent.MemoryPaths"/> for a test. Each parameter is the property of its name.</summary>
    public static MemoryPaths MemoryPaths(string? auto = null) => new(auto);

    /// <summary>Builds a <see cref="Claude.Agent.StatusMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static StatusMessage StatusMessage(
        ActivityStatus? status = null,
        PermissionMode? permissionMode = null,
        CompactResult? compactResult = null,
        string? compactError = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(uuid, sessionId, raw)
        {
            Status = status,
            PermissionMode = permissionMode,
            CompactResult = compactResult,
            CompactError = compactError,
        };

    /// <summary>Builds a <see cref="Claude.Agent.CompactBoundaryMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static CompactBoundaryMessage CompactBoundaryMessage(
        CompactMetadata? compactMetadata = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(compactMetadata ?? CompactMetadata(), uuid, sessionId, raw);

    /// <summary>Builds a <see cref="Claude.Agent.CompactMetadata"/> for a test. Each parameter is the property of its name.</summary>
    public static CompactMetadata CompactMetadata(
        CompactTrigger trigger = default,
        long preTokens = 0,
        long? postTokens = null,
        TimeSpan? duration = null,
        PreservedSegment? preservedSegment = null,
        PreservedMessages? preservedMessages = null) =>
        new(trigger, preTokens)
        {
            PostTokens = postTokens,
            Duration = duration,
            PreservedSegment = preservedSegment,
            PreservedMessages = preservedMessages,
        };

    /// <summary>Builds a <see cref="Claude.Agent.PreservedSegment"/> for a test. Each parameter is the property of its name.</summary>
    public static PreservedSegment PreservedSegment(string? headUuid = null, string? anchorUuid = null, string? tailUuid = null) =>
        new(headUuid ?? string.Empty, anchorUuid ?? string.Empty, tailUuid ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.PreservedMessages"/> for a test. Each parameter is the property of its name.</summary>
    public static PreservedMessages PreservedMessages(string? anchorUuid = null, IEnumerable<string>? uuids = null) =>
        new(anchorUuid ?? string.Empty, [.. uuids ?? []]);

    /// <summary>Builds an <see cref="Claude.Agent.ApiRetryMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static ApiRetryMessage ApiRetryMessage(
        long attempt = 0,
        long maxRetries = 0,
        TimeSpan retryDelay = default,
        long? errorStatus = null,
        AssistantErrorCode? error = null,
        ApiRetryNoResponse? noResponse = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(attempt, maxRetries, retryDelay, uuid, sessionId, raw)
        {
            ErrorStatus = errorStatus,
            Error = error,
            NoResponse = noResponse,
        };

    /// <summary>Builds an <see cref="Claude.Agent.ApiRetryNoResponse"/> for a test. Each parameter is the property of its name.</summary>
    public static ApiRetryNoResponse ApiRetryNoResponse(TimeSpan waited = default, TimeSpan retryWait = default) =>
        new(waited, retryWait);

    /// <summary>Builds a <see cref="Claude.Agent.ControlRequestProgressMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static ControlRequestProgressMessage ControlRequestProgressMessage(
        string? requestId = null,
        ControlRequestProgressStatus status = default,
        long? attempt = null,
        long? maxRetries = null,
        TimeSpan? retryDelay = null,
        long? errorStatus = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(requestId ?? string.Empty, status, uuid, sessionId, raw)
        {
            Attempt = attempt,
            MaxRetries = maxRetries,
            RetryDelay = retryDelay,
            ErrorStatus = errorStatus,
        };

    /// <summary>Builds a <see cref="Claude.Agent.ThinkingTokensMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static ThinkingTokensMessage ThinkingTokensMessage(
        long estimatedTokens = 0,
        long estimatedTokensDelta = 0,
        string? userMessageUuid = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(estimatedTokens, estimatedTokensDelta, uuid, sessionId, raw) { UserMessageUuid = userMessageUuid };

    /// <summary>Builds a <see cref="Claude.Agent.SessionStateChangedMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static SessionStateChangedMessage SessionStateChangedMessage(
        SessionState state = default,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(state, uuid, sessionId, raw);

    /// <summary>Builds a <see cref="Claude.Agent.WorkerShuttingDownMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static WorkerShuttingDownMessage WorkerShuttingDownMessage(
        string? reason = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(reason ?? string.Empty, uuid, sessionId, raw);

    /// <summary>Builds a <see cref="Claude.Agent.HookStartedMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static HookStartedMessage HookStartedMessage(
        string? hookId = null,
        string? hookName = null,
        HookEvent hookEvent = default,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(hookId ?? string.Empty, hookName ?? string.Empty, hookEvent, uuid, sessionId, raw);

    /// <summary>Builds a <see cref="Claude.Agent.HookProgressMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static HookProgressMessage HookProgressMessage(
        string? hookId = null,
        string? hookName = null,
        HookEvent hookEvent = default,
        string? standardOutput = null,
        string? standardError = null,
        string? output = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(
            hookId ?? string.Empty,
            hookName ?? string.Empty,
            hookEvent,
            standardOutput ?? string.Empty,
            standardError ?? string.Empty,
            output ?? string.Empty,
            uuid,
            sessionId,
            raw);

    /// <summary>Builds a <see cref="Claude.Agent.HookResponseMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static HookResponseMessage HookResponseMessage(
        string? hookId = null,
        string? hookName = null,
        HookEvent hookEvent = default,
        string? output = null,
        string? standardOutput = null,
        string? standardError = null,
        HookOutcome outcome = default,
        long? exitCode = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(
            hookId ?? string.Empty,
            hookName ?? string.Empty,
            hookEvent,
            output ?? string.Empty,
            standardOutput ?? string.Empty,
            standardError ?? string.Empty,
            outcome,
            uuid,
            sessionId,
            raw)
        {
            ExitCode = exitCode,
        };

    /// <summary>Builds a <see cref="Claude.Agent.PermissionDeniedMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static PermissionDeniedMessage PermissionDeniedMessage(
        string? toolName = null,
        string? toolUseId = null,
        string? message = null,
        string? agentId = null,
        string? decisionReasonType = null,
        string? decisionReason = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(toolName ?? string.Empty, toolUseId ?? string.Empty, message ?? string.Empty, uuid, sessionId, raw)
        {
            AgentId = agentId,
            DecisionReasonType = decisionReasonType,
            DecisionReason = decisionReason,
        };

    /// <summary>Builds a <see cref="Claude.Agent.NotificationMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static NotificationMessage NotificationMessage(
        string? key = null,
        string? text = null,
        NotificationPriority priority = default,
        string? color = null,
        TimeSpan? timeout = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(key ?? string.Empty, text ?? string.Empty, priority, uuid, sessionId, raw)
        {
            Color = color,
            Timeout = timeout,
        };

    /// <summary>Builds an <see cref="Claude.Agent.InformationalMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static InformationalMessage InformationalMessage(
        string? content = null,
        InformationalLevel level = default,
        string? toolUseId = null,
        bool? preventContinuation = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(content ?? string.Empty, level, uuid, sessionId, raw)
        {
            ToolUseId = toolUseId,
            PreventContinuation = preventContinuation,
        };

    /// <summary>Builds a <see cref="Claude.Agent.LocalCommandOutputMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static LocalCommandOutputMessage LocalCommandOutputMessage(
        string? content = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(content ?? string.Empty, uuid, sessionId, raw);

    /// <summary>Builds an <see cref="Claude.Agent.ElicitationCompleteMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static ElicitationCompleteMessage ElicitationCompleteMessage(
        string? mcpServerName = null,
        string? elicitationId = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(mcpServerName ?? string.Empty, elicitationId ?? string.Empty, uuid, sessionId, raw);

    /// <summary>Builds a <see cref="Claude.Agent.MemoryRecallMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static MemoryRecallMessage MemoryRecallMessage(
        MemoryRecallMode mode = default,
        IEnumerable<RecalledMemory>? memories = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(mode, [.. memories ?? []], uuid, sessionId, raw);

    /// <summary>Builds a <see cref="Claude.Agent.RecalledMemory"/> for a test. Each parameter is the property of its name.</summary>
    public static RecalledMemory RecalledMemory(string? path = null, MemoryScope scope = default, string? content = null) =>
        new(path ?? string.Empty, scope) { Content = content };

    /// <summary>Builds a <see cref="Claude.Agent.FilesPersistedMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static FilesPersistedMessage FilesPersistedMessage(
        IEnumerable<PersistedFile>? files = null,
        IEnumerable<FilePersistenceFailure>? failed = null,
        string? processedAt = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new([.. files ?? []], [.. failed ?? []], processedAt ?? string.Empty, uuid, sessionId, raw);

    /// <summary>Builds a <see cref="Claude.Agent.PersistedFile"/> for a test. Each parameter is the property of its name.</summary>
    public static PersistedFile PersistedFile(string? fileName = null, string? fileId = null) =>
        new(fileName ?? string.Empty, fileId ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.FilePersistenceFailure"/> for a test. Each parameter is the property of its name.</summary>
    public static FilePersistenceFailure FilePersistenceFailure(string? fileName = null, string? error = null) =>
        new(fileName ?? string.Empty, error ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.CommandsChangedMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static CommandsChangedMessage CommandsChangedMessage(
        IEnumerable<SlashCommandInfo>? commands = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new([.. commands ?? []], uuid, sessionId, raw);

    /// <summary>Builds a <see cref="Claude.Agent.SlashCommandInfo"/> for a test. Each parameter is the property of its name.</summary>
    public static SlashCommandInfo SlashCommandInfo(
        string? name = null,
        string? description = null,
        string? argumentHint = null,
        IEnumerable<string>? aliases = null,
        bool? isBuiltIn = null) =>
        new(name ?? string.Empty, description ?? string.Empty, argumentHint ?? string.Empty)
        {
            Aliases = [.. aliases ?? []],
            IsBuiltIn = isBuiltIn,
        };

    /// <summary>Builds a <see cref="Claude.Agent.PluginInstallMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static PluginInstallMessage PluginInstallMessage(
        PluginInstallStatus status = default,
        string? name = null,
        string? error = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(status, uuid, sessionId, raw) { Name = name, Error = error };

    /// <summary>Builds a <see cref="Claude.Agent.ModelRefusalFallbackMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static ModelRefusalFallbackMessage ModelRefusalFallbackMessage(
        RefusalFallbackTrigger trigger = default,
        RefusalFallbackDirection direction = default,
        RefusalFallbackScope? scope = null,
        string? originalModel = null,
        string? fallbackModel = null,
        string? content = null,
        string? requestId = null,
        string? apiRefusalCategory = null,
        string? apiRefusalExplanation = null,
        IEnumerable<string>? retractedMessageUuids = null,
        string? refusedUserMessageUuid = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(
            trigger,
            direction,
            originalModel ?? string.Empty,
            fallbackModel ?? string.Empty,
            content ?? string.Empty,
            uuid,
            sessionId,
            raw)
        {
            Scope = scope,
            RequestId = requestId,
            ApiRefusalCategory = apiRefusalCategory,
            ApiRefusalExplanation = apiRefusalExplanation,
            RetractedMessageUuids = [.. retractedMessageUuids ?? []],
            RefusedUserMessageUuid = refusedUserMessageUuid,
        };

    /// <summary>Builds a <see cref="Claude.Agent.ModelRefusalNoFallbackMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static ModelRefusalNoFallbackMessage ModelRefusalNoFallbackMessage(
        string? originalModel = null,
        string? content = null,
        string? requestId = null,
        string? apiRefusalCategory = null,
        string? apiRefusalExplanation = null,
        string? refusedUserMessageUuid = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(originalModel ?? string.Empty, content ?? string.Empty, uuid, sessionId, raw)
        {
            RequestId = requestId,
            ApiRefusalCategory = apiRefusalCategory,
            ApiRefusalExplanation = apiRefusalExplanation,
            RefusedUserMessageUuid = refusedUserMessageUuid,
        };

    /// <summary>Builds a <see cref="Claude.Agent.BackgroundTasksChangedMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static BackgroundTasksChangedMessage BackgroundTasksChangedMessage(
        IEnumerable<BackgroundTask>? tasks = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new([.. tasks ?? []], uuid, sessionId, raw);

    /// <summary>Builds a <see cref="Claude.Agent.BackgroundTask"/> for a test. Each parameter is the property of its name.</summary>
    public static BackgroundTask BackgroundTask(
        string? taskId = null,
        TaskType taskType = default,
        string? description = null,
        bool? ambient = null) =>
        new(taskId ?? string.Empty, taskType, description ?? string.Empty) { Ambient = ambient };

    /// <summary>Builds a <see cref="Claude.Agent.LocalAgentTaskStartedMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static LocalAgentTaskStartedMessage LocalAgentTaskStartedMessage(
        string? taskId = null,
        string? description = null,
        string? toolUseId = null,
        string? subagentType = null,
        bool? isBackgrounded = null,
        long? spawnDepth = null,
        string? prompt = null,
        bool? skipTranscript = null,
        bool? ambient = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(taskId ?? string.Empty, description ?? string.Empty, uuid, sessionId, raw)
        {
            ToolUseId = toolUseId,
            SubagentType = subagentType,
            IsBackgrounded = isBackgrounded,
            SpawnDepth = spawnDepth,
            Prompt = prompt,
            SkipTranscript = skipTranscript,
            Ambient = ambient,
        };

    /// <summary>Builds a <see cref="Claude.Agent.LocalBashTaskStartedMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static LocalBashTaskStartedMessage LocalBashTaskStartedMessage(
        string? taskId = null,
        string? description = null,
        string? toolUseId = null,
        bool? isBackgrounded = null,
        bool? skipTranscript = null,
        bool? ambient = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(taskId ?? string.Empty, description ?? string.Empty, uuid, sessionId, raw)
        {
            ToolUseId = toolUseId,
            IsBackgrounded = isBackgrounded,
            SkipTranscript = skipTranscript,
            Ambient = ambient,
        };

    /// <summary>Builds a <see cref="Claude.Agent.LocalWorkflowTaskStartedMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static LocalWorkflowTaskStartedMessage LocalWorkflowTaskStartedMessage(
        string? taskId = null,
        string? description = null,
        string? toolUseId = null,
        string? workflowName = null,
        string? prompt = null,
        bool? skipTranscript = null,
        bool? ambient = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(taskId ?? string.Empty, description ?? string.Empty, uuid, sessionId, raw)
        {
            ToolUseId = toolUseId,
            WorkflowName = workflowName,
            Prompt = prompt,
            SkipTranscript = skipTranscript,
            Ambient = ambient,
        };

    /// <summary>Builds an <see cref="Claude.Agent.UnknownTaskStartedMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static UnknownTaskStartedMessage UnknownTaskStartedMessage(
        TaskType taskType = default,
        string? taskId = null,
        string? description = null,
        string? toolUseId = null,
        bool? skipTranscript = null,
        bool? ambient = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(taskType, taskId ?? string.Empty, description ?? string.Empty, uuid, sessionId, raw)
        {
            ToolUseId = toolUseId,
            SkipTranscript = skipTranscript,
            Ambient = ambient,
        };

    /// <summary>Builds a <see cref="Claude.Agent.TaskProgressMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static TaskProgressMessage TaskProgressMessage(
        string? taskId = null,
        string? description = null,
        string? toolUseId = null,
        string? subagentType = null,
        long? totalTokens = null,
        long? toolUseCount = null,
        TimeSpan? duration = null,
        string? lastToolName = null,
        string? summary = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(taskId ?? string.Empty, description ?? string.Empty, uuid, sessionId, raw)
        {
            ToolUseId = toolUseId,
            SubagentType = subagentType,
            TotalTokens = totalTokens,
            ToolUseCount = toolUseCount,
            Duration = duration,
            LastToolName = lastToolName,
            Summary = summary,
        };

    /// <summary>Builds a <see cref="Claude.Agent.TaskNotificationMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static TaskNotificationMessage TaskNotificationMessage(
        string? taskId = null,
        TaskNotificationStatus status = default,
        string? outputFile = null,
        string? summary = null,
        string? toolUseId = null,
        TaskNotificationReason? reason = null,
        long? totalTokens = null,
        long? toolUseCount = null,
        TimeSpan? duration = null,
        IEnumerable<ResourceLink>? resourceLinks = null,
        bool? skipTranscript = null,
        bool? ambient = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(taskId ?? string.Empty, status, outputFile ?? string.Empty, summary ?? string.Empty, uuid, sessionId, raw)
        {
            ToolUseId = toolUseId,
            Reason = reason,
            TotalTokens = totalTokens,
            ToolUseCount = toolUseCount,
            Duration = duration,
            ResourceLinks = [.. resourceLinks ?? []],
            SkipTranscript = skipTranscript,
            Ambient = ambient,
        };

    /// <summary>Builds a <see cref="Claude.Agent.TaskUpdatedMessage"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to none, as for a message the library assembled.</summary>
    public static TaskUpdatedMessage TaskUpdatedMessage(
        TaskPatch? patch = null,
        string? taskId = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(patch ?? TaskPatch(), taskId, uuid, sessionId, raw);

    /// <summary>Builds a <see cref="Claude.Agent.TaskPatch"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskPatch TaskPatch(
        BackgroundTaskStatus? status = null,
        string? description = null,
        DateTimeOffset? endTime = null,
        TimeSpan? totalPausedTime = null,
        string? error = null,
        bool? isBackgrounded = null) =>
        new()
        {
            Status = status,
            Description = description,
            EndTime = endTime,
            TotalPausedTime = totalPausedTime,
            Error = error,
            IsBackgrounded = isBackgrounded,
        };

    /// <summary>Builds a <see cref="Claude.Agent.ResourceLink"/> for a test. Each parameter is the property of its name.</summary>
    public static ResourceLink ResourceLink(
        string? uri = null,
        string? name = null,
        string? title = null,
        string? description = null,
        string? mimeType = null,
        long? size = null) =>
        new(uri ?? string.Empty, name ?? string.Empty)
        {
            Title = title,
            Description = description,
            MimeType = mimeType,
            Size = size,
        };

    /// <summary>Builds a <see cref="Claude.Agent.PermissionDenial"/> for a test. Each parameter is the property of its name. <c>rawInput</c> defaults to an empty object.</summary>
    public static PermissionDenial PermissionDenial(
        string? toolName = null,
        string? toolUseId = null,
        ToolInput? input = null,
        JsonElement rawInput = default) =>
        new(toolName ?? string.Empty, toolUseId ?? string.Empty, input, Object(rawInput));

    /// <summary>Builds a <see cref="Claude.Agent.DeferredToolUse"/> for a test. Each parameter is the property of its name. <c>rawInput</c> defaults to an empty object.</summary>
    public static DeferredToolUse DeferredToolUse(
        string? id = null,
        string? name = null,
        ToolInput? input = null,
        JsonElement rawInput = default) =>
        new(id ?? string.Empty, name ?? string.Empty, input, Object(rawInput));
}
