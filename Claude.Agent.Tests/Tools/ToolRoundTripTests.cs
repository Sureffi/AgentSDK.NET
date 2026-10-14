using System.Collections;
using System.Reflection;
using System.Text.Json;
using Claude.Agent.Tests.Replay;
using Claude.Agent.Tests.Wire;

namespace Claude.Agent.Tests.Tools;

// A tool's input and output go in as well as out: a caller builds one through its public
// constructor, the library writes it as the wire's JSON, and reading that back gives the same view.
// Every concrete tool type is in the table below, held by a test that fails when one is added and
// left out.
[TestClass]
public sealed class ToolRoundTripTests
{
    private static readonly (string Tool, ToolInput Input)[] Inputs =
    [
        ("Agent", new AgentToolInput(
            "look it up",
            "find the line",
            subagentType: "bee",
            model: "haiku",
            runInBackground: true,
            name: "scout",
            teamName: "hive",
            mode: PermissionMode.AcceptEdits,
            isolation: AgentIsolation.Worktree)),
        ("Bash", new BashToolInput(
            "wc -l notes.txt",
            timeout: TimeSpan.FromSeconds(30),
            description: "count the lines",
            runInBackground: false,
            dangerouslyDisableSandbox: false)),
        ("TaskOutput", new TaskOutputToolInput("bregtd04n", block: true, timeout: TimeSpan.FromMilliseconds(1500))),
        ("TaskStop", new TaskStopToolInput("bregtd04n", "shell-1")),
        ("Edit", new EditToolInput("/notes.txt", "alpha", "beta", replaceAll: true)),
        ("Read", new ReadToolInput("/notes.txt", offset: 2, limit: 40, pages: "1-3")),
        ("Write", new WriteToolInput("/draft.txt", "alpha")),
        ("Glob", new GlobToolInput("*.txt", "/work")),
        ("Grep", new GrepToolInput(
            "line",
            path: "/work",
            glob: "*.txt",
            outputMode: GrepOutputMode.FilesWithMatches,
            beforeContextLines: 1,
            afterContextLines: 2,
            surroundingContextLines: 3,
            contextLines: 4,
            showLineNumbers: true,
            caseInsensitive: false,
            matchesOnly: true,
            fileType: "cs",
            headLimit: 10,
            offset: 5,
            multiline: true)),
        ("NotebookEdit", new NotebookEditToolInput(
            "/cells.ipynb",
            "print(2)",
            cellId: "a1",
            cellType: NotebookCellType.Code,
            editMode: NotebookEditMode.Insert)),
        ("WebFetch", new WebFetchToolInput("https://example.com", "what is the page title?")),
        ("WebSearch", new WebSearchToolInput("the query", ["good.example"], ["bad.example"])),
        ("TaskCreate", new TaskCreateToolInput("write the brief", "the brief the lane builds from", activeForm: "writing the brief")),
        ("TaskGet", new TaskGetToolInput("task-1")),
        ("TaskUpdate", new TaskUpdateToolInput(
            "task-1",
            subject: "write the brief",
            description: "the brief the lane builds from",
            activeForm: "writing the brief",
            status: TaskUpdateStatus.InProgress,
            addBlocks: ["task-2"],
            addBlockedBy: ["task-3"],
            owner: "the desk")),
        ("TaskList", new TaskListToolInput()),
        ("CronCreate", new CronCreateToolInput("30 14 * * 1", "read the mail", recurring: true, durable: false)),
        ("CronDelete", new CronDeleteToolInput("job-1")),
        ("CronList", new CronListToolInput()),
        ("ScheduleWakeup", new ScheduleWakeupToolInput(
            TimeSpan.FromMinutes(5),
            "the build takes about five minutes",
            "carry on where this left off",
            stop: false,
            nothingChanged: true)),
        ("Monitor", new MonitorToolInput(
            "the build",
            TimeSpan.FromMinutes(5),
            command: "tail -f build.log",
            webSocket: new MonitorToolWebSocket("wss://example.com/feed", ["one", "two"]))),
        ("Workflow", new WorkflowToolInput(
            script: "the whole script",
            name: "the named one",
            description: "what it is for",
            title: "what it is called",
            scriptPath: "/work/lanes.script",
            resumeFromRunId: "run-1")),
        ("EnterWorktree", new EnterWorktreeToolInput("run/lane", "/work/tree")),
        ("ExitWorktree", new ExitWorktreeKeepToolInput()),
        ("ExitWorktree", new ExitWorktreeRemoveToolInput(discardChanges: true)),
        ("PushNotification", new PushNotificationToolInput("the build is green", PushNotificationStatus.Proactive)),
        ("ReportFindings", new ReportFindingsToolInput([Finding], Effort.High)),
        ("Artifact", new ArtifactPublishToolInput(
            "/work/page.html",
            favicon: "chart",
            icon: "chart",
            title: "the page",
            description: "what the page is",
            label: "draft for the desk",
            url: "https://example.com/a/1",
            force: true,
            contract: "latest")),
        ("Artifact", new ArtifactListToolInput(limit: 25, scope: ArtifactScope.All)),
        ("Artifact", new ArtifactReadToolInput("https://example.com/a/1", prompt: "what does the page say?")),
        ("Artifact", new ArtifactListTypesToolInput()),
        ("Artifact", new ArtifactWatchToolInput("https://example.com/a/1")),
        ("Artifact", new ArtifactUnwatchToolInput("https://example.com/a/1")),
        ("Artifact", new ArtifactStatusToolInput("https://example.com/a/1")),
        ("Artifact", new ArtifactUploadAssetToolInput(
            "https://example.com/a/1",
            filePath: "/work/logo.png",
            filePaths: ["/work/logo.png", "/work/map.png"])),
        ("Artifact", new ArtifactListAssetsToolInput("https://example.com/a/1", after: "cursor-1")),
        ("Artifact", new ArtifactReadAssetToolInput("https://example.com/a/1", "0123456789abcdef0123456789abcdef", outputDirectory: "/work/assets")),
        ("Artifact", new ArtifactDeleteAssetToolInput("https://example.com/a/1", "0123456789abcdef0123456789abcdef")),
        ("RemoteTrigger", new RemoteTriggerListToolInput()),
        ("RemoteTrigger", new RemoteTriggerGetToolInput("trig_1")),
        ("RemoteTrigger", new RemoteTriggerCreateToolInput(Recorded.Json("""{"name": "nightly", "cron_expression": "0 3 * * *", "job_config": {"ccr": {"events": []}}}"""))),
        ("RemoteTrigger", new RemoteTriggerUpdateToolInput("trig_1", Recorded.Json("""{"enabled": false}"""))),
        ("RemoteTrigger", new RemoteTriggerRunToolInput("trig_1")),
        ("RemoteTrigger", new RemoteTriggerRunToolInput("trig_1", Recorded.Json("""{"note": "now"}"""))),
        ("RemoteTrigger", new RemoteTriggerCreateWebhookTriggerToolInput(Recorded.Json("""{"routine_trigger_id": "trig_1", "events": ["push"]}"""))),
        ("RemoteTrigger", new RemoteTriggerListRunsToolInput("trig_1", cursor: "page-2")),
        ("RemoteTrigger", new RemoteTriggerGetRunLogToolInput("session_1", cursor: "page-2")),
    ];

    // One finding, every member set, carried by the input and by the output alike.
    private static ReportFindingsToolFinding Finding => new(
        "src/Reader.cs",
        "the index runs past the end of the list",
        "a list of one and an offset of one: the read throws",
        line: 12,
        shortSummary: "index past the end",
        category: "correctness",
        verdict: ReportFindingsVerdict.Confirmed,
        outcome: ReportFindingsOutcome.Fixed);

    private static readonly (string Tool, ToolOutput Output)[] Outputs =
    [
        ("Agent", new AgentCompletedToolOutput(
            "agent-1",
            [new AgentToolTextBlock("the answer")],
            3,
            TimeSpan.FromMilliseconds(1422),
            8548,
            new AgentToolUsage(
                10,
                20,
                cacheCreationInputTokens: 30,
                cacheReadInputTokens: 40,
                serverToolUse: new AgentServerToolUse(1, 2),
                serviceTier: ServiceTier.Standard,
                cacheCreation: new AgentCacheCreation(5, 6),
                inferenceGeo: "not_available",
                speed: "standard",
                outputTokenDetails: new AgentOutputTokenDetails(7)),
            "find the line",
            agentType: "bee",
            resolvedModel: "claude-haiku",
            modelsUsed: ["claude-haiku"],
            toolStatistics: new AgentToolStatistics(1, 2, 3, 4, 5, 6, 7, 8),
            worktreePath: "/work/tree",
            worktreeBranch: "run/bee",
            harnessNoteCount: 0,
            harnessTailCount: 0,
            harnessSectionHash: "abc123")),
        ("Agent", new AgentAsyncLaunchedToolOutput(
            "agent-2",
            "look it up",
            "find the line",
            "/tasks/agent-2.output",
            isAsync: true,
            resolvedModel: "claude-haiku",
            modelsUsed: ["claude-haiku"],
            canReadOutputFile: true)),
        ("Agent", new AgentRemoteLaunchedToolOutput("task-9", "https://example.com/s/9", "look it up", "find the line", "/tasks/task-9.output")),
        ("Bash", new BashToolOutput(
            "2 notes.txt",
            "a warning",
            interrupted: false,
            rawOutputPath: "/tmp/out",
            isImage: false,
            backgroundTaskId: "bregtd04n",
            backgroundedByUser: true,
            timedOutAfter: TimeSpan.FromMilliseconds(250),
            backgroundWorkingDirectoryHint: "/work",
            backgroundEndsWithFinalResponse: true,
            dangerouslyDisableSandbox: false,
            returnCodeInterpretation: "the command failed",
            noOutputExpected: false,
            persistedOutputPath: "/tmp/kept",
            persistedOutputSize: 4096,
            staleReadFileStateHint: "notes.txt changed",
            gitHubRateLimitHint: "slow down",
            gitOperation: new BashGitOperation(
                new BashGitCommit("0f1e2d3", GitCommitAction.Amended, "main"),
                new BashGitPush("main"),
                new BashGitBranch("refs/heads/main", GitBranchAction.Rebased),
                new BashGitPullRequest(7, GitPullRequestAction.AutoMergeEnabled, "https://example.com/pull/7")))),
        ("TaskOutput", new TaskOutputToolOutput(
            TaskRetrievalStatus.NotReady,
            new TaskOutputToolTask("bregtd04n", "local_bash", "running", "sleep 30", "some output", exitCode: 0))),
        ("TaskStop", new TaskStopToolOutput("stopped it", "bregtd04n", "local_bash", "sleep 30")),
        ("Edit", new EditToolOutput(
            "/draft.txt",
            "alpha",
            "beta",
            [new FilePatchHunk(1, 1, 1, 1, ["-alpha", "+beta"])],
            userModified: true,
            replaceAll: false,
            originalFile: "alpha",
            gitDiff: new FileGitDiff("draft.txt", GitDiffStatus.Modified, 1, 1, 2, "@@", "origin"),
            staged: true)),
        ("Read", new ReadTextToolOutput(
            new ReadTextFile("/notes.txt", "first line\n", 2, 1, 2, truncatedByTokenCap: true),
            new ReadArtifactReference("notes", "3"))),
        ("Read", new ReadImageToolOutput(new ReadImageFile("AAAA", ImageMediaType.Png, 1024, new ReadImageDimensions(10, 20, 5, 10)))),
        ("Read", new ReadNotebookToolOutput(new ReadNotebookFile(
            "/cells.ipynb",
            [new ReadNotebookCell(NotebookCellType.Code, "print(1)", "a1", "csharp"), new ReadNotebookCell(NotebookCellType.Markdown, "# title", null, null)]))),
        ("Read", new ReadPdfToolOutput(new ReadPdfFile("/paper.pdf", "BBBB", 2048))),
        ("Read", new ReadPartsToolOutput(
            new ReadPartsFile("/paper.pdf", 2048, 2, "/tmp/pages"),
            firstPage: 1,
            pages: [new ReadPartsPage("CCCC", ImageMediaType.Jpeg), new ReadPartsPage(string.Empty, ImageMediaType.Jpeg, "the page would not render")])),
        ("Read", new ReadFileUnchangedToolOutput(new ReadUnchangedFile("/notes.txt"), FileUnchangedSource.Seeded)),
        ("Write", new WriteCreateToolOutput(
            "/draft.txt",
            "alpha",
            gitDiff: new FileGitDiff("draft.txt", GitDiffStatus.Added, 1, 0, 1, "@@"),
            userModified: false,
            staged: false)),
        ("Write", new WriteUpdateToolOutput(
            "/draft.txt",
            "beta",
            [new FilePatchHunk(1, 1, 1, 1, ["-alpha", "+beta"])],
            originalFile: "alpha",
            gitDiff: new FileGitDiff("draft.txt", GitDiffStatus.Modified, 1, 1, 2, "@@"),
            userModified: false,
            staged: false)),
        ("Glob", new GlobToolOutput(TimeSpan.FromMilliseconds(3), 1, ["notes.txt"], truncated: false, totalMatchCount: 1, countIsComplete: true)),
        ("Grep", new GrepFilesWithMatchesToolOutput(["notes.txt", "draft.txt"], 2, 3, appliedLimit: 2, appliedOffset: 1)),
        ("Grep", new GrepContentToolOutput("1-first line\n2:second line", 2, 4, appliedLimit: 2, appliedOffset: 1)),
        ("Grep", new GrepCountToolOutput("notes.txt:2", 2, 3, appliedLimit: 1, appliedOffset: 1)),
        ("NotebookEdit", new NotebookEditToolOutput(
            "print(2)",
            NotebookCellType.Markdown,
            "a language",
            NotebookEditMode.Delete,
            "/cells.ipynb",
            "{}",
            "{}",
            oldSource: "print(1)",
            cellId: "a1",
            error: "the cell was gone")),
        ("WebFetch", new WebFetchToolOutput(
            559,
            200,
            "OK",
            "the page title",
            TimeSpan.FromMilliseconds(1702),
            "https://example.com",
            new WebFetchArtifactReference("page", "2", seeded: false))),
        ("WebSearch", new WebSearchToolOutput(
            "the query",
            [new WebSearchHitResult("srvtoolu_1", [new WebSearchLink("a title", "https://example.com/a")]), new WebSearchTextResult("the answer")],
            TimeSpan.FromSeconds(3.8158053),
            searchCount: 1)),
        ("TaskCreate", new TaskCreateToolOutput(new TaskCreateToolTask("task-1", "write the brief"))),
        ("TaskGet", new TaskGetToolOutput(new TaskGetToolTask(
            "task-1",
            "write the brief",
            "the brief the lane builds from",
            TaskItemStatus.InProgress,
            ["task-2"],
            ["task-3"]))),
        ("TaskUpdate", new TaskUpdateToolOutput(
            true,
            "task-1",
            ["status", "owner"],
            error: "the owner is not on this machine",
            statusChange: new TaskUpdateToolStatusChange("pending", "in_progress"))),
        ("TaskList", new TaskListToolOutput(
            [new TaskListToolTask("task-1", "write the brief", TaskItemStatus.Pending, ["task-3"], owner: "the desk")])),
        ("CronCreate", new CronCreateToolOutput("job-1", "every Monday at half past two", true, durable: true)),
        ("CronDelete", new CronDeleteToolOutput("job-1")),
        ("CronList", new CronListToolOutput(
            [new CronListToolJob("job-1", "30 14 * * 1", "every Monday at half past two", "read the mail", recurring: true, durable: false)])),
        ("ScheduleWakeup", new ScheduleWakeupToolOutput(
            DateTimeOffset.FromUnixTimeMilliseconds(1764600000000),
            TimeSpan.FromMinutes(5),
            wasClamped: true,
            stopped: false,
            cancelledWakeupCount: 2)),
        ("Monitor", new MonitorToolOutput("bregtd04n", TimeSpan.FromMinutes(5), persistent: false)),
        ("Workflow", new WorkflowAsyncLaunchedToolOutput(
            "bregtd04n",
            taskType: WorkflowTaskType.LocalWorkflow,
            workflowName: "the lanes",
            summary: "three lanes, one each",
            runId: "run-1",
            transcriptDirectory: "/work/transcripts",
            scriptPath: "/work/lanes.script",
            error: "the script would not parse")),
        ("Workflow", new WorkflowRemoteLaunchedToolOutput(
            "task-9",
            "https://example.com/s/9",
            taskType: WorkflowTaskType.RemoteAgent,
            workflowName: "the lanes",
            summary: "three lanes, one each",
            warning: "the branch here is ahead of the pushed one")),
        ("EnterWorktree", new EnterWorktreeToolOutput("/work/tree", "moved into the worktree", worktreeBranch: "run/lane")),
        ("ExitWorktree", new ExitWorktreeKeepToolOutput(
            "/work",
            "/work/tree",
            "left the worktree where it stands",
            worktreeBranch: "run/lane",
            tmuxSessionName: "lane")),
        ("ExitWorktree", new ExitWorktreeRemoveToolOutput(
            "/work",
            "/work/tree",
            "removed the worktree and its branch",
            worktreeBranch: "run/lane",
            discardedFileCount: 2,
            discardedCommitCount: 1)),
        ("PushNotification", new PushNotificationToolOutput(
            "the build is green",
            pushSent: true,
            localSent: false,
            disabledReason: PushNotificationDisabledReason.UserPresent,
            sentAt: "2026-09-21T09:00:00Z")),
        ("ReportFindings", new ReportFindingsToolOutput(1, [Finding], Effort.Max)),
        ("Artifact", new ArtifactPublishedToolOutput(
            "https://example.com/a/1",
            "/work/page.html",
            artifactId: "artifact-1",
            title: "the page",
            version: "3",
            stored: new ArtifactStoredPage("latest", preferredContract: "latest", carried: true, read: "writer"),
            warnings: ["the title came from the file"],
            publishesRemaining: 4,
            publishesResetAt: 1764600000000,
            contract: "latest",
            updated: true,
            icon: "chart",
            faviconSent: true,
            iconDropped: false,
            audience: "the account",
            sequence: 3,
            unchanged: false,
            liveSubscription: "subscription-1")),
        ("Artifact", new ArtifactListToolOutput(
            [new ArtifactListEntry(
                "the page",
                "https://example.com/a/1",
                favicon: "chart",
                updatedAt: "2026-09-21T09:00:00Z",
                relation: ArtifactRelation.Shared,
                external: true,
                role: ArtifactRole.Commenter)],
            truncated: true,
            scope: ArtifactListScope.All,
            externalListed: true)),
        ("Artifact", new ArtifactReadToolOutput(
            new ArtifactReadResult("https://example.com/a/1", 4096, 200, "OK", "the page itself", TimeSpan.FromMilliseconds(42)),
            new ArtifactVersionReference("the-page", "3", seeded: false))),
        ("Artifact", new ArtifactWatchToolOutput(new ArtifactWatchState(
            "https://example.com/a/1",
            true,
            "armed",
            reason: "the page publishes from elsewhere",
            durableSkipReason: "no trigger is set up",
            taskId: "bregtd04n",
            since: 1764600000000,
            tokenExpiresAt: 1764603600000,
            rail: "live",
            triggerId: "trigger-1",
            durableSince: "2026-09-21T09:00:00Z",
            statusCode: 200,
            detail: "the connection stands",
            note: "this session does not keep track of new versions",
            events: ["published"]))),
        ("Artifact", new ArtifactUnwatchToolOutput(new ArtifactUnwatchState("https://example.com/a/1", true))),
        ("Artifact", new ArtifactStatusToolOutput(
            [
                new ArtifactLiveWatch(
                    "https://example.com/a/1",
                    "bregtd04n",
                    1764600000000,
                    true,
                    true,
                    1764603600000,
                    connecting: false,
                    armedVia: "the watch call"),
                new ArtifactDurableWakeWatch(
                    "https://example.com/a/2",
                    "trigger-1",
                    "2026-09-21T09:00:00Z",
                    events: ["published"],
                    restored: true),
                new ArtifactStoppedWatch(
                    "https://example.com/a/3",
                    "the session asked",
                    since: 1764600000000,
                    isExplicit: true,
                    armedVia: "the watch call"),
            ],
            filterUrl: "https://example.com/a/1",
            arms: [new ArtifactWatchArm(
                "https://example.com/a/1",
                "open",
                rail: "live",
                reconnect: true,
                failureCount: 1,
                maxFailureCount: 5,
                nextIn: TimeSpan.FromSeconds(30),
                lastFailure: "2026-09-21T08:59:00Z",
                reason: "the other end closed",
                detail: "it will be tried again",
                serverMessage: "going away",
                at: 1764600000000)])),
        ("Artifact", new ArtifactAssetUploadToolOutput(
            new ArtifactAssetUpload(
                "0123456789abcdef0123456789abcdef",
                "https://example.com/a/1/logo.png",
                1024,
                "image/png",
                "logo.png",
                sha256: "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08"),
            new ArtifactWriteReference("https://example.com/a/1"))),
        ("Artifact", new ArtifactAssetUploadsToolOutput(
            new ArtifactAssetUploadBatch(
                "https://example.com/a/1",
                [
                    new ArtifactAssetUploadedOutcome(
                        "/work/logo.png",
                        "0123456789abcdef0123456789abcdef",
                        "https://example.com/a/1/logo.png",
                        1024,
                        "image/png",
                        "logo.png",
                        sha256: "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08"),
                    new ArtifactAssetUploadFailedOutcome(
                        "/work/map.png",
                        ArtifactAssetUploadStatus.NotAttempted,
                        "too_large",
                        "the file is past what a store holds",
                        mayBeStored: true),
                ]),
            new ArtifactWriteReference("https://example.com/a/1"))),
        ("Artifact", new ArtifactAssetListToolOutput(new ArtifactAssetListing(
            "https://example.com/a/1",
            [new ArtifactAsset(
                "0123456789abcdef0123456789abcdef",
                "https://example.com/a/1/logo.png",
                "image/png",
                1024,
                "2026-09-21T09:00:00Z",
                sha256: "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08")],
            new ArtifactAssetUsage(1, 1024, 100, 1048576),
            next: "cursor-1",
            cowritten: true,
            outsideWriter: false))),
        ("Artifact", new ArtifactAssetReadToolOutput(new ArtifactAssetRead(
            "0123456789abcdef0123456789abcdef",
            "/work/assets/logo.png",
            1024,
            "image/png",
            "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08",
            cowritten: true,
            outsideWriter: false,
            publicRead: true,
            foreign: false))),
        ("Artifact", new ArtifactAssetDeleteToolOutput(
            new ArtifactAssetDeletion("0123456789abcdef0123456789abcdef", true),
            new ArtifactWriteReference("https://example.com/a/1"))),
        ("RemoteTrigger", new RemoteTriggerToolOutput(200, """{"trigger":{"id":"trig_1","enabled":true}}""", summary: "next run in an hour")),
    ];

    [TestMethod]
    public void EveryToolInputWrittenAndReadBackIsTheSameView()
    {
        foreach (var (tool, input) in Inputs)
        {
            var written = ToolTypes.WriteInput(input);
            var read = ToolTypes.ReadInput(tool, written);
            Assert.IsNotNull(read, $"{tool}: what the writer wrote does not read back as an input at all: {written.GetRawText()}");
            Same(input, read, input.GetType().Name);
            Assert.AreEqual(written.GetRawText(), ToolTypes.WriteInput(read).GetRawText(), tool);
        }
    }

    [TestMethod]
    public void EveryToolOutputWrittenAndReadBackIsTheSameView()
    {
        foreach (var (tool, output) in Outputs)
        {
            var written = ToolTypes.WriteOutput(output);
            var read = ToolTypes.ReadOutput(tool, written);
            Assert.IsNotNull(read, $"{tool}: what the writer wrote does not read back as an output at all: {written.GetRawText()}");
            Same(output, read, output.GetType().Name);
            Assert.AreEqual(written.GetRawText(), ToolTypes.WriteOutput(read).GetRawText(), tool);
        }
    }

    // A member the caller left out stays out: an input built with its required members alone writes
    // only those keys, and reads back as the same view.
    [TestMethod]
    public void AToolInputBuiltWithItsRequiredMembersAloneWritesOnlyThose()
    {
        var bare = new ReadToolInput("/notes.txt");
        var written = ToolTypes.WriteInput(bare);
        Assert.AreEqual("""{"file_path":"/notes.txt"}""", written.GetRawText());
        Same(bare, ToolTypes.ReadInput("Read", written), nameof(ReadToolInput));

        var search = new WebSearchToolInput("the query");
        Assert.AreEqual("""{"query":"the query"}""", ToolTypes.WriteInput(search).GetRawText());
        Assert.AreEqual(0, search.AllowedDomains.Count);
    }

    // The table above covers every concrete tool type the library exports, so one added and left out
    // of it fails here rather than going untested.
    [TestMethod]
    public void EveryConcreteToolTypeIsRoundTripped()
    {
        var exported = typeof(ClaudeAgentClient).Assembly.GetExportedTypes()
            .Where(type => type.IsClass && !type.IsAbstract && (type.IsSubclassOf(typeof(ToolInput)) || type.IsSubclassOf(typeof(ToolOutput))))
            .ToArray();

        CollectionAssert.AreEquivalent(
            exported,
            Inputs.Select(one => one.Input.GetType()).Concat(Outputs.Select(one => one.Output.GetType())).Distinct().ToArray());
    }

    // A caller builds each through one public constructor whose required members are required
    // parameters, and reads it back through properties that have no setter.
    [TestMethod]
    public void EveryConcreteToolTypeIsBuiltThroughOnePublicConstructorWithNoSetter()
    {
        foreach (var built in Inputs.Select(one => (object)one.Input).Concat(Outputs.Select(one => (object)one.Output)))
        {
            var type = built.GetType();
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            Assert.AreEqual(1, constructors.Length, type.Name);
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.IsNull(property.GetSetMethod(nonPublic: false), $"{type.Name}.{property.Name}");
            }
        }
    }

    // The other half of that claim, which counting constructors cannot see: a member the type says
    // is always there is a parameter a caller must pass. Give them all defaults and every round trip
    // above still passes, since it passes its values positionally — so the optionality is read here,
    // off the parameter, against what the member says about itself.
    [TestMethod]
    public void EveryToolTypesRequiredMembersAreRequiredParameters()
    {
        var nullability = new NullabilityInfoContext();
        foreach (var built in Inputs.Select(one => (object)one.Input).Concat(Outputs.Select(one => (object)one.Output)))
        {
            var type = built.GetType();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var parameter in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)[0].GetParameters())
            {
                Assert.IsTrue(
                    properties.TryGetValue(parameter.Name!, out var property),
                    $"{type.Name}'s parameter {parameter.Name} is no member of it");

                // A collection says absence by being empty, so a list member left out is no member
                // left unset; anything else non-nullable is a member the type says is always there.
                var required = Nullable.GetUnderlyingType(property.PropertyType) is null
                    && nullability.Create(property).ReadState != NullabilityState.Nullable
                    && (property.PropertyType == typeof(string) || !typeof(System.Collections.IEnumerable).IsAssignableFrom(property.PropertyType));
                if (required)
                {
                    Assert.IsFalse(
                        parameter.HasDefaultValue,
                        $"{type.Name}.{property.Name} is always there and {parameter.Name} may be left out");
                }
            }
        }
    }

    // A view of a tool this library types, written by the wrong tool's name, reads back as null: the
    // writer is not what decides the shape, the name is.
    [TestMethod]
    public void AToolsOwnJsonReadUnderAnotherToolsNameIsNull()
    {
        var written = ToolTypes.WriteInput(new GlobToolInput("*.txt"));
        Assert.IsNull(ToolTypes.ReadInput("Read", written));
        Assert.IsNull(ToolTypes.ReadInput("DesignSync", written));
    }

    private static void Same(object? expected, object? actual, string where)
    {
        if (expected is null || actual is null)
        {
            Assert.AreEqual(expected, actual, where);
            return;
        }

        // A body kept raw is the same view when it is the same JSON; two copies of it are two elements.
        if (expected is JsonElement json)
        {
            Assert.IsTrue(JsonElement.DeepEquals(json, (JsonElement)actual), $"{where}: {json.GetRawText()} read back as {((JsonElement)actual).GetRawText()}");
            return;
        }

        var type = expected.GetType();
        if (type.IsValueType || expected is string)
        {
            Assert.AreEqual(expected, actual, where);
            return;
        }

        // A list built by a collection expression and a list read off the wire are the same view when
        // they carry the same items in the same order; the concrete list type is not the claim.
        if (expected is IEnumerable left && actual is IEnumerable right)
        {
            var one = left.Cast<object?>().ToArray();
            var other = right.Cast<object?>().ToArray();
            Assert.AreEqual(one.Length, other.Length, $"{where}.Count");
            for (var index = 0; index < one.Length; index++)
            {
                Same(one[index], other[index], $"{where}[{index}]");
            }

            return;
        }

        Assert.AreEqual(type, actual.GetType(), where);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        if (properties.Length == 0)
        {
            // A tool whose input the wire declares as an empty object has no members: that the view
            // read back is of the same type is the whole of the claim there is to make.
            return;
        }

        foreach (var property in properties)
        {
            Same(property.GetValue(expected), property.GetValue(actual), $"{where}.{property.Name}");
        }
    }
}
