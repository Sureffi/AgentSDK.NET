using System.Collections.Immutable;
using System.Text.Json;

namespace Claude.Agent;

// One type per event, over the common base. The event is kept as the binary wrote it; the members
// below it are the event's own. An event this library has no type for, and an input missing one of
// its event's required members or carrying it in the wrong JSON kind, is UnknownHookInput with the
// whole input as its Raw: degrade, never throw, never skip.
//
// The tool events carry a ToolCall of their own, a snapshot of what the binary is about to do or
// has just done. The binary is waiting on the hook, so it is never the call a run pairs.
internal static class HookInputParser
{
    public static Result<HookInput, ParseError> Parse(JsonElement input, string line)
    {
        var element = input.ValueKind == JsonValueKind.Object ? input : EmptyObject.Value;
        if (!Identity.RequireSession(element.StringOrNull("session_id"), "request.input.session_id", line)
                .TryGet(out var sessionId, out var sessionError))
        {
            return sessionError;
        }

        var hookEvent = new HookEvent(element.StringOrNull("hook_event_name") ?? string.Empty);
        var common = new HookInputCommon
        {
            SessionId = sessionId,
            TranscriptPath = element.StringOrNull("transcript_path") ?? string.Empty,
            WorkingDirectory = element.StringOrNull("cwd") ?? string.Empty,
            PromptId = element.StringOrNull("prompt_id"),
            PermissionMode = element.StringOrNull("permission_mode") is { } mode ? new PermissionMode(mode) : (PermissionMode?)null,
            AgentId = Identity.Agent(element.StringOrNull("agent_id")),
            AgentType = element.StringOrNull("agent_type") is { } agentType ? new AgentType(agentType) : (AgentType?)null,
            Effort = Level(element),
            ScratchpadDirectory = element.StringOrNull("scratchpad_dir"),
            Raw = element,
        };

        return Typed(hookEvent, common, element) ?? new UnknownHookInput(hookEvent, common);
    }

    private static HookInput? Typed(HookEvent hookEvent, HookInputCommon common, JsonElement element) =>
        hookEvent.ToString() switch
        {
            "PreToolUse" => Started(element) is { } call
                ? new PreToolUseHookInput(common, call, McpServer(element))
                : null,
            "PostToolUse" => Answered(element) is { } call
                ? new PostToolUseHookInput(common, call, Milliseconds(element, "duration_ms"), McpServer(element))
                : null,
            "PostToolUseFailure" => Failed(element) is { } call
                ? new PostToolUseFailureHookInput(common, call, element.BoolOrNull("is_interrupt"), Milliseconds(element, "duration_ms"), McpServer(element))
                : null,
            "PostToolBatch" => Batch(element) is { } calls
                ? new PostToolBatchHookInput(common, calls)
                : null,
            "Notification" => element.StringOrNull("message") is { } message && element.StringOrNull("notification_type") is { } notificationType
                ? new NotificationHookInput(common, message, element.StringOrNull("title"), new NotificationType(notificationType))
                : null,
            "UserPromptSubmit" => element.StringOrNull("prompt") is { } prompt
                ? new UserPromptSubmitHookInput(common, prompt, Set(element, "source", value => new UserPromptSubmitSource(value)), element.StringOrNull("session_title"))
                : null,
            "UserPromptExpansion" => Expansion(common, element),
            "SessionStart" => element.StringOrNull("source") is { } source
                ? new SessionStartHookInput(
                    common,
                    new SessionStartSource(source),
                    element.StringOrNull("model"),
                    element.StringOrNull("session_title"),
                    Seconds(element, "seconds_since_last_response"),
                    element.Int64OrNull("context_tokens"),
                    element.BoolOrNull("prompt_cache_likely_expired"),
                    element.DecimalOrNull("estimated_cache_write_usd"))
                : null,
            "SessionEnd" => element.StringOrNull("reason") is { } reason
                ? new SessionEndHookInput(common, new SessionEndReason(reason))
                : null,
            "Stop" => element.BoolOrNull("stop_hook_active") is { } stopActive
                ? new StopHookInput(common, stopActive, element.StringOrNull("last_assistant_message"), BackgroundTasks(element), SessionCrons(element))
                : null,
            "StopFailure" => element.StringOrNull("error") is { } error
                ? new StopFailureHookInput(common, new AssistantErrorCode(error), element.StringOrNull("error_details"), element.StringOrNull("last_assistant_message"))
                : null,
            "SubagentStart" => common.AgentId is not null && common.AgentType is not null
                ? new SubagentStartHookInput(common)
                : null,
            "SubagentStop" => element.BoolOrNull("stop_hook_active") is { } stopActive
                && common.AgentId is not null && common.AgentType is not null
                && element.StringOrNull("agent_transcript_path") is { } agentTranscriptPath
                ? new SubagentStopHookInput(common, stopActive, agentTranscriptPath, element.StringOrNull("last_assistant_message"), BackgroundTasks(element), SessionCrons(element))
                : null,
            "PreCompact" => element.StringOrNull("trigger") is { } trigger
                ? new PreCompactHookInput(common, new CompactTrigger(trigger), element.StringOrNull("custom_instructions"))
                : null,
            "PostCompact" => element.StringOrNull("trigger") is { } trigger && element.StringOrNull("compact_summary") is { } summary
                ? new PostCompactHookInput(common, new CompactTrigger(trigger), summary)
                : null,
            "PreModelSwitch" => Switch(element) is { } model
                ? new PreModelSwitchHookInput(
                    common,
                    model.FromModel,
                    model.ToModel,
                    model.RequestedModel,
                    new PreModelSwitchSource(model.Source),
                    model.ContextTokens,
                    model.IsPromptCacheWarm,
                    new ModelSwitchCacheTimeToLive(model.CacheTimeToLive),
                    model.EstimatedCacheWriteCost,
                    new ModelSwitchPricing(model.Pricing))
                : null,
            "PostModelSwitch" => Switch(element) is { } model
                ? new PostModelSwitchHookInput(
                    common,
                    model.FromModel,
                    model.ToModel,
                    model.RequestedModel,
                    new PostModelSwitchSource(model.Source),
                    model.ContextTokens,
                    model.IsPromptCacheWarm,
                    new ModelSwitchCacheTimeToLive(model.CacheTimeToLive),
                    model.EstimatedCacheWriteCost,
                    new ModelSwitchPricing(model.Pricing))
                : null,
            "PermissionRequest" => Asked(element) is { } call
                ? new PermissionRequestHookInput(common, call, Suggestions(element), McpServer(element))
                : null,
            "PermissionDenied" => Denied(element) is { } call
                ? new PermissionDeniedHookInput(common, call, McpServer(element))
                : null,
            "Setup" => element.StringOrNull("trigger") is { } trigger
                ? new SetupHookInput(common, new SetupTrigger(trigger))
                : null,
            "TeammateIdle" => element.StringOrNull("teammate_name") is { } teammate && element.StringOrNull("team_name") is { } team
                ? new TeammateIdleHookInput(common, teammate, team)
                : null,
            "TaskCreated" => Summary(element) is { } task
                ? new TaskCreatedHookInput(common, task.Id, task.Subject, task.Description, task.TeammateName, task.TeamName)
                : null,
            "TaskCompleted" => Summary(element) is { } task
                ? new TaskCompletedHookInput(common, task.Id, task.Subject, task.Description, task.TeammateName, task.TeamName)
                : null,
            "Elicitation" => element.StringOrNull("mcp_server_name") is { } server && element.StringOrNull("message") is { } message
                ? new ElicitationHookInput(
                    common,
                    server,
                    message,
                    Set(element, "mode", value => new ElicitationMode(value)),
                    element.StringOrNull("url"),
                    element.StringOrNull("elicitation_id"),
                    Object(element, "requested_schema"))
                : null,
            "ElicitationResult" => element.StringOrNull("mcp_server_name") is { } server && element.StringOrNull("action") is { } action
                ? new ElicitationResultHookInput(
                    common,
                    server,
                    element.StringOrNull("elicitation_id"),
                    Set(element, "mode", value => new ElicitationMode(value)),
                    new ElicitationAction(action),
                    Object(element, "content"))
                : null,
            "ConfigChange" => element.StringOrNull("source") is { } source
                ? new ConfigChangeHookInput(common, new ConfigChangeSource(source), element.StringOrNull("file_path"))
                : null,
            "WorktreeCreate" => element.StringOrNull("name") is { } name
                ? new WorktreeCreateHookInput(common, name)
                : null,
            "WorktreeRemove" => element.StringOrNull("worktree_path") is { } path
                ? new WorktreeRemoveHookInput(common, path)
                : null,
            "InstructionsLoaded" => Instructions(common, element),
            "CwdChanged" => element.StringOrNull("old_cwd") is { } old && element.StringOrNull("new_cwd") is { } current
                ? new WorkingDirectoryChangedHookInput(common, old, current)
                : null,
            "FileChanged" => element.StringOrNull("file_path") is { } path && element.StringOrNull("event") is { } change
                ? new FileChangedHookInput(common, path, new FileChangedEvent(change))
                : null,
            "DirectoryAdded" => element.StringOrNull("directory") is { } directory && element.StringOrNull("source") is { } source
                ? new DirectoryAddedHookInput(common, directory, new DirectoryAddedSource(source))
                : null,
            "MessageDisplay" => Display(common, element),
            _ => null,
        };

    private static UserPromptExpansionHookInput? Expansion(HookInputCommon common, JsonElement element)
    {
        if (element.StringOrNull("expansion_type") is not { } expansionType
            || element.StringOrNull("command_name") is not { } commandName
            || element.StringOrNull("command_args") is not { } commandArguments
            || element.StringOrNull("prompt") is not { } prompt)
        {
            return null;
        }

        return new UserPromptExpansionHookInput(
            common,
            new UserPromptExpansionType(expansionType),
            commandName,
            commandArguments,
            element.StringOrNull("command_source"),
            prompt);
    }

    private static InstructionsLoadedHookInput? Instructions(HookInputCommon common, JsonElement element)
    {
        if (element.StringOrNull("file_path") is not { } filePath
            || element.StringOrNull("memory_type") is not { } memoryType
            || element.StringOrNull("load_reason") is not { } loadReason)
        {
            return null;
        }

        return new InstructionsLoadedHookInput(
            common,
            filePath,
            new InstructionsMemoryType(memoryType),
            new InstructionsLoadReason(loadReason),
            Strings(element, "globs"),
            element.StringOrNull("trigger_file_path"),
            element.StringOrNull("parent_file_path"));
    }

    private static MessageDisplayHookInput? Display(HookInputCommon common, JsonElement element)
    {
        if (element.StringOrNull("turn_id") is not { } turnId
            || element.StringOrNull("message_id") is not { } messageId
            || element.Int64OrNull("index") is not { } index
            || element.BoolOrNull("final") is not { } isFinal
            || element.StringOrNull("delta") is not { } delta)
        {
            return null;
        }

        return new MessageDisplayHookInput(common, turnId, messageId, index, isFinal, delta);
    }

    // The tool's half of a tool event: the name, the input the model wrote and the id.
    private static ToolCall? Started(JsonElement element)
    {
        if (element.ToolNameOrNull("tool_name") is not { } name
            || element.ElementOrNull("tool_input") is not { } input
            || element.ToolUseIdOrNull("tool_use_id") is not { } id)
        {
            return null;
        }

        return new ToolCall(id, name, ToolTypes.ReadInput(name, input), input, ToolCallStatus.Started);
    }

    private static ToolCall? Answered(JsonElement element) =>
        Started(element) is { } call && element.ElementOrNull("tool_response") is { } response
            ? Closed(call, response)
            : null;

    private static ToolCall Closed(ToolCall call, JsonElement response) =>
        new(call.Id, call.Name, call.Input, call.RawInput, ToolCallStatus.Answered)
        {
            Output = ToolTypes.ReadOutput(call.Name, response),
            RawOutput = response,
        };

    private static ToolCall? Failed(JsonElement element) =>
        Started(element) is { } call && element.StringOrNull("error") is { } error
            ? new ToolCall(call.Id, call.Name, call.Input, call.RawInput, ToolCallStatus.Failed) { Error = error }
            : null;

    // PermissionRequest names no tool use: the hook_callback envelope carries the id, and this
    // library does not surface it, so the call goes over with an empty one.
    private static ToolCall? Asked(JsonElement element)
    {
        if (element.ToolNameOrNull("tool_name") is not { } name || element.ElementOrNull("tool_input") is not { } input)
        {
            return null;
        }

        return new ToolCall(string.Empty, name, ToolTypes.ReadInput(name, input), input, ToolCallStatus.Started);
    }

    // A refused call is answered with an error, as the page has it, and the refusal's reason is
    // that error.
    private static ToolCall? Denied(JsonElement element) =>
        Started(element) is { } call && element.StringOrNull("reason") is { } reason
            ? new ToolCall(call.Id, call.Name, call.Input, call.RawInput, ToolCallStatus.Failed) { Error = reason }
            : null;

    // A batch entry answers when the binary reported an output for it and stands started when it
    // did not. An entry missing a name, an input or an id is left out.
    private static ImmutableArray<ToolCall>? Batch(JsonElement element)
    {
        if (!element.TryProperty("tool_calls", out var calls) || calls.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var builder = ImmutableArray.CreateBuilder<ToolCall>();
        foreach (var entry in calls.EnumerateArray())
        {
            if (Started(entry) is not { } call)
            {
                continue;
            }

            builder.Add(entry.ElementOrNull("tool_response") is { } response ? Closed(call, response) : call);
        }

        return builder.ToImmutable();
    }

    private static McpServerOrigin? McpServer(JsonElement element) =>
        element.ElementOrNull("mcp_server") is { } server
        && server.StringOrNull("name") is { } name
        && server.StringOrNull("source") is { } source
            ? new McpServerOrigin(name, new McpServerSource(source))
            : null;

    private static ImmutableArray<PermissionUpdate> Suggestions(JsonElement element) =>
        element.TryProperty("permission_suggestions", out var suggestions)
            ? PermissionUpdateParser.ParseAll(suggestions)
            : [];

    private static Effort? Level(JsonElement element) =>
        element.ElementOrNull("effort")?.StringOrNull("level") is { } level ? new Effort(level) : (Effort?)null;

    private static T? Set<T>(JsonElement element, string name, Func<string, T> make)
        where T : struct =>
        element.StringOrNull(name) is { } value ? make(value) : (T?)null;

    private static JsonElement? Object(JsonElement element, string name) =>
        element.ElementOrNull(name) is { ValueKind: JsonValueKind.Object } value ? value : null;

    private static ImmutableArray<string> Strings(JsonElement element, string name)
    {
        if (!element.TryProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                builder.Add(item.GetString() ?? string.Empty);
            }
        }

        return builder.ToImmutable();
    }

    // An entry missing a member every kind carries is left out: the list is of what the binary named
    // whole. The kind's label is which type an entry is, and a label with no type is the unknown, the
    // entry as the binary wrote it beside the members every kind carries.
    private static ImmutableArray<StopHookBackgroundTask> BackgroundTasks(JsonElement element)
    {
        var builder = ImmutableArray.CreateBuilder<StopHookBackgroundTask>();
        foreach (var entry in Entries(element, "background_tasks"))
        {
            if (entry.StringOrNull("id") is not { } id
                || entry.StringOrNull("type") is not { } taskType
                || entry.StringOrNull("status") is not { } status
                || entry.StringOrNull("description") is not { } description)
            {
                continue;
            }

            builder.Add(taskType switch
            {
                "shell" => new ShellStopHookBackgroundTask(id, new BackgroundTaskStatus(status), description)
                {
                    Command = entry.StringOrNull("command"),
                },
                "subagent" => new SubagentStopHookBackgroundTask(id, new BackgroundTaskStatus(status), description)
                {
                    AgentType = entry.StringOrNull("agent_type") is { } agentType ? new AgentType(agentType) : (AgentType?)null,
                },
                _ => new UnknownStopHookBackgroundTask(new StopHookBackgroundTaskType(taskType), id, new BackgroundTaskStatus(status), description, entry.Clone()),
            });
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<SessionCron> SessionCrons(JsonElement element)
    {
        var builder = ImmutableArray.CreateBuilder<SessionCron>();
        foreach (var entry in Entries(element, "session_crons"))
        {
            if (entry.StringOrNull("id") is not { } id
                || entry.StringOrNull("schedule") is not { } schedule
                || entry.BoolOrNull("recurring") is not { } isRecurring
                || entry.StringOrNull("prompt") is not { } prompt)
            {
                continue;
            }

            builder.Add(new SessionCron(id, schedule, isRecurring, prompt));
        }

        return builder.ToImmutable();
    }

    private static JsonElement.ArrayEnumerator Entries(JsonElement element, string name) =>
        element.TryProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : default;

    private static ModelSwitch? Switch(JsonElement element)
    {
        if (element.StringOrNull("from_model") is not { } fromModel
            || element.StringOrNull("to_model") is not { } toModel
            || element.StringOrNull("source") is not { } source
            || element.Int64OrNull("context_tokens") is not { } contextTokens
            || element.BoolOrNull("prompt_cache_warm") is not { } isWarm
            || element.StringOrNull("cache_ttl") is not { } cacheTimeToLive
            || element.DecimalOrNull("estimated_cache_write_usd") is not { } cost
            || element.StringOrNull("pricing") is not { } pricing)
        {
            return null;
        }

        return new ModelSwitch(fromModel, toModel, element.StringOrNull("requested_model"), source, contextTokens, isWarm, cacheTimeToLive, cost, pricing);
    }

    private static TaskSummary? Summary(JsonElement element) =>
        element.StringOrNull("task_id") is { } id && element.StringOrNull("task_subject") is { } subject
            ? new TaskSummary(
                id,
                subject,
                element.StringOrNull("task_description"),
                element.StringOrNull("teammate_name"),
                element.StringOrNull("team_name"))
            : null;

    private const long MaxMilliseconds = long.MaxValue / TimeSpan.TicksPerMillisecond;

    // A count of milliseconds as a duration, null when no TimeSpan holds it, as a number of the
    // wrong kind would be.
    private static TimeSpan? Milliseconds(JsonElement element, string name)
    {
        if (element.Int64OrNull(name) is not { } milliseconds || milliseconds < -MaxMilliseconds || milliseconds > MaxMilliseconds)
        {
            return null;
        }

        return TimeSpan.FromTicks(milliseconds * TimeSpan.TicksPerMillisecond);
    }

    // A count of seconds as a duration, null when no TimeSpan holds it.
    private static TimeSpan? Seconds(JsonElement element, string name)
    {
        if (element.DoubleOrNull(name) is not { } seconds
            || !double.IsFinite(seconds)
            || seconds < -TimeSpan.MaxValue.TotalSeconds
            || seconds > TimeSpan.MaxValue.TotalSeconds)
        {
            return null;
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private sealed record ModelSwitch(
        string FromModel,
        string ToModel,
        string? RequestedModel,
        string Source,
        long ContextTokens,
        bool IsPromptCacheWarm,
        string CacheTimeToLive,
        decimal EstimatedCacheWriteCost,
        string Pricing);

    private sealed record TaskSummary(string Id, string Subject, string? Description, string? TeammateName, string? TeamName);
}
