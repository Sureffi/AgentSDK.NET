using System.Collections.Immutable;
using System.Text.Json;

namespace Claude.Agent;

internal static class UsageParser
{
    public static Usage? ParseUsage(JsonElement? usage)
    {
        if (usage is not { ValueKind: JsonValueKind.Object } element)
        {
            return null;
        }

        return new Usage
        {
            InputTokens = element.Int64OrNull("input_tokens"),
            OutputTokens = element.Int64OrNull("output_tokens"),
            CacheCreationInputTokens = element.Int64OrNull("cache_creation_input_tokens"),
            CacheReadInputTokens = element.Int64OrNull("cache_read_input_tokens"),
            CacheCreation = CacheCreation(element.ElementOrNull("cache_creation")),
            OutputTokensDetails = OutputTokens(element.ElementOrNull("output_tokens_details")),
            ServerToolUse = ServerTools(element.ElementOrNull("server_tool_use")),
            ServiceTier = element.StringOrNull("service_tier") is { } tier ? new ServiceTier(tier) : (ServiceTier?)null,
            Speed = element.StringOrNull("speed") is { } speed ? new InferenceSpeed(speed) : (InferenceSpeed?)null,
            InferenceRegion = element.StringOrNull("inference_geo"),
            Iterations = Iterations(element.ElementOrNull("iterations")),
            FallbackCredit = FallbackCredit(element.ElementOrNull("fallback_credit")),
        };
    }

    public static Dictionary<string, ModelUsage> ModelUsages(JsonElement? element)
    {
        var map = new Dictionary<string, ModelUsage>();
        if (element is not { ValueKind: JsonValueKind.Object } usages)
        {
            return map;
        }

        foreach (var usage in usages.EnumerateObject())
        {
            if (usage.Value.ValueKind != JsonValueKind.Object || Identity.ModelId(usage.Name) is not { } model)
            {
                continue;
            }

            map[model] = new ModelUsage
            {
                InputTokens = usage.Value.Int64OrNull("inputTokens"),
                OutputTokens = usage.Value.Int64OrNull("outputTokens"),
                ThinkingTokens = usage.Value.Int64OrNull("thinkingTokens"),
                CacheReadInputTokens = usage.Value.Int64OrNull("cacheReadInputTokens"),
                CacheCreationInputTokens = usage.Value.Int64OrNull("cacheCreationInputTokens"),
                WebSearchRequests = usage.Value.Int64OrNull("webSearchRequests"),
                CostUsd = usage.Value.DecimalOrNull("costUSD"),
                CostBasis = usage.Value.StringOrNull("costBasis") is { } basis ? new ModelUsageCostBasis(basis) : (ModelUsageCostBasis?)null,
                ContextWindow = usage.Value.Int64OrNull("contextWindow"),
                MaxOutputTokens = usage.Value.Int64OrNull("maxOutputTokens"),
                CanonicalModel = Identity.ModelId(usage.Value.StringOrNull("canonicalModel")),
                Provider = usage.Value.StringOrNull("provider"),
            };
        }

        return map;
    }

    private static CacheCreationTokens? CacheCreation(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Object } cache
            ? new CacheCreationTokens
            {
                Ephemeral1HourInputTokens = cache.Int64OrNull("ephemeral_1h_input_tokens"),
                Ephemeral5MinuteInputTokens = cache.Int64OrNull("ephemeral_5m_input_tokens"),
            }
            : null;

    private static OutputTokensDetails? OutputTokens(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Object } details
            ? new OutputTokensDetails { ThinkingTokens = details.Int64OrNull("thinking_tokens") }
            : null;

    private static ServerToolUsage? ServerTools(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Object } tools
            ? new ServerToolUsage
            {
                WebFetchRequests = tools.Int64OrNull("web_fetch_requests"),
                WebSearchRequests = tools.Int64OrNull("web_search_requests"),
            }
            : null;

    // Every iteration of the request, in the order the API reported them. A kind with no type is the
    // unknown of the set, beside the entry as written.
    private static ImmutableArray<UsageIteration> Iterations(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Array } entries)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<UsageIteration>();
        foreach (var entry in entries.EnumerateArray())
        {
            builder.Add(Iteration(entry));
        }

        return builder.ToImmutable();
    }

    private static UsageIteration Iteration(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            return new UnknownUsageIteration(string.Empty, entry);
        }

        var input = entry.Int64OrNull("input_tokens");
        var output = entry.Int64OrNull("output_tokens");
        var written = entry.Int64OrNull("cache_creation_input_tokens");
        var read = entry.Int64OrNull("cache_read_input_tokens");
        var cache = CacheCreation(entry.ElementOrNull("cache_creation"));
        var model = Identity.ModelId(entry.StringOrNull("model"));

        return entry.StringOrNull("type") switch
        {
            "message" => new MessageUsageIteration
            {
                Model = model,
                InputTokens = input,
                OutputTokens = output,
                CacheCreationInputTokens = written,
                CacheReadInputTokens = read,
                CacheCreation = cache,
            },
            "compaction" => new CompactionUsageIteration
            {
                InputTokens = input,
                OutputTokens = output,
                CacheCreationInputTokens = written,
                CacheReadInputTokens = read,
                CacheCreation = cache,
            },
            "advisor_message" => new AdvisorMessageUsageIteration
            {
                Model = model,
                InputTokens = input,
                OutputTokens = output,
                CacheCreationInputTokens = written,
                CacheReadInputTokens = read,
                CacheCreation = cache,
            },
            "fallback_message" => new FallbackMessageUsageIteration
            {
                Model = model,
                InputTokens = input,
                OutputTokens = output,
                CacheCreationInputTokens = written,
                CacheReadInputTokens = read,
                CacheCreation = cache,
            },
            // The counts are the base's, and a sum over the iterations must not lose the ones whose
            // kind this library has no type for.
            var type => new UnknownUsageIteration(type ?? string.Empty, entry)
            {
                InputTokens = input,
                OutputTokens = output,
                CacheCreationInputTokens = written,
                CacheReadInputTokens = read,
                CacheCreation = cache,
            },
        };
    }

    private static FallbackCredit? FallbackCredit(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Object } credit)
        {
            return null;
        }

        return new FallbackCredit { Status = Status(credit.ElementOrNull("status")) };
    }

    private static FallbackCreditStatus? Status(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Object } status)
        {
            return null;
        }

        return status.StringOrNull("type") switch
        {
            "redeemed" => new RedeemedFallbackCreditStatus(),
            "not_applied" => new NotAppliedFallbackCreditStatus
            {
                Reason = status.StringOrNull("reason") is { } reason
                    ? new FallbackCreditNotAppliedReason(reason)
                    : (FallbackCreditNotAppliedReason?)null,
                FieldsToRemove = Strings(status.ElementOrNull("remove_to_redeem")),
            },
            var type => new UnknownFallbackCreditStatus(type ?? string.Empty, status),
        };
    }

    private static ImmutableArray<string> Strings(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Array } items)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                builder.Add(item.GetString() ?? string.Empty);
            }
        }

        return builder.ToImmutable();
    }
}
