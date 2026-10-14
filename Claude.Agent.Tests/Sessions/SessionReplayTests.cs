using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Sessions;

[TestClass]
public sealed class SessionReplayTests
{
    private const string ToolUsePrompt = "Read notes.txt in the working directory and reply with its first line only.";
    private const string DefaultsPrompt = "Reply with exactly one word: ok";
    private const string RecordedCwd = "/tmp/claude-agent-fixtures/cwd";

    [TestMethod]
    public async Task ToolUseFixtureStreamsTheAssistantTheToolResultAndTheAnswer()
    {
        await using var server = FixtureServer.Start("tool-use");
        using var host = TestHost.Start(server);
        var agent = host.Client;

        var seen = new List<AgentMessage>();
        await foreach (var message in agent.QueryAsync(ToolUsePrompt))
        {
            seen.Add(message);
        }

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(RateLimitMessage),
                typeof(SystemInitMessage),
                typeof(ThinkingTokensMessage),
                typeof(ThinkingTokensMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(ToolCallMessage),
                typeof(UserMessage),
                typeof(ToolCallMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(ResultMessage),
            },
            seen.ConvertAll(message => message.GetType()));

        var toolUse = ((AssistantMessage)seen[5]).ToolUses.Single();
        Assert.AreEqual("Read", toolUse.Name);
        Assert.AreEqual(ToolCallStatus.Started, ((ToolCallMessage)seen[6]).ToolCall.Status);

        var toolResult = ((UserMessage)seen[7]).Content.OfType<ToolResultBlock>().Single();
        Assert.AreEqual(toolUse.Id, toolResult.ToolUseId);
        Assert.AreEqual(ToolCallStatus.Answered, ((ToolCallMessage)seen[8]).ToolCall.Status);
        Assert.AreEqual(toolUse.Id, ((ToolCallMessage)seen[8]).ToolCall.Id);
        Assert.AreEqual("first line", ((AssistantMessage)seen[10]).Text);
        await AssertWroteTheRecordedStdin(server, "tool-use");
    }

    [TestMethod]
    public async Task StreamingFixtureYieldsEveryPartialMessageAsAStreamEvent()
    {
        await using var server = FixtureServer.Start("streaming");
        using var host = TestHost.Start(
            server,
            options: options => options.IncludePartialMessages = true);
        var agent = host.Client;

        var seen = new List<AgentMessage>();
        await foreach (var message in agent.QueryAsync("What is a mint?"))
        {
            seen.Add(message);
        }

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        Assert.AreEqual(25, seen.OfType<StreamEventMessage>().Count());
        Assert.AreEqual(5, seen.TakeWhile(message => message is not AssistantMessage).OfType<StreamEventMessage>().Count());
        Assert.AreEqual(22, seen.Take(seen.FindLastIndex(message => message is AssistantMessage)).OfType<StreamEventMessage>().Count());
        await AssertWroteTheRecordedStdin(server, "streaming");
    }

    // The binary as it ships: no tools named, so the init frame lists the ones 2.1.276 offers by
    // default. Glob, Grep and TodoWrite are not among them.
    [TestMethod]
    public async Task TheDefaultsFixtureListsTheToolsTheBinaryOffersWhenTheCallNamesNone()
    {
        await using var server = FixtureServer.Start("defaults");
        using var host = TestHost.Start(server, options =>
        {
            options.WorkingDirectory = RecordedCwd;
            options.SettingSources = [];
            options.Model = "haiku";
            options.MaxTurns = 1;
        });

        var seen = new List<AgentMessage>();
        await foreach (var message in host.Client.QueryAsync(DefaultsPrompt))
        {
            seen.Add(message);
        }

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(SystemInitMessage),
                typeof(RateLimitMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(ResultMessage),
            },
            seen.ConvertAll(message => message.GetType()));

        var init = (SystemInitMessage)seen[0];
        Assert.AreEqual("2.1.276", init.ClaudeCodeVersion);
        CollectionAssert.AreEqual(
            new[]
            {
                "Task", "Artifact", "ArtifactComments", "ArtifactData", "Bash", "CronCreate", "CronDelete",
                "CronList", "DesignSync", "Edit", "EnterWorktree", "ExitWorktree", "ListAgents", "Monitor",
                "NotebookEdit", "PushNotification", "Read", "RemoteTrigger", "ReportFindings", "ScheduleWakeup",
                "SendMessage", "Skill", "TaskCreate", "TaskGet", "TaskList", "TaskOutput", "TaskStop",
                "TaskUpdate", "ToolSearch", "WebFetch", "WebSearch", "Workflow", "Write",
            },
            init.Tools.ToArray());

        // The login's own connector, not yet connected when the init frame went out.
        var connector = init.McpServers.Single();
        Assert.AreEqual("claude.ai Claude Docs", connector.Name);
        Assert.AreEqual("pending", connector.Status);

        Assert.IsInstanceOfType<ThinkingBlock>(((AssistantMessage)seen[2]).Content.Single());
        Assert.AreEqual("ok", ((AssistantMessage)seen[3]).Text);
        Assert.AreEqual("ok", ((ResultMessage)seen[4]).Result);
        await AssertWroteTheRecordedStdin(server, "defaults");
    }

    private static async Task AssertWroteTheRecordedStdin(FixtureServer server, string fixture)
    {
        var recorded = Fixture.Load(fixture).Stdin.Anonymised();
        await server.WaitForWrites(recorded.Length, TimeSpan.FromSeconds(10));
        CollectionAssert.AreEqual(recorded, server.Written.Anonymised());
    }
}
