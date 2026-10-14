using System.Text.Json;
using System.Text.Json.Serialization;

namespace Claude.Agent;

/// <summary>What one sampling iteration of an API request spent. Which kind of iteration it was is the type, and serialized it is the <c>type</c> the binary gives the kind.</summary>
[JsonConverter(typeof(UsageIterationJsonConverter))]
public abstract class UsageIteration
{
    private protected UsageIteration()
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
}

/// <summary>A turn of the model itself: one call in a server-side tool loop. The last one of a request says how large the context had grown.</summary>
public sealed class MessageUsageIteration : UsageIteration
{
    [JsonConstructor]
    internal MessageUsageIteration()
    {
    }

    /// <summary>The model that ran the iteration; the binary leaves the key out, so this is null on every recording.</summary>
    [JsonInclude]
    [JsonPropertyName("model")]
    public string? Model { get; internal init; }
}

/// <summary>The work of summarising a context that was closed. Its tokens are the summary's own, not the closed context's, and are outside the request's totals.</summary>
public sealed class CompactionUsageIteration : UsageIteration
{
    [JsonConstructor]
    internal CompactionUsageIteration()
    {
    }
}

/// <summary>A turn of the advisor model.</summary>
public sealed class AdvisorMessageUsageIteration : UsageIteration
{
    [JsonConstructor]
    internal AdvisorMessageUsageIteration()
    {
    }

    /// <summary>The model that ran the iteration.</summary>
    [JsonInclude]
    [JsonPropertyName("model")]
    public string? Model { get; internal init; }
}

/// <summary>The turn of a fallback model that served the answer after the first choice would not.</summary>
public sealed class FallbackMessageUsageIteration : UsageIteration
{
    [JsonConstructor]
    internal FallbackMessageUsageIteration()
    {
    }

    /// <summary>The model that ran the iteration.</summary>
    [JsonInclude]
    [JsonPropertyName("model")]
    public string? Model { get; internal init; }
}

/// <summary>An iteration of a kind this library has no type for, or an entry that names no kind or is not an object: its kind's name and the entry as written. Serialized, the entry is its <c>raw</c> member.</summary>
public sealed class UnknownUsageIteration : UsageIteration
{
    [JsonConstructor]
    internal UnknownUsageIteration(string type, JsonElement raw)
    {
        Type = type;
        Raw = raw;
    }

    /// <summary>The entry's <c>type</c>; empty when it names none or is not an object.</summary>
    [JsonPropertyName("type")]
    public string Type { get; }

    /// <summary>The entry as the binary wrote it.</summary>
    [JsonPropertyName("raw")]
    public JsonElement Raw { get; }
}
