using System.Text.Json;
using Claude.Agent.Tests.Wire;

namespace Claude.Agent.Tests.Hooks;

// Twenty-four of the thirty-three events are declared and never recorded, so they are held here on
// hand-written lines: the wire text is this file's own, one line per event, carrying the members the
// declaration gives that event and nothing else. The nine events a recording carries are held by the
// replays in Sessions/ and Wire/; what a recording never fills — Stop's session crons, and every
// background task's kind but a shell and a subagent, among them — is held here as well.
//
// Each case is written as the event's own members alone, beginning with the comma that joins them to
// the common ones, and Parse below puts the line together.
[TestClass]
public sealed class HookInputDecodeTests
{
    private const string SessionId = "1e0b4a55-0000-4000-8000-00000000c0de";
    private const string TranscriptPath = "/var/agent/transcripts/build.jsonl";
    private const string WorkingDirectory = "/srv/build";

    [TestMethod]
    public void EveryHookInputCarriesTheCommonMembersItsEventNameAndKeepsTheWholeLineForTheCodec()
    {
        var input = Decode<SetupHookInput>(
            "Setup",
            """
            ,"prompt_id":"prompt-41","permission_mode":"acceptEdits","agent_id":"agent-9","agent_type":"reviewer","effort":{"level":"high"},"scratchpad_dir":"/srv/build/.scratch","trigger":"maintenance"
            """);

        Assert.AreEqual(HookEvent.Setup, input.Event);
        Assert.AreEqual(SessionId, input.SessionId);
        Assert.AreEqual(TranscriptPath, input.TranscriptPath);
        Assert.AreEqual(WorkingDirectory, input.WorkingDirectory);
        Assert.AreEqual("prompt-41", input.PromptId);
        Assert.AreEqual(PermissionMode.AcceptEdits, input.PermissionMode);
        Assert.AreEqual("agent-9", input.AgentId);
        Assert.AreEqual(new AgentType("reviewer"), input.AgentType);
        Assert.AreEqual(Effort.High, input.Effort);
        Assert.AreEqual("/srv/build/.scratch", input.ScratchpadDirectory);
        Assert.AreEqual(SetupTrigger.Maintenance, input.Trigger);
        Assert.AreEqual("maintenance", input.Json.GetProperty("trigger").GetString());
    }

    // The members the binary did not send are null, not empty: a caller can tell "not said" from
    // "said to be nothing".
    [TestMethod]
    public void TheCommonMembersTheBinaryDidNotSendAreNull()
    {
        var input = Decode<SetupHookInput>(
            "Setup",
            """
            ,"trigger":"init"
            """);

        Assert.IsNull(input.PromptId);
        Assert.IsNull(input.PermissionMode);
        Assert.IsNull(input.AgentId);
        Assert.IsNull(input.AgentType);
        Assert.IsNull(input.Effort);
        Assert.IsNull(input.ScratchpadDirectory);
        Assert.AreEqual(SetupTrigger.Initialization, input.Trigger);
    }

    [TestMethod]
    public void ABatchOfToolCallsReadsEachEntryAnsweredWhereItHasAnOutputAndStartedWhereItDoesNot()
    {
        var input = Decode<PostToolBatchHookInput>(
            "PostToolBatch",
            """
            ,"tool_calls":[{"tool_name":"Read","tool_input":{"file_path":"/srv/build/log.txt"},"tool_use_id":"toolu_batch_1","tool_response":{"content":"one line"}},{"tool_name":"Grep","tool_input":{"pattern":"fail"},"tool_use_id":"toolu_batch_2"}]
            """);

        Assert.AreEqual(2, input.ToolCalls.Count);

        var answered = input.ToolCalls[0];
        Assert.AreEqual("Read", answered.Name);
        Assert.AreEqual("toolu_batch_1", answered.Id);
        Assert.AreEqual(ToolCallStatus.Answered, answered.Status);
        Assert.AreEqual("one line", answered.RawOutput!.Value.GetProperty("content").GetString());

        var started = input.ToolCalls[1];
        Assert.AreEqual("Grep", started.Name);
        Assert.AreEqual(ToolCallStatus.Started, started.Status);
        Assert.IsNull(started.RawOutput);
    }

    // The list is of what the binary named whole: an entry with no tool name is not a call, so it is
    // left out rather than read as one with an empty name.
    [TestMethod]
    public void ABatchEntryMissingItsToolNameInputOrIdIsLeftOutOfTheList()
    {
        var input = Decode<PostToolBatchHookInput>(
            "PostToolBatch",
            """
            ,"tool_calls":[{"tool_input":{},"tool_use_id":"toolu_batch_1"},{"tool_name":"Read","tool_use_id":"toolu_batch_2"},{"tool_name":"Glob","tool_input":{}},{"tool_name":"Bash","tool_input":{"command":"true"},"tool_use_id":"toolu_batch_4"}]
            """);

        Assert.AreEqual("Bash", input.ToolCalls.Single().Name);
    }

    [TestMethod]
    public void ANotificationCarriesItsMessageTitleAndTheTypeTheBinaryNamesItBy()
    {
        var input = Decode<NotificationHookInput>(
            "Notification",
            """
            ,"message":"The build needs an answer.","title":"Waiting","notification_type":"permission_prompt"
            """);

        Assert.AreEqual("The build needs an answer.", input.Message);
        Assert.AreEqual("Waiting", input.Title);
        Assert.AreEqual(new NotificationType("permission_prompt"), input.NotificationType);
        Assert.IsFalse(input.NotificationType.IsDefined, "no recording founds a notification type, so the library names none");
    }

    [TestMethod]
    public void AnExpansionCarriesWhatIsBeingExpandedTheCommandAndThePromptItProduced()
    {
        var input = Decode<UserPromptExpansionHookInput>(
            "UserPromptExpansion",
            """
            ,"expansion_type":"slash_command","command_name":"review","command_args":"--since yesterday","command_source":"project","prompt":"Review the changes since yesterday."
            """);

        Assert.AreEqual(UserPromptExpansionType.SlashCommand, input.ExpansionType);
        Assert.AreEqual("review", input.CommandName);
        Assert.AreEqual("--since yesterday", input.CommandArguments);
        Assert.AreEqual("project", input.CommandSource);
        Assert.AreEqual("Review the changes since yesterday.", input.Prompt);
    }

    [TestMethod]
    public void ASessionStartCarriesHowItBeganTheModelAndWhatItKnowsOfTheCache()
    {
        var input = Decode<SessionStartHookInput>(
            "SessionStart",
            """
            ,"source":"resume","model":"claude-haiku-4-5","session_title":"the build","seconds_since_last_response":92.5,"context_tokens":41200,"prompt_cache_likely_expired":true,"estimated_cache_write_usd":0.0413
            """);

        Assert.AreEqual(SessionStartSource.Resume, input.Source);
        Assert.AreEqual("claude-haiku-4-5", input.Model);
        Assert.AreEqual("the build", input.SessionTitle);
        Assert.AreEqual(TimeSpan.FromSeconds(92.5), input.TimeSinceLastResponse);
        Assert.AreEqual(41200L, input.ContextTokens);
        Assert.IsTrue(input.IsPromptCacheLikelyExpired);
        Assert.AreEqual(0.0413m, input.EstimatedCacheWriteCost);
    }

    [TestMethod]
    public void ASessionEndCarriesWhyItEnded()
    {
        var input = Decode<SessionEndHookInput>(
            "SessionEnd",
            """
            ,"reason":"prompt_input_exit"
            """);

        Assert.AreEqual(SessionEndReason.PromptInputExit, input.Reason);
    }

    // A task is one type a kind: a shell with its command, a subagent with its agent type, each with
    // only its own members, and a key the binary writes beside another kind is no member of either.
    // The session crons, which no recording fills, are held on the same hand-written line.
    [TestMethod]
    public void AStopCarriesTheBackgroundTasksEachAsItsKindsTypeAndTheSessionCronsStillStanding()
    {
        var input = Decode<StopHookInput>(
            "Stop",
            """
            ,"stop_hook_active":true,"last_assistant_message":"Done.","background_tasks":[{"id":"bg-1","type":"shell","status":"running","description":"tail the log","command":"tail -f log.txt"},{"id":"bg-2","type":"subagent","status":"pending","description":"second opinion","agent_type":"reviewer","server":"notes","tool":"search","name":"reviewer-2"}],"session_crons":[{"id":"cron-1","schedule":"0 9 * * 1","recurring":true,"prompt":"Open the week."}]
            """);

        Assert.IsTrue(input.IsStopHookActive);
        Assert.AreEqual("Done.", input.LastAssistantMessage);

        var shell = (ShellStopHookBackgroundTask)input.BackgroundTasks[0];
        Assert.AreEqual("bg-1", shell.Id);
        Assert.AreEqual(StopHookBackgroundTaskType.Shell, shell.TaskType);
        Assert.AreEqual(BackgroundTaskStatus.Running, shell.Status);
        Assert.AreEqual("tail the log", shell.Description);
        Assert.AreEqual("tail -f log.txt", shell.Command);

        var agent = (SubagentStopHookBackgroundTask)input.BackgroundTasks[1];
        Assert.AreEqual("bg-2", agent.Id);
        Assert.AreEqual(StopHookBackgroundTaskType.Subagent, agent.TaskType);
        Assert.AreEqual(BackgroundTaskStatus.Pending, agent.Status);
        Assert.AreEqual("second opinion", agent.Description);
        Assert.AreEqual(new AgentType("reviewer"), agent.AgentType);

        CollectionAssert.AreEquivalent(
            new[] { nameof(StopHookBackgroundTask.TaskType), nameof(StopHookBackgroundTask.Id), nameof(StopHookBackgroundTask.Status), nameof(StopHookBackgroundTask.Description), nameof(ShellStopHookBackgroundTask.Command) },
            typeof(ShellStopHookBackgroundTask).GetProperties().Select(property => property.Name).ToArray());
        CollectionAssert.AreEquivalent(
            new[] { nameof(StopHookBackgroundTask.TaskType), nameof(StopHookBackgroundTask.Id), nameof(StopHookBackgroundTask.Status), nameof(StopHookBackgroundTask.Description), nameof(SubagentStopHookBackgroundTask.AgentType) },
            typeof(SubagentStopHookBackgroundTask).GetProperties().Select(property => property.Name).ToArray());

        var cron = input.SessionCrons.Single();
        Assert.AreEqual("cron-1", cron.Id);
        Assert.AreEqual("0 9 * * 1", cron.Schedule);
        Assert.IsTrue(cron.IsRecurring);
        Assert.AreEqual("Open the week.", cron.Prompt);
    }

    // The binary 2.1.276 writes a task's kind through its label table, two kinds sharing monitor, and
    // a kind the table has no label for as the kind's own name, which the set does not name. The label
    // is which type the task is: shell and subagent have one, and every other label, named by the set
    // or not, is the unknown, the entry as the binary wrote it beside the members every kind carries.
    [TestMethod]
    public void AStoppedTurnsBackgroundTaskTypeIsTheBinarysLabelOrTheKindsOwnNameAndALabelWithNoTypeIsTheUnknown()
    {
        string[] labels = ["subagent", "workflow", "shell", "monitor", "MCP task", "teammate", "dream", "auto-mode scan", "cloud session", "local_side_task"];
        var tasks = string.Join(",", labels.Select((label, index) => $"{{\"id\":\"bg-{index}\",\"type\":\"{label}\",\"status\":\"running\",\"description\":\"work\",\"name\":\"n-{index}\"}}"));
        var input = Decode<StopHookInput>("Stop", $",\"stop_hook_active\":false,\"background_tasks\":[{tasks}]");

        CollectionAssert.AreEqual(
            new[]
            {
                StopHookBackgroundTaskType.Subagent,
                StopHookBackgroundTaskType.Workflow,
                StopHookBackgroundTaskType.Shell,
                StopHookBackgroundTaskType.Monitor,
                StopHookBackgroundTaskType.McpTask,
                StopHookBackgroundTaskType.Teammate,
                StopHookBackgroundTaskType.Dream,
                StopHookBackgroundTaskType.AutoModeScan,
                StopHookBackgroundTaskType.CloudSession,
                new StopHookBackgroundTaskType("local_side_task"),
            },
            input.BackgroundTasks.Select(task => task.TaskType).ToArray());
        Assert.IsTrue(input.BackgroundTasks.Take(9).All(task => task.TaskType.IsDefined));
        Assert.IsFalse(input.BackgroundTasks[9].TaskType.IsDefined);
        Assert.AreEqual("local_side_task", input.BackgroundTasks[9].TaskType.ToString());

        Assert.IsInstanceOfType<SubagentStopHookBackgroundTask>(input.BackgroundTasks[0]);
        Assert.IsInstanceOfType<ShellStopHookBackgroundTask>(input.BackgroundTasks[2]);
        var unknown = input.BackgroundTasks.Where((_, index) => index is not (0 or 2)).ToArray();
        Assert.AreEqual(8, unknown.Length);
        foreach (var task in unknown)
        {
            var read = (UnknownStopHookBackgroundTask)task;
            var index = Array.IndexOf(labels, read.TaskType.ToString());
            Assert.AreEqual($"bg-{index}", read.Id);
            Assert.AreEqual(BackgroundTaskStatus.Running, read.Status);
            Assert.AreEqual("work", read.Description);
            Assert.AreEqual(labels[index], read.Raw.GetProperty("type").GetString());
            Assert.AreEqual($"n-{index}", read.Raw.GetProperty("name").GetString());
        }
    }

    [TestMethod]
    public void ABackgroundTaskOrCronMissingOneOfItsOwnMembersIsLeftOutOfItsList()
    {
        var input = Decode<StopHookInput>(
            "Stop",
            """
            ,"stop_hook_active":false,"background_tasks":[{"id":"bg-1","type":"shell","status":"running"},{"id":"bg-2","type":"shell","status":"running","description":"kept"}],"session_crons":[{"id":"cron-1","schedule":"0 9 * * 1","prompt":"no recurring flag"}]
            """);

        Assert.AreEqual("bg-2", input.BackgroundTasks.Single().Id);
        Assert.AreEqual(0, input.SessionCrons.Count);
    }

    [TestMethod]
    public void AStopFailureCarriesTheErrorCodeAndItsDetails()
    {
        var input = Decode<StopFailureHookInput>(
            "StopFailure",
            """
            ,"error":"rate_limit","error_details":"retry after 30s","last_assistant_message":"Still working."
            """);

        Assert.AreEqual(AssistantErrorCode.RateLimit, input.ErrorCode);
        Assert.AreEqual("retry after 30s", input.ErrorDetails);
        Assert.AreEqual("Still working.", input.LastAssistantMessage);
    }

    [TestMethod]
    public void APostCompactCarriesTheTriggerAndTheSummaryTheCompactionWrote()
    {
        var input = Decode<PostCompactHookInput>(
            "PostCompact",
            """
            ,"trigger":"auto","compact_summary":"The build was cut at the third tool call."
            """);

        Assert.AreEqual(CompactTrigger.Auto, input.Trigger);
        Assert.AreEqual("The build was cut at the third tool call.", input.CompactSummary);
    }

    [TestMethod]
    public void AModelSwitchCarriesBothModelsWhatAskedForItAndWhatItCostsTheCache()
    {
        const string Own = """
            ,"from_model":"claude-haiku-4-5","to_model":"claude-opus-4-1","requested_model":"opus","source":"picker","context_tokens":88000,"prompt_cache_warm":true,"cache_ttl":"1h","estimated_cache_write_usd":0.72,"pricing":"catalog"
            """;

        var before = Decode<PreModelSwitchHookInput>("PreModelSwitch", Own);
        Assert.AreEqual("claude-haiku-4-5", before.FromModel);
        Assert.AreEqual("claude-opus-4-1", before.ToModel);
        Assert.AreEqual("opus", before.RequestedModel);
        Assert.AreEqual(PreModelSwitchSource.Picker, before.Source);
        Assert.AreEqual(88000L, before.ContextTokens);
        Assert.IsTrue(before.IsPromptCacheWarm);
        Assert.AreEqual(ModelSwitchCacheTimeToLive.OneHour, before.CacheTimeToLive);
        Assert.AreEqual(0.72m, before.EstimatedCacheWriteCost);
        Assert.AreEqual(ModelSwitchPricing.Catalog, before.Pricing);

        var after = Decode<PostModelSwitchHookInput>("PostModelSwitch", Own);
        Assert.AreEqual(PostModelSwitchSource.Picker, after.Source);
        Assert.AreEqual(ModelSwitchCacheTimeToLive.OneHour, after.CacheTimeToLive);
        Assert.AreEqual(ModelSwitchPricing.Catalog, after.Pricing);
    }

    // The two switches take their source from sets of their own: only the one after the switch names
    // the model the binary chose by itself.
    [TestMethod]
    public void OnlyThePostModelSwitchNamesAutoAsASource()
    {
        const string Own = """
            ,"from_model":"claude-haiku-4-5","to_model":"claude-opus-4-1","source":"auto","context_tokens":10,"prompt_cache_warm":false,"cache_ttl":"5m","estimated_cache_write_usd":0,"pricing":"default"
            """;

        Assert.IsTrue(Decode<PostModelSwitchHookInput>("PostModelSwitch", Own).Source.IsDefined);
        Assert.IsFalse(Decode<PreModelSwitchHookInput>("PreModelSwitch", Own).Source.IsDefined);
    }

    // A refused call is answered with an error, as the page has it: the refusal's reason is that
    // error, and the call stands failed.
    [TestMethod]
    public void APermissionDeniedCarriesItsReasonAsTheFailedCallsError()
    {
        var input = Decode<PermissionDeniedHookInput>(
            "PermissionDenied",
            """
            ,"tool_name":"Write","tool_input":{"file_path":"/etc/hosts"},"tool_use_id":"toolu_denied_1","reason":"The path is outside the working directory.","mcp_server":{"name":"notes","source":"project"}
            """);

        Assert.AreEqual(ToolCallStatus.Failed, input.ToolCall.Status);
        Assert.AreEqual("Write", input.ToolCall.Name);
        Assert.AreEqual("toolu_denied_1", input.ToolCall.Id);
        Assert.AreEqual("The path is outside the working directory.", input.ToolCall.Error);
        Assert.AreEqual("notes", input.McpServer!.Name);
        Assert.AreEqual(new McpServerSource("project"), input.McpServer.Source);
    }

    [TestMethod]
    public void ATeammateIdleCarriesTheTeammateAndTheTeam()
    {
        var input = Decode<TeammateIdleHookInput>(
            "TeammateIdle",
            """
            ,"teammate_name":"reviewer","team_name":"build"
            """);

        Assert.AreEqual("reviewer", input.TeammateName);
        Assert.AreEqual("build", input.TeamName);
    }

    [TestMethod]
    public void ATaskCreatedAndATaskCompletedCarryTheSameSummaryOfTheTask()
    {
        const string Own = """
            ,"task_id":"task-3","task_subject":"raise the floor","task_description":"the walk reaches further now","teammate_name":"reviewer","team_name":"build"
            """;

        var created = Decode<TaskCreatedHookInput>("TaskCreated", Own);
        Assert.AreEqual("task-3", created.TaskId);
        Assert.AreEqual("raise the floor", created.TaskSubject);
        Assert.AreEqual("the walk reaches further now", created.TaskDescription);
        Assert.AreEqual("reviewer", created.TeammateName);
        Assert.AreEqual("build", created.TeamName);

        var completed = Decode<TaskCompletedHookInput>("TaskCompleted", Own);
        Assert.AreEqual("task-3", completed.TaskId);
        Assert.AreEqual("raise the floor", completed.TaskSubject);
        Assert.AreEqual("build", completed.TeamName);
    }

    [TestMethod]
    public void AnElicitationCarriesTheServerTheMessageAndTheSchemaItAsksAgainst()
    {
        var input = Decode<ElicitationHookInput>(
            "Elicitation",
            """
            ,"mcp_server_name":"notes","message":"Which notebook?","mode":"form","url":"https://notes.invalid/pick","elicitation_id":"elicit-5","requested_schema":{"type":"object","properties":{"notebook":{"type":"string"}}}
            """);

        Assert.AreEqual("notes", input.McpServerName);
        Assert.AreEqual("Which notebook?", input.Message);
        Assert.AreEqual(ElicitationMode.Form, input.Mode);
        Assert.AreEqual("https://notes.invalid/pick", input.Url);
        Assert.AreEqual("elicit-5", input.ElicitationId);
        Assert.AreEqual("object", input.RequestedSchema!.Value.GetProperty("type").GetString());
    }

    [TestMethod]
    public void AnElicitationResultCarriesWhatTheAnswerWasAndTheContentItCameWith()
    {
        var input = Decode<ElicitationResultHookInput>(
            "ElicitationResult",
            """
            ,"mcp_server_name":"notes","elicitation_id":"elicit-5","mode":"form","action":"accept","content":{"notebook":"build"}
            """);

        Assert.AreEqual("notes", input.McpServerName);
        Assert.AreEqual("elicit-5", input.ElicitationId);
        Assert.AreEqual(ElicitationMode.Form, input.Mode);
        Assert.AreEqual(ElicitationAction.Accept, input.Action);
        Assert.AreEqual("build", input.Content!.Value.GetProperty("notebook").GetString());
    }

    // A schema or a content that is not an object is not read as one: the member is null and the
    // whole line still stands in Raw.
    [TestMethod]
    public void AnElicitationSchemaOrContentThatIsNotAnObjectIsNull()
    {
        var elicitation = Decode<ElicitationHookInput>(
            "Elicitation",
            """
            ,"mcp_server_name":"notes","message":"Which notebook?","requested_schema":"object"
            """);
        Assert.IsNull(elicitation.RequestedSchema);
        Assert.IsNull(elicitation.Mode);

        var result = Decode<ElicitationResultHookInput>(
            "ElicitationResult",
            """
            ,"mcp_server_name":"notes","action":"cancel","content":["build"]
            """);
        Assert.IsNull(result.Content);
        Assert.AreEqual(ElicitationAction.Cancel, result.Action);
    }

    [TestMethod]
    public void AConfigChangeCarriesWhichSettingsChangedAndTheFileTheyAreIn()
    {
        var input = Decode<ConfigChangeHookInput>(
            "ConfigChange",
            """
            ,"source":"project_settings","file_path":"/srv/build/.claude/settings.json"
            """);

        Assert.AreEqual(ConfigChangeSource.ProjectSettings, input.Source);
        Assert.AreEqual("/srv/build/.claude/settings.json", input.FilePath);
    }

    [TestMethod]
    public void AWorktreeCreateCarriesItsNameAndAWorktreeRemoveCarriesItsPath()
    {
        var created = Decode<WorktreeCreateHookInput>(
            "WorktreeCreate",
            """
            ,"name":"run-hooks"
            """);
        Assert.AreEqual("run-hooks", created.Name);

        var removed = Decode<WorktreeRemoveHookInput>(
            "WorktreeRemove",
            """
            ,"worktree_path":"/srv/build/worktrees/run-hooks"
            """);
        Assert.AreEqual("/srv/build/worktrees/run-hooks", removed.WorktreePath);
    }

    [TestMethod]
    public void InstructionsLoadedCarriesTheFileItsMemoryTypeWhyItLoadedAndTheGlobsThatMatched()
    {
        var input = Decode<InstructionsLoadedHookInput>(
            "InstructionsLoaded",
            """
            ,"file_path":"/srv/build/CLAUDE.md","memory_type":"Project","load_reason":"path_glob_match","globs":["src/**/*.cs","tests/**/*.cs"],"trigger_file_path":"/srv/build/src/Program.cs","parent_file_path":"/srv/CLAUDE.md"
            """);

        Assert.AreEqual("/srv/build/CLAUDE.md", input.FilePath);
        Assert.AreEqual(InstructionsMemoryType.Project, input.MemoryType);
        Assert.AreEqual(InstructionsLoadReason.PathGlobMatch, input.LoadReason);
        CollectionAssert.AreEqual(new[] { "src/**/*.cs", "tests/**/*.cs" }, input.Globs.ToArray());
        Assert.AreEqual("/srv/build/src/Program.cs", input.TriggerFilePath);
        Assert.AreEqual("/srv/CLAUDE.md", input.ParentFilePath);
    }

    // The globs are a list of strings: an entry of another JSON kind is not one, and is left out.
    [TestMethod]
    public void AGlobThatIsNotAStringIsLeftOutOfTheGlobs()
    {
        var input = Decode<InstructionsLoadedHookInput>(
            "InstructionsLoaded",
            """
            ,"file_path":"/srv/build/CLAUDE.md","memory_type":"User","load_reason":"session_start","globs":["src/**/*.cs",17,null,{"glob":"tests/**"}]
            """);

        Assert.AreEqual("src/**/*.cs", input.Globs.Single());
    }

    // The event is CwdChanged on the wire and the working directory changing in the library's own
    // words: no abbreviation reaches a caller.
    [TestMethod]
    public void TheCwdChangedEventIsTheWorkingDirectoryChangedInputWithBothDirectories()
    {
        var input = Decode<WorkingDirectoryChangedHookInput>(
            "CwdChanged",
            """
            ,"old_cwd":"/srv/build","new_cwd":"/srv/build/packages/agent"
            """);

        Assert.AreEqual(HookEvent.WorkingDirectoryChanged, input.Event);
        Assert.AreEqual("CwdChanged", input.Event.ToString());
        Assert.AreEqual("/srv/build", input.OldWorkingDirectory);
        Assert.AreEqual("/srv/build/packages/agent", input.NewWorkingDirectory);
    }

    [TestMethod]
    public void AFileChangedCarriesThePathAndWhatHappenedToIt()
    {
        var input = Decode<FileChangedHookInput>(
            "FileChanged",
            """
            ,"file_path":"/srv/build/notes.txt","event":"unlink"
            """);

        Assert.AreEqual("/srv/build/notes.txt", input.FilePath);
        Assert.AreEqual(FileChangedEvent.Unlink, input.Change);
    }

    [TestMethod]
    public void ADirectoryAddedCarriesTheDirectoryAndWhatAddedIt()
    {
        var input = Decode<DirectoryAddedHookInput>(
            "DirectoryAdded",
            """
            ,"directory":"/srv/build/vendor","source":"register_repo_root"
            """);

        Assert.AreEqual("/srv/build/vendor", input.Directory);
        Assert.AreEqual(DirectoryAddedSource.RegisterRepositoryRoot, input.Source);
    }

    [TestMethod]
    public void AMessageDisplayCarriesTheTurnTheMessageItsPlaceAndTheTextAdded()
    {
        var input = Decode<MessageDisplayHookInput>(
            "MessageDisplay",
            """
            ,"turn_id":"turn-2","message_id":"msg_01Display","index":3,"final":true,"delta":" and done."
            """);

        Assert.AreEqual("turn-2", input.TurnId);
        Assert.AreEqual("msg_01Display", input.MessageId);
        Assert.AreEqual(3L, input.Index);
        Assert.IsTrue(input.IsFinal);
        Assert.AreEqual(" and done.", input.Delta);
    }

    // A subagent's events are told apart by the agent the common members name: with no agent type on
    // the line there is no subagent to speak of, and the input degrades.
    [TestMethod]
    public void ASubagentStartIsTypedOnlyWhenTheLineNamesTheAgentAndItsType()
    {
        var typed = Decode<SubagentStartHookInput>(
            "SubagentStart",
            """
            ,"agent_id":"agent-9","agent_type":"reviewer"
            """);
        Assert.AreEqual("agent-9", typed.AgentId);
        Assert.AreEqual(new AgentType("reviewer"), typed.AgentType);

        var degraded = Parse(
            "SubagentStart",
            """
            ,"agent_id":"agent-9"
            """);
        Assert.IsInstanceOfType<UnknownHookInput>(degraded);
    }

    [TestMethod]
    public void ASubagentStopCarriesTheSubagentsOwnTranscript()
    {
        var input = Decode<SubagentStopHookInput>(
            "SubagentStop",
            """
            ,"agent_id":"agent-9","agent_type":"reviewer","stop_hook_active":false,"agent_transcript_path":"/var/agent/transcripts/agent-9.jsonl","last_assistant_message":"Reviewed."
            """);

        Assert.IsFalse(input.IsStopHookActive);
        Assert.AreEqual("/var/agent/transcripts/agent-9.jsonl", input.AgentTranscriptPath);
        Assert.AreEqual("Reviewed.", input.LastAssistantMessage);
        Assert.AreEqual(0, input.BackgroundTasks.Count);
        Assert.AreEqual(0, input.SessionCrons.Count);
    }

    // A duration comes over as a count: milliseconds on a tool event, seconds on a session start.
    // A count no TimeSpan holds is not a duration, so the member is null and the line still reads.
    [TestMethod]
    public void ACountOfMillisecondsOrSecondsBecomesADurationAndOneTooLargeToHoldIsNull()
    {
        var timed = Decode<PostToolUseHookInput>(
            "PostToolUse",
            """
            ,"tool_name":"Bash","tool_input":{"command":"true"},"tool_use_id":"toolu_timed_1","tool_response":{"stdout":""},"duration_ms":1450
            """);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1450), timed.Duration);

        var overlong = Decode<PostToolUseHookInput>(
            "PostToolUse",
            """
            ,"tool_name":"Bash","tool_input":{"command":"true"},"tool_use_id":"toolu_timed_2","tool_response":{"stdout":""},"duration_ms":9223372036854775807
            """);
        Assert.IsNull(overlong.Duration);

        var unheld = Decode<SessionStartHookInput>(
            "SessionStart",
            """
            ,"source":"startup","seconds_since_last_response":1e300
            """);
        Assert.IsNull(unheld.TimeSinceLastResponse);
    }

    // Degrade, never throw, never skip: an event whose own required member is absent is the unknown
    // of the set, its event name kept and the whole line standing in Raw for the caller to read.
    [TestMethod]
    public void AnEventMissingOneOfItsOwnRequiredMembersIsTheUnknownWithItsEventNameAndTheWholeLine()
    {
        (string Event, string Own)[] cases =
        [
            ("SessionEnd", ""),
            ("PostCompact", """
                ,"trigger":"manual"
                """),
            ("Notification", """
                ,"message":"nothing names the type"
                """),
            ("FileChanged", """
                ,"file_path":"/srv/build/notes.txt"
                """),
            ("DirectoryAdded", """
                ,"directory":"/srv/build/vendor"
                """),
            ("MessageDisplay", """
                ,"turn_id":"turn-2","message_id":"msg_01Display","index":3,"final":true
                """),
            ("InstructionsLoaded", """
                ,"file_path":"/srv/build/CLAUDE.md","memory_type":"User"
                """),
            ("TaskCreated", """
                ,"task_id":"task-3"
                """),
            ("Elicitation", """
                ,"mcp_server_name":"notes"
                """),
            ("WorktreeRemove", ""),
            ("PreModelSwitch", """
                ,"from_model":"a","to_model":"b","source":"sdk"
                """),
            ("PreToolUse", """
                ,"tool_name":"Read","tool_input":{"file_path":"/srv/build/log.txt"}
                """),
        ];

        foreach (var (name, own) in cases)
        {
            var input = Parse(name, own);
            var unknown = input as UnknownHookInput;
            Assert.IsNotNull(unknown, $"{name} without its required members read as {input.GetType().Name}");
            Assert.AreEqual(name, unknown.Event.ToString());
            Assert.IsTrue(unknown.Event.IsDefined, $"{name} is an event this library names");
            Assert.AreEqual(SessionId, unknown.Raw.GetProperty("session_id").GetString());
        }
    }

    // The set is closed against no value at all, but reading is not calling: a value the library does
    // not name is kept as the binary wrote it and the line goes on.
    [TestMethod]
    public void AClosedSetValueTheLibraryDoesNotNameIsKeptAsTheBinaryWroteIt()
    {
        var input = Decode<FileChangedHookInput>(
            "FileChanged",
            """
            ,"file_path":"/srv/build/notes.txt","event":"rename"
            """);

        Assert.AreEqual("rename", input.Change.ToString());
        Assert.IsFalse(input.Change.IsDefined);
    }

    // The session id is the one member no input can do without: a line missing it is rejected, not
    // degraded, because nothing downstream can place it.
    [TestMethod]
    public void AHookInputWithNoSessionIdIsRejected()
    {
        const string Line = """{"hook_event_name":"Setup","trigger":"init","cwd":"/srv/build"}""";

        var parsed = HookInputParser.Parse(Recorded.Json(Line), Line);
        Assert.IsFalse(parsed.IsOk);
    }

    private static T Decode<T>(string hookEventName, string ownMembers)
        where T : HookInput
    {
        var input = Parse(hookEventName, ownMembers);
        Assert.IsInstanceOfType<T>(input, $"{hookEventName} read as {input.GetType().Name}");
        return (T)input;
    }

    // The common members, then the event's own text as the case above wrote it.
    private static HookInput Parse(string hookEventName, string ownMembers)
    {
        var line = $$"""
            {"session_id":"{{SessionId}}","transcript_path":"{{TranscriptPath}}","cwd":"{{WorkingDirectory}}","hook_event_name":"{{hookEventName}}"{{ownMembers}}}
            """;

        using var document = JsonDocument.Parse(line);
        var parsed = HookInputParser.Parse(document.RootElement.Clone(), line);
        Assert.IsTrue(parsed.IsOk, $"{line}: {Recorded.Reason(parsed)}");
        return parsed.Value;
    }
}
