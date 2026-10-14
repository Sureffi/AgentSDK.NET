using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace Claude.Agent;

// The shapes a message carries inside it: the API's own message, where a turn came from, what the
// context window holds, what the session has spent. Each read degrades on its own — a shape that
// does not fit is null where it stands, and the message is still handed out — except the API
// message on an assistant frame, which is that frame's whole point.
internal static class MessageShapeParser
{
    public static Result<ApiMessage, ParseError> RequireApiMessage(JsonElement message, string line, string path)
    {
        if (!Identity.RequireModel(message.StringOrNull("model"), $"{path}.model", line).TryGet(out var model, out var modelError))
        {
            return modelError;
        }

        if (message.ElementOrNull("content") is not { } content)
        {
            return JsonReading.Missing($"{path}.content", line);
        }

        if (!ContentBlockParser.ParseBlocks(content, line, $"{path}.content").TryGet(out var blocks, out var blockError))
        {
            return blockError;
        }

        return Build(message, blocks);
    }

    // The same shape where nothing requires it: a transcript's assistant line, whose content is the
    // blocks of the turn or the string a turn of one text block was written as.
    public static ApiMessage? ReadApiMessage(JsonElement? element, string line, string path)
    {
        if (element is not { ValueKind: JsonValueKind.Object } message
            || message.ElementOrNull("content") is not { } content
            || !ContentBlockParser.ParseContent(content, line, $"{path}.content").TryGet(out var blocks, out _))
        {
            return null;
        }

        return Build(message, blocks);
    }

    private static ApiMessage Build(JsonElement message, IReadOnlyList<ContentBlock> blocks) =>
        new(blocks, message.StringOrNull("model") ?? string.Empty)
        {
            Id = message.StringOrNull("id"),
            StopReason = Set(message.StringOrNull("stop_reason"), value => new StopReason(value)),
            StopSequence = message.StringOrNull("stop_sequence"),
            StopDetails = ReadStopDetails(message.ElementOrNull("stop_details")),
            Usage = UsageParser.ParseUsage(message.ElementOrNull("usage")),
            Container = ReadContainer(message.ElementOrNull("container")),
            ContextManagement = ReadContextManagement(message.ElementOrNull("context_management")),
            Diagnostics = ReadDiagnostics(message.ElementOrNull("diagnostics")),
            InputTransformations = Items(message.ElementOrNull("input_transformations"), ReadInputTransformation),
        };

    private static StopDetails? ReadStopDetails(JsonElement? element) =>
        Object(element) is not { } details
            ? null
            : new StopDetails(new StopDetailsType(details.StringOrNull("type") ?? string.Empty))
            {
                Category = Set(details.StringOrNull("category"), value => new RefusalCategory(value)),
                Explanation = details.StringOrNull("explanation"),
                FallbackCreditToken = details.StringOrNull("fallback_credit_token"),
                FallbackHasPrefillClaim = details.BoolOrNull("fallback_has_prefill_claim"),
                RecommendedModel = details.StringOrNull("recommended_model"),
            };

    private static MessageContainer? ReadContainer(JsonElement? element) =>
        Object(element) is not { } container
            ? null
            : new MessageContainer(container.StringOrNull("id") ?? string.Empty)
            {
                ExpiresAt = container.StringOrNull("expires_at"),
                Skills = Items(container.ElementOrNull("skills"), skill => new ContainerSkill(
                    skill.StringOrNull("skill_id") ?? string.Empty,
                    new ContainerSkillType(skill.StringOrNull("type") ?? string.Empty),
                    skill.StringOrNull("version") ?? string.Empty)),
            };

    private static ContextManagement? ReadContextManagement(JsonElement? element) =>
        Object(element) is not { } management
            ? null
            : new ContextManagement(Items(management.ElementOrNull("applied_edits"), edit =>
                new ContextEdit(
                    new ContextEditType(edit.StringOrNull("type") ?? string.Empty),
                    edit.Int64OrNull("cleared_input_tokens") ?? 0)
                {
                    ClearedToolUses = edit.Int64OrNull("cleared_tool_uses"),
                    ClearedThinkingTurns = edit.Int64OrNull("cleared_thinking_turns"),
                }));

    private static MessageDiagnostics? ReadDiagnostics(JsonElement? element)
    {
        if (Object(element) is not { } diagnostics)
        {
            return null;
        }

        var reason = Object(diagnostics.ElementOrNull("cache_miss_reason")) is { } miss
            ? new CacheMissReason(new CacheMissReasonType(miss.StringOrNull("type") ?? string.Empty))
            {
                CacheMissedInputTokens = miss.Int64OrNull("cache_missed_input_tokens"),
            }
            : null;

        return new MessageDiagnostics(reason);
    }

    private static InputTransformation ReadInputTransformation(JsonElement element) =>
        new(
            new InputTransformationType(element.StringOrNull("type") ?? string.Empty),
            element.StringOrNull("path") ?? string.Empty,
            new InputTransformationReason(element.StringOrNull("reason") ?? string.Empty));

    // Where a turn came from: one type per kind the binary names, and its own unknown, so a kind
    // this library has no type for is still handed over whole.
    public static MessageOrigin? ReadOrigin(JsonElement? element)
    {
        if (Object(element) is not { } origin)
        {
            return null;
        }

        var kind = origin.StringOrNull("kind") ?? string.Empty;
        return kind switch
        {
            "human" => new HumanOrigin(),
            "channel" => new ChannelOrigin(origin.StringOrNull("server") ?? string.Empty),
            "peer" => new PeerOrigin(origin.StringOrNull("from") ?? string.Empty)
            {
                FromMode = Set(origin.StringOrNull("fromMode"), value => new PeerOriginMode(value)),
                Name = origin.StringOrNull("name"),
                FromSession = origin.StringOrNull("fromSession"),
                SenderTaskId = origin.StringOrNull("senderTaskId"),
                Body = origin.StringOrNull("body"),
                VerifiedPeerProcessId = origin.Int64OrNull("verifiedPeerPid"),
            },
            "task-notification" => new TaskNotificationOrigin
            {
                Subkind = Set(origin.StringOrNull("subkind"), value => new TaskNotificationSource(value)),
            },
            "coordinator" => new CoordinatorOrigin(),
            "unclassified" => new UnclassifiedOrigin(),
            "observer" => new ObserverOrigin(
                origin.StringOrNull("from") ?? string.Empty,
                origin.StringOrNull("senderTaskId") ?? string.Empty),
            "auto-continuation" => new AutoContinuationOrigin(),
            "observer-activity" => new ObserverActivityOrigin(),
            _ => new UnknownMessageOrigin(kind, origin),
        };
    }

    public static MessageContextUsage? ReadContextUsage(JsonElement? element) =>
        Object(element) is not { } usage
            ? null
            : new MessageContextUsage(
                usage.StringOrNull("model") ?? string.Empty,
                usage.Int64OrNull("total_tokens") ?? 0,
                usage.Int64OrNull("raw_max_tokens") ?? 0,
                usage.DoubleOrNull("percentage") ?? 0)
            {
                OverLimit = Object(usage.ElementOrNull("over_limit")) is { } over
                    ? new ContextUsageLimit(
                        over.Int64OrNull("tokens_over") ?? 0,
                        new ContextUsageLimitKind(over.StringOrNull("kind") ?? string.Empty))
                    : null,
                Categories = Items(usage.ElementOrNull("categories"), category => new MessageContextUsageCategory(
                    category.StringOrNull("name") ?? string.Empty,
                    category.Int64OrNull("tokens") ?? 0,
                    new ContextSpace(category.StringOrNull("kind") ?? string.Empty))),
                McpTools = Items(usage.ElementOrNull("mcp_tools"), tool => new MessageContextUsageMcpTool(
                    tool.StringOrNull("name") ?? string.Empty,
                    tool.StringOrNull("server_name") ?? string.Empty,
                    tool.Int64OrNull("tokens") ?? 0)),
                MemoryFiles = Items(usage.ElementOrNull("memory_files"), file => new MessageContextUsageMemoryFile(
                    file.StringOrNull("path") ?? string.Empty,
                    file.StringOrNull("type") ?? string.Empty,
                    file.Int64OrNull("tokens") ?? 0)),
                Agents = Items(usage.ElementOrNull("agents"), agent => new MessageContextUsageAgent(
                    agent.StringOrNull("agent_type") ?? string.Empty,
                    agent.StringOrNull("source") ?? string.Empty,
                    agent.Int64OrNull("tokens") ?? 0)),
                Skills = Items(usage.ElementOrNull("skills"), skill => new MessageContextUsageSkill(
                    skill.StringOrNull("name") ?? string.Empty,
                    skill.StringOrNull("source") ?? string.Empty,
                    skill.Int64OrNull("tokens") ?? 0)
                {
                    PluginName = skill.StringOrNull("plugin_name"),
                }),
            };

    public static UsageReport? ReadUsageReport(JsonElement? element)
    {
        if (Object(element) is not { } report || Object(report.ElementOrNull("session")) is not { } session)
        {
            return null;
        }

        return new UsageReport(new UsageReportSession(
            session.DecimalOrNull("total_cost_usd") ?? 0,
            WireTime.Milliseconds(session.Int64OrNull("total_api_duration_ms") ?? 0) ?? TimeSpan.Zero,
            WireTime.Milliseconds(session.Int64OrNull("total_duration_ms") ?? 0) ?? TimeSpan.Zero,
            session.Int64OrNull("total_lines_added") ?? 0,
            session.Int64OrNull("total_lines_removed") ?? 0)
        {
            ModelUsage = UsageParser.ModelUsages(session.ElementOrNull("model_usage")),
        })
        {
            RateLimits = Object(report.ElementOrNull("rate_limits")) is { } limits
                ? new UsageReportRateLimits
                {
                    Limits = Items(limits.ElementOrNull("limits"), ReadUsageReportLimit),
                    ExtraUsage = Object(limits.ElementOrNull("extra_usage")) is { } extra
                        ? new UsageReportExtraUsage(extra.BoolOrNull("is_enabled") ?? false)
                        {
                            MonthlyLimit = extra.DoubleOrNull("monthly_limit"),
                            UsedCredits = extra.DoubleOrNull("used_credits"),
                            Utilization = extra.DoubleOrNull("utilization"),
                            Currency = extra.StringOrNull("currency"),
                        }
                        : null,
                }
                : null,
        };
    }

    private static UsageReportLimit ReadUsageReportLimit(JsonElement limit) =>
        new(
            limit.StringOrNull("kind") ?? string.Empty,
            limit.StringOrNull("group") ?? string.Empty,
            limit.DoubleOrNull("percent") ?? 0)
        {
            ResetsAt = limit.StringOrNull("resets_at"),
            Scope = Object(limit.ElementOrNull("scope")) is { } scope
                ? new UsageReportLimitScope
                {
                    Model = ReadTarget(scope.ElementOrNull("model")),
                    Surface = ReadTarget(scope.ElementOrNull("surface")),
                }
                : null,
            Severity = limit.StringOrNull("severity"),
            IsActive = limit.BoolOrNull("is_active"),
        };

    private static UsageReportTarget? ReadTarget(JsonElement? element) =>
        Object(element) is { } target ? new UsageReportTarget(target.StringOrNull("display_name") ?? string.Empty) : null;

    public static SubagentStatistics? ReadSubagentStatistics(JsonElement? element)
    {
        if (Object(element) is not { } statistics)
        {
            return null;
        }

        return new SubagentStatistics(
            statistics.Int64OrNull("spawned") ?? 0,
            statistics.Int64OrNull("spawned_by_subagents") ?? 0,
            statistics.Int64OrNull("started_in_background") ?? 0,
            statistics.Int64OrNull("completed") ?? 0,
            statistics.Int64OrNull("failed") ?? 0,
            statistics.Int64OrNull("max_depth") ?? 0)
        {
            Requested = Object(statistics.ElementOrNull("requested")) is { } requested
                ? new SubagentRequestCounts(
                    requested.Int64OrNull("background") ?? 0,
                    requested.Int64OrNull("foreground") ?? 0,
                    requested.Int64OrNull("unset") ?? 0)
                : null,
            Killed = Object(statistics.ElementOrNull("killed")) is { } killed
                ? new SubagentKillCounts(
                    killed.Int64OrNull("parent") ?? 0,
                    killed.Int64OrNull("user") ?? 0,
                    killed.Int64OrNull("system") ?? 0)
                : null,
            Refused = Object(statistics.ElementOrNull("refused")) is { } refused
                ? new SubagentRefusalCounts(
                    refused.Int64OrNull("budget") ?? 0,
                    refused.Int64OrNull("concurrency_limit") ?? 0,
                    refused.Int64OrNull("depth_limit") ?? 0)
                : null,
            ByType = Counts(statistics.ElementOrNull("by_type")),
        };
    }

    public static IReadOnlyList<ToolUseDisplay> ReadToolUseDisplays(JsonElement? element) =>
        Items(element, display => new ToolUseDisplay(
            display.StringOrNull("id") ?? string.Empty,
            display.StringOrNull("display_name") ?? string.Empty,
            display.StringOrNull("server_display_name") ?? string.Empty));

    public static IReadOnlyList<ToolResultNote> ReadToolResultNotes(JsonElement? element) =>
        Items(element, note => new ToolResultNote(
            note.StringOrNull("id") ?? string.Empty,
            note.StringOrNull("non_execution_kind") ?? string.Empty));

    public static IReadOnlyList<ResourceLink> ReadResourceLinks(JsonElement? element) =>
        Items(element, link => new ResourceLink(
            link.StringOrNull("uri") ?? string.Empty,
            link.StringOrNull("name") ?? string.Empty)
        {
            Title = link.StringOrNull("title"),
            Description = link.StringOrNull("description"),
            MimeType = link.StringOrNull("mimeType"),
            Size = link.Int64OrNull("size"),
        });

    public static IReadOnlyDictionary<string, RateLimitWindow> ReadRateLimitWindows(JsonElement? element)
    {
        if (Object(element) is not { } windows)
        {
            return ImmutableDictionary<string, RateLimitWindow>.Empty;
        }

        var builder = ImmutableDictionary.CreateBuilder<string, RateLimitWindow>(StringComparer.Ordinal);
        foreach (var window in windows.EnumerateObject())
        {
            if (window.Value.ValueKind == JsonValueKind.Object)
            {
                builder[window.Name] = new RateLimitWindow
                {
                    Utilization = window.Value.DoubleOrNull("utilization"),
                    ResetsAt = WireTime.UnixSeconds(window.Value.Int64OrNull("resetsAt")),
                };
            }
        }

        return builder.ToImmutable();
    }

    // A tool's arguments as they went to the model, by tool-use id. The typed input of each sits on
    // the tool-use block of the same id, so this is the raw beside it.
    public static IReadOnlyDictionary<string, JsonElement> ReadWireToolInputs(JsonElement? element)
    {
        if (Object(element) is not { } inputs)
        {
            return ImmutableDictionary<string, JsonElement>.Empty;
        }

        var builder = ImmutableDictionary.CreateBuilder<string, JsonElement>(StringComparer.Ordinal);
        foreach (var input in inputs.EnumerateObject())
        {
            builder[input.Name] = input.Value;
        }

        return builder.ToImmutable();
    }

    public static IReadOnlyDictionary<string, long> Counts(JsonElement? element)
    {
        if (Object(element) is not { } counts)
        {
            return ImmutableDictionary<string, long>.Empty;
        }

        var builder = ImmutableDictionary.CreateBuilder<string, long>(StringComparer.Ordinal);
        foreach (var count in counts.EnumerateObject())
        {
            if (count.Value.ValueKind == JsonValueKind.Number && count.Value.TryGetInt64(out var number))
            {
                builder[count.Name] = number;
            }
        }

        return builder.ToImmutable();
    }

    public static IReadOnlyList<string> Strings(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Array } array)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                builder.Add(item.GetString() ?? string.Empty);
            }
        }

        return builder.ToImmutable();
    }

    public static IReadOnlyList<T> Items<T>(JsonElement? element, Func<JsonElement, T> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        if (element is not { ValueKind: JsonValueKind.Array } array)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<T>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                builder.Add(read(item));
            }
        }

        return builder.ToImmutable();
    }

    // A closed-set member read off the wire: the value as it stands when there is one, and no value
    // at all when the key is absent. Reading is not calling, so nothing here refuses a value.
    public static T? Set<T>(string? value, Func<string, T> make)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(make);
        return value is null ? null : make(value);
    }

    private static JsonElement? Object(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Object } value ? value : null;
}

// Numbers on the wire that are times: what holds them, and what makes no sense as one.
internal static class WireTime
{
    private const long MaxMilliseconds = long.MaxValue / TimeSpan.TicksPerMillisecond;

    private const long MinUnixSeconds = -62_135_596_800;

    private const long MaxUnixSeconds = 253_402_300_799;

    private const long MinUnixMilliseconds = MinUnixSeconds * 1000;

    private const long MaxUnixMilliseconds = (MaxUnixSeconds * 1000) + 999;

    // A count of milliseconds as a duration, null when no TimeSpan holds it: a number out of the
    // member's range is a misfit, as one of the wrong kind is.
    public static TimeSpan? Milliseconds(long milliseconds) =>
        milliseconds is >= -MaxMilliseconds and <= MaxMilliseconds
            ? TimeSpan.FromTicks(milliseconds * TimeSpan.TicksPerMillisecond)
            : null;

    public static TimeSpan? Milliseconds(long? milliseconds) =>
        milliseconds is { } value ? Milliseconds(value) : null;

    public static TimeSpan? Seconds(double? seconds) =>
        seconds is { } value && double.IsFinite(value) && Math.Abs(value) < TimeSpan.MaxValue.TotalSeconds
            ? TimeSpan.FromSeconds(value)
            : null;

    // Seconds since the Unix epoch as an instant, null when no DateTimeOffset holds it.
    public static DateTimeOffset? UnixSeconds(long? seconds) =>
        seconds is { } value and >= MinUnixSeconds and <= MaxUnixSeconds
            ? DateTimeOffset.FromUnixTimeSeconds(value)
            : null;

    public static DateTimeOffset? UnixMilliseconds(long? milliseconds) =>
        milliseconds is { } value and >= MinUnixMilliseconds and <= MaxUnixMilliseconds
            ? DateTimeOffset.FromUnixTimeMilliseconds(value)
            : null;

    // A time the binary wrote as text, null when it is not one.
    public static DateTimeOffset? Text(string? value) =>
        value is not null
        && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
}
