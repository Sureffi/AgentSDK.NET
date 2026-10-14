using System.Text.Json.Serialization;

namespace Claude.Agent;

/// <summary>
/// What one API request spent. On an assistant message this is the snapshot taken as the message
/// started, not the final count; on a result it is the sum over the turn's calls to the main model.
/// It serializes through System.Text.Json and reads back as it was, under the names the binary
/// writes the usage with, so a caller can keep it.
/// </summary>
public sealed class Usage
{
    [JsonConstructor]
    internal Usage()
    {
    }

    /// <summary>Input tokens neither read from nor written to the prompt cache.</summary>
    [JsonInclude]
    [JsonPropertyName("input_tokens")]
    public long? InputTokens { get; internal init; }

    /// <summary>Output tokens.</summary>
    [JsonInclude]
    [JsonPropertyName("output_tokens")]
    public long? OutputTokens { get; internal init; }

    /// <summary>Input tokens written to the prompt cache.</summary>
    [JsonInclude]
    [JsonPropertyName("cache_creation_input_tokens")]
    public long? CacheCreationInputTokens { get; internal init; }

    /// <summary>Input tokens read from the prompt cache.</summary>
    [JsonInclude]
    [JsonPropertyName("cache_read_input_tokens")]
    public long? CacheReadInputTokens { get; internal init; }

    /// <summary><see cref="CacheCreationInputTokens"/> split by how long each entry is kept.</summary>
    [JsonInclude]
    [JsonPropertyName("cache_creation")]
    public CacheCreationTokens? CacheCreation { get; internal init; }

    /// <summary><see cref="OutputTokens"/> split by what they were spent on.</summary>
    [JsonInclude]
    [JsonPropertyName("output_tokens_details")]
    public OutputTokensDetails? OutputTokensDetails { get; internal init; }

    /// <summary>How many requests the API's own server-side tools made.</summary>
    [JsonInclude]
    [JsonPropertyName("server_tool_use")]
    public ServerToolUsage? ServerToolUse { get; internal init; }

    /// <summary>The service tier that served the request.</summary>
    [JsonInclude]
    [JsonPropertyName("service_tier")]
    public ServiceTier? ServiceTier { get; internal init; }

    /// <summary>How fast the output tokens were generated.</summary>
    [JsonInclude]
    [JsonPropertyName("speed")]
    public InferenceSpeed? Speed { get; internal init; }

    /// <summary>The geographic region the request was served from (<c>inference_geo</c>); the empty string on a result whose turn made no API call.</summary>
    [JsonInclude]
    [JsonPropertyName("inference_geo")]
    public string? InferenceRegion { get; internal init; }

    /// <summary>One entry per sampling iteration of the request, in the order they ran; empty when the binary reported none.</summary>
    [JsonInclude]
    [JsonPropertyName("iterations")]
    public IReadOnlyList<UsageIteration> Iterations { get; internal init; } = [];

    /// <summary>What became of a fallback credit presented with the request; null when none was.</summary>
    [JsonInclude]
    [JsonPropertyName("fallback_credit")]
    public FallbackCredit? FallbackCredit { get; internal init; }
}

/// <summary>Tokens written to the prompt cache, split by how long the entry is kept.</summary>
public sealed class CacheCreationTokens
{
    [JsonConstructor]
    internal CacheCreationTokens()
    {
    }

    /// <summary>Input tokens written to a cache entry kept one hour.</summary>
    [JsonInclude]
    [JsonPropertyName("ephemeral_1h_input_tokens")]
    public long? Ephemeral1HourInputTokens { get; internal init; }

    /// <summary>Input tokens written to a cache entry kept five minutes.</summary>
    [JsonInclude]
    [JsonPropertyName("ephemeral_5m_input_tokens")]
    public long? Ephemeral5MinuteInputTokens { get; internal init; }
}

/// <summary>Output tokens split by what they were spent on. The split is for reading; the billed total stays <see cref="Usage.OutputTokens"/>.</summary>
public sealed class OutputTokensDetails
{
    [JsonConstructor]
    internal OutputTokensDetails()
    {
    }

    /// <summary>Output tokens the model spent thinking, delimiters included; already counted in <see cref="Usage.OutputTokens"/>.</summary>
    [JsonInclude]
    [JsonPropertyName("thinking_tokens")]
    public long? ThinkingTokens { get; internal init; }
}

/// <summary>How many requests the API's own server-side tools made while serving one request.</summary>
public sealed class ServerToolUsage
{
    [JsonConstructor]
    internal ServerToolUsage()
    {
    }

    /// <summary>Fetches the API's web fetch tool made.</summary>
    [JsonInclude]
    [JsonPropertyName("web_fetch_requests")]
    public long? WebFetchRequests { get; internal init; }

    /// <summary>Searches the API's web search tool made.</summary>
    [JsonInclude]
    [JsonPropertyName("web_search_requests")]
    public long? WebSearchRequests { get; internal init; }
}
