using System.Collections.Immutable;
using System.Net.Sockets;
using System.Text;

namespace Claude.Agent.Tests.Replay;

[TestClass]
public sealed class ReplaySmokeTests
{
    [TestMethod]
    public async Task TrivialFixture_ReplaysTheRecordedTurnAndWritesTheRecordedStdin()
    {
        await using var server = FixtureServer.Start("trivial");
        using var host = TestHost.Start(server);
        var agent = host.Client;

        var seen = new List<AgentMessage>();
        await foreach (var message in agent.QueryAsync("Reply with exactly one word: pong"))
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

        await server.WaitForWrites(2, TimeSpan.FromSeconds(10));

        CollectionAssert.AreEqual(
            Fixture.Load("trivial").Stdin.Anonymised(),
            server.Written.Anonymised());
    }

    // The comparison above is the server's own as well: a replay by name that writes another line
    // than the recording fails as the server closes, whether or not the test compares.
    [TestMethod]
    public async Task AReplayThatWritesAnotherLineThanTheRecordingFailsAsTheServerCloses()
    {
        var server = FixtureServer.Start("trivial");
        using (var host = TestHost.Start(server))
        {
            await foreach (var message in host.Client.QueryAsync("Reply with exactly one word: ping"))
            {
                _ = message;
            }
        }

        var failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(async () => await server.DisposeAsync());
        StringAssert.Contains(failure.Message, "trivial: line 2 written is not the recorded stdin");
    }

    // The comparison masks a prompt's uuid by its value and keeps the member where the recording has
    // it: a prompt written with another uuid is held to the recording, and one written without the
    // member is not. The lines go out on a socket of the test's own, as a client that dropped the
    // uuid would write them.
    [TestMethod]
    public async Task AReplayWhosePromptGoesOutWithoutItsUuidFailsAsTheServerClosesAndOneWithAnotherUuidDoesNot()
    {
        var recorded = Fixture.Load("task-background").Stdin;
        var uuid = Fixture.UuidOf(recorded[1]);
        Assert.IsNotNull(uuid);

        await using (var server = FixtureServer.Start("task-background"))
        {
            await WriteAndReadToTheEndAsync(server, recorded[0], recorded[1].Replace(uuid, Guid.NewGuid().ToString(), StringComparison.Ordinal));
            Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        }

        var without = recorded[1].Replace($", \"uuid\": \"{uuid}\"", string.Empty, StringComparison.Ordinal);
        Assert.IsNull(Fixture.UuidOf(without));

        var dropped = FixtureServer.Start("task-background");
        await WriteAndReadToTheEndAsync(dropped, recorded[0], without);

        var failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(async () => await dropped.DisposeAsync());
        StringAssert.Contains(failure.Message, "task-background: line 2 written is not the recorded stdin");
    }

    // What a client's control request made the binary do is played once the client has written it:
    // the binary's cancel of its own pending request after the interrupt that stopped the turn, and
    // the frames of a stopped task before stop_task's answer. A frame right after an answer is the
    // answered request's doing, as client's status frame is set_permission_mode's, and waits for no
    // later request.
    [TestMethod]
    public void AFrameAControlRequestCausedWaitsForThatRequestAndAFrameRightAfterAnAnswerWaitsForNoLaterOne()
    {
        var cancel = Script.OfFixture(Fixture.Load("cancel"), awaitStdinEnd: false).Steps;
        CollectionAssert.AreEqual(
            new ScriptStep[] { new AwaitClientRequest(2), new AwaitClientRequest(3) },
            StepsBefore(cancel, line => Fixture.TypeOf(line) == "control_cancel_request"));

        var task = Script.OfFixture(Fixture.Load("task"), awaitStdinEnd: false).Steps;
        CollectionAssert.AreEqual(
            new ScriptStep[] { new AwaitClientRequest(2) },
            StepsBefore(task, line => line.Contains("\"subtype\":\"background_tasks_changed\",\"tasks\":[]", StringComparison.Ordinal)));

        var client = Script.OfFixture(Fixture.Load("client"), awaitStdinEnd: false).Steps;
        var status = StepsBefore(client, line => line.Contains("\"subtype\":\"status\"", StringComparison.Ordinal)).Single();
        Assert.IsInstanceOfType<AnswerControlRequest>(status);
    }

    [TestMethod]
    public async Task ClientFixture_HoldsTheFirstTurnUntilTheClientSendsAPrompt()
    {
        var server = FixtureServer.Start("client");

        using (var host = TestHost.Start(server, options => options.SystemPrompt = AgentSystemPrompt.Preset("Be terse.")))
        {
            await using var session = await host.Client.StartSessionAsync();
            Assert.IsNotNull(session.ServerInfo);
            Assert.IsFalse(
                server.Written.Any(line => Fixture.TypeOf(line) == "user"),
                "the client sent a prompt the test never asked for.");
        }

        await server.DisposeAsync();
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
    }

    // The step the play takes right before each line `emitted` picks.
    private static ScriptStep[] StepsBefore(ImmutableArray<ScriptStep> steps, Func<string, bool> emitted) =>
        [.. steps.Select((step, at) => (step, at))
            .Where(each => each.step is EmitLine emit && emitted(emit.Line))
            .Select(each => steps[each.at - 1])];

    // Writes `lines` and ends stdin, then reads what the server plays until it hangs up.
    private static async Task WriteAndReadToTheEndAsync(FixtureServer server, params string[] lines)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(server.EndPoint);
        await using var stream = client.GetStream();
        foreach (var line in lines)
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"));
        }

        client.Client.Shutdown(SocketShutdown.Send);
        await stream.CopyToAsync(Stream.Null).WaitAsync(TimeSpan.FromSeconds(10));
    }
}
