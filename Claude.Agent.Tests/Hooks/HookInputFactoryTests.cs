using Claude.Agent.Tests.Wire;

namespace Claude.Agent.Tests.Hooks;

// One test per model of this family: the builder a caller's test uses carries every parameter
// through to the property of its name. The common members are held once, on one builder, since every
// hook input takes them the same way.
[TestClass]
public sealed class HookInputFactoryTests
{
    private static readonly ToolCall Call = ClaudeAgentModelFactory.ToolCall(
        id: "toolu_factory_1",
        name: "Read",
        rawInput: Recorded.Json("""{"file_path":"/srv/build/log.txt"}"""));

    private static readonly McpServerOrigin Server = ClaudeAgentModelFactory.McpServerOrigin("notes", "project");

    [TestMethod]
    public void EveryHookInputTakesTheCommonMembersTheSameWay()
    {
        var input = ClaudeAgentModelFactory.SetupHookInput(
            sessionId: "session-1",
            transcriptPath: "/var/agent/transcripts/build.jsonl",
            workingDirectory: "/srv/build",
            promptId: "prompt-41",
            permissionMode: PermissionMode.Plan,
            agentId: "agent-9",
            agentType: "reviewer",
            effort: Effort.Max,
            scratchpadDirectory: "/srv/build/.scratch",
            trigger: SetupTrigger.Maintenance);

        Assert.AreEqual("session-1", input.SessionId);
        Assert.AreEqual("/var/agent/transcripts/build.jsonl", input.TranscriptPath);
        Assert.AreEqual("/srv/build", input.WorkingDirectory);
        Assert.AreEqual("prompt-41", input.PromptId);
        Assert.AreEqual(PermissionMode.Plan, input.PermissionMode);
        Assert.AreEqual("agent-9", input.AgentId);
        Assert.AreEqual(new AgentType("reviewer"), input.AgentType);
        Assert.AreEqual(Effort.Max, input.Effort);
        Assert.AreEqual("/srv/build/.scratch", input.ScratchpadDirectory);
        Assert.AreEqual(HookEvent.Setup, input.Event);
    }

    // A builder left to its defaults still hands back a whole model: the strings are empty, the
    // lists are empty, and the line kept for the codec is an empty object rather than an undefined
    // element, as is the unknown's raw.
    [TestMethod]
    public void ABuilderLeftToItsDefaultsHandsBackAWholeModel()
    {
        var input = ClaudeAgentModelFactory.StopHookInput();

        Assert.AreEqual(string.Empty, input.SessionId);
        Assert.AreEqual(string.Empty, input.TranscriptPath);
        Assert.AreEqual(string.Empty, input.WorkingDirectory);
        Assert.AreEqual(0, input.BackgroundTasks.Count);
        Assert.AreEqual(0, input.SessionCrons.Count);
        Assert.AreEqual(System.Text.Json.JsonValueKind.Object, input.Json.ValueKind);
        Assert.AreEqual(System.Text.Json.JsonValueKind.Object, ClaudeAgentModelFactory.UnknownHookInput().Raw.ValueKind);
    }

    [TestMethod]
    public void ThePreToolUseBuilderCarriesTheCallAndItsServer()
    {
        var input = ClaudeAgentModelFactory.PreToolUseHookInput(toolCall: Call, mcpServer: Server);

        Assert.AreEqual(HookEvent.PreToolUse, input.Event);
        Assert.AreSame(Call, input.ToolCall);
        Assert.AreSame(Server, input.McpServer);
    }

    [TestMethod]
    public void ThePostToolUseBuilderCarriesTheCallItsDurationAndItsServer()
    {
        var input = ClaudeAgentModelFactory.PostToolUseHookInput(
            toolCall: Call,
            duration: TimeSpan.FromMilliseconds(1450),
            mcpServer: Server);

        Assert.AreEqual(HookEvent.PostToolUse, input.Event);
        Assert.AreSame(Call, input.ToolCall);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1450), input.Duration);
        Assert.AreSame(Server, input.McpServer);
    }

    [TestMethod]
    public void ThePostToolUseFailureBuilderCarriesWhetherTheCallWasCutShort()
    {
        var input = ClaudeAgentModelFactory.PostToolUseFailureHookInput(
            toolCall: Call,
            isInterrupt: true,
            duration: TimeSpan.FromSeconds(2),
            mcpServer: Server);

        Assert.AreEqual(HookEvent.PostToolUseFailure, input.Event);
        Assert.AreSame(Call, input.ToolCall);
        Assert.IsTrue(input.IsInterrupt);
        Assert.AreEqual(TimeSpan.FromSeconds(2), input.Duration);
        Assert.AreSame(Server, input.McpServer);
    }

    [TestMethod]
    public void ThePostToolBatchBuilderCarriesTheCallsOfTheBatch()
    {
        var input = ClaudeAgentModelFactory.PostToolBatchHookInput(toolCalls: [Call]);

        Assert.AreEqual(HookEvent.PostToolBatch, input.Event);
        Assert.AreSame(Call, input.ToolCalls.Single());
    }

    [TestMethod]
    public void TheNotificationBuilderCarriesTheMessageTitleAndType()
    {
        var input = ClaudeAgentModelFactory.NotificationHookInput(
            message: "The build needs an answer.",
            title: "Waiting",
            notificationType: "permission_prompt");

        Assert.AreEqual(HookEvent.Notification, input.Event);
        Assert.AreEqual("The build needs an answer.", input.Message);
        Assert.AreEqual("Waiting", input.Title);
        Assert.AreEqual(new NotificationType("permission_prompt"), input.NotificationType);
    }

    [TestMethod]
    public void TheUserPromptSubmitBuilderCarriesThePromptAndWhatSubmittedIt()
    {
        var input = ClaudeAgentModelFactory.UserPromptSubmitHookInput(
            prompt: "Read the log.",
            source: UserPromptSubmitSource.ScheduleWakeup,
            sessionTitle: "the build");

        Assert.AreEqual(HookEvent.UserPromptSubmit, input.Event);
        Assert.AreEqual("Read the log.", input.Prompt);
        Assert.AreEqual(UserPromptSubmitSource.ScheduleWakeup, input.Source);
        Assert.AreEqual("the build", input.SessionTitle);
    }

    [TestMethod]
    public void TheUserPromptExpansionBuilderCarriesTheCommandAndTheProducedPrompt()
    {
        var input = ClaudeAgentModelFactory.UserPromptExpansionHookInput(
            expansionType: UserPromptExpansionType.McpPrompt,
            commandName: "review",
            commandArguments: "--since yesterday",
            commandSource: "project",
            prompt: "Review the changes since yesterday.");

        Assert.AreEqual(HookEvent.UserPromptExpansion, input.Event);
        Assert.AreEqual(UserPromptExpansionType.McpPrompt, input.ExpansionType);
        Assert.AreEqual("review", input.CommandName);
        Assert.AreEqual("--since yesterday", input.CommandArguments);
        Assert.AreEqual("project", input.CommandSource);
        Assert.AreEqual("Review the changes since yesterday.", input.Prompt);
    }

    [TestMethod]
    public void TheSessionStartBuilderCarriesHowItBeganAndWhatItKnowsOfTheCache()
    {
        var input = ClaudeAgentModelFactory.SessionStartHookInput(
            source: SessionStartSource.Fork,
            model: "claude-haiku-4-5",
            sessionTitle: "the build",
            timeSinceLastResponse: TimeSpan.FromSeconds(92.5),
            contextTokens: 41200,
            isPromptCacheLikelyExpired: true,
            estimatedCacheWriteCost: 0.0413m);

        Assert.AreEqual(HookEvent.SessionStart, input.Event);
        Assert.AreEqual(SessionStartSource.Fork, input.Source);
        Assert.AreEqual("claude-haiku-4-5", input.Model);
        Assert.AreEqual("the build", input.SessionTitle);
        Assert.AreEqual(TimeSpan.FromSeconds(92.5), input.TimeSinceLastResponse);
        Assert.AreEqual(41200L, input.ContextTokens);
        Assert.IsTrue(input.IsPromptCacheLikelyExpired);
        Assert.AreEqual(0.0413m, input.EstimatedCacheWriteCost);
    }

    [TestMethod]
    public void TheSessionEndBuilderCarriesWhyItEnded()
    {
        var input = ClaudeAgentModelFactory.SessionEndHookInput(reason: SessionEndReason.Logout);

        Assert.AreEqual(HookEvent.SessionEnd, input.Event);
        Assert.AreEqual(SessionEndReason.Logout, input.Reason);
    }

    [TestMethod]
    public void TheStopBuilderCarriesTheLastMessageTheBackgroundTasksAndTheCrons()
    {
        var task = ClaudeAgentModelFactory.ShellStopHookBackgroundTask(id: "bg-1");
        var cron = ClaudeAgentModelFactory.SessionCron(id: "cron-1");

        var input = ClaudeAgentModelFactory.StopHookInput(
            isStopHookActive: true,
            lastAssistantMessage: "Done.",
            backgroundTasks: [task],
            sessionCrons: [cron]);

        Assert.AreEqual(HookEvent.Stop, input.Event);
        Assert.IsTrue(input.IsStopHookActive);
        Assert.AreEqual("Done.", input.LastAssistantMessage);
        Assert.AreSame(task, input.BackgroundTasks.Single());
        Assert.AreSame(cron, input.SessionCrons.Single());
    }

    [TestMethod]
    public void TheStopFailureBuilderCarriesTheErrorCodeAndItsDetails()
    {
        var input = ClaudeAgentModelFactory.StopFailureHookInput(
            errorCode: AssistantErrorCode.Overloaded,
            errorDetails: "retry after 30s",
            lastAssistantMessage: "Still working.");

        Assert.AreEqual(HookEvent.StopFailure, input.Event);
        Assert.AreEqual(AssistantErrorCode.Overloaded, input.ErrorCode);
        Assert.AreEqual("retry after 30s", input.ErrorDetails);
        Assert.AreEqual("Still working.", input.LastAssistantMessage);
    }

    [TestMethod]
    public void TheSubagentStartBuilderNamesTheAgentAndNothingElseOfItsOwn()
    {
        var input = ClaudeAgentModelFactory.SubagentStartHookInput(agentId: "agent-9", agentType: "reviewer");

        Assert.AreEqual(HookEvent.SubagentStart, input.Event);
        Assert.AreEqual("agent-9", input.AgentId);
        Assert.AreEqual(new AgentType("reviewer"), input.AgentType);
    }

    [TestMethod]
    public void TheSubagentStopBuilderCarriesTheSubagentsOwnTranscript()
    {
        var input = ClaudeAgentModelFactory.SubagentStopHookInput(
            isStopHookActive: true,
            agentTranscriptPath: "/var/agent/transcripts/agent-9.jsonl",
            lastAssistantMessage: "Reviewed.",
            backgroundTasks: [ClaudeAgentModelFactory.SubagentStopHookBackgroundTask(id: "bg-1")],
            sessionCrons: [ClaudeAgentModelFactory.SessionCron(id: "cron-1")]);

        Assert.AreEqual(HookEvent.SubagentStop, input.Event);
        Assert.IsTrue(input.IsStopHookActive);
        Assert.AreEqual("/var/agent/transcripts/agent-9.jsonl", input.AgentTranscriptPath);
        Assert.AreEqual("Reviewed.", input.LastAssistantMessage);
        Assert.AreEqual("bg-1", input.BackgroundTasks.Single().Id);
        Assert.AreEqual("cron-1", input.SessionCrons.Single().Id);
    }

    [TestMethod]
    public void ThePreCompactBuilderCarriesTheTriggerAndTheInstructionsGivenWithIt()
    {
        var input = ClaudeAgentModelFactory.PreCompactHookInput(
            trigger: CompactTrigger.Manual,
            customInstructions: "Keep the tool calls.");

        Assert.AreEqual(HookEvent.PreCompact, input.Event);
        Assert.AreEqual(CompactTrigger.Manual, input.Trigger);
        Assert.AreEqual("Keep the tool calls.", input.CustomInstructions);
    }

    [TestMethod]
    public void ThePostCompactBuilderCarriesTheTriggerAndTheSummary()
    {
        var input = ClaudeAgentModelFactory.PostCompactHookInput(
            trigger: CompactTrigger.Auto,
            compactSummary: "The build was cut at the third tool call.");

        Assert.AreEqual(HookEvent.PostCompact, input.Event);
        Assert.AreEqual(CompactTrigger.Auto, input.Trigger);
        Assert.AreEqual("The build was cut at the third tool call.", input.CompactSummary);
    }

    [TestMethod]
    public void ThePreModelSwitchBuilderCarriesBothModelsAndWhatTheSwitchCostsTheCache()
    {
        var input = ClaudeAgentModelFactory.PreModelSwitchHookInput(
            fromModel: "claude-haiku-4-5",
            toModel: "claude-opus-4-1",
            requestedModel: "opus",
            source: PreModelSwitchSource.Command,
            contextTokens: 88000,
            isPromptCacheWarm: true,
            cacheTimeToLive: ModelSwitchCacheTimeToLive.FiveMinutes,
            estimatedCacheWriteCost: 0.72m,
            pricing: ModelSwitchPricing.Configured);

        Assert.AreEqual(HookEvent.PreModelSwitch, input.Event);
        Assert.AreEqual("claude-haiku-4-5", input.FromModel);
        Assert.AreEqual("claude-opus-4-1", input.ToModel);
        Assert.AreEqual("opus", input.RequestedModel);
        Assert.AreEqual(PreModelSwitchSource.Command, input.Source);
        Assert.AreEqual(88000L, input.ContextTokens);
        Assert.IsTrue(input.IsPromptCacheWarm);
        Assert.AreEqual(ModelSwitchCacheTimeToLive.FiveMinutes, input.CacheTimeToLive);
        Assert.AreEqual(0.72m, input.EstimatedCacheWriteCost);
        Assert.AreEqual(ModelSwitchPricing.Configured, input.Pricing);
    }

    [TestMethod]
    public void ThePostModelSwitchBuilderTakesItsSourceFromItsOwnSet()
    {
        var input = ClaudeAgentModelFactory.PostModelSwitchHookInput(
            fromModel: "claude-haiku-4-5",
            toModel: "claude-opus-4-1",
            source: PostModelSwitchSource.Auto,
            contextTokens: 88000,
            isPromptCacheWarm: false,
            cacheTimeToLive: ModelSwitchCacheTimeToLive.OneHour,
            estimatedCacheWriteCost: 0.72m,
            pricing: ModelSwitchPricing.Catalog);

        Assert.AreEqual(HookEvent.PostModelSwitch, input.Event);
        Assert.AreEqual(PostModelSwitchSource.Auto, input.Source);
        Assert.IsNull(input.RequestedModel);
        Assert.IsFalse(input.IsPromptCacheWarm);
        Assert.AreEqual(ModelSwitchCacheTimeToLive.OneHour, input.CacheTimeToLive);
        Assert.AreEqual(ModelSwitchPricing.Catalog, input.Pricing);
    }

    [TestMethod]
    public void ThePermissionRequestBuilderCarriesTheCallAndTheSuggestionsBesideIt()
    {
        var suggestion = PermissionUpdate.SetMode(PermissionMode.AcceptEdits, PermissionUpdateDestination.Session);

        var input = ClaudeAgentModelFactory.PermissionRequestHookInput(
            toolCall: Call,
            permissionSuggestions: [suggestion],
            mcpServer: Server);

        Assert.AreEqual(HookEvent.PermissionRequest, input.Event);
        Assert.AreSame(Call, input.ToolCall);
        Assert.AreSame(suggestion, input.PermissionSuggestions.Single());
        Assert.AreSame(Server, input.McpServer);
    }

    [TestMethod]
    public void ThePermissionDeniedBuilderCarriesTheFailedCall()
    {
        var refused = ClaudeAgentModelFactory.ToolCall(
            id: "toolu_denied_1",
            name: "Write",
            status: ToolCallStatus.Failed,
            error: "The path is outside the working directory.");

        var input = ClaudeAgentModelFactory.PermissionDeniedHookInput(toolCall: refused, mcpServer: Server);

        Assert.AreEqual(HookEvent.PermissionDenied, input.Event);
        Assert.AreSame(refused, input.ToolCall);
        Assert.AreEqual("The path is outside the working directory.", input.ToolCall.Error);
        Assert.AreSame(Server, input.McpServer);
    }

    [TestMethod]
    public void TheSetupBuilderCarriesItsTrigger()
    {
        var input = ClaudeAgentModelFactory.SetupHookInput(trigger: SetupTrigger.Initialization);

        Assert.AreEqual(HookEvent.Setup, input.Event);
        Assert.AreEqual(SetupTrigger.Initialization, input.Trigger);
    }

    [TestMethod]
    public void TheTeammateIdleBuilderCarriesTheTeammateAndTheTeam()
    {
        var input = ClaudeAgentModelFactory.TeammateIdleHookInput(teammateName: "reviewer", teamName: "build");

        Assert.AreEqual(HookEvent.TeammateIdle, input.Event);
        Assert.AreEqual("reviewer", input.TeammateName);
        Assert.AreEqual("build", input.TeamName);
    }

    [TestMethod]
    public void TheTaskCreatedBuilderCarriesTheTasksSummary()
    {
        var input = ClaudeAgentModelFactory.TaskCreatedHookInput(
            taskId: "task-3",
            taskSubject: "raise the floor",
            taskDescription: "the walk reaches further now",
            teammateName: "reviewer",
            teamName: "build");

        Assert.AreEqual(HookEvent.TaskCreated, input.Event);
        Assert.AreEqual("task-3", input.TaskId);
        Assert.AreEqual("raise the floor", input.TaskSubject);
        Assert.AreEqual("the walk reaches further now", input.TaskDescription);
        Assert.AreEqual("reviewer", input.TeammateName);
        Assert.AreEqual("build", input.TeamName);
    }

    [TestMethod]
    public void TheTaskCompletedBuilderCarriesTheSameSummary()
    {
        var input = ClaudeAgentModelFactory.TaskCompletedHookInput(
            taskId: "task-3",
            taskSubject: "raise the floor",
            taskDescription: "the walk reaches further now",
            teammateName: "reviewer",
            teamName: "build");

        Assert.AreEqual(HookEvent.TaskCompleted, input.Event);
        Assert.AreEqual("task-3", input.TaskId);
        Assert.AreEqual("raise the floor", input.TaskSubject);
        Assert.AreEqual("the walk reaches further now", input.TaskDescription);
        Assert.AreEqual("reviewer", input.TeammateName);
        Assert.AreEqual("build", input.TeamName);
    }

    [TestMethod]
    public void TheElicitationBuilderCarriesTheServerTheMessageAndTheSchema()
    {
        var input = ClaudeAgentModelFactory.ElicitationHookInput(
            mcpServerName: "notes",
            message: "Which notebook?",
            mode: ElicitationMode.Url,
            url: "https://notes.invalid/pick",
            elicitationId: "elicit-5",
            requestedSchema: Recorded.Json("""{"type":"object"}"""));

        Assert.AreEqual(HookEvent.Elicitation, input.Event);
        Assert.AreEqual("notes", input.McpServerName);
        Assert.AreEqual("Which notebook?", input.Message);
        Assert.AreEqual(ElicitationMode.Url, input.Mode);
        Assert.AreEqual("https://notes.invalid/pick", input.Url);
        Assert.AreEqual("elicit-5", input.ElicitationId);
        Assert.AreEqual("object", input.RequestedSchema!.Value.GetProperty("type").GetString());
    }

    [TestMethod]
    public void TheElicitationResultBuilderCarriesTheAnswerAndItsContent()
    {
        var input = ClaudeAgentModelFactory.ElicitationResultHookInput(
            mcpServerName: "notes",
            elicitationId: "elicit-5",
            mode: ElicitationMode.Form,
            action: ElicitationAction.Decline,
            content: Recorded.Json("""{"notebook":"build"}"""));

        Assert.AreEqual(HookEvent.ElicitationResult, input.Event);
        Assert.AreEqual("notes", input.McpServerName);
        Assert.AreEqual("elicit-5", input.ElicitationId);
        Assert.AreEqual(ElicitationMode.Form, input.Mode);
        Assert.AreEqual(ElicitationAction.Decline, input.Action);
        Assert.AreEqual("build", input.Content!.Value.GetProperty("notebook").GetString());
    }

    [TestMethod]
    public void TheConfigChangeBuilderCarriesTheSettingsAndTheirFile()
    {
        var input = ClaudeAgentModelFactory.ConfigChangeHookInput(
            source: ConfigChangeSource.Skills,
            filePath: "/srv/build/.claude/skills/review.md");

        Assert.AreEqual(HookEvent.ConfigChange, input.Event);
        Assert.AreEqual(ConfigChangeSource.Skills, input.Source);
        Assert.AreEqual("/srv/build/.claude/skills/review.md", input.FilePath);
    }

    [TestMethod]
    public void TheWorktreeCreateBuilderCarriesItsName()
    {
        var input = ClaudeAgentModelFactory.WorktreeCreateHookInput(name: "run-hooks");

        Assert.AreEqual(HookEvent.WorktreeCreate, input.Event);
        Assert.AreEqual("run-hooks", input.Name);
    }

    [TestMethod]
    public void TheWorktreeRemoveBuilderCarriesItsPath()
    {
        var input = ClaudeAgentModelFactory.WorktreeRemoveHookInput(worktreePath: "/srv/build/worktrees/run-hooks");

        Assert.AreEqual(HookEvent.WorktreeRemove, input.Event);
        Assert.AreEqual("/srv/build/worktrees/run-hooks", input.WorktreePath);
    }

    [TestMethod]
    public void TheInstructionsLoadedBuilderCarriesTheFileItsTypeAndWhyItLoaded()
    {
        var input = ClaudeAgentModelFactory.InstructionsLoadedHookInput(
            filePath: "/srv/build/CLAUDE.md",
            memoryType: InstructionsMemoryType.Managed,
            loadReason: InstructionsLoadReason.NestedTraversal,
            globs: ["src/**/*.cs"],
            triggerFilePath: "/srv/build/src/Program.cs",
            parentFilePath: "/srv/CLAUDE.md");

        Assert.AreEqual(HookEvent.InstructionsLoaded, input.Event);
        Assert.AreEqual("/srv/build/CLAUDE.md", input.FilePath);
        Assert.AreEqual(InstructionsMemoryType.Managed, input.MemoryType);
        Assert.AreEqual(InstructionsLoadReason.NestedTraversal, input.LoadReason);
        Assert.AreEqual("src/**/*.cs", input.Globs.Single());
        Assert.AreEqual("/srv/build/src/Program.cs", input.TriggerFilePath);
        Assert.AreEqual("/srv/CLAUDE.md", input.ParentFilePath);
    }

    [TestMethod]
    public void TheWorkingDirectoryChangedBuilderCarriesBothDirectories()
    {
        var input = ClaudeAgentModelFactory.WorkingDirectoryChangedHookInput(
            oldWorkingDirectory: "/srv/build",
            newWorkingDirectory: "/srv/build/packages/agent");

        Assert.AreEqual(HookEvent.WorkingDirectoryChanged, input.Event);
        Assert.AreEqual("/srv/build", input.OldWorkingDirectory);
        Assert.AreEqual("/srv/build/packages/agent", input.NewWorkingDirectory);
    }

    [TestMethod]
    public void TheFileChangedBuilderCarriesThePathAndWhatHappenedToIt()
    {
        var input = ClaudeAgentModelFactory.FileChangedHookInput(
            filePath: "/srv/build/notes.txt",
            change: FileChangedEvent.Add);

        Assert.AreEqual(HookEvent.FileChanged, input.Event);
        Assert.AreEqual("/srv/build/notes.txt", input.FilePath);
        Assert.AreEqual(FileChangedEvent.Add, input.Change);
    }

    [TestMethod]
    public void TheDirectoryAddedBuilderCarriesTheDirectoryAndWhatAddedIt()
    {
        var input = ClaudeAgentModelFactory.DirectoryAddedHookInput(
            directory: "/srv/build/vendor",
            source: DirectoryAddedSource.SlashCommand);

        Assert.AreEqual(HookEvent.DirectoryAdded, input.Event);
        Assert.AreEqual("/srv/build/vendor", input.Directory);
        Assert.AreEqual(DirectoryAddedSource.SlashCommand, input.Source);
    }

    [TestMethod]
    public void TheMessageDisplayBuilderCarriesTheTurnTheMessageAndTheTextAdded()
    {
        var input = ClaudeAgentModelFactory.MessageDisplayHookInput(
            turnId: "turn-2",
            messageId: "msg_01Display",
            index: 3,
            isFinal: true,
            delta: " and done.");

        Assert.AreEqual(HookEvent.MessageDisplay, input.Event);
        Assert.AreEqual("turn-2", input.TurnId);
        Assert.AreEqual("msg_01Display", input.MessageId);
        Assert.AreEqual(3L, input.Index);
        Assert.IsTrue(input.IsFinal);
        Assert.AreEqual(" and done.", input.Delta);
    }

    [TestMethod]
    public void TheUnknownHookInputBuilderKeepsTheEventNameAsItWasWritten()
    {
        var input = ClaudeAgentModelFactory.UnknownHookInput(
            hookEvent: new HookEvent("TeaBreak"),
            sessionId: "session-1",
            raw: Recorded.Json("""{"hook_event_name":"TeaBreak"}"""));

        Assert.AreEqual("TeaBreak", input.Event.ToString());
        Assert.IsFalse(input.Event.IsDefined);
        Assert.AreEqual("session-1", input.SessionId);
        Assert.AreEqual("TeaBreak", input.Raw.GetProperty("hook_event_name").GetString());
    }

    // One builder a kind of background task: each sets the members every kind carries, its kind's
    // label, and its own member; the unknown takes the label and the entry as the binary wrote it.
    [TestMethod]
    public void TheBackgroundTaskBuildersCarryTheFourEveryKindHasAndEachKindsOwn()
    {
        var shell = ClaudeAgentModelFactory.ShellStopHookBackgroundTask(
            id: "bg-1",
            status: BackgroundTaskStatus.Running,
            description: "tail the log",
            command: "tail -f log.txt");

        Assert.AreEqual("bg-1", shell.Id);
        Assert.AreEqual(StopHookBackgroundTaskType.Shell, shell.TaskType);
        Assert.AreEqual(BackgroundTaskStatus.Running, shell.Status);
        Assert.AreEqual("tail the log", shell.Description);
        Assert.AreEqual("tail -f log.txt", shell.Command);

        var agent = ClaudeAgentModelFactory.SubagentStopHookBackgroundTask(
            id: "bg-2",
            status: BackgroundTaskStatus.Pending,
            description: "second opinion",
            agentType: "reviewer");

        Assert.AreEqual("bg-2", agent.Id);
        Assert.AreEqual(StopHookBackgroundTaskType.Subagent, agent.TaskType);
        Assert.AreEqual(BackgroundTaskStatus.Pending, agent.Status);
        Assert.AreEqual("second opinion", agent.Description);
        Assert.AreEqual(new AgentType("reviewer"), agent.AgentType);

        var unknown = ClaudeAgentModelFactory.UnknownStopHookBackgroundTask(
            taskType: StopHookBackgroundTaskType.Workflow,
            id: "bg-3",
            status: BackgroundTaskStatus.Running,
            description: "the lanes",
            raw: Recorded.Json("""{"id":"bg-3","type":"workflow","status":"running","description":"the lanes","name":"lanes"}"""));

        Assert.AreEqual("bg-3", unknown.Id);
        Assert.AreEqual(StopHookBackgroundTaskType.Workflow, unknown.TaskType);
        Assert.AreEqual(BackgroundTaskStatus.Running, unknown.Status);
        Assert.AreEqual("the lanes", unknown.Description);
        Assert.AreEqual("lanes", unknown.Raw.GetProperty("name").GetString());
        Assert.AreEqual(System.Text.Json.JsonValueKind.Object, ClaudeAgentModelFactory.UnknownStopHookBackgroundTask().Raw.ValueKind);
    }

    [TestMethod]
    public void TheSessionCronBuilderCarriesTheScheduleAndThePromptItWillSend()
    {
        var cron = ClaudeAgentModelFactory.SessionCron(
            id: "cron-1",
            schedule: "0 9 * * 1",
            isRecurring: true,
            prompt: "Open the week.");

        Assert.AreEqual("cron-1", cron.Id);
        Assert.AreEqual("0 9 * * 1", cron.Schedule);
        Assert.IsTrue(cron.IsRecurring);
        Assert.AreEqual("Open the week.", cron.Prompt);
    }

    [TestMethod]
    public void TheMcpServerOriginBuilderCarriesTheServerAndWhereItIsConfigured()
    {
        var origin = ClaudeAgentModelFactory.McpServerOrigin(name: "notes", source: "project");

        Assert.AreEqual("notes", origin.Name);
        Assert.AreEqual(new McpServerSource("project"), origin.Source);
    }
}
