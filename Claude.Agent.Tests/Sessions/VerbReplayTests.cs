using System.Collections.Immutable;
using Claude.Agent.Tests.Mcp;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Sessions;

[TestClass]
public sealed class VerbReplayTests
{
    private const string ParentSessionId = "63bf45b5-1f73-4220-80a6-9518f49b968e";
    private const string ForkedSessionId = "3171ff87-20cb-4dfb-b85f-abbc4cf29d6a";
    private const string RewindPrompt = "Create a file named rewound.txt in the working directory containing exactly: before";
    private const string RewindCheckpointId = "f383c41a-25c3-4535-9242-eda56ff10cce";
    private const string ParentPrompt = "Reply with exactly one word: pong";
    private const string ChildPrompt = "Reply with exactly the word you replied last time.";

    // The lines a session with the calc server writes before its first verb: the initialize, and the
    // server's initialize, initialized and tools/list.
    private const int Handshake = 4;

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task RewindFilesSendsTheUserMessageUuidTheBinaryReplayedAndTakesItsReply()
    {
        await using var server = FixtureServer.Start("rewind-files");
        using var host = TestHost.Start(server, options: options =>
        {
            options.EnableFileCheckpointing = true;
            options.ReplayUserMessages = true;
        });
        await using var session = await ConnectAsync(host);

        string? checkpoint = null;
        await foreach (var message in session.SendAsync(RewindPrompt))
        {
            if (message is UserMessage { Uuid: { } uuid } && checkpoint is null)
            {
                checkpoint = uuid;
            }
        }

        // The binary takes the uuid the prompt was written with as its user message's uuid, and
        // replays it under that uuid.
        Assert.IsNotNull(checkpoint);
        Assert.AreEqual(Fixture.UuidOf(server.Written.First(line => Fixture.TypeOf(line) == "user")), checkpoint);

        await session.RewindFilesAsync(checkpoint);

        await server.WaitForWrites(3, Patience);
        CollectionAssert.AreEqual(
            Fixture.Load("rewind-files").Stdin.Anonymised(),
            server.Written.Anonymised());
    }

    [TestMethod]
    public async Task RewindFilesWithCanRewindFalseAndAReasonFailsWithTheReasonInTheMessage()
    {
        var script = new Script(
        [
            Script.AnswerInitialize,
            new AnswerControlRequest(
                """{"type": "control_response", "response": {"subtype": "success", "request_id": "", "response": {"canRewind": false, "error": "no checkpoint"}}}"""),
        ]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        await using var session = await ConnectAsync(host);

        var rewound = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => session.RewindFilesAsync(RewindCheckpointId));

        Assert.AreEqual(ErrorCodes.CannotRewind, rewound.ErrorCode);
        Assert.AreEqual(
            $"Claude Code cannot rewind files to message {RewindCheckpointId}: no checkpoint",
            rewound.Message);
    }

    [TestMethod]
    public async Task RewindFilesWithCanRewindFalseAndNoReasonFailsWithAMessageThatCarriesNone()
    {
        var script = new Script(
        [
            Script.AnswerInitialize,
            new AnswerControlRequest(
                """{"type": "control_response", "response": {"subtype": "success", "request_id": "", "response": {"canRewind": false}}}"""),
        ]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        await using var session = await ConnectAsync(host);

        var rewound = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => session.RewindFilesAsync(RewindCheckpointId));

        Assert.AreEqual(ErrorCodes.CannotRewind, rewound.ErrorCode);
        Assert.AreEqual(
            $"Claude Code cannot rewind files to message {RewindCheckpointId}",
            rewound.Message);
    }

    [TestMethod]
    public async Task RewindFilesWhoseReplyHasNoCanRewindKeyFailsWithAParseError()
    {
        var script = new Script(
        [
            Script.AnswerInitialize,
            new AnswerControlRequest(
                """{"type": "control_response", "response": {"subtype": "success", "request_id": "", "response": {"skippedLinks": 0}}}"""),
        ]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        await using var session = await ConnectAsync(host);

        var rewound = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => session.RewindFilesAsync(RewindCheckpointId));

        Assert.AreEqual(ErrorCodes.Parse, rewound.ErrorCode);
    }

    // The verbs come once the server's handshake is answered, as the recorder waited for it: a verb
    // ahead of the connection is answered "Server not found". On a connected in-process server the
    // binary refuses a reconnect.
    [TestMethod]
    public async Task ReconnectMcpServerOnAnInProcessServerIsRefusedWithTheBinarysError()
    {
        await using var server = FixtureServer.Start("mcp-reconnect", promptFirst: false);
        using var host = StartWithCalcAsync(server);
        await using var session = await ConnectAsync(host);
        await server.WaitForWrites(Handshake, Patience);

        var reconnected = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => session.ReconnectMcpServerAsync("calc"));

        Assert.AreEqual(ErrorCodes.Control, reconnected.ErrorCode);
        Assert.AreEqual("SDK servers should be handled in print.ts", reconnected.Message);
        Assert.AreEqual(McpServerConnectionStatus.Connected, (await session.GetMcpStatusAsync()).Single(status => status.Name == "calc").Status);

        await server.WaitForWrites(6, Patience);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        CollectionAssert.AreEqual(
            Fixture.Load("mcp-reconnect").Stdin.Anonymised(),
            server.Written.Anonymised());
    }

    // Toggling a connected in-process server off is answered with a success that carries no
    // response; the status still has it connected. Toggling it on is refused.
    [TestMethod]
    public async Task ToggleMcpServerOnAnInProcessServerIsRefusedWithTheBinarysError()
    {
        await using var server = FixtureServer.Start("mcp-toggle", promptFirst: false);
        using var host = StartWithCalcAsync(server);
        await using var session = await ConnectAsync(host);
        await server.WaitForWrites(Handshake, Patience);

        await session.ToggleMcpServerAsync("calc", enabled: false);
        Assert.AreEqual(McpServerConnectionStatus.Connected, (await session.GetMcpStatusAsync()).Single(status => status.Name == "calc").Status);
        var toggled = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => session.ToggleMcpServerAsync("calc", enabled: true));

        Assert.AreEqual(ErrorCodes.Control, toggled.ErrorCode);
        Assert.AreEqual("SDK servers should be handled in print.ts", toggled.Message);

        await server.WaitForWrites(7, Patience);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        CollectionAssert.AreEqual(
            Fixture.Load("mcp-toggle").Stdin.Anonymised(),
            server.Written.Anonymised());
    }

    [TestMethod]
    public async Task StopTaskForAnIdTheBinaryDoesNotKnowIsAnsweredWithSuccess()
    {
        await using var server = FixtureServer.Start("stop-task", promptFirst: false);
        using var host = TestHost.Start(server);
        await using var session = await ConnectAsync(host);

        await session.StopTaskAsync("task_00000000");

        await server.WaitForWrites(2, Patience);
        CollectionAssert.AreEqual(
            Fixture.Load("stop-task").Stdin.Anonymised(),
            server.Written.Anonymised());
    }

    [TestMethod]
    public async Task ForkStartsASecondProcessResumingTheParentAndGetsANewSessionId()
    {
        await using var parent = FixtureServer.Start("fork-parent", stdinDiffers: true);
        await using var child = FixtureServer.Start("fork", stdinDiffers: true);
        using var host = TestHost.Start([parent, child], options: Recording);

        await using var session = await ConnectAsync(host);
        await PromptAsync(session, ParentPrompt);
        Assert.AreEqual(ParentSessionId, session.Id);

        await using var fork = await session.ForkAsync();
        var answer = await PromptAsync(fork, ChildPrompt);

        Assert.AreEqual(ForkedSessionId, fork.Id);
        Assert.AreEqual("pong", answer.Result);
        AssertArgv(host, 0, "fork-parent");
        AssertArgv(host, 1, "fork");
    }

    // The client has no defaults here: what the parent was asked went in on its call, and the fork's
    // argv is the recorded one only if the fork is asked the same.
    [TestMethod]
    public async Task AForkIsAskedWhatTheCallThatStartedItsParentAsked()
    {
        await using var parent = FixtureServer.Start("fork-parent", stdinDiffers: true);
        await using var child = FixtureServer.Start("fork", stdinDiffers: true);
        using var host = TestHost.Start([parent, child]);
        var asked = new ClaudeAgentSessionOptions();
        Recording(asked);

        await using var session = await host.Client.StartSessionAsync(asked);
        await PromptAsync(session, ParentPrompt);

        await using var fork = await session.ForkAsync();
        await PromptAsync(fork, ChildPrompt);

        Assert.AreEqual(ForkedSessionId, fork.Id);
        AssertArgv(host, 0, "fork-parent");
        AssertArgv(host, 1, "fork");
    }

    // What accumulated for the live session is what the fork is asked, once: the defaults are not
    // added a second time on the way through the client.
    [TestMethod]
    public async Task AForkIsLayeredOnceSoTheDefaultsHooksAreNotRegisteredTwice()
    {
        await using var parent = FixtureServer.Start("fork-parent", stdinDiffers: true);
        await using var child = FixtureServer.Start("fork", stdinDiffers: true);
        using var host = TestHost.Start([parent, child], defaults =>
        {
            Recording(defaults);
            defaults.AllowedTools.Add("Read");
            defaults.AddHook(HookEvent.Notification, null, (_, _) => Task.FromResult(new HookOutput()));
        });
        var asked = new ClaudeAgentSessionOptions();
        asked.AllowedTools.Add("Bash");
        asked.AddHook(HookEvent.Notification, null, (_, _) => Task.FromResult(new HookOutput()));

        await using var session = await host.Client.StartSessionAsync(asked);
        await PromptAsync(session, ParentPrompt);
        await using var fork = await session.ForkAsync();
        await PromptAsync(fork, ChildPrompt);

        foreach (var request in host.Transports.Requests)
        {
            Assert.AreEqual(2, request.Session.Hooks.Count);
            CollectionAssert.AreEqual(new[] { "Read", "Bash" }, request.Session.AllowedTools.ToArray());
        }

        Assert.IsTrue(host.Transports.Requests[1].Session.Fork);
    }

    [TestMethod]
    public async Task AResumeIsAskedWhatTheCallThatStartedTheLiveSessionAsked()
    {
        await using var parent = FixtureServer.Start("fork-parent", stdinDiffers: true);
        await using var child = FixtureServer.Start("resume", stdinDiffers: true);
        using var host = TestHost.Start([parent, child]);
        var asked = new ClaudeAgentSessionOptions();
        Recording(asked);

        await using var session = await host.Client.StartSessionAsync(asked);
        await PromptAsync(session, ParentPrompt);

        await using var resume = await session.ResumeAsync();
        await PromptAsync(resume, ChildPrompt);

        Assert.AreEqual(ParentSessionId, resume.Id);
        AssertArgv(host, 1, "resume");
    }

    [TestMethod]
    public async Task ResumeStartsASecondProcessOnTheSameSessionId()
    {
        await using var parent = FixtureServer.Start("fork-parent", stdinDiffers: true);
        await using var child = FixtureServer.Start("resume", stdinDiffers: true);
        using var host = TestHost.Start([parent, child], options: Recording);

        await using var session = await ConnectAsync(host);
        await PromptAsync(session, ParentPrompt);

        await using var resume = await session.ResumeAsync();
        var answer = await PromptAsync(resume, ChildPrompt);

        Assert.AreEqual(ParentSessionId, resume.Id);
        Assert.AreEqual("pong", answer.Result);
        AssertArgv(host, 1, "resume");
    }

    private static void Recording(ClaudeAgentSessionOptions options)
    {
        options.WorkingDirectory = "/tmp/claude-agent-fixtures/cwd";
        options.SettingSources = [];
        options.Model = "haiku";
        options.MaxTurns = 1;
        options.Effort = Effort.Low;
    }

    private static TestHost StartWithCalcAsync(FixtureServer server) => TestHost.Start(
        server,
        options =>
        {
            Recording(options);
            options.Tools = [];
            options.AllowedTools.Add("mcp__calc__add");
            options.McpServers["calc"] = McpServerConfiguration.InProcess(new Calc());
        });

    private static void AssertArgv(TestHost host, int request, string fixture)
    {
        var asked = host.Transports.Requests[request];
        CollectionAssert.AreEqual(Fixture.Load(fixture).Argv.ToArray(), asked.Argv.ToArray(), $"{fixture}.argv.json");
    }

    private static Task<ClaudeAgentSession> ConnectAsync(TestHost host) => host.Client.StartSessionAsync();

    // fork-parent, fork and resume were recorded through a one-shot query, whose prompt carries no
    // uuid, and name no prompt: played to a session, the prompt's run never takes the turn, which
    // reaches what no run reads, its result there.
    private static async Task<ResultMessage> PromptAsync(ClaudeAgentSession session, string prompt)
    {
        _ = session.SendAsync(prompt);
        await foreach (var message in session.ReceiveMessagesAsync())
        {
            if (message is ResultMessage result)
            {
                return result;
            }
        }

        throw new AssertFailedException("the turn reached no result.");
    }
}
