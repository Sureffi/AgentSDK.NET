using System.Text.Json;

namespace Claude.Agent;

public static partial class ClaudeAgentModelFactory
{
    /// <summary>Builds a <see cref="Claude.Agent.Usage"/> for a test. Each parameter is the property of its name.</summary>
    public static Usage Usage(
        long? inputTokens = null,
        long? outputTokens = null,
        long? cacheCreationInputTokens = null,
        long? cacheReadInputTokens = null,
        CacheCreationTokens? cacheCreation = null,
        OutputTokensDetails? outputTokensDetails = null,
        ServerToolUsage? serverToolUse = null,
        ServiceTier? serviceTier = null,
        InferenceSpeed? speed = null,
        string? inferenceRegion = null,
        IEnumerable<UsageIteration>? iterations = null,
        FallbackCredit? fallbackCredit = null) =>
        new()
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CacheCreationInputTokens = cacheCreationInputTokens,
            CacheReadInputTokens = cacheReadInputTokens,
            CacheCreation = cacheCreation,
            OutputTokensDetails = outputTokensDetails,
            ServerToolUse = serverToolUse,
            ServiceTier = serviceTier,
            Speed = speed,
            InferenceRegion = inferenceRegion,
            Iterations = [.. iterations ?? []],
            FallbackCredit = fallbackCredit,
        };

    /// <summary>Builds a <see cref="Claude.Agent.CacheCreationTokens"/> for a test. Each parameter is the property of its name.</summary>
    public static CacheCreationTokens CacheCreationTokens(
        long? ephemeral1HourInputTokens = null,
        long? ephemeral5MinuteInputTokens = null) =>
        new()
        {
            Ephemeral1HourInputTokens = ephemeral1HourInputTokens,
            Ephemeral5MinuteInputTokens = ephemeral5MinuteInputTokens,
        };

    /// <summary>Builds an <see cref="Claude.Agent.OutputTokensDetails"/> for a test. Each parameter is the property of its name.</summary>
    public static OutputTokensDetails OutputTokensDetails(long? thinkingTokens = null) =>
        new() { ThinkingTokens = thinkingTokens };

    /// <summary>Builds a <see cref="Claude.Agent.ServerToolUsage"/> for a test. Each parameter is the property of its name.</summary>
    public static ServerToolUsage ServerToolUsage(long? webFetchRequests = null, long? webSearchRequests = null) =>
        new() { WebFetchRequests = webFetchRequests, WebSearchRequests = webSearchRequests };

    /// <summary>Builds a <see cref="Claude.Agent.MessageUsageIteration"/> for a test. Each parameter is the property of its name.</summary>
    public static MessageUsageIteration MessageUsageIteration(
        string? model = null,
        long? inputTokens = null,
        long? outputTokens = null,
        long? cacheCreationInputTokens = null,
        long? cacheReadInputTokens = null,
        CacheCreationTokens? cacheCreation = null) =>
        new()
        {
            Model = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CacheCreationInputTokens = cacheCreationInputTokens,
            CacheReadInputTokens = cacheReadInputTokens,
            CacheCreation = cacheCreation,
        };

    /// <summary>Builds a <see cref="Claude.Agent.CompactionUsageIteration"/> for a test. Each parameter is the property of its name.</summary>
    public static CompactionUsageIteration CompactionUsageIteration(
        long? inputTokens = null,
        long? outputTokens = null,
        long? cacheCreationInputTokens = null,
        long? cacheReadInputTokens = null,
        CacheCreationTokens? cacheCreation = null) =>
        new()
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CacheCreationInputTokens = cacheCreationInputTokens,
            CacheReadInputTokens = cacheReadInputTokens,
            CacheCreation = cacheCreation,
        };

    /// <summary>Builds an <see cref="Claude.Agent.AdvisorMessageUsageIteration"/> for a test. Each parameter is the property of its name.</summary>
    public static AdvisorMessageUsageIteration AdvisorMessageUsageIteration(
        string? model = null,
        long? inputTokens = null,
        long? outputTokens = null,
        long? cacheCreationInputTokens = null,
        long? cacheReadInputTokens = null,
        CacheCreationTokens? cacheCreation = null) =>
        new()
        {
            Model = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CacheCreationInputTokens = cacheCreationInputTokens,
            CacheReadInputTokens = cacheReadInputTokens,
            CacheCreation = cacheCreation,
        };

    /// <summary>Builds a <see cref="Claude.Agent.FallbackMessageUsageIteration"/> for a test. Each parameter is the property of its name.</summary>
    public static FallbackMessageUsageIteration FallbackMessageUsageIteration(
        string? model = null,
        long? inputTokens = null,
        long? outputTokens = null,
        long? cacheCreationInputTokens = null,
        long? cacheReadInputTokens = null,
        CacheCreationTokens? cacheCreation = null) =>
        new()
        {
            Model = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CacheCreationInputTokens = cacheCreationInputTokens,
            CacheReadInputTokens = cacheReadInputTokens,
            CacheCreation = cacheCreation,
        };

    /// <summary>Builds an <see cref="Claude.Agent.UnknownUsageIteration"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownUsageIteration UnknownUsageIteration(
        string? type = null,
        JsonElement raw = default,
        long? inputTokens = null,
        long? outputTokens = null,
        long? cacheCreationInputTokens = null,
        long? cacheReadInputTokens = null,
        CacheCreationTokens? cacheCreation = null) =>
        new(type ?? string.Empty, Object(raw))
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CacheCreationInputTokens = cacheCreationInputTokens,
            CacheReadInputTokens = cacheReadInputTokens,
            CacheCreation = cacheCreation,
        };

    /// <summary>Builds a <see cref="Claude.Agent.FallbackCredit"/> for a test. Each parameter is the property of its name.</summary>
    public static FallbackCredit FallbackCredit(FallbackCreditStatus? status = null) =>
        new() { Status = status };

    /// <summary>Builds a <see cref="Claude.Agent.RedeemedFallbackCreditStatus"/> for a test.</summary>
    public static RedeemedFallbackCreditStatus RedeemedFallbackCreditStatus() => new();

    /// <summary>Builds a <see cref="Claude.Agent.NotAppliedFallbackCreditStatus"/> for a test. Each parameter is the property of its name.</summary>
    public static NotAppliedFallbackCreditStatus NotAppliedFallbackCreditStatus(
        FallbackCreditNotAppliedReason? reason = null,
        IEnumerable<string>? fieldsToRemove = null) =>
        new() { Reason = reason, FieldsToRemove = [.. fieldsToRemove ?? []] };

    /// <summary>Builds an <see cref="Claude.Agent.UnknownFallbackCreditStatus"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownFallbackCreditStatus UnknownFallbackCreditStatus(string? type = null, JsonElement raw = default) =>
        new(type ?? string.Empty, Object(raw));

    /// <summary>Builds a <see cref="Claude.Agent.ModelUsage"/> for a test. Each parameter is the property of its name.</summary>
    public static ModelUsage ModelUsage(
        long? inputTokens = null,
        long? outputTokens = null,
        long? thinkingTokens = null,
        long? cacheReadInputTokens = null,
        long? cacheCreationInputTokens = null,
        long? webSearchRequests = null,
        decimal? costUsd = null,
        ModelUsageCostBasis? costBasis = null,
        long? contextWindow = null,
        long? maxOutputTokens = null,
        string? canonicalModel = null,
        string? provider = null) =>
        new()
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            ThinkingTokens = thinkingTokens,
            CacheReadInputTokens = cacheReadInputTokens,
            CacheCreationInputTokens = cacheCreationInputTokens,
            WebSearchRequests = webSearchRequests,
            CostUsd = costUsd,
            CostBasis = costBasis,
            ContextWindow = contextWindow,
            MaxOutputTokens = maxOutputTokens,
            CanonicalModel = canonicalModel,
            Provider = provider,
        };
}
