using System.Collections.Immutable;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Sessions;

[TestClass]
public sealed class ConversationResetReplayTests
{
    private const string FixtureName = "conversation-reset";
    private const string SayOne = "Say: one";
    private const string Clear = "/clear";
    private const string SayTwo = "Say: two";
    private const string FirstSessionId = "75ff3a10-c557-40c1-ac04-2f0db4abcccc";
    private const string SecondSessionId = "055b1413-6646-4506-a3fb-5ec0aed1bcfe";
    private const string NewConversationId = "5504c539-1d7c-4409-bc22-7f8ed6218ff6";
    private const string ResetUuid = "170dd46a-4217-4045-bab9-139554514260";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task ClearAnswersWithAConversationResetASecondInitAndAResultThatCountsNoTurns()
    {
        await using var server = FixtureServer.Start(FixtureName);
        using var host = TestHost.Start(server, options: Recording);
        await using var session = await ConnectAsync(host);

        var first = await SendAsync(session, SayOne);

        var cleared = await SendAsync(session, Clear);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        Assert.AreEqual("completed", ((ResultMessage)first[^1]).TerminalReason);
        // The /clear prompt's own frames: queued and started, the reset before the init, and its
        // completed frame placed before the result, which is last.
        CollectionAssert.AreEqual(
            new[]
            {
                typeof(CommandLifecycleMessage),
                typeof(CommandLifecycleMessage),
                typeof(ConversationResetMessage),
                typeof(SystemInitMessage),
                typeof(CommandLifecycleMessage),
                typeof(ResultMessage),
            },
            cleared.ConvertAll(message => message.GetType()));
        CollectionAssert.AreEqual(
            new[] { "queued", "started", "completed" },
            cleared.OfType<CommandLifecycleMessage>().Select(lifecycle => lifecycle.State.ToString()).ToArray());

        var reset = (ConversationResetMessage)cleared[2];
        Assert.AreEqual(NewConversationId, reset.NewConversationId);
        Assert.AreEqual(ResetUuid, reset.Uuid);
        Assert.AreEqual(FirstSessionId, reset.SessionId);

        var reborn = (SystemInitMessage)cleared[3];
        Assert.AreEqual(SecondSessionId, reborn.SessionId);
        Assert.AreNotEqual(reset.NewConversationId, reborn.SessionId);

        var turnless = (ResultMessage)cleared[^1];
        Assert.AreEqual("success", turnless.Subtype);
        Assert.AreEqual(SecondSessionId, turnless.SessionId);
        Assert.AreEqual(0, turnless.TurnCount);
        Assert.AreEqual(0m, turnless.TotalCostUsd);
        Assert.AreEqual(string.Empty, turnless.Result);

        // The binary's empty "modelUsage":{} reaches this side as no map at all.
        Assert.AreEqual(0, turnless.ModelUsage.Count);
        Assert.IsNull(turnless.StopReason);
        Assert.IsNull(turnless.TerminalReason);
    }

    // The recording ends after the third prompt's result, before a completed frame: the session
    // ends there, and the run completes with its trace, the result last.
    [TestMethod]
    public async Task SayTwoAfterTheResetCommitsOneMoreTurnAndTheSessionKeepsTheSecondSessionId()
    {
        await using var server = FixtureServer.Start(FixtureName);
        using var host = TestHost.Start(server, options: Recording);
        await using var session = await ConnectAsync(host);

        await SendAsync(session, SayOne);

        await SendAsync(session, Clear);

        var second = await SendAsync(session, SayTwo);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        CollectionAssert.AreEqual(
            new[]
            {
                typeof(CommandLifecycleMessage),
                typeof(CommandLifecycleMessage),
                typeof(SystemInitMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(ResultMessage),
            },
            second.ConvertAll(message => message.GetType()));

        Assert.AreEqual("two", second.OfType<AssistantMessage>().Last().Text);
        Assert.AreEqual("two", ((ResultMessage)second[^1]).Result);
        Assert.AreEqual(1, ((ResultMessage)second[^1]).TurnCount);
        Assert.AreEqual(SecondSessionId, session.Id);
    }

    [TestMethod]
    public async Task TheSessionRunsTheRecordedCommandLineAndSendsTheInitializeAndTheThreePromptsInOrder()
    {
        await using var server = FixtureServer.Start(FixtureName);
        using var host = TestHost.Start(server, options: Recording);
        await using var session = await ConnectAsync(host);

        await SendAsync(session, SayOne);

        await SendAsync(session, Clear);

        await SendAsync(session, SayTwo);

        await server.WaitForWrites(4, Patience);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");

        var recorded = Fixture.Load(FixtureName);
        CollectionAssert.AreEqual(
            recorded.Stdin.Anonymised(),
            server.Written.Anonymised());

        var asked = host.Transports.Requests.Single();
        CollectionAssert.AreEqual(recorded.Argv.ToArray(), asked.Argv.ToArray(), $"{FixtureName}.argv.json");
    }

    private static void Recording(ClaudeAgentSessionOptions options)
    {
        options.WorkingDirectory = "/tmp/claude-agent-fixtures/cwd";
        options.SettingSources = [];
        options.Model = "haiku";
        options.MaxTurns = 1;
        options.Effort = Effort.Low;
    }

    private static Task<ClaudeAgentSession> ConnectAsync(TestHost host) => host.Client.StartSessionAsync();

    private static async Task<List<AgentMessage>> SendAsync(ClaudeAgentSession session, string prompt)
    {
        var seen = new List<AgentMessage>();
        await foreach (var message in session.SendAsync(prompt))
        {
            seen.Add(message);
        }

        return seen;
    }
}
