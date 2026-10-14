using System.Collections.Immutable;
using System.Text.Json;

namespace Claude.Agent;

internal sealed class PayloadParser : IPayloadParser
{
    private static readonly ServerInfo Unknown = new();

    public Result<ServerInfo, ParseError> ServerInfo(JsonElement? payload)
    {
        if (Body(payload) is not { } body)
        {
            return Unknown;
        }

        return new ServerInfo
        {
            Commands = Commands(body.ElementOrNull("commands")),
            Agents = Agents(body.ElementOrNull("agents")),
            OutputStyle = body.StringOrNull("output_style"),
            AvailableOutputStyles = Strings(body.ElementOrNull("available_output_styles")),
            UserOutputStylesDirectory = body.StringOrNull("user_output_styles_dir"),
            Models = Models(body.ElementOrNull("models")),
            Account = Account(body.ElementOrNull("account")),
            HooksApplied = body.BoolOrNull("hooks_applied"),
            PluginsApplied = body.BoolOrNull("plugins_applied"),
            FastModeState = body.StringOrNull("fast_mode_state") is { } fastMode ? new FastModeState(fastMode) : (FastModeState?)null,
            FastModeDisabledReason = body.StringOrNull("fast_mode_disabled_reason") is { } disabled
                ? new FastModeDisabledReason(disabled)
                : (FastModeDisabledReason?)null,
            ProcessId = body.Int64OrNull("pid"),
            PermissionMode = body.StringOrNull("current_permission_mode") is { } mode ? new PermissionMode(mode) : (PermissionMode?)null,
            SessionState = body.StringOrNull("session_state") is { } state ? new SessionState(state) : (SessionState?)null,
            AnalyticsDisabled = body.BoolOrNull("analytics_disabled"),
            RemoteControlAvailable = body.BoolOrNull("remote_control_available"),
            RemoteControlAutoEnable = body.BoolOrNull("remote_control_auto_enable"),
            RemoteControlAutoOnByDefault = body.BoolOrNull("remote_control_auto_on_by_default"),
            RemoteControlAutoConnectDefault = body.BoolOrNull("remote_control_auto_connect_default"),
            IdeRemoteControlAutoEnableGate = body.BoolOrNull("ide_rc_auto_enable_gate"),
            Capabilities = Strings(body.ElementOrNull("capabilities")),
            FeedbackMode = FeedbackMode(body.ElementOrNull("feedback_mode")),
        };
    }

    public Result<ContextUsage, ParseError> ContextUsage(JsonElement? payload)
    {
        if (Body(payload) is not { } body)
        {
            return Missing(payload, "response");
        }

        if (Identity.ModelId(body.StringOrNull("model")) is not { } model)
        {
            return Missing(payload, "model");
        }

        return new ContextUsage(model)
        {
            Categories = Categories(body.ElementOrNull("categories")),
            TotalTokens = body.Int64OrNull("totalTokens") ?? 0,
            MaxTokens = body.Int64OrNull("maxTokens") ?? 0,
            RawMaxTokens = body.Int64OrNull("rawMaxTokens"),
            Percentage = body.DoubleOrNull("percentage") ?? 0,
            GridRows = GridRows(body.ElementOrNull("gridRows")),
            MemoryFiles = Items(body.ElementOrNull("memoryFiles"), MemoryFile),
            McpTools = Items(body.ElementOrNull("mcpTools"), McpTool),
            DeferredBuiltInTools = OptionalItems(body.ElementOrNull("deferredBuiltinTools"), DeferredTool),
            SystemTools = OptionalItems(body.ElementOrNull("systemTools"), SystemTool),
            SystemPromptSections = OptionalItems(body.ElementOrNull("systemPromptSections"), PromptSection),
            Agents = Items(body.ElementOrNull("agents"), ContextAgent),
            SlashCommands = SlashCommands(body.ElementOrNull("slashCommands")),
            Skills = Skills(body.ElementOrNull("skills")),
            AutoCompactThreshold = body.Int64OrNull("autoCompactThreshold"),
            IsAutoCompactEnabled = body.BoolOrNull("isAutoCompactEnabled") ?? false,
            AutoCompactSource = body.StringOrNull("autocompactSource"),
            MessageBreakdown = MessageBreakdown(body.ElementOrNull("messageBreakdown")),
            ApiUsage = ApiTokens(body.ElementOrNull("apiUsage")),
        };
    }

    public Result<IReadOnlyList<McpServerStatus>, ParseError> McpStatus(JsonElement? payload)
    {
        if (Body(payload) is not { } body)
        {
            return Missing(payload, "response");
        }

        if (!body.TryProperty("mcpServers", out var rawServers) || rawServers.ValueKind != JsonValueKind.Array)
        {
            return Missing("mcpServers");
        }

        var servers = ImmutableArray.CreateBuilder<McpServerStatus>();
        foreach (var server in rawServers.EnumerateArray())
        {
            if (Identity.ServerName(server.StringOrNull("name")) is not { } name)
            {
                continue;
            }

            servers.Add(new McpServerStatus(name, new McpServerConnectionStatus(server.StringOrNull("status") ?? string.Empty))
            {
                ServerInfo = server.ElementOrNull("serverInfo") is { ValueKind: JsonValueKind.Object } info
                    ? new McpServerInfo { Name = info.StringOrNull("name"), Version = info.StringOrNull("version") }
                    : null,
                Error = server.StringOrNull("error"),
                Config = server.ElementOrNull("config") is { } config ? McpServerConfigurationParser.Read(config) : null,
                Scope = server.StringOrNull("scope") is { } scope ? new McpServerSource(scope) : (McpServerSource?)null,
                Source = server.StringOrNull("source") is { } source ? new McpServerSource(source) : (McpServerSource?)null,
                Tools = Items(server.ElementOrNull("tools"), ServerTool),
            });
        }

        return servers.ToImmutable();
    }

    public Result<PermissionMode, ParseError> PermissionMode(JsonElement? payload)
    {
        if (Body(payload) is not { } body)
        {
            return Missing(payload, "response");
        }

        return body.StringOrNull("mode") is { } mode ? new PermissionMode(mode) : Missing("mode");
    }

    public Result<RewindVerdict, ParseError> Rewind(JsonElement? payload)
    {
        if (Body(payload) is not { } body)
        {
            return Missing(payload, "response");
        }

        return body.BoolOrNull("canRewind") is { } canRewind
            ? new RewindVerdict(
                canRewind,
                Strings(body.ElementOrNull("filesChanged")),
                body.Int64OrNull("insertions"),
                body.Int64OrNull("deletions"),
                body.Int64OrNull("skippedLinks"),
                body.StringOrNull("error"))
            : Missing("canRewind");
    }

    public Result<InterruptReceipt, ParseError> Interrupt(JsonElement? payload)
    {
        if (Body(payload) is not { } body)
        {
            return payload is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined }
                ? JsonReading.Invalid("response", "expected an object.", string.Empty)
                : new InterruptReceipt([], null);
        }

        var stillQueued = body.ElementOrNull("still_queued");
        if (stillQueued is { ValueKind: not JsonValueKind.Array })
        {
            return JsonReading.Invalid("still_queued", "expected an array.", string.Empty);
        }

        var cancelled = body.ElementOrNull("cancelled");
        if (cancelled is { ValueKind: not JsonValueKind.Array })
        {
            return JsonReading.Invalid("cancelled", "expected an array.", string.Empty);
        }

        return new InterruptReceipt(Strings(stillQueued), cancelled is null ? null : Strings(cancelled));
    }

    private static JsonElement? Body(JsonElement? payload) =>
        payload is { ValueKind: JsonValueKind.Object } body ? body : null;

    private static ParseError Missing(JsonElement? payload, string key) =>
        payload is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined }
            ? JsonReading.Invalid(key, "expected an object.", string.Empty)
            : JsonReading.Missing(key, string.Empty);

    private static ParseError Missing(string key) => JsonReading.Missing(key, string.Empty);

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

    // Every item a reader makes something of; one it makes nothing of is left out.
    private static ImmutableArray<T> Items<T>(JsonElement? element, Func<JsonElement, T?> read)
        where T : class
    {
        if (element is not { ValueKind: JsonValueKind.Array } items)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<T>();
        foreach (var item in items.EnumerateArray())
        {
            if (read(item) is { } value)
            {
                builder.Add(value);
            }
        }

        return builder.ToImmutable();
    }

    // The same, where the binary leaving the key out means something other than an empty list.
    private static T[]? OptionalItems<T>(JsonElement? element, Func<JsonElement, T?> read)
        where T : class =>
        element is { ValueKind: JsonValueKind.Array } ? [.. Items(element, read)] : null;

    private static ImmutableArray<SlashCommandInfo> Commands(JsonElement? element) =>
        Items(element, command => command.StringOrNull("name") is { } name
            && command.StringOrNull("description") is { } description
            && command.StringOrNull("argumentHint") is { } argumentHint
            ? new SlashCommandInfo(name, description, argumentHint)
            {
                Aliases = Strings(command.ElementOrNull("aliases")),
                IsBuiltIn = command.BoolOrNull("builtin"),
            }
            : null);

    private static ImmutableArray<AgentInfo> Agents(JsonElement? element) =>
        Items(element, agent => Identity.Agent(agent.StringOrNull("name")) is { } name
            ? new AgentInfo(name)
            {
                Description = agent.StringOrNull("description"),
                Model = agent.StringOrNull("model"),
            }
            : null);

    private static ImmutableArray<ModelInfo> Models(JsonElement? element) =>
        Items(element, model => Identity.ModelId(model.StringOrNull("value")) is { } value
            ? new ModelInfo(value)
            {
                ResolvedModel = Identity.ModelId(model.StringOrNull("resolvedModel")),
                DisplayName = model.StringOrNull("displayName"),
                Description = model.StringOrNull("description"),
                SupportsEffort = model.BoolOrNull("supportsEffort"),
                SupportedEffortLevels = [.. Strings(model.ElementOrNull("supportedEffortLevels")).Select(level => new Effort(level))],
                SupportsAdaptiveThinking = model.BoolOrNull("supportsAdaptiveThinking"),
                SupportsFastMode = model.BoolOrNull("supportsFastMode"),
                SupportsAutoMode = model.BoolOrNull("supportsAutoMode"),
            }
            : null);

    private static FeedbackMode? FeedbackMode(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Object } mode
            ? new FeedbackMode
            {
                Kind = mode.StringOrNull("kind") is { } kind ? new FeedbackModeKind(kind) : (FeedbackModeKind?)null,
            }
            : null;

    private static AccountInfo? Account(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Object } account
            ? new AccountInfo
            {
                Email = account.StringOrNull("email"),
                Organization = account.StringOrNull("organization"),
                SubscriptionType = account.StringOrNull("subscriptionType"),
                TokenSource = account.StringOrNull("tokenSource"),
                ApiKeySource = account.StringOrNull("apiKeySource") is { } key ? new ApiKeySource(key) : (ApiKeySource?)null,
                ApiProvider = account.StringOrNull("apiProvider") is { } provider ? new ApiProvider(provider) : (ApiProvider?)null,
            }
            : null;

    private static ImmutableArray<ContextUsageCategory> Categories(JsonElement? element) =>
        Items(element, category => category.StringOrNull("name") is { } name
            ? new ContextUsageCategory(name)
            {
                Tokens = category.Int64OrNull("tokens"),
                Color = category.StringOrNull("color"),
                IsDeferred = category.BoolOrNull("isDeferred"),
                Kind = category.StringOrNull("kind") is { } kind ? new ContextSpace(kind) : (ContextSpace?)null,
            }
            : null);

    private static ImmutableArray<IReadOnlyList<ContextUsageGridSquare>> GridRows(JsonElement? element) =>
        Items<IReadOnlyList<ContextUsageGridSquare>>(element, Row);

    private static IReadOnlyList<ContextUsageGridSquare>? Row(JsonElement row) =>
        row.ValueKind == JsonValueKind.Array ? Items(row, GridSquare) : null;

    private static ContextUsageGridSquare? GridSquare(JsonElement square) =>
        square.ValueKind == JsonValueKind.Object
            ? new ContextUsageGridSquare
            {
                Color = square.StringOrNull("color"),
                IsFilled = square.BoolOrNull("isFilled"),
                CategoryName = square.StringOrNull("categoryName"),
                Tokens = square.Int64OrNull("tokens"),
                Percentage = square.DoubleOrNull("percentage"),
                SquareFullness = square.DoubleOrNull("squareFullness"),
            }
            : null;

    private static ContextUsageMemoryFile? MemoryFile(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
            ? new ContextUsageMemoryFile
            {
                Path = element.StringOrNull("path"),
                Type = element.StringOrNull("type"),
                Tokens = element.Int64OrNull("tokens"),
            }
            : null;

    private static ContextUsageMcpTool? McpTool(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
            ? new ContextUsageMcpTool
            {
                Name = element.StringOrNull("name"),
                ServerName = element.StringOrNull("serverName"),
                Tokens = element.Int64OrNull("tokens"),
                IsLoaded = element.BoolOrNull("isLoaded"),
            }
            : null;

    private static ContextUsageDeferredTool? DeferredTool(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
            ? new ContextUsageDeferredTool
            {
                Name = element.StringOrNull("name"),
                Tokens = element.Int64OrNull("tokens"),
                IsLoaded = element.BoolOrNull("isLoaded"),
            }
            : null;

    private static ContextUsageSystemTool? SystemTool(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
            ? new ContextUsageSystemTool { Name = element.StringOrNull("name"), Tokens = element.Int64OrNull("tokens") }
            : null;

    private static ContextUsageSystemPromptSection? PromptSection(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
            ? new ContextUsageSystemPromptSection { Name = element.StringOrNull("name"), Tokens = element.Int64OrNull("tokens") }
            : null;

    private static ContextUsageAgent? ContextAgent(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
            ? new ContextUsageAgent
            {
                AgentType = element.StringOrNull("agentType"),
                Source = element.StringOrNull("source"),
                Tokens = element.Int64OrNull("tokens"),
            }
            : null;

    private static ContextUsageSlashCommands? SlashCommands(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Object } commands
            ? new ContextUsageSlashCommands
            {
                TotalCommands = commands.Int64OrNull("totalCommands"),
                IncludedCommands = commands.Int64OrNull("includedCommands"),
                Tokens = commands.Int64OrNull("tokens"),
            }
            : null;

    private static ContextUsageSkills? Skills(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Object } skills
            ? new ContextUsageSkills
            {
                TotalSkills = skills.Int64OrNull("totalSkills"),
                IncludedSkills = skills.Int64OrNull("includedSkills"),
                Tokens = skills.Int64OrNull("tokens"),
                SkillFrontmatter = Items(skills.ElementOrNull("skillFrontmatter"), skill => skill.ValueKind == JsonValueKind.Object
                    ? new ContextUsageSkill
                    {
                        Name = skill.StringOrNull("name"),
                        Source = skill.StringOrNull("source"),
                        Tokens = skill.Int64OrNull("tokens"),
                    }
                    : null),
            }
            : null;

    private static ContextUsageMessageBreakdown? MessageBreakdown(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Object } breakdown
            ? new ContextUsageMessageBreakdown
            {
                ToolCallTokens = breakdown.Int64OrNull("toolCallTokens"),
                ToolResultTokens = breakdown.Int64OrNull("toolResultTokens"),
                AttachmentTokens = breakdown.Int64OrNull("attachmentTokens"),
                AssistantMessageTokens = breakdown.Int64OrNull("assistantMessageTokens"),
                UserMessageTokens = breakdown.Int64OrNull("userMessageTokens"),
                RedirectedContextTokens = breakdown.Int64OrNull("redirectedContextTokens"),
                UnattributedTokens = breakdown.Int64OrNull("unattributedTokens"),
                ToolCallsByType = Items(breakdown.ElementOrNull("toolCallsByType"), call => call.ValueKind == JsonValueKind.Object
                    ? new ContextUsageToolCallTokens
                    {
                        Name = call.StringOrNull("name"),
                        CallTokens = call.Int64OrNull("callTokens"),
                        ResultTokens = call.Int64OrNull("resultTokens"),
                    }
                    : null),
                AttachmentsByType = Items(breakdown.ElementOrNull("attachmentsByType"), attachment => attachment.ValueKind == JsonValueKind.Object
                    ? new ContextUsageAttachmentTokens
                    {
                        Name = attachment.StringOrNull("name"),
                        Tokens = attachment.Int64OrNull("tokens"),
                    }
                    : null),
            }
            : null;

    private static ContextUsageApiTokens? ApiTokens(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Object } usage
            ? new ContextUsageApiTokens
            {
                InputTokens = usage.Int64OrNull("input_tokens"),
                OutputTokens = usage.Int64OrNull("output_tokens"),
                CacheCreationInputTokens = usage.Int64OrNull("cache_creation_input_tokens"),
                CacheReadInputTokens = usage.Int64OrNull("cache_read_input_tokens"),
            }
            : null;

    private static McpServerTool? ServerTool(JsonElement element) =>
        Identity.Tool(element.StringOrNull("name")) is { } name
            ? new McpServerTool(name)
            {
                Description = element.StringOrNull("description"),
                Annotations = element.ElementOrNull("annotations") is { ValueKind: JsonValueKind.Object } annotations
                    ? new McpToolAnnotations
                    {
                        IsReadOnly = annotations.BoolOrNull("readOnly"),
                        IsDestructive = annotations.BoolOrNull("destructive"),
                        IsOpenWorld = annotations.BoolOrNull("openWorld"),
                    }
                    : null,
            }
            : null;
}
