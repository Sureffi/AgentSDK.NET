namespace Claude.Agent;

/// <summary>A session has opened. The hook may add to the context the model starts with.</summary>
public sealed class SessionStartHookInput : HookInput
{
    internal SessionStartHookInput(HookInputCommon common,
        SessionStartSource source,
        string? model,
        string? sessionTitle,
        TimeSpan? timeSinceLastResponse,
        long? contextTokens,
        bool? isPromptCacheLikelyExpired,
        decimal? estimatedCacheWriteCost)
        : base(HookEvent.SessionStart, common)
    {
        Source = source;
        Model = model;
        SessionTitle = sessionTitle;
        TimeSinceLastResponse = timeSinceLastResponse;
        ContextTokens = contextTokens;
        IsPromptCacheLikelyExpired = isPromptCacheLikelyExpired;
        EstimatedCacheWriteCost = estimatedCacheWriteCost;
    }

    /// <summary>How the session came to be.</summary>
    public SessionStartSource Source { get; }

    /// <summary>The model the session starts on; null when the binary did not say.</summary>
    public string? Model { get; }

    /// <summary>The session's title; null when it has none yet.</summary>
    public string? SessionTitle { get; }

    /// <summary>How long since the resumed conversation last answered; null except on a resume or a fork.</summary>
    public TimeSpan? TimeSinceLastResponse { get; }

    /// <summary>How many tokens the resumed context holds; null except on a resume or a fork.</summary>
    public long? ContextTokens { get; }

    /// <summary>Whether the prompt cache has probably gone cold; null except on a resume or a fork.</summary>
    public bool? IsPromptCacheLikelyExpired { get; }

    /// <summary>What writing the prompt cache again is reckoned to cost, in United States dollars; null except on a resume or a fork.</summary>
    public decimal? EstimatedCacheWriteCost { get; }
}
