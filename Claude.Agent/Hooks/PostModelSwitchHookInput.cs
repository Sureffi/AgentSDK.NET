namespace Claude.Agent;

/// <summary>The model has changed.</summary>
public sealed class PostModelSwitchHookInput : HookInput
{
    internal PostModelSwitchHookInput(HookInputCommon common,
        string fromModel,
        string toModel,
        string? requestedModel,
        PostModelSwitchSource source,
        long contextTokens,
        bool isPromptCacheWarm,
        ModelSwitchCacheTimeToLive cacheTimeToLive,
        decimal estimatedCacheWriteCost,
        ModelSwitchPricing pricing)
        : base(HookEvent.PostModelSwitch, common)
    {
        FromModel = fromModel;
        ToModel = toModel;
        RequestedModel = requestedModel;
        Source = source;
        ContextTokens = contextTokens;
        IsPromptCacheWarm = isPromptCacheWarm;
        CacheTimeToLive = cacheTimeToLive;
        EstimatedCacheWriteCost = estimatedCacheWriteCost;
        Pricing = pricing;
    }

    /// <summary>The model that was in use.</summary>
    public string FromModel { get; }

    /// <summary>The model now in use.</summary>
    public string ToModel { get; }

    /// <summary>The model as it was asked for, before the binary resolved it; null when nothing named one.</summary>
    public string? RequestedModel { get; }

    /// <summary>What changed it.</summary>
    public PostModelSwitchSource Source { get; }

    /// <summary>How many tokens the context holds.</summary>
    public long ContextTokens { get; }

    /// <summary>Whether the prompt cache is warm for the model left behind.</summary>
    public bool IsPromptCacheWarm { get; }

    /// <summary>How long the prompt cache lives.</summary>
    public ModelSwitchCacheTimeToLive CacheTimeToLive { get; }

    /// <summary>What writing the cache again for the new model is reckoned to cost, in United States dollars.</summary>
    public decimal EstimatedCacheWriteCost { get; }

    /// <summary>Where the price used for that reckoning came from.</summary>
    public ModelSwitchPricing Pricing { get; }
}
