using System.Text.Json;

namespace Claude.Agent;

// The system frame's notices, one type per subtype the binary names. Everything is read off the
// line itself: the frame binding carries the subtype and the envelope, and nothing else of a notice
// is bound, so a member of a shape this library reads wrongly is that member's own loss and never
// the frame's. A subtype with no type here is the unknown, which the codec makes.
internal static class SystemMessageParser
{
    public static Result<AgentMessage, ParseError>? Parse(string subtype, JsonElement raw, string line)
    {
        var uuid = Identity.Message(raw.StringOrNull("uuid"));
        var sessionId = Identity.Session(raw.StringOrNull("session_id"));

        switch (subtype)
        {
            case "init":
                return Init(raw, uuid, sessionId);

            case "task_started":
            case "task_progress":
            case "task_notification":
            case "task_updated":
                return Task(subtype, raw, line, uuid, sessionId);

            case "status":
                return new StatusMessage(uuid, sessionId, raw)
                {
                    Status = MessageShapeParser.Set(raw.StringOrNull("status"), value => new ActivityStatus(value)),
                    PermissionMode = MessageShapeParser.Set(raw.StringOrNull("permissionMode"), value => new PermissionMode(value)),
                    CompactResult = MessageShapeParser.Set(raw.StringOrNull("compact_result"), value => new CompactResult(value)),
                    CompactError = raw.StringOrNull("compact_error"),
                };

            case "compact_boundary":
                {
                    if (raw.ElementOrNull("compact_metadata") is not { ValueKind: JsonValueKind.Object } metadata)
                    {
                        return JsonReading.Missing("compact_metadata", line);
                    }

                    return new CompactBoundaryMessage(
                        new CompactMetadata(
                            new CompactTrigger(metadata.StringOrNull("trigger") ?? string.Empty),
                            metadata.Int64OrNull("pre_tokens") ?? 0)
                        {
                            PostTokens = metadata.Int64OrNull("post_tokens"),
                            Duration = WireTime.Milliseconds(metadata.Int64OrNull("duration_ms")),
                            PreservedSegment = metadata.ElementOrNull("preserved_segment") is { ValueKind: JsonValueKind.Object } segment
                                ? new PreservedSegment(
                                    segment.StringOrNull("head_uuid") ?? string.Empty,
                                    segment.StringOrNull("anchor_uuid") ?? string.Empty,
                                    segment.StringOrNull("tail_uuid") ?? string.Empty)
                                : null,
                            PreservedMessages = metadata.ElementOrNull("preserved_messages") is { ValueKind: JsonValueKind.Object } kept
                                ? new PreservedMessages(
                                    kept.StringOrNull("anchor_uuid") ?? string.Empty,
                                    MessageShapeParser.Strings(kept.ElementOrNull("uuids")))
                                : null,
                            CumulativeDroppedTokens = metadata.Int64OrNull("cumulative_dropped_tokens"),
                        },
                        uuid,
                        sessionId,
                        raw)
                    {
                        LogicalParentUuid = raw.StringOrNull("logical_parent_uuid"),
                    };
                }

            case "api_retry":
                return new ApiRetryMessage(
                    raw.Int64OrNull("attempt") ?? 0,
                    raw.Int64OrNull("max_retries") ?? 0,
                    WireTime.Milliseconds(raw.Int64OrNull("retry_delay_ms") ?? 0) ?? TimeSpan.Zero,
                    uuid,
                    sessionId,
                    raw)
                {
                    ErrorStatus = raw.Int64OrNull("error_status"),
                    Error = MessageShapeParser.Set(raw.StringOrNull("error"), value => new AssistantErrorCode(value)),
                    NoResponse = raw.ElementOrNull("no_response") is { ValueKind: JsonValueKind.Object } silence
                        ? new ApiRetryNoResponse(
                            WireTime.Milliseconds(silence.Int64OrNull("waited_ms") ?? 0) ?? TimeSpan.Zero,
                            WireTime.Milliseconds(silence.Int64OrNull("retry_wait_ms") ?? 0) ?? TimeSpan.Zero)
                        : null,
                };

            case "control_request_progress":
                return new ControlRequestProgressMessage(
                    raw.StringOrNull("request_id") ?? string.Empty,
                    new ControlRequestProgressStatus(raw.StringOrNull("status") ?? string.Empty),
                    uuid,
                    sessionId,
                    raw)
                {
                    Attempt = raw.Int64OrNull("attempt"),
                    MaxRetries = raw.Int64OrNull("max_retries"),
                    RetryDelay = WireTime.Milliseconds(raw.Int64OrNull("retry_delay_ms")),
                    ErrorStatus = raw.Int64OrNull("error_status"),
                };

            case "model_refusal_fallback":
                return new ModelRefusalFallbackMessage(
                    new RefusalFallbackTrigger(raw.StringOrNull("trigger") ?? string.Empty),
                    new RefusalFallbackDirection(raw.StringOrNull("direction") ?? string.Empty),
                    raw.StringOrNull("original_model") ?? string.Empty,
                    raw.StringOrNull("fallback_model") ?? string.Empty,
                    raw.StringOrNull("content") ?? string.Empty,
                    uuid,
                    sessionId,
                    raw)
                {
                    Scope = MessageShapeParser.Set(raw.StringOrNull("scope"), value => new RefusalFallbackScope(value)),
                    RequestId = raw.StringOrNull("request_id"),
                    ApiRefusalCategory = raw.StringOrNull("api_refusal_category"),
                    ApiRefusalExplanation = raw.StringOrNull("api_refusal_explanation"),
                    RetractedMessageUuids = MessageShapeParser.Strings(raw.ElementOrNull("retracted_message_uuids")),
                    RefusedUserMessageUuid = raw.StringOrNull("refused_user_message_uuid"),
                };

            case "model_refusal_no_fallback":
                return new ModelRefusalNoFallbackMessage(
                    raw.StringOrNull("original_model") ?? string.Empty,
                    raw.StringOrNull("content") ?? string.Empty,
                    uuid,
                    sessionId,
                    raw)
                {
                    RequestId = raw.StringOrNull("request_id"),
                    ApiRefusalCategory = raw.StringOrNull("api_refusal_category"),
                    ApiRefusalExplanation = raw.StringOrNull("api_refusal_explanation"),
                    RefusedUserMessageUuid = raw.StringOrNull("refused_user_message_uuid"),
                };

            case "local_command_output":
                return new LocalCommandOutputMessage(raw.StringOrNull("content") ?? string.Empty, uuid, sessionId, raw);

            case "hook_started":
                return new HookStartedMessage(
                    raw.StringOrNull("hook_id") ?? string.Empty,
                    raw.StringOrNull("hook_name") ?? string.Empty,
                    new HookEvent(raw.StringOrNull("hook_event") ?? string.Empty),
                    uuid,
                    sessionId,
                    raw);

            case "hook_progress":
                return new HookProgressMessage(
                    raw.StringOrNull("hook_id") ?? string.Empty,
                    raw.StringOrNull("hook_name") ?? string.Empty,
                    new HookEvent(raw.StringOrNull("hook_event") ?? string.Empty),
                    raw.StringOrNull("stdout") ?? string.Empty,
                    raw.StringOrNull("stderr") ?? string.Empty,
                    raw.StringOrNull("output") ?? string.Empty,
                    uuid,
                    sessionId,
                    raw);

            case "hook_response":
                return new HookResponseMessage(
                    raw.StringOrNull("hook_id") ?? string.Empty,
                    raw.StringOrNull("hook_name") ?? string.Empty,
                    new HookEvent(raw.StringOrNull("hook_event") ?? string.Empty),
                    raw.StringOrNull("output") ?? string.Empty,
                    raw.StringOrNull("stdout") ?? string.Empty,
                    raw.StringOrNull("stderr") ?? string.Empty,
                    new HookOutcome(raw.StringOrNull("outcome") ?? string.Empty),
                    uuid,
                    sessionId,
                    raw)
                {
                    ExitCode = raw.Int64OrNull("exit_code"),
                };

            case "plugin_install":
                return new PluginInstallMessage(
                    new PluginInstallStatus(raw.StringOrNull("status") ?? string.Empty),
                    uuid,
                    sessionId,
                    raw)
                {
                    Name = raw.StringOrNull("name"),
                    Error = raw.StringOrNull("error"),
                };

            case "background_tasks_changed":
                return new BackgroundTasksChangedMessage(
                    MessageShapeParser.Items(raw.ElementOrNull("tasks"), task => new BackgroundTask(
                        task.StringOrNull("task_id") ?? string.Empty,
                        new TaskType(task.StringOrNull("task_type") ?? string.Empty),
                        task.StringOrNull("description") ?? string.Empty)
                    {
                        Ambient = task.BoolOrNull("ambient"),
                    }),
                    uuid,
                    sessionId,
                    raw);

            case "thinking_tokens":
                return new ThinkingTokensMessage(
                    raw.Int64OrNull("estimated_tokens") ?? 0,
                    raw.Int64OrNull("estimated_tokens_delta") ?? 0,
                    uuid,
                    sessionId,
                    raw)
                {
                    UserMessageUuid = raw.StringOrNull("user_message_uuid"),
                };

            case "session_state_changed":
                return new SessionStateChangedMessage(
                    new SessionState(raw.StringOrNull("state") ?? string.Empty),
                    uuid,
                    sessionId,
                    raw);

            case "worker_shutting_down":
                return new WorkerShuttingDownMessage(raw.StringOrNull("reason") ?? string.Empty, uuid, sessionId, raw);

            case "commands_changed":
                return new CommandsChangedMessage(
                    MessageShapeParser.Items(raw.ElementOrNull("commands"), command => new SlashCommandInfo(
                        command.StringOrNull("name") ?? string.Empty,
                        command.StringOrNull("description") ?? string.Empty,
                        command.StringOrNull("argumentHint") ?? string.Empty)
                    {
                        Aliases = MessageShapeParser.Strings(command.ElementOrNull("aliases")),
                        IsBuiltIn = command.BoolOrNull("builtin"),
                    }),
                    uuid,
                    sessionId,
                    raw);

            case "notification":
                return new NotificationMessage(
                    raw.StringOrNull("key") ?? string.Empty,
                    raw.StringOrNull("text") ?? string.Empty,
                    new NotificationPriority(raw.StringOrNull("priority") ?? string.Empty),
                    uuid,
                    sessionId,
                    raw)
                {
                    Color = raw.StringOrNull("color"),
                    Timeout = WireTime.Milliseconds(raw.Int64OrNull("timeout_ms")),
                };

            case "files_persisted":
                return new FilesPersistedMessage(
                    MessageShapeParser.Items(raw.ElementOrNull("files"), file => new PersistedFile(
                        file.StringOrNull("filename") ?? string.Empty,
                        file.StringOrNull("file_id") ?? string.Empty)),
                    MessageShapeParser.Items(raw.ElementOrNull("failed"), failure => new FilePersistenceFailure(
                        failure.StringOrNull("filename") ?? string.Empty,
                        failure.StringOrNull("error") ?? string.Empty)),
                    raw.StringOrNull("processed_at") ?? string.Empty,
                    uuid,
                    sessionId,
                    raw);

            case "memory_recall":
                return new MemoryRecallMessage(
                    new MemoryRecallMode(raw.StringOrNull("mode") ?? string.Empty),
                    MessageShapeParser.Items(raw.ElementOrNull("memories"), memory => new RecalledMemory(
                        memory.StringOrNull("path") ?? string.Empty,
                        new MemoryScope(memory.StringOrNull("scope") ?? string.Empty))
                    {
                        Content = memory.StringOrNull("content"),
                    }),
                    uuid,
                    sessionId,
                    raw);

            case "elicitation_complete":
                return new ElicitationCompleteMessage(
                    raw.StringOrNull("mcp_server_name") ?? string.Empty,
                    raw.StringOrNull("elicitation_id") ?? string.Empty,
                    uuid,
                    sessionId,
                    raw);

            case "permission_denied":
                {
                    if (!Identity.RequireToolName(raw.StringOrNull("tool_name"), "tool_name", line)
                            .TryGet(out var toolName, out var toolError))
                    {
                        return toolError;
                    }

                    if (!Identity.RequireToolUse(raw.StringOrNull("tool_use_id"), "tool_use_id", line)
                            .TryGet(out var toolUseId, out var toolUseError))
                    {
                        return toolUseError;
                    }

                    return new PermissionDeniedMessage(
                        toolName,
                        toolUseId,
                        raw.StringOrNull("message") ?? string.Empty,
                        uuid,
                        sessionId,
                        raw)
                    {
                        AgentId = Identity.Agent(raw.StringOrNull("agent_id")),
                        DecisionReasonType = raw.StringOrNull("decision_reason_type"),
                        DecisionReason = raw.StringOrNull("decision_reason"),
                    };
                }

            case "informational":
                return new InformationalMessage(
                    raw.StringOrNull("content") ?? string.Empty,
                    new InformationalLevel(raw.StringOrNull("level") ?? string.Empty),
                    uuid,
                    sessionId,
                    raw)
                {
                    ToolUseId = Identity.ToolUse(raw.StringOrNull("tool_use_id")),
                    PreventContinuation = raw.BoolOrNull("prevent_continuation"),
                };

            default:
                return null;
        }
    }

    private static Result<AgentMessage, ParseError> Init(JsonElement raw, string? uuid, string? sessionId) =>
        new SystemInitMessage(uuid, sessionId, raw)
        {
            WorkingDirectory = raw.StringOrNull("cwd") is { } workingDirectory && Path.IsPathRooted(workingDirectory)
                ? workingDirectory
                : null,
            Tools = MessageShapeParser.Strings(raw.ElementOrNull("tools")),
            McpServers = McpServers(raw.ElementOrNull("mcp_servers")),
            Model = Ids.NameOrNull(raw.StringOrNull("model")),
            PermissionMode = MessageShapeParser.Set(raw.StringOrNull("permissionMode"), value => new PermissionMode(value)),
            SlashCommands = MessageShapeParser.Strings(raw.ElementOrNull("slash_commands")),
            TerminalSlashCommands = MessageShapeParser.Strings(raw.ElementOrNull("terminal_slash_commands")),
            ApiKeySource = MessageShapeParser.Set(raw.StringOrNull("apiKeySource"), value => new ApiKeySource(value)),
            ClaudeCodeVersion = raw.StringOrNull("claude_code_version"),
            OutputStyle = raw.StringOrNull("output_style"),
            Agents = MessageShapeParser.Strings(raw.ElementOrNull("agents")),
            Skills = MessageShapeParser.Strings(raw.ElementOrNull("skills")),
            Capabilities = MessageShapeParser.Strings(raw.ElementOrNull("capabilities")),
            Betas = MessageShapeParser.Strings(raw.ElementOrNull("betas")),
            Plugins = MessageShapeParser.Items(raw.ElementOrNull("plugins"), plugin => new PluginInfo(
                plugin.StringOrNull("name") ?? string.Empty,
                plugin.StringOrNull("path") ?? string.Empty)
            {
                Version = plugin.StringOrNull("version"),
            }),
            Effort = MessageShapeParser.Set(raw.StringOrNull("effort"), value => new Effort(value)),
            FastModeState = MessageShapeParser.Set(raw.StringOrNull("fast_mode_state"), value => new FastModeState(value)),
            FastModeDisabledReason = MessageShapeParser.Set(
                raw.StringOrNull("fast_mode_disabled_reason"),
                value => new FastModeDisabledReason(value)),
            AnalyticsDisabled = raw.BoolOrNull("analytics_disabled"),
            ProductFeedbackDisabled = raw.BoolOrNull("product_feedback_disabled"),
            MemoryPaths = raw.ElementOrNull("memory_paths") is { ValueKind: JsonValueKind.Object } paths
                ? new MemoryPaths(paths.StringOrNull("auto"))
                : null,
            MessagingSocketPath = raw.StringOrNull("messaging_socket_path"),
            ScratchpadDirectory = raw.StringOrNull("scratchpad_path"),
            IsPerTurnEffortActive = raw.BoolOrNull("per_turn_effort_active"),
            ViewMode = MessageShapeParser.Set(raw.StringOrNull("view_mode"), value => new ViewMode(value)),
        };

    // An entry whose name is empty or padded names no server and is dropped: the tools of a server
    // are found by that name, so an entry without one says nothing.
    private static List<McpServerSummary> McpServers(JsonElement? element)
    {
        var servers = new List<McpServerSummary>();
        foreach (var entry in MessageShapeParser.Items(element, entry => entry))
        {
            if (Identity.ServerName(entry.StringOrNull("name")) is { } name)
            {
                servers.Add(new McpServerSummary(name, new McpServerConnectionStatus(entry.StringOrNull("status") ?? string.Empty))
                {
                    Source = entry.StringOrNull("source") is { } source ? new McpServerSource(source) : (McpServerSource?)null,
                });
            }
        }

        return servers;
    }

    private static Result<AgentMessage, ParseError> Task(
        string subtype,
        JsonElement raw,
        string line,
        string? uuid,
        string? sessionId)
    {
        if (subtype == "task_updated")
        {
            var patch = raw.ElementOrNull("patch") is { ValueKind: JsonValueKind.Object } changed
                ? new TaskPatch
                {
                    Status = MessageShapeParser.Set(changed.StringOrNull("status"), value => new BackgroundTaskStatus(value)),
                    Description = changed.StringOrNull("description"),
                    EndTime = WireTime.UnixMilliseconds(changed.Int64OrNull("end_time")),
                    TotalPausedTime = WireTime.Milliseconds(changed.Int64OrNull("total_paused_ms")),
                    Error = changed.StringOrNull("error"),
                    IsBackgrounded = changed.BoolOrNull("is_backgrounded"),
                }
                : new TaskPatch();

            return new TaskUpdatedMessage(patch, Identity.Task(raw.StringOrNull("task_id")), uuid, sessionId, raw);
        }

        if (!Identity.RequireMessage(raw.StringOrNull("uuid"), "uuid", line).TryGet(out var taskUuid, out var uuidError))
        {
            return uuidError;
        }

        if (!Identity.RequireSession(raw.StringOrNull("session_id"), "session_id", line)
                .TryGet(out var taskSession, out var sessionError))
        {
            return sessionError;
        }

        if (!Identity.RequireTask(raw.StringOrNull("task_id"), "task_id", line).TryGet(out var taskId, out var taskIdError))
        {
            return taskIdError;
        }

        var toolUseId = Identity.ToolUse(raw.StringOrNull("tool_use_id"));
        if (subtype == "task_notification")
        {
            if (!Identity.RequireText(raw.StringOrNull("status"), "status", line).TryGet(out var status, out var statusError))
            {
                return statusError;
            }

            if (!Identity.RequireText(raw.StringOrNull("output_file"), "output_file", line)
                    .TryGet(out var outputFile, out var outputFileError))
            {
                return outputFileError;
            }

            if (!Identity.RequireText(raw.StringOrNull("summary"), "summary", line)
                    .TryGet(out var summary, out var summaryError))
            {
                return summaryError;
            }

            long? reportedTokens = null;
            long? reportedToolUses = null;
            TimeSpan? reportedDuration = null;
            if (raw.ElementOrNull("usage") is { ValueKind: JsonValueKind.Object } reported)
            {
                reportedTokens = reported.Int64OrNull("total_tokens");
                reportedToolUses = reported.Int64OrNull("tool_uses");
                reportedDuration = WireTime.Milliseconds(reported.Int64OrNull("duration_ms"));
            }

            return new TaskNotificationMessage(
                taskId,
                new TaskNotificationStatus(status),
                outputFile,
                summary,
                taskUuid,
                taskSession,
                raw)
            {
                ToolUseId = toolUseId,
                Reason = MessageShapeParser.Set(raw.StringOrNull("reason"), value => new TaskNotificationReason(value)),
                TotalTokens = reportedTokens,
                ToolUseCount = reportedToolUses,
                Duration = reportedDuration,
                ResourceLinks = MessageShapeParser.ReadResourceLinks(raw.ElementOrNull("resource_links")),
                SkipTranscript = raw.BoolOrNull("skip_transcript"),
                Ambient = raw.BoolOrNull("ambient"),
            };
        }

        if (!Identity.RequireText(raw.StringOrNull("description"), "description", line)
                .TryGet(out var description, out var descriptionError))
        {
            return descriptionError;
        }

        if (subtype == "task_started")
        {
            return TaskStarted(raw, taskId, description, toolUseId, taskUuid, taskSession);
        }

        if (raw.ElementOrNull("usage") is not { ValueKind: JsonValueKind.Object } usage)
        {
            return JsonReading.Missing("usage", line);
        }

        return new TaskProgressMessage(taskId, description, taskUuid, taskSession, raw)
        {
            ToolUseId = toolUseId,
            SubagentType = raw.StringOrNull("subagent_type"),
            TotalTokens = usage.Int64OrNull("total_tokens"),
            ToolUseCount = usage.Int64OrNull("tool_uses"),
            Duration = WireTime.Milliseconds(usage.Int64OrNull("duration_ms")),
            LastToolName = Identity.Tool(raw.StringOrNull("last_tool_name")),
            Summary = raw.StringOrNull("summary"),
        };
    }

    // One type a kind of task, by task_type; a kind this library has no type for, or none named, is
    // the unknown of the set, with the members every kind carries.
    private static TaskStartedMessage TaskStarted(
        JsonElement raw,
        string taskId,
        string description,
        string? toolUseId,
        string taskUuid,
        string taskSession)
    {
        var taskType = raw.StringOrNull("task_type");
        return taskType switch
        {
            "local_agent" => new LocalAgentTaskStartedMessage(taskId, description, taskUuid, taskSession, raw)
            {
                SubagentType = raw.StringOrNull("subagent_type"),
                IsBackgrounded = raw.BoolOrNull("is_backgrounded"),
                SpawnDepth = raw.Int64OrNull("spawn_depth"),
                Prompt = raw.StringOrNull("prompt"),
                ToolUseId = toolUseId,
                SkipTranscript = raw.BoolOrNull("skip_transcript"),
                Ambient = raw.BoolOrNull("ambient"),
            },
            "local_bash" => new LocalBashTaskStartedMessage(taskId, description, taskUuid, taskSession, raw)
            {
                IsBackgrounded = raw.BoolOrNull("is_backgrounded"),
                ToolUseId = toolUseId,
                SkipTranscript = raw.BoolOrNull("skip_transcript"),
                Ambient = raw.BoolOrNull("ambient"),
            },
            "local_workflow" => new LocalWorkflowTaskStartedMessage(taskId, description, taskUuid, taskSession, raw)
            {
                WorkflowName = raw.StringOrNull("workflow_name"),
                Prompt = raw.StringOrNull("prompt"),
                ToolUseId = toolUseId,
                SkipTranscript = raw.BoolOrNull("skip_transcript"),
                Ambient = raw.BoolOrNull("ambient"),
            },
            _ => new UnknownTaskStartedMessage(taskType is null ? default : new TaskType(taskType), taskId, description, taskUuid, taskSession, raw)
            {
                ToolUseId = toolUseId,
                SkipTranscript = raw.BoolOrNull("skip_transcript"),
                Ambient = raw.BoolOrNull("ambient"),
            },
        };
    }
}
