namespace Claude.Agent;

/// <summary>The model is about to change. The hook may let the change happen or stop it.</summary>
public sealed class PreModelSwitchHookInput : HookInput
{
    internal PreModelSwitchHookInput(HookInputCommon common,
        string fromModel,
        string toModel,
        string? requestedModel,
        PreModelSwitchSource source,
        long contextTokens,
        bool isPromptCacheWarm,
        ModelSwitchCacheTimeToLive cacheTimeToLive,
        decimal estimatedCacheWriteCost,
        ModelSwitchPricing pricing)
        : base(HookEvent.PreModelSwitch, common)
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

    /// <summary>The model in use now.</summary>
    public string FromModel { get; }

    /// <summary>The model it would change to.</summary>
    public string ToModel { get; }

    /// <summary>The model as it was asked for, before the binary resolved it; null when nothing named one.</summary>
    public string? RequestedModel { get; }

    /// <summary>What asked for the change.</summary>
    public PreModelSwitchSource Source { get; }

    /// <summary>How many tokens the context holds.</summary>
    public long ContextTokens { get; }

    /// <summary>Whether the prompt cache is warm for the model in use.</summary>
    public bool IsPromptCacheWarm { get; }

    /// <summary>How long the prompt cache lives.</summary>
    public ModelSwitchCacheTimeToLive CacheTimeToLive { get; }

    /// <summary>What writing the cache again for the new model is reckoned to cost, in United States dollars.</summary>
    public decimal EstimatedCacheWriteCost { get; }

    /// <summary>Where the price used for that reckoning came from.</summary>
    public ModelSwitchPricing Pricing { get; }
}
