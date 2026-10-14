namespace Claude.Agent;

public static partial class ClaudeAgentModelFactory
{
    /// <summary>Builds an <see cref="Claude.Agent.AgentToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static AgentToolInput AgentToolInput(
        string description = "",
        string prompt = "",
        string? subagentType = null,
        string? model = null,
        bool? runInBackground = null,
        string? name = null,
        string? teamName = null,
        PermissionMode? mode = null,
        AgentIsolation? isolation = null) =>
        new(description, prompt, subagentType, model, runInBackground, name, teamName, mode, isolation);

    /// <summary>Builds an <see cref="Claude.Agent.AgentToolTextBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static AgentToolTextBlock AgentToolTextBlock(string text = "") => new(text);

    /// <summary>Builds an <see cref="Claude.Agent.AgentServerToolUse"/> for a test. Each parameter is the property of its name.</summary>
    public static AgentServerToolUse AgentServerToolUse(long webSearchRequests = 0, long webFetchRequests = 0) =>
        new(webSearchRequests, webFetchRequests);

    /// <summary>Builds an <see cref="Claude.Agent.AgentCacheCreation"/> for a test. Each parameter is the property of its name.</summary>
    public static AgentCacheCreation AgentCacheCreation(long ephemeralOneHourInputTokens = 0, long ephemeralFiveMinuteInputTokens = 0) =>
        new(ephemeralOneHourInputTokens, ephemeralFiveMinuteInputTokens);

    /// <summary>Builds an <see cref="Claude.Agent.AgentOutputTokenDetails"/> for a test. Each parameter is the property of its name.</summary>
    public static AgentOutputTokenDetails AgentOutputTokenDetails(long? thinkingTokens = null) => new(thinkingTokens);

    /// <summary>Builds an <see cref="Claude.Agent.AgentToolUsage"/> for a test. Each parameter is the property of its name.</summary>
    public static AgentToolUsage AgentToolUsage(
        long inputTokens = 0,
        long outputTokens = 0,
        long? cacheCreationInputTokens = null,
        long? cacheReadInputTokens = null,
        AgentServerToolUse? serverToolUse = null,
        ServiceTier? serviceTier = null,
        AgentCacheCreation? cacheCreation = null,
        string? inferenceGeo = null,
        InferenceSpeed? speed = null,
        AgentOutputTokenDetails? outputTokenDetails = null) =>
        new(
            inputTokens,
            outputTokens,
            cacheCreationInputTokens,
            cacheReadInputTokens,
            serverToolUse,
            serviceTier,
            cacheCreation,
            inferenceGeo,
            speed,
            outputTokenDetails);

    /// <summary>Builds an <see cref="Claude.Agent.AgentToolStatistics"/> for a test. Each parameter is the property of its name.</summary>
    public static AgentToolStatistics AgentToolStatistics(
        long readCount = 0,
        long searchCount = 0,
        long bashCount = 0,
        long editFileCount = 0,
        long linesAdded = 0,
        long linesRemoved = 0,
        long otherToolCount = 0,
        long? frameCount = null) =>
        new(readCount, searchCount, bashCount, editFileCount, linesAdded, linesRemoved, otherToolCount, frameCount);

    /// <summary>Builds an <see cref="Claude.Agent.AgentCompletedToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static AgentCompletedToolOutput AgentCompletedToolOutput(
        string agentId = "",
        IEnumerable<AgentToolTextBlock>? content = null,
        long totalToolUseCount = 0,
        TimeSpan totalDuration = default,
        long totalTokens = 0,
        AgentToolUsage? usage = null,
        string prompt = "",
        string? agentType = null,
        string? resolvedModel = null,
        IEnumerable<string>? modelsUsed = null,
        AgentToolStatistics? toolStatistics = null,
        string? worktreePath = null,
        string? worktreeBranch = null,
        long? harnessNoteCount = null,
        long? harnessTailCount = null,
        string? harnessSectionHash = null) =>
        new(
            agentId,
            [.. content ?? []],
            totalToolUseCount,
            totalDuration,
            totalTokens,
            usage ?? AgentToolUsage(),
            prompt,
            agentType,
            resolvedModel,
            [.. modelsUsed ?? []],
            toolStatistics,
            worktreePath,
            worktreeBranch,
            harnessNoteCount,
            harnessTailCount,
            harnessSectionHash);

    /// <summary>Builds an <see cref="Claude.Agent.AgentAsyncLaunchedToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static AgentAsyncLaunchedToolOutput AgentAsyncLaunchedToolOutput(
        string agentId = "",
        string description = "",
        string prompt = "",
        string outputFile = "",
        bool? isAsync = null,
        string? resolvedModel = null,
        IEnumerable<string>? modelsUsed = null,
        bool? canReadOutputFile = null) =>
        new(agentId, description, prompt, outputFile, isAsync, resolvedModel, [.. modelsUsed ?? []], canReadOutputFile);

    /// <summary>Builds an <see cref="Claude.Agent.AgentRemoteLaunchedToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static AgentRemoteLaunchedToolOutput AgentRemoteLaunchedToolOutput(
        string taskId = "",
        string sessionUrl = "",
        string description = "",
        string prompt = "",
        string outputFile = "") =>
        new(taskId, sessionUrl, description, prompt, outputFile);
}
