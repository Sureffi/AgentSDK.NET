using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Sessions;

// The agents fixture: one agent declared in the initialize request, naming the stdio server it may
// use. The binary lists it and starts it through the Agent tool, in the background; it calls that
// server's tool, and the binary opens a continuation turn of its own to report the answer.
[TestClass]
public sealed class AgentsReplayTests
{
    private const string RecordedCwd = "/tmp/claude-agent-fixtures/cwd";
    private const string Prompt = "Use the Agent tool with subagent_type bee and the prompt: pong. Then reply with exactly the subagent's answer.";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task TheDeclaredAgentIsListedByTheInitFrameAndStartedByName()
    {
        var messages = await RunAsync();

        // Two init frames: the turn's, and the continuation's.
        var inits = messages.OfType<SystemInitMessage>().ToArray();
        Assert.AreEqual(2, inits.Length);
        CollectionAssert.Contains(inits[0].Agents.ToArray(), "bee");

        var call = messages.OfType<AssistantMessage>().SelectMany(message => message.ToolUses).First();
        Assert.AreEqual("Agent", call.Name);
        Assert.AreEqual("bee", call.RawInput.GetProperty("subagent_type").GetString());

        var started = messages.OfType<TaskStartedMessage>().First();
        Assert.AreEqual(TaskType.LocalAgent, started.TaskType);
        Assert.AreEqual(call.Id, started.ToolUseId);
        Assert.AreEqual(TaskNotificationStatus.Completed, messages.OfType<TaskNotificationMessage>().Last().Status);
    }

    [TestMethod]
    public async Task TheAgentCallsTheServerItNamesAndItsAnswerIsTheResult()
    {
        var messages = await RunAsync();

        var echo = messages.OfType<AssistantMessage>()
            .Where(message => message.ParentToolUseId is not null)
            .SelectMany(message => message.ToolUses)
            .Single();
        Assert.AreEqual("mcp__echo__echo", echo.Name);
        Assert.AreEqual("pong", echo.RawInput.GetProperty("text").GetString());

        // Two results: the turn that launched the agent, then the continuation that carries its answer.
        var results = messages.OfType<ResultMessage>().ToArray();
        Assert.AreEqual(2, results.Length);
        Assert.AreEqual("pong", results[1].Result);
        Assert.AreSame(results[1], messages[^1]);
    }

    // The one-shot run is both turns, and pairs the subagent's call under the Agent call like any
    // other; the first result closes nothing, the Agent call being answered before it.
    [TestMethod]
    public async Task TheSubagentsCallIsPairedUnderTheAgentCallAndEveryCallClosesOnceBeforeTheFirstResult()
    {
        var messages = await RunAsync();

        var agent = messages.OfType<AssistantMessage>().SelectMany(message => message.ToolUses).First();
        var calls = messages.OfType<ToolCallMessage>().Select(message => message.ToolCall).ToArray();
        CollectionAssert.AreEqual(
            new[] { "Agent Started", "mcp__echo__echo Started", "mcp__echo__echo Answered", "Agent Answered" }.Order().ToArray(),
            calls.Select(call => $"{call.Name} {call.Status}").Order().ToArray());

        var echo = calls.Where(call => call.Name == "mcp__echo__echo").ToArray();
        Assert.IsTrue(echo.All(call => call.ParentToolUseId == agent.Id));
        Assert.IsTrue(calls.Where(call => call.Name == "Agent").All(call => call.ParentToolUseId is null));
        Assert.IsFalse(calls.Any(call => call.Status == ToolCallStatus.Unanswered));

        var firstResult = messages.FindIndex(message => message is ResultMessage);
        Assert.AreEqual(4, messages.Take(firstResult).OfType<ToolCallMessage>().Count());
    }

    // The agent names a server and the session allows only configured ones: the binary blocks the
    // agent's own connection and says so, and the agent reaches the tool through the session's.
    [TestMethod]
    public async Task UnderOnlyConfiguredServersTheBinarySaysTheAgentsServerWasBlocked()
    {
        var messages = await RunAsync();

        var notice = messages.OfType<SystemMessage>().Single(message => message.Subtype == "notification");
        Assert.AreEqual("bee agent MCP server blocked by --strict-mcp-config: echo", notice.Line().GetProperty("text").GetString());
    }

    [TestMethod]
    public async Task TheInitializeCarriesTheAgentAsRecordedOnTheRecordedArgv()
    {
        await using var server = FixtureServer.Start("agents");
        using var host = Start(server);

        await foreach (var _ in host.Client.QueryAsync(Prompt))
        {
        }

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        var fixture = Fixture.Load("agents");
        await server.WaitForWrites(fixture.Stdin.Length, Patience);
        CollectionAssert.AreEqual(
            fixture.Stdin.Anonymised(),
            server.Written.Anonymised());
        StringAssert.Contains(fixture.Stdin[0], "\"mcpServers\": [\"echo\"]");
        CollectionAssert.AreEqual(fixture.Argv.ToArray(), host.Transports.Requests.Single().Argv.ToArray());
    }

    // Finding page:1. The recording, made on 2.1.274 through a query that wrote its prompt without a
    // uuid, names no prompt; played to a session it is a binary that does not name them, and no run
    // takes a turn: both turns, the one the prompt asked for and the one the binary opened itself, reach
    // what no run reads, each result there; the run fails without its result when the session ends,
    // and a prompt sent after it is never written. stdinDiffers: the recording is a query's, and a
    // session writes its prompt with a uuid.
    [TestMethod]
    public async Task InASessionABinaryThatNamesNoPromptLeavesTheRunWithoutATurnAndBothTurnsGoToWhatNoRunReads()
    {
        await using var server = FixtureServer.Start("agents", stdinDiffers: true);
        using var host = Start(server);
        await using var session = await host.Client.StartSessionAsync();

        var run = session.SendAsync(Prompt);
        var next = session.SendAsync("Say: two");
        var unclaimed = new List<AgentMessage>();
        using var patience = new CancellationTokenSource(Patience);
        await foreach (var message in session.ReceiveMessagesAsync(patience.Token))
        {
            unclaimed.Add(message);
        }

        var results = unclaimed.OfType<ResultMessage>().ToArray();
        Assert.AreEqual(2, results.Length);
        StringAssert.StartsWith(results[0].Result, "I've launched the bee agent");
        Assert.AreEqual("pong", results[1].Result);
        Assert.AreSame(results[1], unclaimed[^1]);
        Assert.AreEqual(2, unclaimed.OfType<SystemInitMessage>().Count());

        var failure = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => run.WaitForCompletionAsync().WaitAsync(Patience));
        Assert.AreEqual(0, failure.Messages.Count);
        await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => next.WaitForCompletionAsync().WaitAsync(Patience));
        Assert.AreEqual(2, server.Written.Length, "the initialize and the first prompt");
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
    }

    private static async Task<List<AgentMessage>> RunAsync()
    {
        await using var server = FixtureServer.Start("agents");
        using var host = Start(server);

        var messages = new List<AgentMessage>();
        await foreach (var message in host.Client.QueryAsync(Prompt))
        {
            messages.Add(message);
        }

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        return messages;
    }

    private static TestHost Start(FixtureServer server) => TestHost.Start(server, options: options =>
    {
        options.WorkingDirectory = RecordedCwd;
        options.SettingSources = [];
        options.Model = "haiku";
        options.Tools = ["Agent"];
        options.AllowedTools.Add("Agent");
        options.AllowedTools.Add("mcp__echo__echo");
        options.MaxTurns = 3;
        options.Effort = Effort.Low;
        options.McpServers["echo"] = McpServerConfiguration.Stdio($"{RecordedCwd}/echo-server");
        options.OnlyConfiguredMcpServers = true;
        options.Agents["bee"] = new AgentDefinition(
            "Echoes a word through the echo tool and reports what came back.",
            "You are bee. Call the echo tool with the text you are given, then reply with exactly what it returned.")
        {
            Tools = ["mcp__echo__echo"],
            Model = "haiku",
            McpServers = ["echo"],
            MaxTurns = 3,
        };
    });
}
