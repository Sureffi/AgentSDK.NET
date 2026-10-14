using System.Text.Json;
using Claude.Agent.Tests.Wire;

namespace Claude.Agent.Tests.Tools;

// The sixteen typed tools every recorded init list offers and no recording calls: the task list, the
// schedulers, the background runners, the worktree pair, the notifier, the review reporter, the
// artifact surface and the cloud routines. Nothing in the fixtures carries one of their calls, so
// their shapes stand on the declaration alone and these are what hold them: every JSON below is
// hand-written against it, and every test here would fail on a parser that read a key by another
// name, dropped it, or told two variants apart the wrong way.
[TestClass]
public sealed class OfferedToolDecodeTests
{
    [TestMethod]
    public void ATaskCreateNamesTheTaskItPutOnTheList()
    {
        var input = Input<TaskCreateToolInput>("TaskCreate", """
            {"subject":"write the brief","description":"the brief the lane builds from","activeForm":"writing the brief"}
            """);
        Assert.AreEqual("write the brief", input.Subject);
        Assert.AreEqual("the brief the lane builds from", input.Description);
        Assert.AreEqual("writing the brief", input.ActiveForm);

        var output = Output<TaskCreateToolOutput>("TaskCreate", """
            {"task":{"id":"task-1","subject":"write the brief"}}
            """);
        Assert.AreEqual("task-1", output.Task.Id);
        Assert.AreEqual("write the brief", output.Task.Subject);
    }

    [TestMethod]
    public void ATaskGetCarriesWhatTheTaskBlocksAndWhatBlocksIt()
    {
        var input = Input<TaskGetToolInput>("TaskGet", """{"taskId":"task-1"}""");
        Assert.AreEqual("task-1", input.TaskId);

        var output = Output<TaskGetToolOutput>("TaskGet", """
            {"task":{"id":"task-1","subject":"write the brief","description":"the brief the lane builds from",
             "status":"in_progress","blocks":["task-2","task-4"],"blockedBy":["task-3"]}}
            """);
        Assert.IsNotNull(output.Task);
        Assert.AreEqual(TaskItemStatus.InProgress, output.Task.Status);
        Assert.IsTrue(output.Task.Status.IsDefined);
        CollectionAssert.AreEqual(new[] { "task-2", "task-4" }, output.Task.Blocks.ToArray());
        CollectionAssert.AreEqual(new[] { "task-3" }, output.Task.BlockedBy.ToArray());
    }

    // The binary writes the key whether or not there is a task under it, so a null there is no task
    // rather than a shape that misfits.
    [TestMethod]
    public void ATaskGetOfATaskTheListDoesNotHoldCarriesNoTask()
    {
        var output = Output<TaskGetToolOutput>("TaskGet", """{"task":null}""");
        Assert.IsNull(output.Task);
        Assert.AreEqual("""{"task":null}""", ToolTypes.WriteOutput(output).GetRawText());
    }

    [TestMethod]
    public void ATaskUpdateCarriesWhatChangedAndTheMoveTheStatusMade()
    {
        var input = Input<TaskUpdateToolInput>("TaskUpdate", """
            {"taskId":"task-1","subject":"write the brief","description":"the brief the lane builds from",
             "activeForm":"writing the brief","status":"deleted","addBlocks":["task-2"],
             "addBlockedBy":["task-3"],"owner":"the desk"}
            """);
        Assert.AreEqual(TaskUpdateStatus.Deleted, input.Status);
        CollectionAssert.AreEqual(new[] { "task-2" }, input.AddBlocks.ToArray());
        CollectionAssert.AreEqual(new[] { "task-3" }, input.AddBlockedBy.ToArray());
        Assert.AreEqual("the desk", input.Owner);

        var output = Output<TaskUpdateToolOutput>("TaskUpdate", """
            {"success":true,"taskId":"task-1","updatedFields":["status","owner"],
             "statusChange":{"from":"pending","to":"in_progress"}}
            """);
        Assert.IsTrue(output.Success);
        CollectionAssert.AreEqual(new[] { "status", "owner" }, output.UpdatedFields.ToArray());
        Assert.IsNull(output.Error);
        Assert.IsNotNull(output.StatusChange);
        Assert.AreEqual("pending", output.StatusChange.From);
        Assert.AreEqual("in_progress", output.StatusChange.To);
    }

    [TestMethod]
    public void ATaskListNamesEveryTaskAndItsOwner()
    {
        Assert.AreEqual("{}", ToolTypes.WriteInput(Input<TaskListToolInput>("TaskList", "{}")).GetRawText());

        var output = Output<TaskListToolOutput>("TaskList", """
            {"tasks":[{"id":"task-1","subject":"write the brief","status":"pending","blockedBy":[]},
                      {"id":"task-2","subject":"read it back","status":"completed","owner":"the desk","blockedBy":["task-1"]}]}
            """);
        Assert.AreEqual(2, output.Tasks.Count);
        Assert.IsNull(output.Tasks[0].Owner);
        Assert.AreEqual(0, output.Tasks[0].BlockedBy.Count);
        Assert.AreEqual(TaskItemStatus.Completed, output.Tasks[1].Status);
        Assert.AreEqual("the desk", output.Tasks[1].Owner);
    }

    [TestMethod]
    public void ACronCreateCarriesTheScheduleInWordsAndWhetherItSurvivesTheSession()
    {
        var input = Input<CronCreateToolInput>("CronCreate", """
            {"cron":"30 14 * * 1","prompt":"read the mail","recurring":false,"durable":true}
            """);
        Assert.AreEqual("30 14 * * 1", input.Cron);
        Assert.AreEqual("read the mail", input.Prompt);
        Assert.IsFalse(input.Recurring);
        Assert.IsTrue(input.Durable);

        var output = Output<CronCreateToolOutput>("CronCreate", """
            {"id":"job-1","humanSchedule":"every Monday at half past two","recurring":true}
            """);
        Assert.AreEqual("job-1", output.Id);
        Assert.AreEqual("every Monday at half past two", output.HumanSchedule);
        Assert.IsTrue(output.Recurring);
        Assert.IsNull(output.Durable);
    }

    [TestMethod]
    public void ACronDeleteAndACronListCarryTheJobsTheSessionHolds()
    {
        Assert.AreEqual("job-1", Input<CronDeleteToolInput>("CronDelete", """{"id":"job-1"}""").Id);
        Assert.AreEqual("job-1", Output<CronDeleteToolOutput>("CronDelete", """{"id":"job-1"}""").Id);

        var output = Output<CronListToolOutput>("CronList", """
            {"jobs":[{"id":"job-1","cron":"30 14 * * 1","humanSchedule":"every Monday at half past two",
                      "prompt":"read the mail","recurring":true,"durable":false}]}
            """);
        Assert.AreEqual(1, output.Jobs.Count);
        Assert.AreEqual("30 14 * * 1", output.Jobs[0].Cron);
        Assert.IsTrue(output.Jobs[0].Recurring);
        Assert.IsFalse(output.Jobs[0].Durable);
        Assert.AreEqual(0, Output<CronListToolOutput>("CronList", """{"jobs":[]}""").Jobs.Count);
    }

    [TestMethod]
    public void AScheduleWakeupCarriesTheDelayItWasHeldToAndWhenItFires()
    {
        var input = Input<ScheduleWakeupToolInput>("ScheduleWakeup", """
            {"delaySeconds":30,"reason":"the build takes about five minutes","prompt":"carry on","noop":false}
            """);
        Assert.AreEqual(TimeSpan.FromSeconds(30), input.Delay);
        Assert.IsFalse(input.NothingChanged);
        Assert.IsNull(input.Stop);

        var output = Output<ScheduleWakeupToolOutput>("ScheduleWakeup", """
            {"scheduledFor":1764600060000,"clampedDelaySeconds":60,"wasClamped":true}
            """);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeMilliseconds(1764600060000), output.ScheduledFor);
        Assert.AreEqual(TimeSpan.FromMinutes(1), output.ClampedDelay);
        Assert.IsTrue(output.WasClamped);
        Assert.IsNull(output.Stopped);
        Assert.IsNull(output.CancelledWakeupCount);
    }

    [TestMethod]
    public void AScheduleWakeupThatEndedTheLoopCarriesWhatItCancelled()
    {
        var input = Input<ScheduleWakeupToolInput>("ScheduleWakeup", """{"stop":true}""");
        Assert.IsTrue(input.Stop);
        Assert.IsNull(input.Delay);
        Assert.IsNull(input.Reason);

        var output = Output<ScheduleWakeupToolOutput>("ScheduleWakeup", """
            {"scheduledFor":1764600000000,"clampedDelaySeconds":60,"wasClamped":false,"stopped":true,"cancelledWakeups":2}
            """);
        Assert.IsTrue(output.Stopped);
        Assert.AreEqual(2L, output.CancelledWakeupCount);
    }

    [TestMethod]
    public void AMonitorWatchesACommandOrASocketAndCarriesItsDeadline()
    {
        var command = Input<MonitorToolInput>("Monitor", """
            {"description":"the build","timeout_ms":300000,"command":"tail -f build.log"}
            """);
        Assert.AreEqual(TimeSpan.FromMinutes(5), command.Timeout);
        Assert.AreEqual("tail -f build.log", command.Command);
        Assert.IsNull(command.WebSocket);

        var socket = Input<MonitorToolInput>("Monitor", """
            {"description":"the feed","timeout_ms":60000,"ws":{"url":"wss://example.com/feed","protocols":["one","two"]}}
            """);
        Assert.IsNull(socket.Command);
        Assert.IsNotNull(socket.WebSocket);
        Assert.AreEqual("wss://example.com/feed", socket.WebSocket.Url);
        CollectionAssert.AreEqual(new[] { "one", "two" }, socket.WebSocket.Protocols.ToArray());

        var output = Output<MonitorToolOutput>("Monitor", """{"taskId":"bregtd04n","timeoutMs":0,"persistent":true}""");
        Assert.AreEqual("bregtd04n", output.TaskId);
        Assert.AreEqual(TimeSpan.Zero, output.Timeout);
        Assert.IsTrue(output.Persistent);
    }

    // The status is which type the output is. A remote launch has the session to watch or is no remote
    // launch; a status the declaration does not name, or none, is null.
    [TestMethod]
    public void AWorkflowOutputIsOneTypeAStatusAndAStatusUndeclaredIsNull()
    {
        var here = Output<WorkflowAsyncLaunchedToolOutput>("Workflow", """
            {"status":"async_launched","taskId":"bregtd04n","taskType":"local_workflow","workflowName":"the lanes",
             "runId":"run-1","summary":"the script would not parse","error":"line 3: unexpected token"}
            """);
        Assert.AreEqual("bregtd04n", here.TaskId);
        Assert.AreEqual("the lanes", here.WorkflowName);
        Assert.AreEqual("line 3: unexpected token", here.Error);

        var elsewhere = Output<WorkflowRemoteLaunchedToolOutput>("Workflow", """
            {"status":"remote_launched","taskId":"bregtd04n","taskType":"remote_agent","workflowName":"the lanes",
             "summary":"three lanes, one each","sessionUrl":"https://example.com/s/9",
             "warning":"the branch here is ahead of the pushed one"}
            """);
        Assert.AreEqual(WorkflowTaskType.RemoteAgent, elsewhere.TaskType);
        Assert.AreEqual("https://example.com/s/9", elsewhere.SessionUrl);
        Assert.AreEqual("the branch here is ahead of the pushed one", elsewhere.Warning);

        Assert.IsNull(Read("Workflow", """{"status":"completed","taskId":"bregtd04n"}"""));
        Assert.IsNull(Read("Workflow", """{"taskId":"bregtd04n","runId":"run-1"}"""));
        Assert.IsNull(Read("Workflow", """{"status":"remote_launched","taskId":"bregtd04n"}"""));
    }

    [TestMethod]
    public void AWorkflowLaunchedHereCarriesTheRunToResumeFrom()
    {
        var input = Input<WorkflowToolInput>("Workflow", """
            {"name":"the lanes","description":"what it is for","title":"what it is called",
             "scriptPath":"/work/lanes.script","resumeFromRunId":"run-1"}
            """);
        Assert.IsNull(input.Script);
        Assert.AreEqual("the lanes", input.Name);
        Assert.AreEqual("/work/lanes.script", input.ScriptPath);
        Assert.AreEqual("run-1", input.ResumeFromRunId);

        var output = Output<WorkflowAsyncLaunchedToolOutput>("Workflow", """
            {"status":"async_launched","taskId":"bregtd04n","taskType":"local_workflow","runId":"run-1",
             "transcriptDir":"/work/transcripts","scriptPath":"/work/lanes.script"}
            """);
        Assert.AreEqual(WorkflowTaskType.LocalWorkflow, output.TaskType);
        Assert.AreEqual("run-1", output.RunId);
        Assert.AreEqual("/work/transcripts", output.TranscriptDirectory);
        Assert.AreEqual("/work/lanes.script", output.ScriptPath);
        Assert.IsNull(output.Error);
    }

    [TestMethod]
    public void AnEnterWorktreeCarriesTheWorktreeAndItsBranch()
    {
        var byName = Input<EnterWorktreeToolInput>("EnterWorktree", """{"name":"run/lane"}""");
        Assert.AreEqual("run/lane", byName.Name);
        Assert.IsNull(byName.Path);

        var byPath = Input<EnterWorktreeToolInput>("EnterWorktree", """{"path":"/work/tree"}""");
        Assert.IsNull(byPath.Name);
        Assert.AreEqual("/work/tree", byPath.Path);

        var output = Output<EnterWorktreeToolOutput>("EnterWorktree", """
            {"worktreePath":"/work/tree","worktreeBranch":"run/lane","message":"moved into the worktree"}
            """);
        Assert.AreEqual("/work/tree", output.WorktreePath);
        Assert.AreEqual("run/lane", output.WorktreeBranch);
        Assert.AreEqual("moved into the worktree", output.Message);
    }

    // The action is which type the input is: a keep takes nothing, a remove may say whether work is
    // thrown away.
    [TestMethod]
    public void AnExitWorktreeInputIsOneTypeAnActionAndAnActionUndeclaredIsNull()
    {
        Input<ExitWorktreeKeepToolInput>("ExitWorktree", """{"action":"keep"}""");
        Assert.IsTrue(Input<ExitWorktreeRemoveToolInput>("ExitWorktree", """{"action":"remove","discard_changes":true}""").DiscardChanges);
        Assert.IsNull(Input<ExitWorktreeRemoveToolInput>("ExitWorktree", """{"action":"remove"}""").DiscardChanges);

        Assert.IsNull(ReadInput("ExitWorktree", """{"action":"archive"}"""));
        Assert.IsNull(ReadInput("ExitWorktree", """{"discard_changes":true}"""));
    }

    // The action is which type the output is: a keep names the terminal session left with the
    // worktree, a remove counts what was thrown away.
    [TestMethod]
    public void AnExitWorktreeOutputIsOneTypeAnActionAndAnActionUndeclaredIsNull()
    {
        var kept = Output<ExitWorktreeKeepToolOutput>("ExitWorktree", """
            {"action":"keep","originalCwd":"/work","worktreePath":"/work/tree","worktreeBranch":"run/lane",
             "tmuxSessionName":"lane","message":"the worktree stands"}
            """);
        Assert.AreEqual("/work", kept.OriginalWorkingDirectory);
        Assert.AreEqual("run/lane", kept.WorktreeBranch);
        Assert.AreEqual("lane", kept.TmuxSessionName);

        var removed = Output<ExitWorktreeRemoveToolOutput>("ExitWorktree", """
            {"action":"remove","originalCwd":"/work","worktreePath":"/work/tree","worktreeBranch":"run/lane",
             "discardedFiles":2,"discardedCommits":1,"message":"the worktree is gone"}
            """);
        Assert.AreEqual("/work/tree", removed.WorktreePath);
        Assert.AreEqual("the worktree is gone", removed.Message);
        Assert.AreEqual(2L, removed.DiscardedFileCount);
        Assert.AreEqual(1L, removed.DiscardedCommitCount);

        Assert.IsNull(Read("ExitWorktree", """
            {"action":"archive","originalCwd":"/work","worktreePath":"/work/tree","message":"the worktree is packed"}
            """));
        Assert.IsNull(Read("ExitWorktree", """{"originalCwd":"/work","worktreePath":"/work/tree","message":"left"}"""));
    }

    [TestMethod]
    public void APushNotificationCarriesWhyItWasNotPushed()
    {
        var input = Input<PushNotificationToolInput>("PushNotification", """
            {"message":"the build is green","status":"proactive"}
            """);
        Assert.AreEqual(PushNotificationStatus.Proactive, input.Status);
        Assert.IsTrue(input.Status.IsDefined);

        var output = Output<PushNotificationToolOutput>("PushNotification", """
            {"message":"the build is green","pushSent":false,"localSent":true,
             "disabledReason":"user_present","sentAt":"2026-09-21T09:00:00Z"}
            """);
        Assert.IsFalse(output.PushSent);
        Assert.IsTrue(output.LocalSent);
        Assert.AreEqual(PushNotificationDisabledReason.UserPresent, output.DisabledReason);
        Assert.AreEqual("2026-09-21T09:00:00Z", output.SentAt);
    }

    [TestMethod]
    public void AReportFindingsCarriesEveryMemberOfAFindingBothWays()
    {
        const string Finding = """
            {"file":"src/Reader.cs","line":12,"summary":"the index runs past the end of the list",
             "short_summary":"index past the end","failure_scenario":"a list of one and an offset of one",
             "category":"correctness","verdict":"PLAUSIBLE","outcome":"no_change_needed"}
            """;

        var input = Input<ReportFindingsToolInput>("ReportFindings", $$"""{"level":"xhigh","findings":[{{Finding}}]}""");
        Assert.AreEqual(Effort.XHigh, input.Level);
        Assert.AreEqual(1, input.Findings.Count);
        Assert.AreEqual("src/Reader.cs", input.Findings[0].File);
        Assert.AreEqual(12L, input.Findings[0].Line);
        Assert.AreEqual("index past the end", input.Findings[0].ShortSummary);
        Assert.AreEqual("a list of one and an offset of one", input.Findings[0].FailureScenario);
        Assert.AreEqual("correctness", input.Findings[0].Category);
        Assert.AreEqual(ReportFindingsVerdict.Plausible, input.Findings[0].Verdict);
        Assert.AreEqual(ReportFindingsOutcome.NoChangeNeeded, input.Findings[0].Outcome);

        var output = Output<ReportFindingsToolOutput>("ReportFindings", $$"""{"count":1,"findings":[{{Finding}}]}""");
        Assert.AreEqual(1L, output.Count);
        Assert.IsNull(output.Level);
        Assert.AreEqual(ReportFindingsVerdict.Plausible, output.Findings[0].Verdict);

        var bare = Output<ReportFindingsToolOutput>("ReportFindings", """
            {"count":0,"findings":[{"file":"src/Reader.cs","summary":"nothing survived","failure_scenario":"none"}]}
            """);
        Assert.IsNull(bare.Findings[0].Line);
        Assert.IsNull(bare.Findings[0].Verdict);
        Assert.IsNull(bare.Findings[0].Outcome);
    }

    // The action is which type the input is, each with the keys that action takes. The declaration
    // names "publish" and makes it what no action means, so a call without one publishes, and a
    // publish is written with its action named.
    [TestMethod]
    public void AnArtifactInputIsOneTypeAnActionAndAnActionUndeclaredIsNull()
    {
        var publish = Input<ArtifactPublishToolInput>("Artifact", """
            {"file_path":"/work/page.html","icon":"chart","title":"the page","description":"what the page is",
             "label":"draft for the desk","force":true,"contract":"latest"}
            """);
        Assert.AreEqual("/work/page.html", publish.FilePath);
        Assert.AreEqual("chart", publish.Icon);
        Assert.IsTrue(publish.Force);
        Assert.AreEqual("latest", publish.Contract);
        Assert.IsNull(publish.Url);
        Assert.AreEqual(
            """{"action":"publish","file_path":"/work/page.html"}""",
            ToolTypes.WriteInput(Input<ArtifactPublishToolInput>("Artifact", """{"action":"publish","file_path":"/work/page.html"}""")).GetRawText());
        Assert.AreEqual(
            """{"action":"publish","file_path":"/work/page.html"}""",
            ToolTypes.WriteInput(Input<ArtifactPublishToolInput>("Artifact", """{"action":null,"file_path":"/work/page.html"}""")).GetRawText());

        var listing = Input<ArtifactListToolInput>("Artifact", """{"action":"list","limit":25,"scope":"shared"}""");
        Assert.AreEqual(25L, listing.Limit);
        Assert.AreEqual(ArtifactScope.Shared, listing.Scope);

        var read = Input<ArtifactReadToolInput>("Artifact", """
            {"action":"read","url":"https://example.com/a/1","prompt":"what does the page say?"}
            """);
        Assert.AreEqual("https://example.com/a/1", read.Url);
        Assert.AreEqual("what does the page say?", read.Prompt);

        Input<ArtifactListTypesToolInput>("Artifact", """{"action":"list_types"}""");
        Assert.AreEqual("https://example.com/a/1", Input<ArtifactWatchToolInput>("Artifact", """{"action":"watch","url":"https://example.com/a/1"}""").Url);
        Assert.AreEqual("https://example.com/a/1", Input<ArtifactUnwatchToolInput>("Artifact", """{"action":"unwatch","url":"https://example.com/a/1"}""").Url);
        Assert.IsNull(Input<ArtifactStatusToolInput>("Artifact", """{"action":"status"}""").Url);

        var upload = Input<ArtifactUploadAssetToolInput>("Artifact", """
            {"action":"upload_asset","url":"https://example.com/a/1","file_paths":["/work/logo.png","/work/map.png"]}
            """);
        Assert.AreEqual("https://example.com/a/1", upload.Url);
        Assert.IsNull(upload.FilePath);
        CollectionAssert.AreEqual(new[] { "/work/logo.png", "/work/map.png" }, upload.FilePaths.ToArray());

        var assets = Input<ArtifactListAssetsToolInput>("Artifact", """
            {"action":"list_assets","url":"https://example.com/a/1","after":"cursor-1"}
            """);
        Assert.AreEqual("cursor-1", assets.After);

        var saved = Input<ArtifactReadAssetToolInput>("Artifact", """
            {"action":"read_asset","url":"https://example.com/a/1","asset_id":"0123456789abcdef0123456789abcdef","out_dir":"/work/assets"}
            """);
        Assert.AreEqual("0123456789abcdef0123456789abcdef", saved.AssetId);
        Assert.AreEqual("/work/assets", saved.OutputDirectory);

        var deleted = Input<ArtifactDeleteAssetToolInput>("Artifact", """
            {"action":"delete_asset","url":"https://example.com/a/1","asset_id":"0123456789abcdef0123456789abcdef"}
            """);
        Assert.AreEqual("https://example.com/a/1", deleted.Url);
        Assert.AreEqual("0123456789abcdef0123456789abcdef", deleted.AssetId);

        Assert.IsNull(ReadInput("Artifact", """{"action":"copy_from","url":"https://example.com/a/1"}"""));
        Assert.IsNull(ReadInput("Artifact", """{"action":3,"file_path":"/work/page.html"}"""));
        Assert.IsNull(ReadInput("Artifact", """{"title":"a publish without the file"}"""));
        Assert.IsNull(ReadInput("Artifact", """{"action":"read"}"""));
        Assert.IsNull(ReadInput("Artifact", """{"action":"read_asset","url":"https://example.com/a/1"}"""));
        Assert.IsNull(ReadInput("Artifact", """{"action":"upload_asset","url":"https://example.com/a/1"}"""));
        Assert.IsNull(ReadInput("Artifact", """{"action":"upload_asset","url":"https://example.com/a/1","file_paths":[]}"""));
    }

    [TestMethod]
    public void AnArtifactPublishCarriesWhatTheStoreKeptAndWhatIsLeftInTheWindow()
    {
        var output = Output<ArtifactPublishedToolOutput>("Artifact", """
            {"url":"https://example.com/a/1","path":"/work/page.html","artifact_id":"artifact-1","title":"the page",
             "version":"3","capabilities":{"anything":[1,2]},
             "stored":{"contract":"latest","preferredContract":"latest","capabilities":{"anything":true},
                       "carried":true,"read":"writer"},
             "warnings":["the title came from the file"],"publishesRemaining":4,"publishesResetAt":1764600000000,
             "contract":"latest","updated":true,"icon":"chart","faviconSent":true,"iconDropped":true,
             "audience":"the account","seq":3,"unchanged":true,"liveSubscription":"subscription-1"}
            """);
        Assert.AreEqual("https://example.com/a/1", output.Url);
        Assert.AreEqual("/work/page.html", output.Path);
        Assert.IsNotNull(output.Stored);
        Assert.AreEqual("latest", output.Stored.Contract);
        Assert.AreEqual("writer", output.Stored.Read);
        Assert.IsTrue(output.Stored.Carried);
        CollectionAssert.AreEqual(new[] { "the title came from the file" }, output.Warnings.ToArray());
        Assert.AreEqual(4L, output.PublishesRemaining);
        Assert.AreEqual(1764600000000d, output.PublishesResetAt);
        Assert.AreEqual(3L, output.Sequence);
        Assert.IsTrue(output.FaviconSent);
        Assert.IsTrue(output.IconDropped);
        Assert.IsTrue(output.Unchanged);
        Assert.AreEqual("subscription-1", output.LiveSubscription);
    }

    [TestMethod]
    public void AnArtifactListingCarriesHowTheAccountStandsToEachPage()
    {
        var output = Output<ArtifactListToolOutput>("Artifact", """
            {"artifacts":[{"title":"the page","url":"https://example.com/a/1","favicon":"chart",
                           "updatedAt":"2026-09-21T09:00:00Z","rel":"shared","external":true,"role":"commenter"},
                          {"title":"another","url":"https://example.com/a/2"}],
             "truncated":true,"scope":"all","external_listed":true}
            """);
        Assert.AreEqual(2, output.Artifacts.Count);
        Assert.AreEqual(ArtifactRelation.Shared, output.Artifacts[0].Relation);
        Assert.AreEqual(ArtifactRole.Commenter, output.Artifacts[0].Role);
        Assert.IsTrue(output.Artifacts[0].External);
        Assert.IsNull(output.Artifacts[1].Relation);
        Assert.IsNull(output.Artifacts[1].Role);
        Assert.IsTrue(output.Truncated);
        Assert.AreEqual(ArtifactListScope.All, output.Scope);
        Assert.IsTrue(output.ExternalListed);
    }

    [TestMethod]
    public void AnArtifactReadCarriesTheFetchAndTheVersionItMarkedAsSeen()
    {
        var output = Output<ArtifactReadToolOutput>("Artifact", """
            {"read":{"url":"https://example.com/a/1","bytes":4096,"code":200,"codeText":"OK",
                     "result":"the page itself","durationMs":42},
             "artifactRead":{"slug":"the-page","ver":"3","seeded":false}}
            """);
        Assert.AreEqual(4096L, output.Read.Bytes);
        Assert.AreEqual(200L, output.Read.Code);
        Assert.AreEqual("OK", output.Read.CodeText);
        Assert.AreEqual("the page itself", output.Read.Result);
        Assert.AreEqual(TimeSpan.FromMilliseconds(42), output.Read.Duration);
        Assert.IsNotNull(output.ArtifactRead);
        Assert.AreEqual("the-page", output.ArtifactRead.Slug);
        Assert.AreEqual("3", output.ArtifactRead.Version);
        Assert.IsFalse(output.ArtifactRead.Seeded);
    }

    [TestMethod]
    public void AnArtifactWatchAndUnwatchCarryWhatBecameOfTheRequest()
    {
        var watch = Output<ArtifactWatchToolOutput>("Artifact", """
            {"watch":{"url":"https://example.com/a/1","watching":false,"outcome":"unavailable",
                      "reason":"this session does not keep track of new versions",
                      "durable_skip_reason":"no trigger is set up","task_id":"bregtd04n","since":1764600000000,
                      "token_expires_at":1764603600000,"rail":"live","trigger_id":"trigger-1",
                      "durable_since":"2026-09-21T09:00:00Z","status":409,"detail":"nothing was armed",
                      "note":"ask again elsewhere","events":["published","deleted"]}}
            """);
        Assert.IsFalse(watch.Watch.Watching);
        Assert.AreEqual("unavailable", watch.Watch.Outcome);
        Assert.AreEqual("bregtd04n", watch.Watch.TaskId);
        Assert.AreEqual(1764600000000d, watch.Watch.Since);
        Assert.AreEqual(1764603600000d, watch.Watch.TokenExpiresAt);
        Assert.AreEqual(409L, watch.Watch.StatusCode);
        CollectionAssert.AreEqual(new[] { "published", "deleted" }, watch.Watch.Events.ToArray());

        var unwatch = Output<ArtifactUnwatchToolOutput>("Artifact", """
            {"unwatch":{"url":"https://example.com/a/1","was_watching":true}}
            """);
        Assert.AreEqual("https://example.com/a/1", unwatch.Unwatch.Url);
        Assert.IsTrue(unwatch.Unwatch.WasWatching);
    }

    [TestMethod]
    public void AnArtifactStatusTellsTheThreeShapesOfWatchApartByTheArmTheyName()
    {
        var output = Output<ArtifactStatusToolOutput>("Artifact", """
            {"watches":[{"url":"https://example.com/a/1","task_id":"bregtd04n","since":1764600000000,
                         "explicit":true,"connected":true,"connecting":false,"token_expires_at":1764603600000,
                         "armed_via":"the watch call"},
                        {"url":"https://example.com/a/2","rail":"durable_wake","trigger_id":"trigger-1",
                         "since":"2026-09-21T09:00:00Z","events":["published"],"restored":true},
                        {"url":"https://example.com/a/3","rail":"live_stopped","since":1764600000000,
                         "explicit":false,"armed_via":"the watch call","stop_kind":"the session asked"}],
             "filter_url":"https://example.com/a/1",
             "arms":[{"url":"https://example.com/a/1","rail":"live","state":"open","reconnect":true,"failures":1,
                      "max_failures":5,"next_in_s":30,"last_failure":"2026-09-21T08:59:00Z",
                      "reason":"the other end closed","detail":"it will be tried again",
                      "server_message":"going away","at":1764600000000}]}
            """);
        Assert.AreEqual(3, output.Watches.Count);

        var live = (ArtifactLiveWatch)output.Watches[0];
        Assert.AreEqual("bregtd04n", live.TaskId);
        Assert.IsTrue(live.IsExplicit);
        Assert.IsTrue(live.Connected);
        Assert.IsFalse(live.Connecting);
        Assert.AreEqual(1764603600000d, live.TokenExpiresAt);

        var durable = (ArtifactDurableWakeWatch)output.Watches[1];
        Assert.AreEqual("trigger-1", durable.TriggerId);
        Assert.AreEqual("2026-09-21T09:00:00Z", durable.Since);
        Assert.IsTrue(durable.Restored);

        var stopped = (ArtifactStoppedWatch)output.Watches[2];
        Assert.AreEqual("the session asked", stopped.StopKind);
        Assert.AreEqual(1764600000000d, stopped.Since);
        Assert.IsFalse(stopped.IsExplicit);

        Assert.AreEqual("https://example.com/a/1", output.FilterUrl);
        Assert.AreEqual(1, output.Arms.Count);
        Assert.AreEqual("open", output.Arms[0].State);
        Assert.AreEqual(1L, output.Arms[0].FailureCount);
        Assert.AreEqual(5L, output.Arms[0].MaxFailureCount);
        Assert.AreEqual(TimeSpan.FromSeconds(30), output.Arms[0].NextIn);
        Assert.AreEqual("going away", output.Arms[0].ServerMessage);
        Assert.AreEqual(1764600000000d, output.Arms[0].At);
    }

    [TestMethod]
    public void AnArtifactAssetUploadCarriesTheFileAndABatchCarriesWhyOneDidNotGoUp()
    {
        var one = Output<ArtifactAssetUploadToolOutput>("Artifact", """
            {"written":{"url":"https://example.com/a/1"},
             "asset_upload":{"id":"0123456789abcdef0123456789abcdef","url":"https://example.com/a/1/logo.png",
                             "size_bytes":1024,"content_type":"image/png","file_name":"logo.png"}}
            """);
        Assert.IsNotNull(one.Written);
        Assert.AreEqual("https://example.com/a/1", one.Written.Url);
        Assert.AreEqual(1024L, one.AssetUpload.SizeInBytes);
        Assert.AreEqual("image/png", one.AssetUpload.ContentType);
        Assert.AreEqual("logo.png", one.AssetUpload.FileName);
        Assert.IsNull(one.AssetUpload.Sha256);

        var many = Output<ArtifactAssetUploadsToolOutput>("Artifact", """
            {"asset_uploads":{"url":"https://example.com/a/1","results":[
                {"file_path":"/work/logo.png","status":"uploaded","id":"0123456789abcdef0123456789abcdef",
                 "url":"https://example.com/a/1/logo.png","size_bytes":1024,"content_type":"image/png",
                 "sha256":"9f86d081","file_name":"logo.png"},
                {"file_path":"/work/map.png","status":"not_attempted","reason":"too_large",
                 "message":"the file is past what a store holds","may_be_stored":true}]}}
            """);
        Assert.IsNull(many.Written);
        Assert.AreEqual(2, many.AssetUploads.Results.Count);

        var uploaded = (ArtifactAssetUploadedOutcome)many.AssetUploads.Results[0];
        Assert.AreEqual("/work/logo.png", uploaded.FilePath);
        Assert.AreEqual("9f86d081", uploaded.Sha256);

        var refused = (ArtifactAssetUploadFailedOutcome)many.AssetUploads.Results[1];
        Assert.AreEqual(ArtifactAssetUploadStatus.NotAttempted, refused.Status);
        Assert.AreEqual("too_large", refused.Reason);
        Assert.IsTrue(refused.MayBeStored);
    }

    [TestMethod]
    public void AnArtifactAssetListingCarriesHowMuchOfTheStoreIsTaken()
    {
        var output = Output<ArtifactAssetListToolOutput>("Artifact", """
            {"asset_list":{"url":"https://example.com/a/1",
                           "assets":[{"id":"0123456789abcdef0123456789abcdef","url":"https://example.com/a/1/logo.png",
                                      "content_type":"image/png","size_bytes":1024,"sha256":"9f86d081",
                                      "created_at":"2026-09-21T09:00:00Z"}],
                           "usage":{"files":1,"bytes":1024,"max_files":100,"max_bytes":1048576},
                           "next":"cursor-1","cowritten":true,"outside_writer":true}}
            """);
        Assert.AreEqual(1, output.AssetList.Assets.Count);
        Assert.AreEqual("2026-09-21T09:00:00Z", output.AssetList.Assets[0].CreatedAt);
        Assert.AreEqual(1L, output.AssetList.Usage.FileCount);
        Assert.AreEqual(1024L, output.AssetList.Usage.Bytes);
        Assert.AreEqual(100L, output.AssetList.Usage.MaxFileCount);
        Assert.AreEqual(1048576L, output.AssetList.Usage.MaxBytes);
        Assert.AreEqual("cursor-1", output.AssetList.Next);
        Assert.IsTrue(output.AssetList.Cowritten);
        Assert.IsTrue(output.AssetList.OutsideWriter);
    }

    [TestMethod]
    public void AnArtifactAssetReadAndDeleteCarryTheFileTheyActedOn()
    {
        var read = Output<ArtifactAssetReadToolOutput>("Artifact", """
            {"asset_read":{"id":"0123456789abcdef0123456789abcdef","path":"/work/assets/logo.png","size_bytes":1024,
                           "content_type":"image/png","sha256":"9f86d081","public_read":true,"foreign":true}}
            """);
        Assert.AreEqual("/work/assets/logo.png", read.AssetRead.Path);
        Assert.AreEqual("9f86d081", read.AssetRead.Sha256);
        Assert.IsTrue(read.AssetRead.PublicRead);
        Assert.IsTrue(read.AssetRead.Foreign);
        Assert.IsNull(read.AssetRead.Cowritten);

        var deleted = Output<ArtifactAssetDeleteToolOutput>("Artifact", """
            {"written":{"url":"https://example.com/a/1"},
             "asset_delete":{"id":"0123456789abcdef0123456789abcdef","deleted":true}}
            """);
        Assert.IsTrue(deleted.AssetDelete.Deleted);
        Assert.IsNotNull(deleted.Written);
    }

    // The action is which type the input is, each with the keys that action takes and the binary
    // requires of it. The body the binary requires of create, update and create_webhook_trigger, a JSON
    // object, is on the view as it came, outliving the document it was read from, and is written back
    // with it, so a view handed back is the call the binary takes; one of those actions without a body,
    // or with one that is no object, reads as null. Run's body is optional: null when the call sends
    // none or null, as any key's null is its absence here, the object as it came when it sends one,
    // and a run whose body is anything else reads as null.
    [TestMethod]
    public void ARemoteTriggerInputIsOneTypeAnActionAndAnActionUndeclaredIsNull()
    {
        Input<RemoteTriggerListToolInput>("RemoteTrigger", """{"action":"list"}""");
        Assert.AreEqual("trig_1", Input<RemoteTriggerGetToolInput>("RemoteTrigger", """{"action":"get","trigger_id":"trig_1"}""").TriggerId);
        var create = Input<RemoteTriggerCreateToolInput>("RemoteTrigger", """{"action":"create","body":{"name":"nightly","cron_expression":"0 3 * * *"}}""");
        Assert.AreEqual("""{"name":"nightly","cron_expression":"0 3 * * *"}""", create.Body.GetRawText());
        var update = Input<RemoteTriggerUpdateToolInput>("RemoteTrigger", """{"action":"update","trigger_id":"trig_1","body":{"enabled":false}}""");
        Assert.AreEqual("trig_1", update.TriggerId);
        Assert.AreEqual("""{"enabled":false}""", update.Body.GetRawText());
        var run = Input<RemoteTriggerRunToolInput>("RemoteTrigger", """{"action":"run","trigger_id":"trig_1"}""");
        Assert.AreEqual("trig_1", run.TriggerId);
        Assert.IsNull(run.Body);
        var runWithBody = Input<RemoteTriggerRunToolInput>("RemoteTrigger", """{"action":"run","trigger_id":"trig_1","body":{"note":"now"}}""");
        Assert.AreEqual("""{"note":"now"}""", runWithBody.Body?.GetRawText());
        var webhook = Input<RemoteTriggerCreateWebhookTriggerToolInput>("RemoteTrigger", """{"action":"create_webhook_trigger","body":{"routine_trigger_id":"trig_1"}}""");
        Assert.AreEqual("""{"routine_trigger_id":"trig_1"}""", webhook.Body.GetRawText());

        var runs = Input<RemoteTriggerListRunsToolInput>("RemoteTrigger", """{"action":"list_runs","trigger_id":"trig_1","cursor":"page-2"}""");
        Assert.AreEqual("trig_1", runs.TriggerId);
        Assert.AreEqual("page-2", runs.Cursor);
        Assert.IsNull(Input<RemoteTriggerListRunsToolInput>("RemoteTrigger", """{"action":"list_runs","trigger_id":"trig_1"}""").Cursor);

        var log = Input<RemoteTriggerGetRunLogToolInput>("RemoteTrigger", """{"action":"get_run_log","session_id":"session_1","cursor":"page-3"}""");
        Assert.AreEqual("session_1", log.SessionId);
        Assert.AreEqual("page-3", log.Cursor);

        Assert.AreEqual(
            """{"action":"create","body":{"name":"nightly","cron_expression":"0 3 * * *"}}""",
            ToolTypes.WriteInput(create).GetRawText());
        Assert.AreEqual(
            """{"action":"update","trigger_id":"trig_1","body":{"enabled":false}}""",
            ToolTypes.WriteInput(update).GetRawText());
        Assert.AreEqual(
            """{"action":"create_webhook_trigger","body":{"routine_trigger_id":"trig_1"}}""",
            ToolTypes.WriteInput(webhook).GetRawText());
        Assert.AreEqual("""{"action":"run","trigger_id":"trig_1"}""", ToolTypes.WriteInput(run).GetRawText());
        Assert.AreEqual(
            """{"action":"run","trigger_id":"trig_1","body":{"note":"now"}}""",
            ToolTypes.WriteInput(runWithBody).GetRawText());

        Assert.IsNull(ReadInput("RemoteTrigger", """{"action":"delete","trigger_id":"trig_1"}"""));
        Assert.IsNull(ReadInput("RemoteTrigger", """{"trigger_id":"trig_1"}"""));
        Assert.IsNull(ReadInput("RemoteTrigger", """{"action":"get"}"""));
        Assert.IsNull(ReadInput("RemoteTrigger", """{"action":"update","body":{"enabled":false}}"""));
        Assert.IsNull(ReadInput("RemoteTrigger", """{"action":"run"}"""));
        Assert.IsNull(ReadInput("RemoteTrigger", """{"action":"list_runs","cursor":"page-2"}"""));
        Assert.IsNull(ReadInput("RemoteTrigger", """{"action":"get_run_log","trigger_id":"trig_1"}"""));
        Assert.IsNull(ReadInput("RemoteTrigger", """{"action":"create"}"""));
        Assert.IsNull(ReadInput("RemoteTrigger", """{"action":"create","body":"nightly"}"""));
        Assert.IsNull(ReadInput("RemoteTrigger", """{"action":"update","trigger_id":"trig_1"}"""));
        Assert.IsNull(ReadInput("RemoteTrigger", """{"action":"update","trigger_id":"trig_1","body":null}"""));
        Assert.IsNull(ReadInput("RemoteTrigger", """{"action":"create_webhook_trigger"}"""));
        Assert.IsNull(ReadInput("RemoteTrigger", """{"action":"create_webhook_trigger","body":[]}"""));
        Assert.IsNull(ReadInput("RemoteTrigger", """{"action":"run","trigger_id":"trig_1","body":"now"}"""));
        Assert.IsNull(Input<RemoteTriggerRunToolInput>("RemoteTrigger", """{"action":"run","trigger_id":"trig_1","body":null}""").Body);
    }

    // A caller's body is kept as a copy that outlives the document it came from, and a body the binary
    // would refuse, anything but a JSON object, the default element among it, is refused naming it.
    [TestMethod]
    public void ARemoteTriggerBodyIsAnObjectKeptAsACopy()
    {
        RemoteTriggerUpdateToolInput update;
        using (var document = JsonDocument.Parse("""{"enabled": false}"""))
        {
            update = new RemoteTriggerUpdateToolInput("trig_1", document.RootElement);
        }

        Assert.AreEqual("""{"enabled": false}""", update.Body.GetRawText());

        foreach (var body in new[] { default, Recorded.Json("null"), Recorded.Json("\"nightly\""), Recorded.Json("[]") })
        {
            Assert.AreEqual("body", Assert.ThrowsExactly<ArgumentException>(() => new RemoteTriggerCreateToolInput(body)).ParamName);
            Assert.AreEqual("body", Assert.ThrowsExactly<ArgumentException>(() => new RemoteTriggerUpdateToolInput("trig_1", body)).ParamName);
            Assert.AreEqual("body", Assert.ThrowsExactly<ArgumentException>(() => new RemoteTriggerCreateWebhookTriggerToolInput(body)).ParamName);
            Assert.AreEqual("body", Assert.ThrowsExactly<ArgumentException>(() => new RemoteTriggerRunToolInput("trig_1", body)).ParamName);
        }

        Assert.IsNull(new RemoteTriggerRunToolInput("trig_1").Body);
    }

    // The answer is three scalars. The service's answer is a string whatever it holds, JSON text, an
    // empty string or not JSON at all, and it is handed over as the text it is.
    [TestMethod]
    public void ARemoteTriggerOutputCarriesTheServicesAnswerAsTheStringItIs()
    {
        var output = Output<RemoteTriggerToolOutput>("RemoteTrigger", """
            {"status":200,"json":"{\"trigger\":{\"id\":\"trig_1\",\"enabled\":true}}","summary":"next run in an hour"}
            """);
        Assert.AreEqual(200L, output.Status);
        Assert.AreEqual("""{"trigger":{"id":"trig_1","enabled":true}}""", output.Json);
        Assert.AreEqual("next run in an hour", output.Summary);

        var refused = Output<RemoteTriggerToolOutput>("RemoteTrigger", """{"status":404,"json":"not found"}""");
        Assert.AreEqual(404L, refused.Status);
        Assert.AreEqual("not found", refused.Json);
        Assert.IsNull(refused.Summary);

        Assert.AreEqual(string.Empty, Output<RemoteTriggerToolOutput>("RemoteTrigger", """{"status":204,"json":""}""").Json);

        Assert.IsNull(Read("RemoteTrigger", """{"status":200,"json":{"trigger":{"id":"trig_1"}}}"""));
        Assert.IsNull(Read("RemoteTrigger", """{"status":"200","json":"{}"}"""));
        Assert.IsNull(Read("RemoteTrigger", """{"json":"{}"}"""));
    }

    // Degrade, never throw: the view is null beside the raw, whatever the JSON does.
    [TestMethod]
    public void AShapeOfAnOfferedToolThatMisfitsIsNullAndNothingThrows()
    {
        // No key of the eleven Artifact shapes: the whole output is null.
        Assert.IsNull(Read("Artifact", """{"list_types":["html","markdown"]}"""));

        // An arm of a watch this library has no shape for takes the whole output with it.
        Assert.IsNull(Read("Artifact", """{"watches":[{"url":"https://example.com/a/1","rail":"telepathy"}]}"""));

        // A status of an upload result this library has no shape for does the same.
        Assert.IsNull(Read("Artifact", """
            {"asset_uploads":{"url":"https://example.com/a/1","results":[{"file_path":"/work/logo.png","status":"pending"}]}}
            """));

        // A required member missing.
        Assert.IsNull(Read("TaskCreate", """{"task":{"id":"task-1"}}"""));
        Assert.IsNull(Read("CronCreate", """{"id":"job-1","humanSchedule":"every Monday"}"""));
        Assert.IsNull(Read("EnterWorktree", """{"worktreePath":"/work/tree"}"""));

        // A required member of the wrong JSON kind.
        Assert.IsNull(Read("Monitor", """{"taskId":"bregtd04n","timeoutMs":"300000"}"""));

        // A number no time can be made of, under a key the view reads as one.
        Assert.IsNull(Read("ScheduleWakeup", """
            {"scheduledFor":9223372036854775807,"clampedDelaySeconds":60,"wasClamped":false}
            """));

        // A nested shape that misfits takes the whole view with it.
        Assert.IsNull(Read("TaskList", """{"tasks":[{"id":"task-1","subject":"the brief","status":"pending"}]}"""));
        Assert.IsNull(Read("Artifact", """{"asset_list":{"url":"https://example.com/a/1","assets":[],"usage":{"files":1}}}"""));

        // An output that is not an object at all.
        Assert.IsNull(Read("CronList", """["job-1"]"""));
        Assert.IsNull(Read("Workflow", "null"));

        // The wrong tool's name over the right tool's JSON. A tool whose input the declaration gives
        // no required key — ScheduleWakeup, Workflow, EnterWorktree and the two that take an empty
        // object — reads any object as its own input, and that is the declaration's doing,
        // not a hole in the dispatch: the name is what decides the shape.
        Assert.IsNull(ToolTypes.ReadInput("TaskCreate", ToolTypes.WriteInput(new CronDeleteToolInput("job-1"))));
        Assert.IsNotNull(ToolTypes.ReadInput("ScheduleWakeup", ToolTypes.WriteInput(new CronDeleteToolInput("job-1"))));
    }

    private static T Input<T>(string tool, string json)
        where T : ToolInput
    {
        using var document = JsonDocument.Parse(json);
        var read = ToolTypes.ReadInput(tool, document.RootElement);
        Assert.IsNotNull(read, json);
        return (T)read;
    }

    private static T Output<T>(string tool, string json)
        where T : ToolOutput
    {
        using var document = JsonDocument.Parse(json);
        var read = ToolTypes.ReadOutput(tool, document.RootElement);
        Assert.IsNotNull(read, json);
        return (T)read;
    }

    private static ToolOutput? Read(string tool, string json)
    {
        using var document = JsonDocument.Parse(json);
        return ToolTypes.ReadOutput(tool, document.RootElement);
    }

    private static ToolInput? ReadInput(string tool, string json)
    {
        using var document = JsonDocument.Parse(json);
        return ToolTypes.ReadInput(tool, document.RootElement);
    }
}
