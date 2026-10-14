using System.Collections.Immutable;
using System.Text.Json;

namespace Claude.Agent;

internal static class MessageParser
{
    public static Result<AgentMessage, ParseError> Parse(Frame frame, JsonElement raw, string line) => frame switch
    {
        UserFrame user => ParseUser(user, raw, line),
        AssistantFrame assistant => ParseAssistant(assistant, raw, line),
        SystemFrame system => ParseSystem(system, raw, line),
        ResultFrame result => ParseResult(result, raw, line),
        StreamEventFrame stream => ParseStreamEvent(stream, raw, line),
        RateLimitEventFrame rateLimit => ParseRateLimitEvent(rateLimit, raw, line),
        ConversationResetFrame reset => ParseConversationReset(reset, raw, line),
        CommandLifecycleFrame lifecycle => ParseCommandLifecycle(lifecycle, raw, line),
        ToolProgressFrame => ParseToolProgress(raw, line),
        ToolUseSummaryFrame => ParseToolUseSummary(raw, line),
        AuthStatusFrame => ParseAuthStatus(raw),
        PromptSuggestionFrame => ParsePromptSuggestion(raw, line),
        ActiveGoalFrame => ParseActiveGoal(raw),
        _ => JsonReading.Invalid("type", "not a message frame.", line),
    };

    private static Result<AgentMessage, ParseError> ParseCommandLifecycle(
        CommandLifecycleFrame frame,
        JsonElement raw,
        string line)
    {
        if (!Identity.RequireMessage(frame.CommandUuid, "command_uuid", line).TryGet(out var commandUuid, out var commandError))
        {
            return commandError;
        }

        if (!Identity.RequireText(frame.State, "state", line).TryGet(out var state, out var stateError))
        {
            return stateError;
        }

        return new CommandLifecycleMessage(
            commandUuid,
            new CommandLifecycleState(state),
            Identity.Message(frame.Uuid),
            Identity.Session(frame.SessionId),
            raw);
    }

    private static Result<AgentMessage, ParseError> ParseUser(UserFrame frame, JsonElement raw, string line)
    {
        if (raw.ElementOrNull("message") is not { ValueKind: JsonValueKind.Object } body)
        {
            return JsonReading.Missing("message", line);
        }

        if (body.ElementOrNull("content") is not { } content)
        {
            return JsonReading.Missing("message.content", line);
        }

        if (!ContentBlockParser.ParseContent(content, line, "message.content").TryGet(out var blocks, out var contentError))
        {
            return contentError;
        }

        return new UserMessage(blocks, Identity.Message(frame.Uuid), Identity.Session(frame.SessionId), raw)
        {
            Role = MessageShapeParser.Set(body.StringOrNull("role"), value => new MessageRole(value)),
            IsReplay = raw.BoolOrNull("isReplay") ?? false,
            ParentToolUseId = Identity.ToolUse(frame.ParentToolUseId),
            RawToolOutput = frame.ToolUseResult,
            IsSynthetic = raw.BoolOrNull("isSynthetic"),
            Priority = MessageShapeParser.Set(raw.StringOrNull("priority"), value => new UserPriority(value)),
            Origin = MessageShapeParser.ReadOrigin(frame.Origin),
            ShouldQuery = raw.BoolOrNull("shouldQuery"),
            Timestamp = WireTime.Text(raw.StringOrNull("timestamp")),
            SubagentType = raw.StringOrNull("subagent_type"),
            TaskDescription = raw.StringOrNull("task_description"),
            ToolResultNotes = MessageShapeParser.ReadToolResultNotes(raw.ElementOrNull("tool_result_meta")),
        };
    }

    private static Result<AgentMessage, ParseError> ParseAssistant(
        AssistantFrame frame,
        JsonElement raw,
        string line)
    {
        if (raw.ElementOrNull("message") is not { ValueKind: JsonValueKind.Object } body)
        {
            return JsonReading.Missing("message", line);
        }

        if (!MessageShapeParser.RequireApiMessage(body, line, "message").TryGet(out var message, out var error))
        {
            return error;
        }

        return new AssistantMessage(message, Identity.Message(frame.Uuid), Identity.Session(frame.SessionId), raw)
        {
            ParentToolUseId = Identity.ToolUse(frame.ParentToolUseId),
            Error = MessageShapeParser.Set(
                frame.Error is { ValueKind: JsonValueKind.String } code ? code.GetString() : null,
                value => new AssistantErrorCode(value)),
            RequestId = raw.StringOrNull("request_id"),
            UserMessageUuid = raw.StringOrNull("user_message_uuid"),
            UserMessageUuids = MessageShapeParser.Strings(raw.ElementOrNull("user_message_uuids")),
            ResumeReason = raw.StringOrNull("resume_reason"),
            ResumedFromIncompleteThinking = raw.BoolOrNull("resumed_from_incomplete_thinking") ?? false,
            Supersedes = MessageShapeParser.Strings(raw.ElementOrNull("supersedes")),
            Aborted = raw.BoolOrNull("aborted") ?? false,
            SubagentType = raw.StringOrNull("subagent_type"),
            TaskDescription = raw.StringOrNull("task_description"),
            Timestamp = WireTime.Text(raw.StringOrNull("timestamp")),
            ContextUsage = MessageShapeParser.ReadContextUsage(raw.ElementOrNull("context_usage")),
            UsageReport = MessageShapeParser.ReadUsageReport(raw.ElementOrNull("usage_report")),
            RawWireToolInputs = MessageShapeParser.ReadWireToolInputs(raw.ElementOrNull("wire_tool_inputs")),
            ToolUseDisplays = MessageShapeParser.ReadToolUseDisplays(raw.ElementOrNull("tool_use_meta")),
            IsMeta = raw.BoolOrNull("is_meta") ?? false,
            LocalCommandSource = raw.StringOrNull("local_command_source"),
        };
    }

    private static Result<AgentMessage, ParseError> ParseSystem(SystemFrame frame, JsonElement raw, string line)
    {
        if (frame.Subtype is not { } subtype)
        {
            return JsonReading.Missing("subtype", line);
        }

        // A subtype with no type of its own is no message of this library's: the codec makes it the
        // unknown, with its subtype and its line.
        return SystemMessageParser.Parse(subtype, raw, line)
            ?? JsonReading.Invalid("subtype", "no message type for it.", line);
    }

    private static Result<AgentMessage, ParseError> ParseResult(ResultFrame frame, JsonElement raw, string line)
    {
        if (frame.Subtype is not { } subtype)
        {
            return JsonReading.Missing("subtype", line);
        }

        if (frame.DurationMs is not { } durationMs)
        {
            return JsonReading.Missing("duration_ms", line);
        }

        if (frame.DurationApiMs is not { } durationApiMs)
        {
            return JsonReading.Missing("duration_api_ms", line);
        }

        if (WireTime.Milliseconds(durationMs) is not { } duration)
        {
            return JsonReading.Invalid("duration_ms", "out of a duration's range.", line);
        }

        if (WireTime.Milliseconds(durationApiMs) is not { } apiDuration)
        {
            return JsonReading.Invalid("duration_api_ms", "out of a duration's range.", line);
        }

        if (frame.IsError is not { } isError)
        {
            return JsonReading.Missing("is_error", line);
        }

        if (frame.NumTurns is not { } numTurns)
        {
            return JsonReading.Missing("num_turns", line);
        }

        if (!Identity.RequireSession(frame.SessionId, "session_id", line)
                .TryGet(out var sessionId, out var sessionError))
        {
            return sessionError;
        }

        DeferredToolUse? deferred = null;
        if (frame.DeferredToolUse is { } deferredBody)
        {
            if (!Identity.RequireToolUse(deferredBody.Id, "deferred_tool_use.id", line)
                    .TryGet(out var deferredToolUseId, out var deferredIdError))
            {
                return deferredIdError;
            }

            if (!Identity.RequireToolName(deferredBody.Name, "deferred_tool_use.name", line)
                    .TryGet(out var deferredTool, out var deferredNameError))
            {
                return deferredNameError;
            }

            if (deferredBody.Input.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                return JsonReading.Missing("deferred_tool_use.input", line);
            }

            deferred = new DeferredToolUse(
                deferredToolUseId,
                deferredTool,
                ToolTypes.ReadInput(deferredTool, deferredBody.Input),
                deferredBody.Input);
        }

        if (!Denials(frame.PermissionDenials, line).TryGet(out var denials, out var denialError))
        {
            return denialError;
        }

        return new ResultMessage(
            new ResultMessageSubtype(subtype),
            duration,
            apiDuration,
            isError,
            numTurns,
            sessionId,
            Identity.Message(frame.Uuid),
            raw)
        {
            StopReason = MessageShapeParser.Set(frame.StopReason, value => new StopReason(value)),
            TotalCostUsd = frame.TotalCostUsd,
            Usage = UsageParser.ParseUsage(frame.Usage),
            Result = frame.Result,
            ModelUsage = UsageParser.ModelUsages(frame.ModelUsage),
            PermissionDenials = denials,
            DeferredToolUse = deferred,
            Errors = Errors(frame.Errors),
            StartupFailureReason = MessageShapeParser.Set(
                raw.StringOrNull("startup_failure_reason"),
                value => new StartupFailureReason(value)),
            ApiErrorStatus = frame.ApiErrorStatus,
            TerminalReason = MessageShapeParser.Set(frame.TerminalReason, value => new TerminalReason(value)),
            QueuedTurnCount = raw.Int64OrNull("queued_turn_count"),
            UserMessageUuid = raw.StringOrNull("user_message_uuid"),
            UserMessageUuids = MessageShapeParser.Strings(raw.ElementOrNull("user_message_uuids")),
            ResumeReason = raw.StringOrNull("resume_reason"),
            ResultIndex = raw.Int64OrNull("result_index"),
            FastModeState = MessageShapeParser.Set(raw.StringOrNull("fast_mode_state"), value => new FastModeState(value)),
            FastModeDisabledReason = MessageShapeParser.Set(
                raw.StringOrNull("fast_mode_disabled_reason"),
                value => new FastModeDisabledReason(value)),
            Origin = MessageShapeParser.ReadOrigin(frame.Origin),
            SubagentStatistics = MessageShapeParser.ReadSubagentStatistics(raw.ElementOrNull("subagent_stats")),
            LocalCommand = raw.StringOrNull("local_command"),
            WarmSpareClaimed = raw.BoolOrNull("warm_spare_claimed"),
            TimeToFirstToken = WireTime.Milliseconds(raw.Int64OrNull("ttft_ms")),
            TimeToFirstStreamedToken = WireTime.Milliseconds(raw.Int64OrNull("ttft_stream_ms")),
            TimeToRequest = WireTime.Milliseconds(raw.Int64OrNull("time_to_request_ms")),
            TimeToRequestFromSpawn = WireTime.Milliseconds(raw.Int64OrNull("time_to_request_from_spawn_ms")),
            TimeToFirstContentFrame = WireTime.Milliseconds(raw.Int64OrNull("first_content_frame_ms")),
            RequestSentWallTime = WireTime.Milliseconds(raw.Int64OrNull("request_sent_wall_ms")),
            FirstStreamPost = WireTime.Milliseconds(raw.Int64OrNull("first_stream_post_ms")),
            FirstStreamPostAcknowledgement = WireTime.Milliseconds(raw.Int64OrNull("first_stream_post_ack_ms")),
            FirstStreamPostWallTime = WireTime.Milliseconds(raw.Int64OrNull("first_stream_post_wall_ms")),
            TimeOrigin = WireTime.UnixMilliseconds(raw.Int64OrNull("time_origin_ms")),
        };
    }

    private static ImmutableArray<string> Errors(JsonElement? element) => element switch
    {
        { ValueKind: JsonValueKind.String } text => [text.GetString() ?? string.Empty],
        { ValueKind: JsonValueKind.Array } => [.. MessageShapeParser.Strings(element)],
        _ => [],
    };

    private static Result<IReadOnlyList<PermissionDenial>, ParseError> Denials(JsonElement? element, string line)
    {
        if (element is not { ValueKind: JsonValueKind.Array } array)
        {
            return ImmutableArray<PermissionDenial>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<PermissionDenial>();
        foreach (var denial in array.EnumerateArray())
        {
            if (denial.ValueKind != JsonValueKind.Object)
            {
                return JsonReading.Invalid("permission_denials[]", "expected an object.", line);
            }

            if (!denial.RequireToolName("tool_name", line, "permission_denials[].tool_name").TryGet(out var toolName, out var toolNameError))
            {
                return toolNameError;
            }

            if (!denial.RequireToolUseId("tool_use_id", line, "permission_denials[].tool_use_id").TryGet(out var toolUseId, out var toolUseIdError))
            {
                return toolUseIdError;
            }

            if (!denial.RequireElement("tool_input", line, "permission_denials[].tool_input").TryGet(out var input, out var inputError))
            {
                return inputError;
            }

            builder.Add(new PermissionDenial(toolName, toolUseId, ToolTypes.ReadInput(toolName, input), input));
        }

        return builder.ToImmutable();
    }

    private static Result<AgentMessage, ParseError> ParseStreamEvent(
        StreamEventFrame frame,
        JsonElement raw,
        string line)
    {
        if (!Identity.RequireMessage(frame.Uuid, "uuid", line).TryGet(out var uuid, out var uuidError))
        {
            return uuidError;
        }

        if (!Identity.RequireSession(frame.SessionId, "session_id", line)
                .TryGet(out var sessionId, out var sessionError))
        {
            return sessionError;
        }

        if (frame.Event is not { } @event)
        {
            return JsonReading.Missing("event", line);
        }

        return new StreamEventMessage(@event, Identity.ToolUse(frame.ParentToolUseId), uuid, sessionId, raw)
        {
            TimeToFirstToken = WireTime.Milliseconds(raw.Int64OrNull("ttft_ms")),
            UserMessageUuid = raw.StringOrNull("user_message_uuid"),
            UserMessageUuids = MessageShapeParser.Strings(raw.ElementOrNull("user_message_uuids")),
            ResumeReason = raw.StringOrNull("resume_reason"),
        };
    }

    private static Result<AgentMessage, ParseError> ParseRateLimitEvent(
        RateLimitEventFrame frame,
        JsonElement raw,
        string line)
    {
        if (frame.RateLimitInfo is not { } info)
        {
            return JsonReading.Missing("rate_limit_info", line);
        }

        if (info.Status is not { } status)
        {
            return JsonReading.Missing("rate_limit_info.status", line);
        }

        if (!Identity.RequireMessage(frame.Uuid, "uuid", line).TryGet(out var uuid, out var uuidError))
        {
            return uuidError;
        }

        if (!Identity.RequireSession(frame.SessionId, "session_id", line)
                .TryGet(out var sessionId, out var sessionError))
        {
            return sessionError;
        }

        var reported = raw.ElementOrNull("rate_limit_info");
        return new RateLimitMessage(new RateLimitStatus(status), uuid, sessionId, raw)
        {
            ResetsAt = WireTime.UnixSeconds(info.ResetsAt),
            LimitType = MessageShapeParser.Set(info.RateLimitType, value => new RateLimitType(value)),
            Utilization = info.Utilization,
            OverageStatus = MessageShapeParser.Set(info.OverageStatus, value => new RateLimitStatus(value)),
            OverageResetsAt = WireTime.UnixSeconds(info.OverageResetsAt),
            OverageDisabledReason = MessageShapeParser.Set(
                info.OverageDisabledReason,
                value => new OverageDisabledReason(value)),
            IsUsingOverage = info.IsUsingOverage,
            OverageInUse = info.OverageInUse,
            SurpassedThreshold = info.SurpassedThreshold,
            LimitScope = MessageShapeParser.Set(info.LimitScope, value => new RateLimitScope(value)),
            ErrorCode = MessageShapeParser.Set(info.ErrorCode, value => new RateLimitErrorCode(value)),
            CanUserPurchaseCredits = info.CanUserPurchaseCredits,
            HasChargeableSavedPaymentMethod = info.HasChargeableSavedPaymentMethod,
            UnifiedWindows = MessageShapeParser.ReadRateLimitWindows(
                reported is { } carried ? carried.ElementOrNull("unifiedWindows") : null),
        };
    }

    private static Result<AgentMessage, ParseError> ParseConversationReset(
        ConversationResetFrame frame,
        JsonElement raw,
        string line)
    {
        if (!Identity.RequireText(frame.NewConversationId, "new_conversation_id", line)
                .TryGet(out var newConversationId, out var conversationError))
        {
            return conversationError;
        }

        if (!Identity.RequireMessage(frame.Uuid, "uuid", line).TryGet(out var uuid, out var uuidError))
        {
            return uuidError;
        }

        if (!Identity.RequireSession(frame.SessionId, "session_id", line)
                .TryGet(out var sessionId, out var sessionError))
        {
            return sessionError;
        }

        return new ConversationResetMessage(newConversationId, uuid, sessionId, raw);
    }

    private static Result<AgentMessage, ParseError> ParseToolProgress(JsonElement raw, string line)
    {
        if (!Identity.RequireToolUse(raw.StringOrNull("tool_use_id"), "tool_use_id", line)
                .TryGet(out var toolUseId, out var toolUseError))
        {
            return toolUseError;
        }

        if (!Identity.RequireToolName(raw.StringOrNull("tool_name"), "tool_name", line)
                .TryGet(out var toolName, out var toolNameError))
        {
            return toolNameError;
        }

        return new ToolProgressMessage(
            toolUseId,
            toolName,
            WireTime.Seconds(raw.DoubleOrNull("elapsed_time_seconds")) ?? TimeSpan.Zero,
            Identity.Message(raw.StringOrNull("uuid")),
            Identity.Session(raw.StringOrNull("session_id")),
            raw)
        {
            ParentToolUseId = Identity.ToolUse(raw.StringOrNull("parent_tool_use_id")),
            TaskId = Identity.Task(raw.StringOrNull("task_id")),
            Heartbeat = raw.BoolOrNull("heartbeat"),
            SubagentType = raw.StringOrNull("subagent_type"),
            SubagentRetry = raw.ElementOrNull("subagent_retry") is { ValueKind: JsonValueKind.Object } retry
                ? new SubagentRetry(
                    retry.StringOrNull("agent_id") ?? string.Empty,
                    retry.Int64OrNull("attempt") ?? 0,
                    retry.Int64OrNull("max_retries") ?? 0,
                    WireTime.Milliseconds(retry.Int64OrNull("retry_delay_ms") ?? 0) ?? TimeSpan.Zero,
                    retry.StringOrNull("error_category") ?? string.Empty)
                {
                    ErrorStatus = retry.Int64OrNull("error_status"),
                }
                : null,
        };
    }

    private static Result<AgentMessage, ParseError> ParseToolUseSummary(JsonElement raw, string line)
    {
        if (!Identity.RequireText(raw.StringOrNull("summary"), "summary", line)
                .TryGet(out var summary, out var summaryError))
        {
            return summaryError;
        }

        return new ToolUseSummaryMessage(
            summary,
            MessageShapeParser.Strings(raw.ElementOrNull("preceding_tool_use_ids")),
            Identity.Message(raw.StringOrNull("uuid")),
            Identity.Session(raw.StringOrNull("session_id")),
            raw);
    }

    private static Result<AgentMessage, ParseError> ParseAuthStatus(JsonElement raw) =>
        new AuthStatusMessage(
            raw.BoolOrNull("isAuthenticating") ?? false,
            MessageShapeParser.Strings(raw.ElementOrNull("output")),
            Identity.Message(raw.StringOrNull("uuid")),
            Identity.Session(raw.StringOrNull("session_id")),
            raw)
        {
            Error = raw.StringOrNull("error"),
        };

    private static Result<AgentMessage, ParseError> ParsePromptSuggestion(JsonElement raw, string line)
    {
        if (!Identity.RequireText(raw.StringOrNull("suggestion"), "suggestion", line)
                .TryGet(out var suggestion, out var suggestionError))
        {
            return suggestionError;
        }

        return new PromptSuggestionMessage(
            suggestion,
            Identity.Message(raw.StringOrNull("uuid")),
            Identity.Session(raw.StringOrNull("session_id")),
            raw);
    }

    private static Result<AgentMessage, ParseError> ParseActiveGoal(JsonElement raw) =>
        new ActiveGoalMessage(
            raw.ElementOrNull("value") is { ValueKind: JsonValueKind.Object } goal
                ? new ActiveGoal(
                    goal.StringOrNull("condition") ?? string.Empty,
                    goal.Int64OrNull("iterations") ?? 0,
                    WireTime.UnixMilliseconds(goal.Int64OrNull("set_at")),
                    goal.Int64OrNull("tokens_at_start") ?? 0)
                {
                    LastReason = goal.StringOrNull("last_reason"),
                }
                : null,
            Identity.Message(raw.StringOrNull("uuid")),
            Identity.Session(raw.StringOrNull("session_id")),
            raw);
}

internal static class Identity
{
    public static string? Session(string? value) => Ids.KeyPartOrNull(value);

    public static string? Message(string? value) => Ids.NameOrNull(value);

    public static string? ToolUse(string? value) => Ids.NameOrNull(value);

    public static string? Task(string? value) => Ids.NameOrNull(value);

    public static string? Tool(string? value) => Ids.NameOrNull(value);

    public static string? ServerName(string? value) => Ids.KeyPartOrNull(value);

    public static string? Agent(string? value) => Ids.NameOrNull(value);

    public static string? ModelId(string? value) => Ids.KeyPartOrNull(value);

    public static Result<string, ParseError> RequireTask(string? value, string key, string line) =>
        Require(value, key, line, Ids.IsName, "not a task id.");

    public static Result<string, ParseError> RequireServerName(string? value, string key, string line) =>
        Require(value, key, line, Ids.IsKeyPart, "not an MCP server name.");

    public static Result<string, ParseError> RequireText(string? value, string key, string line) =>
        Require(value, key, line, Ids.IsName, "must not be empty.");

    public static Result<string, ParseError> RequireSession(string? value, string key, string line) =>
        Require(value, key, line, Ids.IsKeyPart, "not a session id.");

    public static Result<string, ParseError> RequireMessage(string? value, string key, string line) =>
        Require(value, key, line, Ids.IsName, "not a message id.");

    public static Result<string, ParseError> RequireToolUse(string? value, string key, string line) =>
        Require(value, key, line, Ids.IsName, "not a tool use id.");

    public static Result<string, ParseError> RequireToolName(string? value, string key, string line) =>
        Require(value, key, line, Ids.IsName, "not a tool name.");

    public static Result<string, ParseError> RequireModel(string? value, string key, string line) =>
        Require(value, key, line, Ids.IsName, "not a model.");

    private static Result<string, ParseError> Require(string? value, string key, string line, Func<string?, bool> check, string problem) =>
        value is not { } text ? JsonReading.Missing(key, line)
        : check(text) ? text
        : JsonReading.Invalid(key, problem, line);
}

internal static class EmptyObject
{
    public static JsonElement Value { get; } = JsonDocument.Parse("{}").RootElement.Clone();
}
