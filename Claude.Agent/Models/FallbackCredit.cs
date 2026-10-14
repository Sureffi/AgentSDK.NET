using System.Text.Json;
using System.Text.Json.Serialization;

namespace Claude.Agent;

/// <summary>What became of a fallback credit presented with an API request: a credit reprices a retry as if the conversation had been on the retry's model all along.</summary>
public sealed class FallbackCredit
{
    [JsonConstructor]
    internal FallbackCredit()
    {
    }

    /// <summary>Whether the reprice was applied, and when it was not, why.</summary>
    [JsonInclude]
    [JsonPropertyName("status")]
    public FallbackCreditStatus? Status { get; internal init; }
}

/// <summary>Whether a fallback credit repriced the request. Which it was is the type, and serialized it is the <c>type</c> the binary gives the outcome.</summary>
[JsonConverter(typeof(FallbackCreditStatusJsonConverter))]
public abstract class FallbackCreditStatus
{
    private protected FallbackCreditStatus()
    {
    }
}

/// <summary>The reprice was applied, the shift being zero included.</summary>
public sealed class RedeemedFallbackCreditStatus : FallbackCreditStatus
{
    [JsonConstructor]
    internal RedeemedFallbackCreditStatus()
    {
    }
}

/// <summary>The reprice was not applied; the request was billed at its own price.</summary>
public sealed class NotAppliedFallbackCreditStatus : FallbackCreditStatus
{
    [JsonConstructor]
    internal NotAppliedFallbackCreditStatus()
    {
    }

    /// <summary>Why the reprice was not applied.</summary>
    [JsonInclude]
    [JsonPropertyName("reason")]
    public FallbackCreditNotAppliedReason? Reason { get; internal init; }

    /// <summary>The request's own fields to drop before retrying, so the retry can redeem the credit; sent only when the reason is that such fields were present.</summary>
    [JsonInclude]
    [JsonPropertyName("remove_to_redeem")]
    public IReadOnlyList<string> FieldsToRemove { get; internal init; } = [];
}

/// <summary>A fallback-credit outcome of a kind this library has no type for, or one that names no kind: its kind's name and the object as written. Serialized, the object is its <c>raw</c> member.</summary>
public sealed class UnknownFallbackCreditStatus : FallbackCreditStatus
{
    [JsonConstructor]
    internal UnknownFallbackCreditStatus(string type, JsonElement raw)
    {
        Type = type;
        Raw = raw;
    }

    /// <summary>The object's <c>type</c>; empty when it names none.</summary>
    [JsonPropertyName("type")]
    public string Type { get; }

    /// <summary>The object as the binary wrote it.</summary>
    [JsonPropertyName("raw")]
    public JsonElement Raw { get; }
}
