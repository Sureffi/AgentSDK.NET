namespace Claude.Agent.Tests.Wire;

// Every kind of message the binary sends is a type of its own, and each shape nested in one is a
// type too. The lines here are written for the check they stand in, never taken from a recording:
// what the recordings hold is held by the replay tests beside these. Each is written with ' where
// the wire has ", so a line reads as one thing; nothing here carries an apostrophe.
[TestClass]
public sealed class MessageKindTests
{
    public static IEnumerable<object[]> DeclaredSubtypes()
    {
        yield return [Notice("init"), typeof(SystemInitMessage)];
        yield return [Notice("compact_boundary", "'compact_metadata':{'trigger':'auto','pre_tokens':10}"), typeof(CompactBoundaryMessage)];
        yield return [Notice("status", "'status':'compacting'"), typeof(StatusMessage)];
        yield return [Notice("api_retry", "'attempt':2,'max_retries':5,'retry_delay_ms':250"), typeof(ApiRetryMessage)];
        yield return [Notice("control_request_progress", "'request_id':'r-1','status':'api_retry'"), typeof(ControlRequestProgressMessage)];
        yield return [Notice("model_refusal_fallback", "'trigger':'refusal','direction':'retry'"), typeof(ModelRefusalFallbackMessage)];
        yield return [Notice("model_refusal_no_fallback", "'original_model':'claude-haiku-4-5'"), typeof(ModelRefusalNoFallbackMessage)];
        yield return [Notice("local_command_output", "'content':'two files'"), typeof(LocalCommandOutputMessage)];
        yield return [Notice("hook_started", "'hook_id':'h-1','hook_name':'PreToolUse:Bash','hook_event':'PreToolUse'"), typeof(HookStartedMessage)];
        yield return [Notice("hook_progress", "'hook_id':'h-1','stdout':'half way'"), typeof(HookProgressMessage)];
        yield return [Notice("hook_response", "'hook_id':'h-1','outcome':'success','exit_code':0"), typeof(HookResponseMessage)];
        yield return [Notice("plugin_install", "'status':'installed','name':'mill'"), typeof(PluginInstallMessage)];
        yield return [Notice("background_tasks_changed", "'tasks':[]"), typeof(BackgroundTasksChangedMessage)];
        yield return [Notice("thinking_tokens", "'estimated_tokens':120,'estimated_tokens_delta':40"), typeof(ThinkingTokensMessage)];
        yield return [Notice("session_state_changed", "'state':'idle'"), typeof(SessionStateChangedMessage)];
        yield return [Notice("worker_shutting_down", "'reason':'the worker was told to go'"), typeof(WorkerShuttingDownMessage)];
        yield return [Notice("commands_changed", "'commands':[]"), typeof(CommandsChangedMessage)];
        yield return [Notice("notification", "'key':'cost','text':'spent','priority':'low'"), typeof(NotificationMessage)];
        yield return [Notice("files_persisted", "'files':[],'failed':[],'processed_at':'2026-09-21T09:00:00Z'"), typeof(FilesPersistedMessage)];
        yield return [Notice("memory_recall", "'mode':'select','memories':[]"), typeof(MemoryRecallMessage)];
        yield return [Notice("elicitation_complete", "'mcp_server_name':'echo','elicitation_id':'e-1'"), typeof(ElicitationCompleteMessage)];
        yield return [Notice("permission_denied", "'tool_name':'Write','tool_use_id':'toolu_1','message':'no'"), typeof(PermissionDeniedMessage)];
        yield return [Notice("informational", "'content':'the sandbox is off','level':'warning'"), typeof(InformationalMessage)];
        yield return [Notice("task_started", "'task_id':'t-1','description':'read the notes','task_type':'local_bash'"), typeof(LocalBashTaskStartedMessage)];
        yield return [Notice("task_progress", "'task_id':'t-1','description':'reading','usage':{'total_tokens':12}"), typeof(TaskProgressMessage)];
        yield return [Notice("task_notification", "'task_id':'t-1','status':'completed','output_file':'/tmp/t-1.output','summary':'done'"), typeof(TaskNotificationMessage)];
        yield return [Notice("task_updated", "'task_id':'t-1','patch':{'status':'running'}"), typeof(TaskUpdatedMessage)];
    }

    public static IEnumerable<object[]> FramesOutsideTheSystemUnion()
    {
        yield return [Frame("tool_progress", "'tool_use_id':'toolu_1','tool_name':'Bash','elapsed_time_seconds':1.5"), typeof(ToolProgressMessage)];
        yield return [Frame("tool_use_summary", "'summary':'read three files'"), typeof(ToolUseSummaryMessage)];
        yield return [Frame("auth_status", "'isAuthenticating':true,'output':['waiting']"), typeof(AuthStatusMessage)];
        yield return [Frame("prompt_suggestion", "'suggestion':'run the tests'"), typeof(PromptSuggestionMessage)];
        yield return [Frame("active_goal", "'value':{'condition':'tests pass','iterations':2}"), typeof(ActiveGoalMessage)];
    }

    [TestMethod]
    [DynamicData(nameof(DeclaredSubtypes), DynamicDataSourceType.Method)]
    public void EverySystemSubtypeTheBinaryDeclaresIsItsOwnMessageTypeCarryingItsSubtype(string line, Type expected)
    {
        var message = Decode(line);

        Assert.AreEqual(expected, message.GetType());
        Assert.IsInstanceOfType<SystemMessage>(message, out var notice);
        StringAssert.Contains(line, $"\"subtype\":\"{notice.Subtype}\"");
    }

    [TestMethod]
    [DynamicData(nameof(FramesOutsideTheSystemUnion), DynamicDataSourceType.Method)]
    public void EveryFrameOutsideTheSystemUnionIsItsOwnMessageType(string line, Type expected) =>
        Assert.AreEqual(expected, Decode(line).GetType());

    // A task's start is one type a kind of task, each with only its own members; a kind this library
    // has no type for, or none named, is the unknown of that set, its kind's name beside the members
    // every kind carries and the line.
    [TestMethod]
    public void ATaskStartIsTheTypeOfItsKindWithOnlyItsOwnMembers()
    {
        var agent = (LocalAgentTaskStartedMessage)Decode(Notice(
            "task_started",
            "'task_id':'t-1','tool_use_id':'toolu_1','description':'read','subagent_type':'reader','is_backgrounded':false,'spawn_depth':2,'task_type':'local_agent','prompt':'read the notes','skip_transcript':true,'ambient':true"));
        Assert.AreEqual(TaskType.LocalAgent, agent.TaskType);
        Assert.AreEqual("t-1", agent.TaskId);
        Assert.AreEqual("toolu_1", agent.ToolUseId);
        Assert.AreEqual("read", agent.Description);
        Assert.AreEqual("reader", agent.SubagentType);
        Assert.AreEqual(false, agent.IsBackgrounded);
        Assert.AreEqual(2L, agent.SpawnDepth);
        Assert.AreEqual("read the notes", agent.Prompt);
        Assert.AreEqual(true, agent.SkipTranscript);
        Assert.AreEqual(true, agent.Ambient);

        var shell = (LocalBashTaskStartedMessage)Decode(Notice("task_started", "'task_id':'t-2','description':'sleep 5','is_backgrounded':true,'task_type':'local_bash'"));
        Assert.AreEqual(TaskType.LocalBash, shell.TaskType);
        Assert.AreEqual(true, shell.IsBackgrounded);

        var workflow = (LocalWorkflowTaskStartedMessage)Decode(Notice(
            "task_started",
            "'task_id':'t-3','description':'spec','task_type':'local_workflow','workflow_name':'spec','prompt':'run the spec'"));
        Assert.AreEqual(TaskType.LocalWorkflow, workflow.TaskType);
        Assert.AreEqual("spec", workflow.WorkflowName);
        Assert.AreEqual("run the spec", workflow.Prompt);

        var unnamed = (UnknownTaskStartedMessage)Decode(Notice("task_started", "'task_id':'t-4','description':'dreaming','task_type':'dream','tool_use_id':'toolu_4'"));
        Assert.AreEqual(new TaskType("dream"), unnamed.TaskType);
        Assert.IsFalse(unnamed.TaskType.IsDefined);
        Assert.AreEqual("t-4", unnamed.TaskId);
        Assert.AreEqual("toolu_4", unnamed.ToolUseId);
        Assert.AreEqual("dream", unnamed.Line().GetProperty("task_type").GetString());

        var none = (UnknownTaskStartedMessage)Decode(Notice("task_started", "'task_id':'t-5','description':'no kind'"));
        Assert.AreEqual(string.Empty, none.TaskType.ToString());
        Assert.AreEqual("no kind", none.Description);
    }

    // A pushed command list carries the same entries as the initialize reply, the mark on the
    // binary's own commands included; a command without it is null.
    [TestMethod]
    public void ACommandsChangedEntryCarriesTheMarkOnTheBinarysOwnCommands()
    {
        var changed = (CommandsChangedMessage)Decode(Notice(
            "commands_changed",
            "'commands':[{'name':'clear','description':'clears','argumentHint':'','builtin':true},{'name':'mill','description':'grinds','argumentHint':'[grain]','aliases':['grind']}]"));

        Assert.AreEqual(true, changed.Commands[0].IsBuiltIn);
        Assert.IsNull(changed.Commands[1].IsBuiltIn);
        CollectionAssert.AreEqual(new[] { "grind" }, changed.Commands[1].Aliases.ToArray());
    }

    [TestMethod]
    public void ASystemSubtypeWithNoTypeOfItsOwnIsTheUnknownOfTheMessageSetWithItsTypeAndSubtype()
    {
        var message = Decode(Notice("moon_phase_changed", "'phase':'waxing'"));

        Assert.IsInstanceOfType<UnknownMessage>(message, out var unknown);
        Assert.AreEqual("system", unknown.Type);
        Assert.AreEqual("moon_phase_changed", unknown.Subtype);
        Assert.AreEqual("waxing", unknown.Line().GetProperty("phase").GetString());
    }

    [TestMethod]
    public void AnAssistantsApiMessageReadsWholeWithItsStopDetailsContainerContextManagementAndDiagnostics()
    {
        var message = Decode(Frame(
            "assistant",
            """
            'message':{
              'id':'msg_1','model':'claude-haiku-4-5','stop_reason':'refusal','stop_sequence':null,
              'content':[{'type':'text','text':'no'}],
              'stop_details':{'type':'refusal','category':'cyber','explanation':'it was asked not to'},
              'container':{'id':'c-1','expires_at':'2026-09-21T10:00:00Z','skills':[{'skill_id':'s-1','type':'custom','version':'2'}]},
              'context_management':{'applied_edits':[{'type':'clear_tool_uses_20250919','cleared_input_tokens':900,'cleared_tool_uses':3}]},
              'diagnostics':{'cache_miss_reason':{'type':'model_changed','cache_missed_input_tokens':120}},
              'input_transformations':[{'type':'thinking_dropped','path':'message.content','reason':'model_binding_mismatch'}]}
            """));

        Assert.IsInstanceOfType<AssistantMessage>(message, out var assistant);
        var api = assistant.Message;
        Assert.AreEqual("msg_1", api.Id);
        Assert.AreEqual(StopReason.Refusal, api.StopReason);
        Assert.AreEqual(StopDetailsType.Refusal, api.StopDetails!.Type);
        Assert.AreEqual(RefusalCategory.Cyber, api.StopDetails.Category);
        Assert.AreEqual("it was asked not to", api.StopDetails.Explanation);
        Assert.AreEqual(ContainerSkillType.Custom, api.Container!.Skills.Single().Type);
        Assert.AreEqual(ContextEditType.ClearToolUses20250919, api.ContextManagement!.AppliedEdits.Single().Type);
        Assert.AreEqual(900, api.ContextManagement.AppliedEdits.Single().ClearedInputTokens);
        Assert.AreEqual(3, api.ContextManagement.AppliedEdits.Single().ClearedToolUses);
        Assert.AreEqual(CacheMissReasonType.ModelChanged, api.Diagnostics!.CacheMissReason!.Type);
        Assert.AreEqual(120, api.Diagnostics.CacheMissReason.CacheMissedInputTokens);
        Assert.AreEqual(InputTransformationType.ThinkingDropped, api.InputTransformations.Single().Type);
        Assert.AreEqual(InputTransformationReason.ModelBindingMismatch, api.InputTransformations.Single().Reason);
    }

    [TestMethod]
    public void AnAssistantCarriesItsContextUsageItsUsageReportAndTheToolInputsAsTheWireHadThem()
    {
        var message = Decode(Frame(
            "assistant",
            """
            'message':{'model':'claude-haiku-4-5','content':[{'type':'text','text':'hi'}]},
            'context_usage':{'model':'claude-haiku-4-5','total_tokens':4000,'raw_max_tokens':200000,'percentage':2.0,
              'over_limit':{'tokens_over':12,'kind':'hard_limit'},
              'categories':[{'name':'System prompt','tokens':1200,'kind':'used'}],
              'skills':[{'name':'bees','source':'plugin','tokens':90,'plugin_name':'apiary'}]},
            'usage_report':{'session':{'total_cost_usd':0.25,'total_duration_ms':8000,'total_lines_added':3},
              'rate_limits':{'limits':[{'kind':'five_hour','group':'default','percent':12.5,'is_active':true}],
                'extra_usage':{'is_enabled':true,'monthly_limit':40.0,'currency':'USD'}}},
            'wire_tool_inputs':{'toolu_1':{'command':'ls'}}
            """));

        Assert.IsInstanceOfType<AssistantMessage>(message, out var assistant);
        Assert.AreEqual(ContextUsageLimitKind.HardLimit, assistant.ContextUsage!.OverLimit!.Kind);
        Assert.AreEqual(12, assistant.ContextUsage.OverLimit.TokensOver);
        Assert.AreEqual(ContextSpace.Used, assistant.ContextUsage.Categories.Single().Kind);
        Assert.AreEqual("apiary", assistant.ContextUsage.Skills.Single().PluginName);
        Assert.AreEqual(0.25m, assistant.UsageReport!.Session.TotalCostUsd);
        Assert.AreEqual(TimeSpan.FromSeconds(8), assistant.UsageReport.Session.TotalDuration);
        Assert.AreEqual(12.5, assistant.UsageReport.RateLimits!.Limits.Single().Percent);
        Assert.IsTrue(assistant.UsageReport.RateLimits.Limits.Single().IsActive is true);
        Assert.AreEqual("USD", assistant.UsageReport.RateLimits.ExtraUsage!.Currency);
        Assert.AreEqual("ls", assistant.RawWireToolInputs["toolu_1"].GetProperty("command").GetString());
    }

    [TestMethod]
    public void AUserTurnsOriginIsTheTypeOfItsKindAndAKindWithNoTypeIsTheUnknownOriginWithItsObject()
    {
        Assert.IsInstanceOfType<HumanOrigin>(OriginOf("{'kind':'human'}"));
        Assert.IsInstanceOfType<ChannelOrigin>(OriginOf("{'kind':'channel','server':'slack'}"), out var channel);
        Assert.AreEqual("slack", channel.Server);

        Assert.IsInstanceOfType<PeerOrigin>(
            OriginOf("{'kind':'peer','from':'daedalus','fromMode':'bypass','verifiedPeerPid':8121}"),
            out var peer);
        Assert.AreEqual("daedalus", peer.From);
        Assert.AreEqual(PeerOriginMode.Bypass, peer.FromMode);
        Assert.AreEqual(8121, peer.VerifiedPeerProcessId);

        Assert.IsInstanceOfType<TaskNotificationOrigin>(
            OriginOf("{'kind':'task-notification','subkind':'scheduled-trigger'}"),
            out var notified);
        Assert.AreEqual(TaskNotificationSource.ScheduledTrigger, notified.Subkind);

        Assert.IsInstanceOfType<ObserverOrigin>(OriginOf("{'kind':'observer','from':'watcher','senderTaskId':'t-1'}"));
        Assert.IsInstanceOfType<CoordinatorOrigin>(OriginOf("{'kind':'coordinator'}"));
        Assert.IsInstanceOfType<UnclassifiedOrigin>(OriginOf("{'kind':'unclassified'}"));
        Assert.IsInstanceOfType<AutoContinuationOrigin>(OriginOf("{'kind':'auto-continuation'}"));
        Assert.IsInstanceOfType<ObserverActivityOrigin>(OriginOf("{'kind':'observer-activity'}"));

        Assert.IsInstanceOfType<UnknownMessageOrigin>(OriginOf("{'kind':'carrier-pigeon','bird':'hen'}"), out var unknown);
        Assert.AreEqual("carrier-pigeon", unknown.Type);
        Assert.AreEqual("hen", unknown.Raw.GetProperty("bird").GetString());
    }

    [TestMethod]
    public void AUserTurnCarriesItsRoleItsPriorityAndTheNotesOnItsToolResults()
    {
        var message = Decode(Frame(
            "user",
            """
            'priority':'now','isReplay':true,
            'message':{'role':'user','content':'ping'},
            'tool_result_meta':[{'id':'toolu_1','non_execution_kind':'denied'}]
            """));

        Assert.IsInstanceOfType<UserMessage>(message, out var user);
        Assert.AreEqual(MessageRole.User, user.Role);
        Assert.AreEqual(UserPriority.Now, user.Priority);
        Assert.IsTrue(user.IsReplay);
        Assert.AreEqual("ping", user.Text);
        Assert.AreEqual("toolu_1", user.ToolResultNotes.Single().Id);
        Assert.AreEqual("denied", user.ToolResultNotes.Single().NonExecutionKind);
    }

    [TestMethod]
    public void AResultCarriesItsStartupFailureItsFastModeItsSubagentStatisticsAndItsTimings()
    {
        var message = Decode(Frame(
            "result",
            """
            'subtype':'error_during_execution','duration_ms':1200,'duration_api_ms':900,'is_error':true,'num_turns':2,
            'startup_failure_reason':'cwd_unavailable','fast_mode_state':'off','fast_mode_disabled_reason':'preference',
            'queued_turn_count':3,'result_index':1,
            'subagent_stats':{'spawned':4,'completed':3,'failed':1,'killed':{'user':1},'by_type':{'bee':2}},
            'ttft_ms':180,'time_origin_ms':1790000000000
            """));

        Assert.IsInstanceOfType<ResultMessage>(message, out var result);
        Assert.AreEqual(ResultMessageSubtype.ErrorDuringExecution, result.Subtype);
        Assert.AreEqual(StartupFailureReason.CwdUnavailable, result.StartupFailureReason);
        Assert.AreEqual(FastModeState.Off, result.FastModeState);
        Assert.AreEqual(FastModeDisabledReason.Preference, result.FastModeDisabledReason);
        Assert.AreEqual(3, result.QueuedTurnCount);
        Assert.AreEqual(1, result.ResultIndex);
        Assert.AreEqual(4, result.SubagentStatistics!.Spawned);
        Assert.AreEqual(1, result.SubagentStatistics.Killed!.User);
        Assert.AreEqual(2, result.SubagentStatistics.ByType["bee"]);
        Assert.AreEqual(TimeSpan.FromMilliseconds(180), result.TimeToFirstToken);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeMilliseconds(1790000000000), result.TimeOrigin);
    }

    [TestMethod]
    public void ARateLimitEventCarriesItsScopeItsErrorCodeItsOverageAndEachUnifiedWindow()
    {
        var message = Decode(Frame(
            "rate_limit_event",
            """
            'rate_limit_info':{'status':'allowed_warning','rateLimitType':'five_hour','utilization':82.5,
              'overageStatus':'allowed','isUsingOverage':true,'surpassedThreshold':80.0,
              'limitScope':'service','errorCode':'credits_required','canUserPurchaseCredits':true,
              'unifiedWindows':{'five_hour':{'utilization':82.5,'resetsAt':1790000000}}}
            """));

        Assert.IsInstanceOfType<RateLimitMessage>(message, out var limit);
        Assert.AreEqual(RateLimitStatus.AllowedWarning, limit.Status);
        Assert.AreEqual(RateLimitStatus.Allowed, limit.OverageStatus);
        Assert.AreEqual(RateLimitType.FiveHour, limit.LimitType);
        Assert.AreEqual(RateLimitScope.Service, limit.LimitScope);
        Assert.AreEqual(RateLimitErrorCode.CreditsRequired, limit.ErrorCode);
        Assert.IsTrue(limit.IsUsingOverage is true);
        Assert.IsTrue(limit.CanUserPurchaseCredits is true);
        Assert.AreEqual(80.0, limit.SurpassedThreshold);
        Assert.AreEqual(82.5, limit.UnifiedWindows["five_hour"].Utilization);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1790000000), limit.UnifiedWindows["five_hour"].ResetsAt);
    }

    [TestMethod]
    public void AnInitCarriesItsPluginsItsMcpServersItsMemoryPathsAndItsBetas()
    {
        var message = Decode(Notice(
            "init",
            """
            'cwd':'/tmp/work','betas':['context-1m'],'memory_paths':{'auto':'/tmp/work/.memory'},
            'plugins':[{'name':'mill','path':'/opt/mill','version':'3'}],
            'mcp_servers':[{'name':'echo','status':'connected','source':'project'},{'name':'  ','status':'failed'}],
            'messaging_socket_path':'/run/claude.sock','effort':'low','fast_mode_state':'on'
            """));

        Assert.IsInstanceOfType<SystemInitMessage>(message, out var init);
        Assert.AreEqual("/tmp/work", init.WorkingDirectory);
        CollectionAssert.AreEqual(new[] { "context-1m" }, init.Betas.ToArray());
        Assert.AreEqual("/tmp/work/.memory", init.MemoryPaths!.Auto);
        Assert.AreEqual("mill", init.Plugins.Single().Name);
        Assert.AreEqual("3", init.Plugins.Single().Version);
        Assert.AreEqual("echo", init.McpServers.Single().Name);
        Assert.AreEqual(McpServerConnectionStatus.Connected, init.McpServers.Single().Status);
        Assert.AreEqual(new McpServerSource("project"), init.McpServers.Single().Source);
        Assert.AreEqual("/run/claude.sock", init.MessagingSocketPath);
        Assert.AreEqual(Effort.Low, init.Effort);
        Assert.AreEqual(FastModeState.On, init.FastModeState);
    }

    [TestMethod]
    public void ATaskPatchIsTerminalOnlyWhereItsStatusEndsTheTask()
    {
        foreach (var (status, terminal) in new[]
        {
            ("pending", false),
            ("running", false),
            ("paused", false),
            ("completed", true),
            ("failed", true),
            ("killed", true),
        })
        {
            var message = Decode(Notice("task_updated", $"'task_id':'t-1','patch':{{'status':'{status}'}}"));

            Assert.IsInstanceOfType<TaskUpdatedMessage>(message, out var updated);
            Assert.AreEqual(new BackgroundTaskStatus(status), updated.Patch.Status);
            Assert.AreEqual(terminal, updated.Patch.IsTerminal, $"'{status}' was read the other way round.");
        }
    }

    [TestMethod]
    public void ATaskNotificationCarriesItsResourceLinksAndWhatTheTaskSpent()
    {
        var message = Decode(Notice(
            "task_notification",
            """
            'task_id':'t-1','status':'stopped','output_file':'/tmp/t-1.output','summary':'killed',
            'reason':'worker_restart','ambient':true,
            'usage':{'total_tokens':4200,'tool_uses':7,'duration_ms':3000},
            'resource_links':[{'uri':'file:///tmp/a.md','name':'notes','mimeType':'text/markdown','size':90}]
            """));

        Assert.IsInstanceOfType<TaskNotificationMessage>(message, out var notification);
        Assert.AreEqual(TaskNotificationStatus.Stopped, notification.Status);
        Assert.AreEqual(TaskNotificationReason.WorkerRestart, notification.Reason);
        Assert.AreEqual(4200, notification.TotalTokens);
        Assert.AreEqual(7, notification.ToolUseCount);
        Assert.AreEqual(TimeSpan.FromSeconds(3), notification.Duration);
        Assert.IsTrue(notification.Ambient is true);
        Assert.AreEqual("notes", notification.ResourceLinks.Single().Name);
        Assert.AreEqual("text/markdown", notification.ResourceLinks.Single().MimeType);
        Assert.AreEqual(90, notification.ResourceLinks.Single().Size);
    }

    [TestMethod]
    public void ACompactBoundaryCarriesWhatStartedTheCompactionAndWhatItKept()
    {
        var message = Decode(Notice(
            "compact_boundary",
            """
            'compact_metadata':{'trigger':'manual','pre_tokens':150000,'post_tokens':20000,'duration_ms':4000,
              'preserved_segment':{'head_uuid':'a','anchor_uuid':'b','tail_uuid':'c'},
              'preserved_messages':{'anchor_uuid':'b','uuids':['b','c']}}
            """));

        Assert.IsInstanceOfType<CompactBoundaryMessage>(message, out var boundary);
        var metadata = boundary.CompactMetadata;
        Assert.AreEqual(CompactTrigger.Manual, metadata.Trigger);
        Assert.AreEqual(150000, metadata.PreTokens);
        Assert.AreEqual(20000, metadata.PostTokens);
        Assert.AreEqual(TimeSpan.FromSeconds(4), metadata.Duration);
        Assert.AreEqual("a", metadata.PreservedSegment!.HeadUuid);
        Assert.AreEqual("c", metadata.PreservedSegment.TailUuid);
        CollectionAssert.AreEqual(new[] { "b", "c" }, metadata.PreservedMessages!.Uuids.ToArray());
    }

    // The two keys the recorded compaction frame carries beside the ones above: what every
    // compaction of the session has dropped, and the message the compacted branch hangs off.
    [TestMethod]
    public void ACompactBoundaryCarriesWhatTheCompactionsDroppedAndTheMessageTheBranchHangsOff()
    {
        var message = Decode(Notice(
            "compact_boundary",
            """
            'compact_metadata':{'trigger':'manual','pre_tokens':23008,'post_tokens':1801,
              'cumulative_dropped_tokens':21207,'duration_ms':13790},
            'logical_parent_uuid':'f50d7c2d-0000-4000-8000-000000000000'
            """));

        Assert.IsInstanceOfType<CompactBoundaryMessage>(message, out var boundary);
        Assert.AreEqual(21207, boundary.CompactMetadata.CumulativeDroppedTokens);
        Assert.AreEqual("f50d7c2d-0000-4000-8000-000000000000", boundary.LogicalParentUuid);
    }

    [TestMethod]
    public void AHookNoticeCarriesItsEventItsStreamsAndHowItEnded()
    {
        var message = Decode(Notice(
            "hook_response",
            """
            'hook_id':'h-1','hook_name':'PostToolUse:Write','hook_event':'PostToolUse',
            'output':'kept','stdout':'kept','stderr':'','outcome':'error','exit_code':2
            """));

        Assert.IsInstanceOfType<HookResponseMessage>(message, out var response);
        Assert.AreEqual(HookEvent.PostToolUse, response.HookEvent);
        Assert.AreEqual(HookOutcome.Error, response.Outcome);
        Assert.AreEqual("kept", response.Output);
        Assert.AreEqual(string.Empty, response.StandardError);
        Assert.AreEqual(2, response.ExitCode);
    }

    [TestMethod]
    public void AMemoryRecallCarriesEachMemoryItsPathItsScopeAndWhatItSays()
    {
        var message = Decode(Notice(
            "memory_recall",
            "'mode':'synthesize','memories':[{'path':'/tmp/work/.memory/a.md','scope':'team','content':'one line'}]"));

        Assert.IsInstanceOfType<MemoryRecallMessage>(message, out var recall);
        Assert.AreEqual(MemoryRecallMode.Synthesize, recall.Mode);
        Assert.AreEqual("/tmp/work/.memory/a.md", recall.Memories.Single().Path);
        Assert.AreEqual(MemoryScope.Team, recall.Memories.Single().Scope);
        Assert.AreEqual("one line", recall.Memories.Single().Content);
    }

    // A member the library reads wrongly is that member's own loss: the notice is handed out all the
    // same, that member null, and the line is on it whole.
    [TestMethod]
    public void AMemberOfANoticeThatDoesNotFitIsNullAndTheNoticeIsStillHandedOut()
    {
        var message = Decode(Notice("notification", "'key':'cost','text':'spent','priority':'low','timeout_ms':'soon'"));

        Assert.IsInstanceOfType<NotificationMessage>(message, out var notification);
        Assert.IsNull(notification.Timeout);
        Assert.AreEqual("cost", notification.Key);
        Assert.AreEqual(NotificationPriority.Low, notification.Priority);
        Assert.AreEqual("soon", notification.Line().GetProperty("timeout_ms").GetString());
    }

    [TestMethod]
    public void AToolProgressCarriesItsElapsedTimeAndTheRetryOfTheSubagentBehindIt()
    {
        var message = Decode(Frame(
            "tool_progress",
            """
            'tool_use_id':'toolu_1','tool_name':'Agent','elapsed_time_seconds':12.5,'task_id':'t-1','heartbeat':true,
            'subagent_retry':{'agent_id':'a-1','attempt':2,'max_retries':3,'retry_delay_ms':500,
              'error_category':'overloaded','error_status':529}
            """));

        Assert.IsInstanceOfType<ToolProgressMessage>(message, out var progress);
        Assert.AreEqual(TimeSpan.FromMilliseconds(12500), progress.ElapsedTime);
        Assert.AreEqual("t-1", progress.TaskId);
        Assert.IsTrue(progress.Heartbeat is true);
        Assert.AreEqual(2, progress.SubagentRetry!.Attempt);
        Assert.AreEqual(TimeSpan.FromMilliseconds(500), progress.SubagentRetry.RetryDelay);
        Assert.AreEqual("overloaded", progress.SubagentRetry.ErrorCategory);
        Assert.AreEqual(529, progress.SubagentRetry.ErrorStatus);
    }

    [TestMethod]
    public void AnActiveGoalCarriesTheConditionTheSessionWorksTowardsAndAClearedOneCarriesNone()
    {
        Assert.IsInstanceOfType<ActiveGoalMessage>(
            Decode(Frame(
                "active_goal",
                "'value':{'condition':'the tests pass','iterations':3,'set_at':1790000000000,'tokens_at_start':900,'last_reason':'still red'}")),
            out var set);
        Assert.AreEqual("the tests pass", set.Value!.Condition);
        Assert.AreEqual(3, set.Value.Iterations);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeMilliseconds(1790000000000), set.Value.SetAt);
        Assert.AreEqual(900, set.Value.TokensAtStart);
        Assert.AreEqual("still red", set.Value.LastReason);

        Assert.IsInstanceOfType<ActiveGoalMessage>(Decode(Frame("active_goal", "'value':null")), out var cleared);
        Assert.IsNull(cleared.Value);
    }

    private static MessageOrigin? OriginOf(string origin)
    {
        var message = Decode(Frame("user", $"'origin':{origin},'message':{{'role':'user','content':'ping'}}"));

        Assert.IsInstanceOfType<UserMessage>(message, out var user);
        return user.Origin;
    }

    private static string Notice(string subtype, string members = "") =>
        Frame("system", members.Length == 0 ? $"'subtype':'{subtype}'" : $"'subtype':'{subtype}',{members}");

    private static string Frame(string type, string members) =>
        $"{{'type':'{type}','uuid':'u-1','session_id':'s-1',{members}}}".Replace('\'', '"');

    private static AgentMessage Decode(string line)
    {
        var decoded = new FrameCodec().Decode(line);

        Assert.IsTrue(decoded.IsOk, Recorded.Reason(decoded));
        Assert.IsInstanceOfType<MessageFrame>(decoded.Value, out var frame);
        return frame.Message;
    }
}
