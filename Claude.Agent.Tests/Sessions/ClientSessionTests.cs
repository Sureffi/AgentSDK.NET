using System.Collections.Immutable;
using Claude.Agent.Recorder;
using Claude.Agent.Tests.Mcp;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Sessions;

[TestClass]
public sealed class ClientSessionTests
{
    private const string CountPrompt = "Count slowly from 1 to 200, one number per line, no other text.";
    private const string PayloadsPrompt = "Reply with exactly one word: ok";
    private const string RecordedCwd = "/tmp/claude-agent-fixtures/cwd";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task ClientFixtureReportsTheServerInfoTheInitializeReplyCarried()
    {
        await using var server = FixtureServer.Start("client");
        using var host = TestHost.Start(server, AsTheClientFixture);

        await using var session = await ConnectAsync(host);

        Assert.IsNotNull(session.ServerInfo);
        Assert.AreNotEqual(0, session.ServerInfo.Commands.Count);
        Assert.AreEqual("user@example.com", session.ServerInfo.Account?.Email);
    }

    [TestMethod]
    public async Task ClientFixtureRoundTripsEveryControlRequestAndEndsOnTheInterruptedResult()
    {
        await using var server = FixtureServer.Start("client");
        using var host = TestHost.Start(server, AsTheClientFixture);

        await using var session = await ConnectAsync(host);

        var first = await DrainAsync(session.SendAsync("Say: one"));
        Assert.IsInstanceOfType<ResultMessage>(first[^1]);

        await session.SetModelAsync("haiku");
        await session.SetPermissionModeAsync(PermissionMode.AcceptEdits);

        var usage = await session.GetContextUsageAsync();
        Assert.AreEqual(29260, usage.TotalTokens);

        var status = await session.GetMcpStatusAsync();
        Assert.AreEqual("claude.ai Claude Docs", status.Single().Name);

        var second = await DrainAsync(session.SendAsync("Say: two"));
        Assert.IsInstanceOfType<ResultMessage>(second[^1]);

        ResultMessage? aborted = null;
        var interrupted = false;
        await foreach (var message in session.SendAsync(CountPrompt))
        {
            if (!interrupted && message is AssistantMessage)
            {
                interrupted = true;
                await session.InterruptAsync();
            }

            aborted = message as ResultMessage ?? aborted;
        }

        Assert.IsNotNull(aborted);
        Assert.AreEqual("aborted_streaming", aborted.TerminalReason);

        await server.WaitForWrites(9, TimeSpan.FromSeconds(10));
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");

        CollectionAssert.AreEqual(
            Fixture.Load("client").Stdin.Anonymised(),
            server.Written.Anonymised());
    }

    // A session asked a schema taken from a type names that type on a send's trace, and no
    // evidence: a session takes none. The client recording asked no schema; the schema reaches argv
    // alone, which this replay does not hold, so the first prompt's turn plays as recorded.
    [TestMethod]
    public async Task ASessionsSendNamesTheTypeItsSchemaWasTakenFrom()
    {
        await using var server = FixtureServer.Start("client");
        using var host = TestHost.Start(server, options =>
        {
            AsTheClientFixture(options);
            options.SetOutputSchema<WordReport>();
        });

        await using var session = await ConnectAsync(host);
        var trace = await session.SendAsync("Say: one").WaitForCompletionAsync();

        Assert.AreEqual(typeof(WordReport), trace.AnswerType);
        Assert.IsNull(trace.EvidenceType);
    }

    // The run's own interrupt, called from inside the loop that reads it: the read loop is not
    // held by the reader, the binary answers with the turn's result, and the run completes with its
    // trace. What goes out is the recording's stdin, the interrupt among it.
    [TestMethod]
    public async Task TheRunsInterruptCalledFromInsideItsLoopStopsTheTurnAndTheRunCompletesWithItsTrace()
    {
        await using var server = FixtureServer.Start("client");
        using var host = TestHost.Start(server, AsTheClientFixture);

        await using var session = await ConnectAsync(host);

        await DrainAsync(session.SendAsync("Say: one"));
        await session.SetModelAsync("haiku");
        await session.SetPermissionModeAsync(PermissionMode.AcceptEdits);
        await session.GetContextUsageAsync();
        await session.GetMcpStatusAsync();
        await DrainAsync(session.SendAsync("Say: two"));

        var run = session.SendAsync(CountPrompt);
        var seen = new List<AgentMessage>();
        await foreach (var message in run)
        {
            seen.Add(message);
            if (seen.Count(item => item is AssistantMessage) == 1 && message is AssistantMessage)
            {
                await run.InterruptAsync();
            }
        }

        var trace = await run.WaitForCompletionAsync();
        CollectionAssert.AreEqual(seen, trace.Messages.ToList());
        Assert.AreEqual("aborted_streaming", trace.Result.TerminalReason);
        Assert.AreEqual("error_during_execution", trace.Result.Subtype);

        await run.InterruptAsync();

        await server.WaitForWrites(9, Patience);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        CollectionAssert.AreEqual(
            Fixture.Load("client").Stdin.Anonymised(),
            server.Written.Anonymised());
    }

    [TestMethod]
    public async Task ThePayloadsFixtureReportsTheServerInfoTheInitializeReplyCarried()
    {
        var replay = await ReplayPayloadsAsync();

        var info = replay.ServerInfo;
        Assert.IsNotNull(info);
        Assert.AreEqual(56, info.Commands.Count);
        CollectionAssert.AreEqual(
            new[] { "claude", "claude-code-guide", "Explore", "general-purpose", "Plan", "statusline-setup" },
            info.Agents.Select(agent => agent.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "default", "opus[1m]", "claude-fable-5-1[1m]", "sonnet", "haiku" },
            info.Models.Select(model => model.Value).ToArray());
        Assert.AreEqual("default", info.OutputStyle);
        CollectionAssert.AreEqual(
            new[] { "default", "Proactive", "Concise", "Explanatory", "Learning" },
            info.AvailableOutputStyles.ToArray());
        Assert.AreEqual(1476860L, info.ProcessId);
        Assert.AreEqual("default", info.PermissionMode);
        Assert.AreEqual("idle", info.SessionState);
    }

    [TestMethod]
    public async Task ThePayloadsFixtureReportsTheContextUsageOfTheOneTurn()
    {
        var replay = await ReplayPayloadsAsync();

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(CommandLifecycleMessage),
                typeof(CommandLifecycleMessage),
                typeof(SystemInitMessage),
                typeof(RateLimitMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(CommandLifecycleMessage),
                typeof(ResultMessage),
            },
            replay.Turn.Select(message => message.GetType()).ToArray());
        CollectionAssert.AreEqual(
            new[] { "queued", "started", "completed" },
            replay.Turn.OfType<CommandLifecycleMessage>().Select(lifecycle => lifecycle.State.ToString()).ToArray());
        Assert.AreEqual("ok", ((ResultMessage)replay.Turn[^1]).Result);

        var usage = replay.Usage;
        Assert.AreEqual("claude-haiku-4-5-20251001", usage.Model);
        Assert.AreEqual(1263L, usage.TotalTokens);
        Assert.AreEqual(200000L, usage.MaxTokens);
        Assert.AreEqual(1d, usage.Percentage);
        Assert.IsTrue(usage.IsAutoCompactEnabled);
        Assert.AreEqual(167000L, usage.AutoCompactThreshold);
        CollectionAssert.AreEquivalent(
            new Dictionary<string, long?> { ["MCP tools"] = 126, ["Messages"] = 1138, ["Free space"] = 198736 },
            usage.Categories.ToDictionary(category => category.Name, category => category.Tokens));
    }

    // Both servers the call configured, and no other: the stdio one as the binary's own, the
    // in-process one as the SDK's, each connected with its one tool.
    [TestMethod]
    public async Task ThePayloadsFixtureReportsTheStdioAndTheInProcessServerConnectedWithOneToolEach()
    {
        var replay = await ReplayPayloadsAsync();

        var init = replay.Turn.OfType<SystemInitMessage>().Single();
        CollectionAssert.AreEqual(new[] { "mcp__calc__add", "mcp__echo__echo" }, init.Tools.ToArray());
        CollectionAssert.AreEqual(new[] { "echo", "calc" }, init.McpServers.Select(server => server.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "connected", "connected" }, init.McpServers.Select(server => server.Status.ToString()).ToArray());

        CollectionAssert.AreEqual(new[] { "echo", "calc" }, replay.Status.Select(server => server.Name).ToArray());

        var echo = replay.Status[0];
        Assert.AreEqual("connected", echo.Status);
        Assert.AreEqual("1.0.0", echo.ServerInfo?.Version);
        Assert.AreEqual("/tmp/claude-agent-fixtures/cwd/echo-server", ((McpStdioServerConfiguration)echo.Config!).Command);
        Assert.AreEqual(McpServerSource.Dynamic, echo.Scope);
        Assert.AreEqual(McpServerSource.Dynamic, echo.Source);
        CollectionAssert.AreEqual(new[] { "echo" }, echo.Tools.Select(tool => tool.Name).ToArray());

        var calc = replay.Status[1];
        Assert.AreEqual("connected", calc.Status);
        Assert.AreEqual("1.0.0", calc.ServerInfo?.Version);
        Assert.IsNull(calc.Config);
        Assert.AreEqual(McpServerSource.Dynamic, calc.Scope);
        Assert.AreEqual(McpServerSource.Sdk, calc.Source);
        CollectionAssert.AreEqual(new[] { "add" }, calc.Tools.Select(tool => tool.Name).ToArray());
    }

    [TestMethod]
    public async Task SettingThePermissionModeIsAnsweredAndTheBinaryAnnouncesTheNewModeInAStatusFrame()
    {
        var replay = await ReplayPayloadsAsync();

        Assert.IsInstanceOfType<SystemMessage>(replay.AfterMode);
        var status = (SystemMessage)replay.AfterMode;
        Assert.AreEqual("status", status.Subtype);
        Assert.AreEqual("acceptEdits", status.Line().GetProperty("permissionMode").GetString());
    }

    [TestMethod]
    public async Task ThePayloadsFixtureWritesTheInitializeTheBridgesThreeRepliesThePromptAndTheThreeRequests()
    {
        var replay = await ReplayPayloadsAsync();

        CollectionAssert.AreEqual(
            Fixture.Load("payloads").Stdin.Anonymised(),
            replay.Written.Anonymised());
    }

    // The session the recorder drove: the reply to initialize, one turn, then context usage, MCP
    // status and a permission mode, in that order. The in-process server's handshake runs beside
    // the prompt: the recording has the bridge's first reply ahead of the prompt and the other two
    // after it, and the play puts each request where its reply lands so.
    private static async Task<PayloadsReplay> ReplayPayloadsAsync()
    {
        await using var server = FixtureServer.Start("payloads");
        using var host = TestHost.Start(server, options =>
        {
            options.WorkingDirectory = RecordedCwd;
            options.SettingSources = [];
            options.Model = "haiku";
            options.MaxTurns = 1;
            options.Effort = Effort.Low;
            options.Tools = [];
            options.McpServers["calc"] = McpServerConfiguration.InProcess(new Calc());
            options.McpServers["echo"] = McpServerConfiguration.Stdio($"{RecordedCwd}/echo-server");
            options.OnlyConfiguredMcpServers = true;
        });

        await using var session = await ConnectAsync(host);
        var info = session.ServerInfo;

        var turn = await DrainAsync(session.SendAsync(PayloadsPrompt));
        var usage = await session.GetContextUsageAsync();
        var status = await session.GetMcpStatusAsync();
        await session.SetPermissionModeAsync(PermissionMode.AcceptEdits);
        var afterMode = await NextAsync(session);

        await server.WaitForWrites(Fixture.Load("payloads").Stdin.Length, Patience);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        return new PayloadsReplay(info, turn, usage, status, afterMode, server.Written);
    }

    private static async Task<AgentMessage> NextAsync(ClaudeAgentSession session)
    {
        await foreach (var message in session.ReceiveMessagesAsync())
        {
            return message;
        }

        throw new AssertFailedException("the binary sent nothing more.");
    }

    private static Task<ClaudeAgentSession> ConnectAsync(TestHost host) => host.Client.StartSessionAsync();

    // What the recorder asked of the client session: the preset prompt, which the initialize
    // request says keeps its dynamic sections.
    private static void AsTheClientFixture(ClaudeAgentSessionOptions options) =>
        options.SystemPrompt = AgentSystemPrompt.Preset("Be terse.");

    private static async Task<List<AgentMessage>> DrainAsync(ClaudeAgentRun run)
    {
        var seen = new List<AgentMessage>();
        await foreach (var message in run)
        {
            seen.Add(message);
        }

        return seen;
    }

    private sealed record PayloadsReplay(
        ServerInfo? ServerInfo,
        List<AgentMessage> Turn,
        ContextUsage Usage,
        IReadOnlyList<McpServerStatus> Status,
        AgentMessage AfterMode,
        ImmutableArray<string> Written);
}
