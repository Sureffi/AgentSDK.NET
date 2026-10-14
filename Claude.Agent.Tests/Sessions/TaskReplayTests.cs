using System.Collections.Immutable;
using System.Text.Json;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Sessions;

[TestClass]
public sealed class TaskReplayTests
{
    private const string RecordedCwd = "/tmp/claude-agent-fixtures/cwd";

    private const string ShellPrompt =
        "Use the Bash tool with run_in_background set to true to run: sleep 300. "
        + "Then reply with exactly the word: started";

    private const string SubagentPrompt =
        "Use the Agent tool to launch one subagent whose only instruction is: reply with the single word pong. "
        + "Then reply with exactly the subagent's answer.";

    private const string BackgroundSubagentPrompt =
        "Use the Agent tool with run_in_background set to true to launch one subagent whose instruction is: "
        + "read notes.txt in the working directory three separate times with the Read tool, then reply pong. "
        + "Do not wait for it; reply immediately with exactly the word: started";

    private const string TaskSessionId = "feb85f4e-e6fe-4a9f-b7af-21606a78449b";
    private const string ShellTaskId = "b35prjrf0";
    private const string ShellToolUseId = "toolu_01JK3sYzgcM4Bd9d8ZwS2C37";
    private const string ShellSummary = "sleep 300";

    private const string SubagentTaskId = "a8972c652a55930c4";
    private const string SubagentToolUseId = "toolu_019E4aEu6Q3Hqr975Chi93wh";

    private const string BackgroundTaskId = "aa0a98fb68b8b40e7";
    private const string BackgroundToolUseId = "toolu_018e5iQgqf9mRxpwwdbzNWTV";
    private const string BackgroundSessionId = "8e86c9b1-623c-42a6-ae50-cf8cce30a3de";

    private const string TaskDirectory = "/tmp/claude-1000/-tmp-claude-agent-fixtures-cwd";

    private const string TaskToolsPrompt =
        "Do these steps in order, one tool call each, without explaining: 1. Run the shell command `sleep 30` with Bash "
        + "in the background. 2. Call TaskOutput on the task it started, without blocking, with a timeout of 1000. "
        + "3. Stop that task with TaskStop. Then reply with exactly the word: done";

    private const string TaskToolsSessionId = "fa29a6fb-cdc8-4c78-ac59-2be04a219c61";
    private const string TaskToolsTaskId = "bregtd04n";
    private const string TaskToolsBashToolUseId = "toolu_01SePTovcAEu7ohLxAo98U3H";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    // Longer than a stdin closed at a result takes to reach the fixture server by far.
    private static readonly TimeSpan StdinWatch = TimeSpan.FromSeconds(1);

    [TestMethod]
    public async Task TheBashToolRunsInTheBackgroundAndTheSessionGoesIdleOnTheResultWithoutEverTrackingTheShellTask()
    {
        var replay = await ReplayTaskAsync();

        // The prompt's stretch, from its started frame; the rate limit event the binary wrote
        // between the prompt's queued and started frames is not the run's.
        CollectionAssert.AreEqual(
            new[]
            {
                typeof(CommandLifecycleMessage),
                typeof(CommandLifecycleMessage),
                typeof(SystemInitMessage),
                typeof(ThinkingTokensMessage),
                typeof(ThinkingTokensMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(BackgroundTasksChangedMessage),
                typeof(LocalBashTaskStartedMessage),
                typeof(UserMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(CommandLifecycleMessage),
                typeof(ResultMessage),
            },
            replay.ShellTurn.Written().Select(message => message.GetType()).ToArray());
        Assert.IsInstanceOfType<RateLimitMessage>(replay.BeforeShellTurn);

        var started = replay.ShellTurn.OfType<LocalBashTaskStartedMessage>().Single();
        Assert.AreEqual(ShellTaskId, started.TaskId);
        Assert.AreEqual(ShellToolUseId, started.ToolUseId);
        Assert.AreEqual(TaskType.LocalBash, started.TaskType);
        Assert.AreEqual(ShellSummary, started.Description);
        Assert.IsTrue(started.IsBackgrounded is true);

        var announced = AnnouncedTasks(replay.ShellTurn).Single();
        Assert.AreEqual(ShellTaskId, announced.TaskId);
        Assert.AreEqual(TaskType.LocalBash, announced.TaskType);
        Assert.AreEqual(ShellSummary, announced.Description);

        Assert.IsTrue(replay.InFlightAfterShellTurn.IsEmpty, "a local_bash task was held in flight.");
        Assert.AreEqual("started", replay.ShellTurn.OfType<ResultMessage>().Single().Result);
    }

    // The frames of the killed task are played once the stop_task is written, as they follow it,
    // and reach what no run reads.
    [TestMethod]
    public async Task StopTaskOnTheRunningShellTaskIsAnsweredAndTheBinaryKillsItAndReportsItStopped()
    {
        var replay = await ReplayTaskAsync();

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(BackgroundTasksChangedMessage),
                typeof(TaskUpdatedMessage),
                typeof(TaskNotificationMessage),
            },
            replay.AfterStop.Select(message => message.GetType()).ToArray());

        Assert.AreEqual(0, AnnouncedTasks(replay.AfterStop).Count);

        var updated = replay.AfterStop.OfType<TaskUpdatedMessage>().Single();
        Assert.AreEqual(ShellTaskId, updated.TaskId);
        Assert.AreEqual(BackgroundTaskStatus.Killed, updated.Patch.Status);
        Assert.IsTrue(updated.Patch.IsTerminal);

        var notification = replay.AfterStop.OfType<TaskNotificationMessage>().Single();
        Assert.AreEqual(ShellTaskId, notification.TaskId);
        Assert.AreEqual(ShellToolUseId, notification.ToolUseId);
        Assert.AreEqual(TaskNotificationStatus.Stopped, notification.Status);
        Assert.AreEqual($"{TaskDirectory}/{TaskSessionId}/tasks/{ShellTaskId}.output", notification.OutputFile);
        Assert.AreEqual(ShellSummary, notification.Summary);
        Assert.IsNull(notification.TotalTokens);
        Assert.IsNull(notification.Duration);
    }

    [TestMethod]
    public async Task TheAgentToolRunsASubagentInTheForegroundWhichTheSessionHoldsInFlightUntilItsTerminalFrame()
    {
        var replay = await ReplayTaskAsync();

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(CommandLifecycleMessage),
                typeof(CommandLifecycleMessage),
                typeof(SystemInitMessage),
                typeof(ThinkingTokensMessage),
                typeof(ThinkingTokensMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(LocalAgentTaskStartedMessage),
                typeof(UserMessage),
                typeof(TaskUpdatedMessage),
                typeof(TaskNotificationMessage),
                typeof(UserMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(CommandLifecycleMessage),
                typeof(ResultMessage),
            },
            replay.SubagentTurn.Written().Select(message => message.GetType()).ToArray());

        var started = replay.SubagentTurn.OfType<LocalAgentTaskStartedMessage>().Single();
        Assert.AreEqual(SubagentTaskId, started.TaskId);
        Assert.AreEqual(SubagentToolUseId, started.ToolUseId);
        Assert.AreEqual(TaskType.LocalAgent, started.TaskType);
        Assert.IsTrue(started.IsBackgrounded is false);
        Assert.AreEqual("general-purpose", started.SubagentType);
        Assert.AreEqual(1L, started.SpawnDepth);
        Assert.AreEqual("Reply with the single word: pong", started.Prompt);

        var updated = replay.SubagentTurn.OfType<TaskUpdatedMessage>().Single();
        Assert.AreEqual(SubagentTaskId, updated.TaskId);
        Assert.AreEqual(BackgroundTaskStatus.Completed, updated.Patch.Status);

        var notification = replay.SubagentTurn.OfType<TaskNotificationMessage>().Single();
        Assert.AreEqual(TaskNotificationStatus.Completed, notification.Status);
        Assert.AreEqual("pong", notification.Summary);
        Assert.AreEqual(8869L, notification.TotalTokens);
        Assert.AreEqual(0L, notification.ToolUseCount);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1084), notification.Duration);

        Assert.AreEqual("pong", replay.SubagentTurn.OfType<ResultMessage>().Single().Result);
        Assert.IsTrue(replay.InFlightAtEnd.IsEmpty);
    }

    [TestMethod]
    public async Task TheTaskFixtureWritesTheInitializeThePromptTheStopTaskAndTheSecondPromptAndNothingElse()
    {
        var replay = await ReplayTaskAsync();

        CollectionAssert.AreEqual(
            Fixture.Load("task").Stdin.Anonymised(),
            replay.Written.Anonymised());
    }

    [TestMethod]
    public async Task TheResultOfTheTurnArrivesWhileTheBackgroundSubagentRunsSoTheTaskStaysInFlight()
    {
        var replay = await ReplayBackgroundAsync();

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(CommandLifecycleMessage),
                typeof(CommandLifecycleMessage),
                typeof(SystemInitMessage),
                typeof(RateLimitMessage),
                typeof(ThinkingTokensMessage),
                typeof(ThinkingTokensMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(BackgroundTasksChangedMessage),
                typeof(LocalAgentTaskStartedMessage),
                typeof(UserMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(CommandLifecycleMessage),
                typeof(ResultMessage),
            },
            replay.FirstTurn.Written().Select(message => message.GetType()).ToArray());

        var started = replay.FirstTurn.OfType<LocalAgentTaskStartedMessage>().Single();
        Assert.AreEqual(BackgroundTaskId, started.TaskId);
        Assert.AreEqual(BackgroundToolUseId, started.ToolUseId);
        Assert.AreEqual(TaskType.LocalAgent, started.TaskType);
        Assert.IsTrue(started.IsBackgrounded is true);

        var announced = AnnouncedTasks(replay.FirstTurn).Single();
        Assert.AreEqual(BackgroundTaskId, announced.TaskId);
        Assert.AreEqual(TaskType.LocalAgent, announced.TaskType);
        Assert.AreEqual("Read notes.txt three times and reply pong", announced.Description);

        Assert.AreEqual("started", replay.FirstTurn.OfType<ResultMessage>().Single().Result);
        Assert.IsTrue(replay.InFlightAfterFirstTurn.Contains(BackgroundTaskId));
    }

    // Findings run:7 and tests:2. A query keeps stdin open until the result that ends its run: not
    // the first, since the subagent is still out, but the continuation's. The run is both turns: a
    // query's prompt carries no uuid, so its run takes every frame, the lifecycle frames the
    // recording has for the session's prompt among them. stdinDiffers: the recording is a session's,
    // and a query writes its prompt in the one-shot form.
    //
    // The play is held with every line but that last result out — the first result,
    // the subagent's frames and the continuation turn's init all past — and stdin is watched there,
    // for longer than a close at any of them would take to reach the server. A close anywhere in
    // that stretch has reached the server by then, so the watch says the whole stretch and not the
    // second after the first result alone. Where the consumer had read to says nothing: it reads
    // behind the read loop.
    [TestMethod]
    public async Task AQueryHoldsStdinOpenPastTheFirstResultWhileTheBackgroundSubagentRunsAndClosesItAtTheLast()
    {
        // pauseAfterLine counts the recording's stdout lines from one, so the last result's own
        // number less one is the line before it.
        var lastResult = Fixture.Load("task-background").Stdout
            .Index()
            .Last(each => Fixture.TypeOf(each.Item) == "result")
            .Index;

        await using var server = FixtureServer.Start(
            "task-background",
            endAfterLastLine: false,
            stdinDiffers: true,
            pauseAfterLine: lastResult);
        using var host = TestHost.Start(server, options => Recording(options, "Agent", "Read"));

        var seen = new List<AgentMessage>();
        var reading = Task.Run(async () =>
        {
            await foreach (var message in host.Client.QueryAsync(BackgroundSubagentPrompt))
            {
                seen.Add(message);
            }
        });

        await server.Paused.WaitAsync(Patience);
        var ended = await Task.WhenAny(server.StdinEnd, Task.Delay(StdinWatch)) == server.StdinEnd;
        Assert.IsFalse(ended, "stdin closed before the result that ends the query's run.");
        server.Resume();

        await reading.WaitAsync(Patience);

        Assert.IsTrue(server.StdinEnded, "stdin was open when the query's completion was there.");

        var written = seen.Written();
        Assert.AreEqual(15 + 22, written.Count);
        Assert.AreEqual(2, written.OfType<ResultMessage>().Count());
        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
    }

    // A result line the library cannot type, written while the background subagent is out, is not
    // the line that ends the query's run: it is handed out as the unknown of the message set and the
    // run goes on to the result that does end it, the whole continuation turn among its trace.
    [TestMethod]
    public async Task AQuerysMisfitResultWrittenWhileItsBackgroundTaskIsOutIsTheUnknownAndTheRunEndsAtTheResultThatEndsIt()
    {
        var background = Fixture.Load("task-background");
        var script = new Script(
        [
            Script.AnswerInitialize,
            new AwaitPrompt(1),
            .. background.Stdout[1..14].Select(line => new EmitLine(line)),
            new EmitLine(Fixture.WithDurationOutOfRange(background.Stdout[14])),
            .. background.Stdout[15..].Select(line => new EmitLine(line)),
        ])
        {
            Prompts = Fixture.PromptUuids(background.Stdin),
            AwaitStdinEnd = true,
        };

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server, options => Recording(options, "Agent", "Read"));

        var trace = await host.Client.QueryAsync(BackgroundSubagentPrompt).WaitForCompletionAsync().WaitAsync(Patience);

        Assert.AreEqual("success", trace.Result.Subtype);
        Assert.AreEqual(1, trace.Messages.Count(message => message is UnknownMessage { Type: FrameTypes.Result }));
        Assert.AreEqual(1, trace.Messages.OfType<ResultMessage>().Count());
        Assert.IsTrue(server.StdinEnded, "stdin was open when the query's completion was there.");
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
    }

    // Finding pairing:1, its second trigger. A query's run ends at the result written with no
    // background task in flight; a first result written while the subagent is out is not that one.
    // Output that ends there, the process gone with the task still in flight, fails the run for the
    // result that never came, whether or not the subagent had a call open before that first result.
    // Either way the result the binary did send is last in the failure, so
    // ClaudeAgentException.Result is it, and a call closed unanswered comes before it.
    [TestMethod]
    public async Task AQueryWhoseOutputEndsWhileItsBackgroundTaskIsInFlightFailsForTheResultThatNeverCameWithOrWithoutAnOpenCall()
    {
        var background = Fixture.Load("task-background");
        ScriptStep[][] endings =
        [
            [.. background.Stdout[1..15].Select(line => new EmitLine(line))],
            [.. background.Stdout[1..14].Select(line => new EmitLine(line)), new EmitLine(background.Stdout[18]), new EmitLine(background.Stdout[14])],
        ];

        for (var pass = 0; pass < endings.Length; pass++)
        {
            var script = new Script([Script.AnswerInitialize, new AwaitPrompt(1), .. endings[pass], new HangUp()])
            {
                Prompts = Fixture.PromptUuids(background.Stdin),
            };

            await using var server = FixtureServer.Play(script);
            using var host = TestHost.Start(server, options => Recording(options, "Agent", "Read"));

            var failure = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(
                () => host.Client.QueryAsync(BackgroundSubagentPrompt).WaitForCompletionAsync().WaitAsync(Patience));

            Assert.AreEqual(ErrorCodes.Process, failure.ErrorCode);
            StringAssert.Contains(failure.Message, "ended before the run's result");
            Assert.AreEqual(1, failure.Messages.OfType<ResultMessage>().Count());
            Assert.IsInstanceOfType<ResultMessage>(failure.Messages[^1], $"pass {pass + 1}: the result the binary sent is not last.");
            Assert.IsNotNull(failure.Result, $"pass {pass + 1}: the failure carries no result.");

            var unanswered = failure.Messages
                .Index()
                .Where(each => each.Item is ToolCallMessage { ToolCall.Status: ToolCallStatus.Unanswered })
                .Select(each => each.Index)
                .ToArray();
            Assert.AreEqual(pass, unanswered.Length, $"pass {pass + 1}: the calls closed unanswered.");
            Assert.IsTrue(
                unanswered.All(at => at < failure.Messages.Count - 1),
                $"pass {pass + 1}: an unanswered close came after the result.");
        }
    }

    [TestMethod]
    public async Task TheBackgroundSubagentReadsTheFileThreeTimesAndEachReadComesBackAsTaskProgress()
    {
        var replay = await ReplayBackgroundAsync();

        var progress = replay.Continuation.OfType<TaskProgressMessage>().ToArray();
        Assert.AreEqual(3, progress.Length);
        CollectionAssert.AreEqual(new long?[] { 1, 2, 3 }, progress.Select(frame => frame.ToolUseCount).ToArray());
        CollectionAssert.AreEqual(
            new long?[] { 6499, 6502, 6505 },
            progress.Select(frame => frame.TotalTokens).ToArray());

        foreach (var frame in progress)
        {
            Assert.AreEqual(BackgroundTaskId, frame.TaskId);
            Assert.AreEqual(BackgroundToolUseId, frame.ToolUseId);
            Assert.AreEqual("Read", frame.LastToolName);
            Assert.AreEqual("Reading notes.txt", frame.Description);
        }

        var updated = replay.Continuation.OfType<TaskUpdatedMessage>().Single();
        Assert.AreEqual(BackgroundTaskId, updated.TaskId);
        Assert.AreEqual(BackgroundTaskStatus.Completed, updated.Patch.Status);

        var notification = replay.Continuation.OfType<TaskNotificationMessage>().Single();
        Assert.AreEqual(TaskNotificationStatus.Completed, notification.Status);
        Assert.AreEqual($"{TaskDirectory}/{BackgroundSessionId}/tasks/{BackgroundTaskId}.output", notification.OutputFile);
        Assert.AreEqual("pong", notification.Summary);
        Assert.AreEqual(8221L, notification.TotalTokens);
        Assert.AreEqual(3L, notification.ToolUseCount);
        Assert.AreEqual(TimeSpan.FromMilliseconds(4475), notification.Duration);
    }

    [TestMethod]
    public async Task TheBinaryStartsAContinuationTurnOfItsOwnOnceTheSubagentEndsAndTheSessionSettlesOnItsResult()
    {
        var replay = await ReplayBackgroundAsync();

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(TaskProgressMessage),
                typeof(UserMessage),
                typeof(AssistantMessage),
                typeof(TaskProgressMessage),
                typeof(UserMessage),
                typeof(AssistantMessage),
                typeof(TaskProgressMessage),
                typeof(UserMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(BackgroundTasksChangedMessage),
                typeof(TaskUpdatedMessage),
                typeof(TaskNotificationMessage),
                typeof(SystemInitMessage),
                typeof(ThinkingTokensMessage),
                typeof(ThinkingTokensMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(ResultMessage),
            },
            replay.Continuation.Written().Select(message => message.GetType()).ToArray());

        var continued = replay.Continuation.OfType<SystemInitMessage>().Single();
        Assert.AreEqual(BackgroundSessionId, continued.SessionId);
        Assert.AreEqual(0, AnnouncedTasks(replay.Continuation).Count);

        var result = (ResultMessage)replay.Continuation[^1];
        Assert.AreEqual(1, result.TurnCount);
        Assert.AreEqual(
            "The background agent completed. It read notes.txt three separate times and replied: **pong**",
            result.Result);
        Assert.IsTrue(replay.InFlightAtEnd.IsEmpty);
    }

    // The Agent call is the first turn's; the subagent's three reads arrive after that turn's result,
    // where no run is waiting, and are paired there all the same, each under the Agent call.
    [TestMethod]
    public async Task TheBackgroundSubagentsReadsArePairedAsCallsUnderTheAgentCallInWhatNoRunReads()
    {
        var replay = await ReplayBackgroundAsync();

        var agent = replay.FirstTurn.OfType<ToolCallMessage>().Select(message => message.ToolCall).ToArray();
        CollectionAssert.AreEqual(
            new[] { ToolCallStatus.Started, ToolCallStatus.Answered },
            agent.Select(call => call.Status).ToArray());
        Assert.IsTrue(agent.All(call => call.Id == BackgroundToolUseId && call.Name == "Agent" && call.ParentToolUseId is null));

        var reads = replay.Continuation.OfType<ToolCallMessage>().Select(message => message.ToolCall).ToArray();
        Assert.AreEqual(6, reads.Length);
        Assert.IsTrue(reads.All(call => call.Name == "Read" && call.ParentToolUseId == BackgroundToolUseId));
        foreach (var pair in reads.Chunk(2))
        {
            Assert.AreEqual(pair[0].Id, pair[1].Id);
            Assert.AreEqual(ToolCallStatus.Started, pair[0].Status);
            Assert.AreEqual(ToolCallStatus.Answered, pair[1].Status);
            Assert.AreEqual($"{RecordedCwd}/notes.txt", pair[1].RawInput.GetProperty("file_path").GetString());
        }
    }

    [TestMethod]
    public async Task TheTaskBackgroundFixtureWritesTheInitializeAndTheOnePromptAndNothingElse()
    {
        var replay = await ReplayBackgroundAsync();

        CollectionAssert.AreEqual(
            Fixture.Load("task-background").Stdin.Anonymised(),
            replay.Written.Anonymised());
    }

    // The task tools on a background shell, in one turn: Bash starts it, TaskOutput reads it without
    // waiting and finds it running, TaskStop ends it.
    [TestMethod]
    public async Task TheBashToolStartsAShellInTheBackgroundAndTaskOutputReadsItStillRunning()
    {
        var (seen, _) = await ReplayTaskToolsAsync();

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(RateLimitMessage),
                typeof(SystemInitMessage),
                typeof(ThinkingTokensMessage),
                typeof(ThinkingTokensMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(BackgroundTasksChangedMessage),
                typeof(LocalBashTaskStartedMessage),
                typeof(UserMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(UserMessage),
                typeof(RateLimitMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(BackgroundTasksChangedMessage),
                typeof(TaskUpdatedMessage),
                typeof(TaskNotificationMessage),
                typeof(UserMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(ResultMessage),
            },
            seen.Written().Select(message => message.GetType()).ToArray());

        var calls = seen.OfType<AssistantMessage>().SelectMany(message => message.ToolUses).ToArray();
        CollectionAssert.AreEqual(new[] { "Bash", "TaskOutput", "TaskStop" }, calls.Select(call => call.Name).ToArray());
        Assert.AreEqual(TaskToolsBashToolUseId, calls[0].Id);
        Assert.AreEqual("sleep 30", calls[0].RawInput.GetProperty("command").GetString());
        Assert.IsTrue(calls[0].RawInput.GetProperty("run_in_background").GetBoolean());
        Assert.AreEqual(TaskToolsTaskId, calls[1].RawInput.GetProperty("task_id").GetString());
        Assert.IsFalse(calls[1].RawInput.GetProperty("block").GetBoolean());
        Assert.AreEqual(1000, calls[1].RawInput.GetProperty("timeout").GetInt32());
        Assert.AreEqual(TaskToolsTaskId, calls[2].RawInput.GetProperty("task_id").GetString());

        var started = seen.OfType<LocalBashTaskStartedMessage>().Single();
        Assert.AreEqual(TaskToolsTaskId, started.TaskId);
        Assert.AreEqual(TaskToolsBashToolUseId, started.ToolUseId);
        Assert.AreEqual(TaskType.LocalBash, started.TaskType);
        Assert.AreEqual("sleep 30", started.Description);
        Assert.IsTrue(started.IsBackgrounded is true);

        var announced = seen.OfType<BackgroundTasksChangedMessage>().First().Tasks.Single();
        Assert.AreEqual(TaskToolsTaskId, announced.TaskId);
        Assert.AreEqual(TaskType.LocalBash, announced.TaskType);

        var output = ToolUseResultOf(seen, calls[1].Id);
        Assert.AreEqual("not_ready", output.GetProperty("retrieval_status").GetString());
        Assert.AreEqual("running", output.GetProperty("task").GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task TaskStopKillsTheShellAndTheBinaryReportsItStoppedBeforeTheToolResult()
    {
        var (seen, _) = await ReplayTaskToolsAsync();

        var stop = seen.OfType<AssistantMessage>().SelectMany(message => message.ToolUses).Single(call => call.Name == "TaskStop");
        var afterStop = seen.Written()
            .SkipWhile(message => message is not AssistantMessage assistant || !assistant.ToolUses.Contains(stop))
            .Skip(1)
            .Take(4)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(BackgroundTasksChangedMessage),
                typeof(TaskUpdatedMessage),
                typeof(TaskNotificationMessage),
                typeof(UserMessage),
            },
            afterStop.Select(message => message.GetType()).ToArray());

        var changed = (BackgroundTasksChangedMessage)afterStop[0];
        Assert.AreEqual(0, changed.Tasks.Count);

        var updated = (TaskUpdatedMessage)afterStop[1];
        Assert.AreEqual(TaskToolsTaskId, updated.TaskId);
        Assert.AreEqual(BackgroundTaskStatus.Killed, updated.Patch.Status);

        var notification = (TaskNotificationMessage)afterStop[2];
        Assert.AreEqual(TaskToolsTaskId, notification.TaskId);
        Assert.AreEqual(TaskToolsBashToolUseId, notification.ToolUseId);
        Assert.AreEqual(TaskNotificationStatus.Stopped, notification.Status);
        Assert.AreEqual($"{TaskDirectory}/{TaskToolsSessionId}/tasks/{TaskToolsTaskId}.output", notification.OutputFile);
        Assert.AreEqual("sleep 30", notification.Summary);

        Assert.AreEqual(stop.Id, ((ToolResultBlock)((UserMessage)afterStop[3]).Content.Single()).ToolUseId);
        Assert.AreEqual(
            "Successfully stopped task: bregtd04n (sleep 30)",
            ToolUseResultOf(seen, stop.Id).GetProperty("message").GetString());

        var result = (ResultMessage)seen[^1];
        Assert.AreEqual("done", result.Result);
        Assert.AreEqual(4, result.TurnCount);
    }

    [TestMethod]
    public async Task TheTaskToolsFixtureWritesTheInitializeAndTheOnePromptAndNothingElse()
    {
        var (_, written) = await ReplayTaskToolsAsync();

        CollectionAssert.AreEqual(
            Fixture.Load("task-tools").Stdin.Anonymised(),
            written.Anonymised());
    }

    private static async Task<(ImmutableArray<AgentMessage> Seen, ImmutableArray<string> Written)> ReplayTaskToolsAsync()
    {
        await using var server = FixtureServer.Start("task-tools");
        using var host = TestHost.Start(server, options: options =>
        {
            Recording(options, "Bash", "TaskOutput", "TaskStop");
            options.Model = "haiku";
            options.MaxTurns = 6;
        });

        var seen = await DrainAsync(host.Client.QueryAsync(TaskToolsPrompt));

        await server.WaitForWrites(2, Patience);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        return (seen, server.Written);
    }

    private static JsonElement ToolUseResultOf(ImmutableArray<AgentMessage> seen, string toolUseId) =>
        seen.OfType<UserMessage>()
            .Single(message => message.Content.OfType<ToolResultBlock>().Any(block => block.ToolUseId == toolUseId))
            .RawToolOutput!.Value;

    private static async Task<TaskReplay> ReplayTaskAsync()
    {
        await using var server = FixtureServer.Start("task");
        using var host = TestHost.Start(server, options: options => Recording(options, "Bash", "Agent"));
        await using var session = await host.Client.StartSessionAsync();

        var shellTurn = await DrainAsync(session.SendAsync(ShellPrompt));
        var inFlightAfterShellTurn = session.Runtime.InFlightTasks;

        await session.StopTaskAsync(ShellTaskId);
        var unclaimed = await TakeAsync(session.ReceiveMessagesAsync(), 4);

        var subagentTurn = await DrainAsync(session.SendAsync(SubagentPrompt));

        await server.WaitForWrites(4, Patience);
        AssertSettled(server, session);

        return new TaskReplay(
            unclaimed[0],
            shellTurn,
            inFlightAfterShellTurn,
            unclaimed[1..],
            subagentTurn,
            server.Written,
            session.Runtime.InFlightTasks);
    }

    // The play pauses after the first result, so what the session holds at that result is looked at
    // before the continuation the recording goes on with can reach it.
    private static async Task<BackgroundReplay> ReplayBackgroundAsync()
    {
        await using var server = FixtureServer.Start("task-background", pauseAfterResult: 1);
        using var host = TestHost.Start(server, options: options => Recording(options, "Agent", "Read"));
        await using var session = await host.Client.StartSessionAsync();

        var firstTurn = await DrainAsync(session.SendAsync(BackgroundSubagentPrompt));
        await server.Paused.WaitAsync(Patience);
        var inFlightAfterFirstTurn = session.Runtime.InFlightTasks;
        server.Resume();

        var continuation = await TakeWrittenAsync(session.ReceiveMessagesAsync(), 22);

        await server.WaitForWrites(2, Patience);
        AssertSettled(server, session);

        return new BackgroundReplay(
            firstTurn,
            inFlightAfterFirstTurn,
            continuation,
            server.Written,
            session.Runtime.InFlightTasks);
    }

    private static void AssertSettled(FixtureServer server, ClaudeAgentSession session)
    {
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        Assert.AreEqual(
            0,
            session.Runtime.RejectedFrames.Count,
            $"the parser rejected: {string.Join("; ", session.Runtime.RejectedFrames.Select(error => error.Message))}");
    }

    private static void Recording(ClaudeAgentSessionOptions options, params string[] tools)
    {
        options.WorkingDirectory = RecordedCwd;
        options.SettingSources = [];
        options.Model = "haiku";
        options.MaxTurns = 3;
        options.Effort = Effort.Low;
        options.Tools = [.. tools];

        foreach (var tool in tools)
        {
            options.AllowedTools.Add(tool);
        }
    }

    private static IReadOnlyList<BackgroundTask> AnnouncedTasks(ImmutableArray<AgentMessage> turn) =>
        turn.OfType<BackgroundTasksChangedMessage>().Single().Tasks;

    private static async Task<ImmutableArray<AgentMessage>> DrainAsync(IAsyncEnumerable<AgentMessage> messages)
    {
        var seen = ImmutableArray.CreateBuilder<AgentMessage>();
        await foreach (var message in messages)
        {
            seen.Add(message);
        }

        return seen.ToImmutable();
    }

    private static async Task<ImmutableArray<AgentMessage>> TakeAsync(IAsyncEnumerable<AgentMessage> messages, int count)
    {
        var seen = ImmutableArray.CreateBuilder<AgentMessage>();
        await foreach (var message in messages)
        {
            seen.Add(message);
            if (seen.Count == count)
            {
                break;
            }
        }

        Assert.AreEqual(count, seen.Count, "the binary stopped sending before the fixture ran out.");
        return seen.ToImmutable();
    }

    // Takes until `count` of the messages the binary wrote have arrived, keeping the tool-call
    // snapshots among them.
    private static async Task<ImmutableArray<AgentMessage>> TakeWrittenAsync(IAsyncEnumerable<AgentMessage> messages, int count)
    {
        var seen = ImmutableArray.CreateBuilder<AgentMessage>();
        var written = 0;
        await foreach (var message in messages)
        {
            seen.Add(message);
            if (message is not ToolCallMessage && ++written == count)
            {
                break;
            }
        }

        Assert.AreEqual(count, written, "the binary stopped sending before the fixture ran out.");
        return seen.ToImmutable();
    }

    private sealed record TaskReplay(
        AgentMessage BeforeShellTurn,
        ImmutableArray<AgentMessage> ShellTurn,
        ImmutableHashSet<string> InFlightAfterShellTurn,
        ImmutableArray<AgentMessage> AfterStop,
        ImmutableArray<AgentMessage> SubagentTurn,
        ImmutableArray<string> Written,
        ImmutableHashSet<string> InFlightAtEnd);

    private sealed record BackgroundReplay(
        ImmutableArray<AgentMessage> FirstTurn,
        ImmutableHashSet<string> InFlightAfterFirstTurn,
        ImmutableArray<AgentMessage> Continuation,
        ImmutableArray<string> Written,
        ImmutableHashSet<string> InFlightAtEnd);
}
