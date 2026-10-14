using Claude.Agent.Tests.Mcp;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Sessions;

[TestClass]
public sealed class CancelReplayTests
{
    private const string ReadPrompt = "Read notes.txt in the working directory and reply with its first line only.";
    private const string WritePrompt =
        "Create a file named cancelled.txt in the working directory containing exactly: no";

    private const string RecordedCwd = "/tmp/claude-agent-fixtures/cwd";
    private const string HookCallbackRequestId = "62a0025f-be33-4283-86de-a828f1684676";
    private const string CanUseToolRequestId = "a38237c3-9a04-42cb-ab04-ae3c3ad39675";
    private const string ReadToolUseId = "toolu_01EzYnLSq1PpXu4DCwqP16fw";
    private const string WriteToolUseId = "toolu_01AMqnDVe8mQAJ5CVAFxKDPT";

    private const string McpPrompt = "Call the wait tool once. Then reply with exactly the word: waited";
    private const string McpToolsCallRequestId = "ce16e961-9a14-4b1e-bff7-bd05e5c21159";
    private const string McpCancelledRequestId = "3fc475f2-e002-47d4-8833-648079650726";
    private const string McpToolUseId = "toolu_01Lq5B9k5Rte1FWMVxVRQuYJ";

    private const string BackgroundShellPrompt =
        "Use the Bash tool with run_in_background set to true to run: sleep 30. Then reply with exactly the word: started";

    private const string BackgroundShellTaskId = "bnz063043";
    private const string BackgroundShellToolUseId = "toolu_012HXiiU2pUEeNJ9ivPHtB6c";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task AnInterruptWhileThePreToolUseHookIsPendingCancelsItsTokenAndTheCallbackIsNeverAnswered()
    {
        var blocking = new BlockingHandlers();
        await using var server = FixtureServer.Start("cancel");
        using var host = StartAsync(server, blocking);
        await using var session = await ConnectAsync(host);

        var result = (await InterruptDuringHookAsync(session, blocking)).Result;
        await blocking.Hook.Cancelled.WaitAsync(Patience);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");

        Assert.IsNotNull(blocking.PreToolUse);
        Assert.AreEqual("Read", blocking.PreToolUse.ToolCall.Name);
        Assert.AreEqual(ReadToolUseId, blocking.PreToolUse.ToolCall.Id);

        Assert.IsFalse(Answered(server, HookCallbackRequestId), "the cancelled hook callback was answered.");

        Assert.AreEqual("error_during_execution", result.Subtype);
        Assert.IsTrue(result.IsError);
        Assert.AreEqual("aborted_tools", result.TerminalReason);
        Assert.IsTrue(result.PermissionDenials.Count == 0);
    }

    [TestMethod]
    public async Task AnInterruptWhileTheWriteAwaitsPermissionCancelsTheHandlersTokenAndTheResultCarriesTheDenial()
    {
        var blocking = new BlockingHandlers();
        await using var server = FixtureServer.Start("cancel");
        using var host = StartAsync(server, blocking);
        await using var session = await ConnectAsync(host);

        await InterruptDuringHookAsync(session, blocking);
        var result = (await InterruptDuringPermissionAsync(session, blocking)).Result;
        await blocking.Permission.Cancelled.WaitAsync(Patience);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");

        Assert.IsNotNull(blocking.Asked);
        Assert.AreEqual("Write", blocking.Asked.ToolCall.Name);
        Assert.AreEqual(WriteToolUseId, blocking.Asked.ToolCall.Id);

        Assert.IsFalse(Answered(server, CanUseToolRequestId), "the cancelled permission request was answered.");

        Assert.AreEqual("error_during_execution", result.Subtype);

        var denial = result.PermissionDenials.Single();
        Assert.AreEqual("Write", denial.ToolName);
        Assert.AreEqual(WriteToolUseId, denial.ToolUseId);
    }

    // An interrupted turn completes with its trace, not an exception. The binary answers the call it
    // cancelled with an error tool_result before the result, so the call closes failed, not
    // unanswered.
    [TestMethod]
    public async Task EachInterruptedTurnCompletesWithItsTraceAndItsCancelledCallClosesFailedBeforeTheResult()
    {
        var blocking = new BlockingHandlers();
        await using var server = FixtureServer.Start("cancel");
        using var host = StartAsync(server, blocking);
        await using var session = await ConnectAsync(host);

        var read = await InterruptDuringHookAsync(session, blocking);
        var write = await InterruptDuringPermissionAsync(session, blocking);

        foreach (var (trace, id, name) in new[] { (read, ReadToolUseId, "Read"), (write, WriteToolUseId, "Write") })
        {
            Assert.AreEqual("aborted_tools", trace.Result.TerminalReason);
            Assert.AreSame(trace.Messages[^1], trace.Result);

            var snapshots = trace.Messages.OfType<ToolCallMessage>().Select(message => message.ToolCall).ToArray();
            CollectionAssert.AreEqual(
                new[] { ToolCallStatus.Started, ToolCallStatus.Failed },
                snapshots.Select(call => call.Status).ToArray(),
                name);
            Assert.IsTrue(snapshots.All(call => call.Id == id && call.Name == name), name);

            var failed = trace.ToolCalls.Single();
            Assert.AreEqual(ToolCallStatus.Failed, failed.Status, name);
            StringAssert.StartsWith(failed.Error, "The user doesn't want to proceed with this tool use.", name);
        }
    }

    [TestMethod]
    public async Task TheCancelFixtureWritesTheInitializeTheTwoPromptsAndTheTwoInterruptsAndNothingElse()
    {
        var blocking = new BlockingHandlers();
        await using var server = FixtureServer.Start("cancel");
        using var host = StartAsync(server, blocking);
        await using var session = await ConnectAsync(host);

        await InterruptDuringHookAsync(session, blocking);
        await InterruptDuringPermissionAsync(session, blocking);

        await server.WaitForWrites(5, Patience);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");

        CollectionAssert.AreEqual(
            Fixture.Load("cancel").Stdin.Anonymised(),
            server.Written.Anonymised());
    }

    // Rule B, a session's run. A break out of the loop over a run, at the tool call its hook or its
    // permission callback holds, interrupts the turn, and the loop is left with the run ended with
    // its interrupted trace; the session lives on and serves its next run, left the same way. The
    // interrupts go out where the recording has them.
    [TestMethod]
    public async Task BreakingOutOfEachRunsLoopAtItsHeldToolInterruptsItsTurnAndTheSessionServesTheNextRun()
    {
        var blocking = new BlockingHandlers();
        await using var server = FixtureServer.Start("cancel");
        using var host = StartAsync(server, blocking);
        await using var session = await ConnectAsync(host);

        foreach (var (prompt, gate) in new[] { (ReadPrompt, blocking.Hook), (WritePrompt, blocking.Permission) })
        {
            var run = session.SendAsync(prompt);
            await foreach (var message in run)
            {
                if (message is ToolCallMessage)
                {
                    await gate.Entered.WaitAsync(Patience);
                    break;
                }
            }

            var completion = run.WaitForCompletionAsync();
            Assert.IsTrue(completion.IsCompletedSuccessfully, $"{prompt}: the run had not ended when the loop was left.");
            var trace = await completion;
            Assert.AreEqual("aborted_tools", trace.Result.TerminalReason, prompt);
            Assert.AreSame(trace.Messages[^1], trace.Result, prompt);
        }

        await server.WaitForWrites(5, Patience);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        CollectionAssert.AreEqual(
            Fixture.Load("cancel").Stdin.Anonymised(),
            server.Written.Anonymised());
    }

    // The mcp-cancel fixture: an interrupt while an in-process MCP tool is still running. The binary
    // gives the call up with MCP's notifications/cancelled naming the call's JSON-RPC id, sends no
    // control_cancel_request, and answers the call itself with an error tool_result. The tool's
    // token is cancelled, the tools/call is never answered, the notification is acknowledged, and
    // the client writes the recorded stdin and nothing else.
    [TestMethod]
    public async Task AnInterruptWhileAnInProcessMcpToolRunsCancelsTheToolsTokenAndItsCallIsNeverAnswered()
    {
        var slow = new SlowTool();
        await using var server = FixtureServer.Start("mcp-cancel");
        using var host = TestHost.Start(server, options: options =>
        {
            options.WorkingDirectory = RecordedCwd;
            options.SettingSources = [];
            options.Model = "haiku";
            options.Tools = [];
            options.AllowedTools.Add("mcp__slow__wait");
            options.MaxTurns = 3;
            options.Effort = Effort.Low;
            options.McpServers["slow"] = McpServerConfiguration.InProcess(slow);
        });
        await using var session = await ConnectAsync(host);

        // The recorder prompted once the server's handshake was answered: the initialize, and the
        // server's initialize, initialized and tools/list.
        await server.WaitForWrites(4, Patience);
        var run = session.SendAsync(McpPrompt);
        await slow.Entered.WaitAsync(Patience);
        await session.InterruptAsync();
        var trace = await run.WaitForCompletionAsync().WaitAsync(Patience);
        await slow.Cancelled.WaitAsync(Patience);
        await server.WaitForWrites(Fixture.Load("mcp-cancel").Stdin.Length, Patience);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        Assert.IsFalse(Answered(server, McpToolsCallRequestId), "the cancelled tools/call was answered.");
        Assert.IsTrue(Answered(server, McpCancelledRequestId), "the notifications/cancelled was not acknowledged.");

        Assert.AreEqual("error_during_execution", trace.Result.Subtype);
        Assert.AreEqual("aborted_tools", trace.Result.TerminalReason);
        var failed = trace.ToolCalls.Single();
        Assert.AreEqual(McpToolUseId, failed.Id);
        Assert.AreEqual(ToolCallStatus.Failed, failed.Status);

        CollectionAssert.AreEqual(
            Fixture.Load("mcp-cancel").Stdin.Anonymised(),
            server.Written.Anonymised());
    }

    // The background-interrupt fixture, 2.1.283: the session's interrupt between turns, while the
    // shell the first turn started in the background still sleeps. The run's own interrupt, once
    // the run has ended, writes nothing; the session's goes out with no turn under way, and the
    // binary answers it with nothing queued. The next frames are the shell's own end, 29 seconds
    // later in the recorder's log: the task completed, not killed, and a turn the binary opens
    // itself on the notification, which reaches what no run reads. The client writes the recorded
    // stdin and nothing else.
    [TestMethod]
    public async Task TheSessionsInterruptBetweenTurnsIsSentAndAnsweredAndTheBackgroundShellRunsToItsEndAndATurnOfTheBinarysOwn()
    {
        var recorded = Fixture.Load("background-interrupt");
        await using var server = FixtureServer.Start("background-interrupt");
        using var host = TestHost.Start(server, options: options =>
        {
            options.WorkingDirectory = RecordedCwd;
            options.SettingSources = [];
            options.Model = "haiku";
            options.Tools = ["Bash"];
            options.AllowedTools.Add("Bash");
            options.MaxTurns = 3;
            options.Effort = Effort.Low;
        });
        await using var session = await ConnectAsync(host);

        var first = session.SendAsync(BackgroundShellPrompt);
        var trace = await first.WaitForCompletionAsync().WaitAsync(Patience);
        Assert.AreEqual("started", trace.Result.Result);
        Assert.AreEqual(BackgroundShellTaskId, trace.Messages.OfType<TaskMessage>().Single().TaskId);
        Assert.AreEqual(BackgroundShellToolUseId, trace.ToolCalls.Single().Id);

        await first.InterruptAsync().WaitAsync(Patience);
        Assert.AreEqual(2, server.Written.Length, "the run's interrupt after its end wrote to the binary.");

        await session.InterruptAsync().WaitAsync(Patience);
        Assert.AreEqual(3, server.Written.Length, "the session's interrupt between turns was not written.");
        Assert.AreEqual(Fixture.Anonymise(recorded.Stdin[2]), Fixture.Anonymise(server.Written[2]));

        var unclaimed = new List<AgentMessage>();
        using var patience = new CancellationTokenSource(Patience);
        await foreach (var message in session.ReceiveMessagesAsync(patience.Token))
        {
            unclaimed.Add(message);
        }

        // The rate limit event the binary wrote between the prompt's queued and started frames,
        // then what followed the interrupt's answer.
        CollectionAssert.AreEqual(
            new[]
            {
                typeof(RateLimitMessage),
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
            unclaimed.Select(message => message.GetType()).ToArray());

        Assert.AreEqual(0, unclaimed.OfType<BackgroundTasksChangedMessage>().Single().Tasks.Count);

        var updated = unclaimed.OfType<TaskUpdatedMessage>().Single();
        Assert.AreEqual(BackgroundShellTaskId, updated.TaskId);
        Assert.AreEqual("completed", updated.Patch.Status.ToString());

        var notification = unclaimed.OfType<TaskNotificationMessage>().Single();
        Assert.AreEqual(BackgroundShellTaskId, notification.TaskId);
        Assert.AreEqual(BackgroundShellToolUseId, notification.ToolUseId);
        Assert.AreEqual(TaskNotificationStatus.Completed, notification.Status);
        Assert.AreEqual("Background command \"Run sleep 30 in the background\" completed (exit code 0)", notification.Summary);

        var own = (ResultMessage)unclaimed[^1];
        Assert.AreEqual("success", own.Subtype);
        Assert.IsFalse(own.IsError);
        Assert.AreEqual("completed", own.TerminalReason);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        CollectionAssert.AreEqual(recorded.Stdin.Anonymised(), server.Written.Anonymised());
        CollectionAssert.AreEqual(recorded.Argv.ToArray(), host.Transports.Last!.Argv.ToArray(), "background-interrupt.argv.json");
    }

    private static async Task<AgentTrace> InterruptDuringHookAsync(
        ClaudeAgentSession session,
        BlockingHandlers blocking)
    {
        var run = session.SendAsync(ReadPrompt);
        await blocking.Hook.Entered.WaitAsync(Patience);

        await session.InterruptAsync();

        return await run.WaitForCompletionAsync();
    }

    private static async Task<AgentTrace> InterruptDuringPermissionAsync(
        ClaudeAgentSession session,
        BlockingHandlers blocking)
    {
        var run = session.SendAsync(WritePrompt);
        await blocking.Permission.Entered.WaitAsync(Patience);

        await session.InterruptAsync();

        return await run.WaitForCompletionAsync();
    }

    private static TestHost StartAsync(FixtureServer server, BlockingHandlers blocking) =>
        TestHost.Start(server, options: options =>
        {
            options.WorkingDirectory = RecordedCwd;
            options.SettingSources = [];
            options.Model = "haiku";
            options.Tools = ["Read", "Write"];
            options.MaxTurns = 3;
            options.Effort = Effort.Low;
            options.AddHook(HookEvent.PreToolUse, "Read", blocking.PreToolUseAsync);
            options.PermissionCallback = blocking.CanUseToolAsync;
        });

    private static Task<ClaudeAgentSession> ConnectAsync(TestHost host) => host.Client.StartSessionAsync();

    private static bool Answered(FixtureServer server, string requestId) =>
        server.Written.Any(line => Fixture.ResponseRequestIdOf(line) == requestId);
}

internal sealed class BlockingHandlers
{
    public BlockingGate Hook { get; } = new();

    public BlockingGate Permission { get; } = new();

    public PreToolUseHookInput? PreToolUse { get; private set; }

    public ToolPermissionRequest? Asked { get; private set; }

    public async Task<HookOutput> PreToolUseAsync(HookInput input, CancellationToken cancellation)
    {
        PreToolUse = input as PreToolUseHookInput;
        await Hook.BlockAsync(cancellation).ConfigureAwait(false);
        return new HookOutput();
    }

    public async Task<ToolPermissionResult> CanUseToolAsync(ToolPermissionRequest request, CancellationToken cancellation)
    {
        Asked = request;
        await Permission.BlockAsync(cancellation).ConfigureAwait(false);
        return ToolPermissionResult.Allow();
    }
}

internal sealed class BlockingGate
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Entered => _entered.Task;

    public Task Cancelled => _cancelled.Task;

    public async Task BlockAsync(CancellationToken cancellation)
    {
        _entered.TrySetResult();

        try
        {
            await _released.Task.WaitAsync(cancellation).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _cancelled.TrySetResult();
            throw;
        }
    }
}
