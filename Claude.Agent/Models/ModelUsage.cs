namespace Claude.Agent;

/// <summary>What one model spent over a session, as an entry of <see cref="ResultMessage.ModelUsage"/>.</summary>
public sealed class ModelUsage
{
    internal ModelUsage()
    {
    }

    /// <summary>Input tokens neither read from nor written to the prompt cache.</summary>
    public long? InputTokens { get; internal init; }

    /// <summary>Output tokens.</summary>
    public long? OutputTokens { get; internal init; }

    /// <summary>Output tokens the model spent thinking; already counted in <see cref="OutputTokens"/>. Counted only over turns the binary recorded it on, so a resumed session can carry a partial figure.</summary>
    public long? ThinkingTokens { get; internal init; }

    /// <summary>Input tokens read from the prompt cache.</summary>
    public long? CacheReadInputTokens { get; internal init; }

    /// <summary>Input tokens written to the prompt cache.</summary>
    public long? CacheCreationInputTokens { get; internal init; }

    /// <summary>Web searches the API ran for the model.</summary>
    public long? WebSearchRequests { get; internal init; }

    /// <summary>What the model cost, in US dollars.</summary>
    public decimal? CostUsd { get; internal init; }

    /// <summary>How that cost was priced.</summary>
    public ModelUsageCostBasis? CostBasis { get; internal init; }

    /// <summary>The model's context window in tokens.</summary>
    public long? ContextWindow { get; internal init; }

    /// <summary>The most tokens the model may write in one answer.</summary>
    public long? MaxOutputTokens { get; internal init; }

    /// <summary>The model's canonical id.</summary>
    public string? CanonicalModel { get; internal init; }

    /// <summary>The provider that served the model.</summary>
    public string? Provider { get; internal init; }
}
