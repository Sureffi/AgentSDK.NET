using System.Collections.Immutable;
using System.Text.Json;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Sessions;

[TestClass]
public sealed class HookEventReplayTests
{
    private const string RecordedCwd = "/tmp/claude-agent-fixtures/cwd";

    private const string HookEventsPrompt =
        "Read notes.txt in the working directory and reply with its first line only.";

    private const string HookEventsToolUseId = "toolu_01CvD7r5bTpVYRDAnzp4P1dz";

    // The settings the recording handed the binary, verbatim: two command hooks beside the SDK's own.
    private const string CommandHookSettings =
        """{"hooks": {"PreToolUse": [{"matcher": "Read", "hooks": [{"type": "command", "command": "exit 0"}]}], "PostToolUse": [{"matcher": "Read", "hooks": [{"type": "command", "command": "exit 0"}]}]}}""";

    private const string HooksMorePrompt =
        "Do these three things in order, each with one tool call: read the file missing.txt in the working "
        + "directory (it does not exist; carry on when the read fails); launch one subagent with the Agent tool "
        + "whose only instruction is to reply pong; write a file named asked.txt containing exactly: yes. Then "
        + "reply with exactly the word: done";

    private const string MissingReadToolUseId = "toolu_019Zsq2ZVRpxLBBKFykqziV4";
    private const string AgentToolUseId = "toolu_015oKRgkCAyTjEJJuxWD6Fiy";
    private const string SubagentId = "a074d0b45a1ff55c7";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static readonly HookEvent[] LateHooks =
    [
        HookEvent.PostToolUseFailure,
        HookEvent.SubagentStart,
        HookEvent.SubagentStop,
        HookEvent.PermissionRequest,
        HookEvent.Notification,
        HookEvent.PreCompact,
    ];

    [TestMethod]
    public async Task IncludeHookEventsTurnsTheTwoSettingsCommandHooksIntoFourTypedSystemFrames()
    {
        var log = new HookLog();
        await using var server = FixtureServer.Start("hook-events");
        using var host = StartHookEventsAsync(server, log);

        var seen = await QueryAsync(host, HookEventsPrompt);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");

        var events = HookEvents(seen);
        Assert.AreEqual(4, events.Length);

        CollectionAssert.AreEqual(
            new[] { "hook_started", "hook_response", "hook_started", "hook_response" },
            events.Select(hook => hook.Subtype).ToArray());
        CollectionAssert.AreEqual(
            new[] { "PreToolUse", "PreToolUse", "PostToolUse", "PostToolUse" },
            events.Select(hook => Text(hook, "hook_event")).ToArray());
        CollectionAssert.AreEqual(
            new[] { "PreToolUse:Read", "PreToolUse:Read", "PostToolUse:Read", "PostToolUse:Read" },
            events.Select(hook => Text(hook, "hook_name")).ToArray());

        foreach (var answered in events.Where(hook => hook.Subtype == "hook_response"))
        {
            Assert.AreEqual("success", Text(answered, "outcome"));
            Assert.AreEqual(0, answered.Line().GetProperty("exit_code").GetInt32());
            Assert.AreEqual(string.Empty, Text(answered, "output"));
            Assert.AreEqual(string.Empty, Text(answered, "stdout"));
            Assert.AreEqual(string.Empty, Text(answered, "stderr"));
        }

        foreach (var started in events.Where(hook => hook.Subtype == "hook_started"))
        {
            Assert.IsNull(Text(started, "outcome"));
            Assert.IsFalse(started.Line().TryGetProperty("exit_code", out var exitCode) && exitCode.ValueKind == JsonValueKind.Number);
        }
    }

    [TestMethod]
    public async Task TheSdkHookFiresOnceThroughHookCallbackWithNoHookEventOfItsOwnAndWritesTheRecordedStdin()
    {
        var log = new HookLog();
        await using var server = FixtureServer.Start("hook-events");
        using var host = StartHookEventsAsync(server, log);

        var seen = await QueryAsync(host, HookEventsPrompt);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        Assert.AreEqual(1, log.Inputs.Length);

        var fired = log.Only<PreToolUseHookInput>();
        Assert.AreEqual("Read", fired.ToolCall.Name);
        Assert.AreEqual(HookEventsToolUseId, fired.ToolCall.Id);
        Assert.AreEqual($"{RecordedCwd}/notes.txt", fired.ToolCall.RawInput.GetProperty("file_path").GetString());

        // The SDK hook is a second PreToolUse hook on the same matcher: two PreToolUse frames, not four, is
        // the binary announcing the settings hook alone.
        Assert.AreEqual(2, HookEvents(seen).Count(hook => Text(hook, "hook_event") == "PreToolUse"));

        await server.WaitForWrites(3, Patience);
        CollectionAssert.AreEqual(
            Fixture.Load("hook-events").Stdin.Anonymised(),
            server.Written.Anonymised());
        AssertArgv(host, "hook-events");
    }

    [TestMethod]
    public async Task TheFourLateHooksTheBinaryReachesCarryTheirTypedInputsWhileNotificationAndPreCompactNeverFire()
    {
        var log = new HookLog();
        ToolPermissionRequest? asked = null;
        await using var server = FixtureServer.Start("hooks-more");
        using var host = StartHooksMoreAsync(server, log, request => asked = request);

        await QueryAsync(host, HooksMorePrompt);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        Assert.AreEqual(4, log.Inputs.Length);

        var failed = log.Only<PostToolUseFailureHookInput>();
        Assert.AreEqual("Read", failed.ToolCall.Name);
        Assert.AreEqual(MissingReadToolUseId, failed.ToolCall.Id);
        Assert.AreEqual(
            $"File does not exist. Note: your current working directory is {RecordedCwd}.",
            failed.ToolCall.Error);
        Assert.AreEqual(false, failed.IsInterrupt);
        Assert.AreEqual($"{RecordedCwd}/missing.txt", failed.ToolCall.RawInput.GetProperty("file_path").GetString());

        var started = log.Only<SubagentStartHookInput>();
        Assert.AreEqual(SubagentId, started.AgentId);
        Assert.AreEqual(AgentType.GeneralPurpose, started.AgentType);

        var stopped = log.Only<SubagentStopHookInput>();
        Assert.AreEqual(SubagentId, stopped.AgentId);
        Assert.AreEqual(AgentType.GeneralPurpose, stopped.AgentType);
        Assert.IsFalse(stopped.IsStopHookActive);
        StringAssert.EndsWith(stopped.AgentTranscriptPath, $"subagents/agent-{SubagentId}.jsonl");
        var backgrounded = (SubagentStopHookBackgroundTask)stopped.BackgroundTasks.Single();
        Assert.AreEqual(SubagentId, backgrounded.Id);
        Assert.AreEqual(StopHookBackgroundTaskType.Subagent, backgrounded.TaskType);
        Assert.AreEqual(BackgroundTaskStatus.Running, backgrounded.Status);
        Assert.AreEqual("reply pong", backgrounded.Description);
        Assert.AreEqual(AgentType.GeneralPurpose, backgrounded.AgentType);

        var permission = log.Only<PermissionRequestHookInput>();
        Assert.AreEqual("Write", permission.ToolCall.Name);
        Assert.AreEqual($"{RecordedCwd}/asked.txt", permission.ToolCall.RawInput.GetProperty("file_path").GetString());
        Assert.AreEqual("yes", permission.ToolCall.RawInput.GetProperty("content").GetString());

        var suggestion = (SetModePermissionUpdate)permission.PermissionSuggestions.Single();
        Assert.AreEqual(PermissionMode.AcceptEdits, suggestion.Mode);
        Assert.AreEqual("session", suggestion.Destination);

        Assert.IsFalse(log.Inputs.Any(input => input.Event == HookEvent.Notification || input.Event == HookEvent.PreCompact));

        Assert.IsNotNull(asked);
        Assert.AreEqual("Write", asked.ToolCall.Name);
        Assert.AreEqual("yes", asked.ToolCall.RawInput.GetProperty("content").GetString());
    }

    [TestMethod]
    public async Task TheSubagentTheAgentToolLaunchedArrivesAsTypedTaskStartedUpdatedAndNotificationFrames()
    {
        var log = new HookLog();
        await using var server = FixtureServer.Start("hooks-more");
        using var host = StartHooksMoreAsync(server, log, _ => { });

        var seen = await QueryAsync(host, HooksMorePrompt);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");

        var task = seen.OfType<TaskStartedMessage>().Single();
        Assert.AreEqual(SubagentId, task.TaskId);
        Assert.AreEqual("reply pong", task.Description);
        Assert.AreEqual(AgentToolUseId, task.ToolUseId);
        Assert.AreEqual(TaskType.LocalAgent, task.TaskType);

        var updated = seen.OfType<TaskUpdatedMessage>().Single();
        Assert.AreEqual(SubagentId, updated.TaskId);
        Assert.AreEqual(BackgroundTaskStatus.Completed, updated.Patch.Status);

        var notified = seen.OfType<TaskNotificationMessage>().Single();
        Assert.AreEqual(SubagentId, notified.TaskId);
        Assert.AreEqual(TaskNotificationStatus.Completed, notified.Status);
        Assert.AreEqual(AgentToolUseId, notified.ToolUseId);
        Assert.AreEqual("pong", notified.Summary);
        Assert.AreEqual(6641L, notified.TotalTokens);

        await server.WaitForWrites(7, Patience);
        CollectionAssert.AreEqual(
            Fixture.Load("hooks-more").Stdin.Anonymised(),
            server.Written.Anonymised());
        AssertArgv(host, "hooks-more");
    }

    [TestMethod]
    public async Task ACompactPromptFiresPreCompactWithTheManualTriggerAndTheBinaryAnswersWithAFreshInit()
    {
        var log = new HookLog();
        await using var server = FixtureServer.Start("compact");
        using var host = StartCompactAsync(server, log);
        await using var session = await ConnectAsync(host);

        var first = await TurnAsync(session, "Say: one");
        Assert.AreEqual("one", Last(first).Result);

        var compacting = await TurnAsync(session, "/compact");

        var trigger = log.Only<PreCompactHookInput>();
        Assert.AreEqual(CompactTrigger.Manual, trigger.Trigger);
        Assert.IsNull(trigger.CustomInstructions);
        Assert.AreEqual(session.Id, trigger.SessionId);

        // The /compact prompt's own frames, from its started frame: the two status frames around
        // the hook, the fresh init, the boundary and the two user frames the compaction wrote.
        var status = compacting.OfType<SystemMessage>().Where(notice => notice.Subtype == "status").ToArray();
        Assert.AreEqual(2, status.Length);
        Assert.AreEqual("compacting", Text(status[0], "status"));
        Assert.IsNull(Text(status[1], "status"));
        Assert.AreEqual("success", Text(status[1], "compact_result"));

        Assert.AreEqual(1, compacting.OfType<SystemInitMessage>().Count());

        var boundary = compacting.OfType<SystemMessage>().Single(notice => notice.Subtype == "compact_boundary");
        Assert.AreEqual("manual", boundary.Line().GetProperty("compact_metadata").GetProperty("trigger").GetString());
        Assert.AreEqual(2, compacting.OfType<UserMessage>().Count());

        var compacted = Last(compacting);
        Assert.AreEqual(0, compacted.TurnCount);
        Assert.AreEqual(string.Empty, compacted.Result);
        Assert.IsNull(compacted.TerminalReason);

        var second = await TurnAsync(session, "Say: two");
        Assert.AreEqual("two", Last(second).Result);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
    }

    [TestMethod]
    public async Task TheCompactSessionWritesTheThreePromptsAndTheHookReplyInTheRecordedOrder()
    {
        var log = new HookLog();
        await using var server = FixtureServer.Start("compact");
        using var host = StartCompactAsync(server, log);

        await using (var session = await ConnectAsync(host))
        {
            await TurnAsync(session, "Say: one");
            await TurnAsync(session, "/compact");
            await TurnAsync(session, "Say: two");
        }

        await server.WaitForWrites(5, Patience);
        CollectionAssert.AreEqual(
            Fixture.Load("compact").Stdin.Anonymised(),
            server.Written.Anonymised());
        AssertArgv(host, "compact");
    }

    private static TestHost StartHookEventsAsync(FixtureServer server, HookLog log) => TestHost.Start(
        server,
        options: options =>
        {
            Recording(options);
            options.Model = "haiku";
            options.Tools = ["Read"];
            options.AllowedTools.Add("Read");
            options.MaxTurns = 3;
            options.Effort = Effort.Low;
            options.IncludeHookEvents = true;
            options.Settings = CommandHookSettings;
            options.AddHook(
                HookEvent.PreToolUse,
                "Read",
                (input, ct) => log.Record(input, new HookOutput()));
        });

    private static TestHost StartHooksMoreAsync(FixtureServer server, HookLog log, Action<ToolPermissionRequest> asked) =>
        TestHost.Start(
            server,
            options: options =>
            {
                Recording(options);
                options.Model = "haiku";
                options.Tools = ["Read", "Agent", "Write"];
                options.AllowedTools.Add("Read");
                options.AllowedTools.Add("Agent");
                options.MaxTurns = 6;
                options.Effort = Effort.Low;

                foreach (var late in LateHooks)
                {
                    options.AddHook(late, null, (input, ct) => log.Record(input, new HookOutput()));
                }

                options.PermissionCallback = (request, ct) =>
                {
                    asked(request);
                    return Task.FromResult(ToolPermissionResult.Allow());
                };
            });

    private static TestHost StartCompactAsync(FixtureServer server, HookLog log) => TestHost.Start(
        server,
        options: options =>
        {
            Recording(options);
            options.Model = "haiku";
            options.MaxTurns = 1;
            options.Effort = Effort.Low;
            options.AddHook(HookEvent.PreCompact, null, (input, ct) => log.Record(input, new HookOutput()));
        });

    private static void Recording(ClaudeAgentSessionOptions options)
    {
        options.WorkingDirectory = RecordedCwd;
        options.SettingSources = [];
    }

    private static void AssertArgv(TestHost host, string fixture)
    {
        var asked = host.Transports.Requests.Single();
        CollectionAssert.AreEqual(Fixture.Load(fixture).Argv.ToArray(), asked.Argv.ToArray(), $"{fixture}.argv.json");
    }

    private static async Task<ImmutableArray<AgentMessage>> QueryAsync(TestHost host, string prompt)
    {
        var agent = host.Client;
        var seen = ImmutableArray.CreateBuilder<AgentMessage>();
        await foreach (var message in agent.QueryAsync(prompt))
        {
            seen.Add(message);
        }

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        return seen.ToImmutable();
    }

    private static Task<ClaudeAgentSession> ConnectAsync(TestHost host) => host.Client.StartSessionAsync();

    private static async Task<ImmutableArray<AgentMessage>> TurnAsync(ClaudeAgentSession session, string prompt)
    {
        var seen = ImmutableArray.CreateBuilder<AgentMessage>();
        await foreach (var message in session.SendAsync(prompt))
        {
            seen.Add(message);
        }

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        return seen.ToImmutable();
    }

    private static ResultMessage Last(ImmutableArray<AgentMessage> turn) => (ResultMessage)turn[^1];

    private static SystemMessage[] HookEvents(ImmutableArray<AgentMessage> seen) =>
        [.. seen.OfType<SystemMessage>().Where(notice => notice.Subtype is "hook_started" or "hook_response")];

    private static string? Text(SystemMessage notice, string name) =>
        notice.Line().TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
