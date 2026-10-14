using System.Collections.Immutable;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Sessions;

[TestClass]
public sealed class SessionFailureTests
{
    [TestMethod]
    public async Task AProcessThatDiesAfterTheInitFrameEndsTheStreamWithItsExitCode()
    {
        var script = new Script(
            [Script.AnswerInitialize, new EmitLine(Fixture.Line("trivial", "system")), new HangUp()],
            ExitCode: 1);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        await using var session = await ConnectAsync(host);

        var (seen, failure) = await DrainAsync(session);

        Assert.IsInstanceOfType<SystemInitMessage>(seen.Single());
        Assert.IsNotNull(failure);
        Assert.AreEqual(ErrorCodes.Process, failure.ErrorCode);
        Assert.AreEqual(1, failure.ExitCode);
    }

    // The result decides: an error result is read where it arrived, and a process dying after it
    // fails as a process, with its exit code and nothing of that result, which is not the failure's
    // to carry again. The recorded order is played: the binary closes every prompted turn with a
    // command_lifecycle frame right after its result.
    [TestMethod]
    public async Task AProcessThatDiesAfterAnErrorResultFailsWithItsExitCodeAndNothingOfThatResult()
    {
        var client = Fixture.Load("client");
        var errorResult = client.Stdout.Index().Last(each => Fixture.TypeOf(each.Item) == "result");
        var closing = client.Stdout[errorResult.Index + 1];
        Assert.AreEqual("command_lifecycle", Fixture.TypeOf(closing), "the recording does not close that prompt after its result.");

        var script = new Script(
            [
                Script.AnswerInitialize,
                new EmitLine(Fixture.Line("client", "system")),
                new EmitLine(errorResult.Item),
                new EmitLine(closing),
                new HangUp(),
            ],
            ExitCode: 1);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        await using var session = await ConnectAsync(host);

        var (seen, failure) = await DrainAsync(session);

        Assert.AreEqual(3, seen.Length);
        Assert.AreEqual("cancelled", ((CommandLifecycleMessage)seen[^1]).State);
        Assert.AreEqual("error_during_execution", seen.OfType<ResultMessage>().Single().Subtype);
        Assert.IsNotNull(failure);
        Assert.AreEqual(ErrorCodes.Process, failure.ErrorCode);
        Assert.AreEqual(1, failure.ExitCode);
        Assert.IsFalse(failure.Message.Contains("ede_diagnostic", StringComparison.Ordinal), failure.Message);
        Assert.IsNull(failure.Result);
    }

    // The same across two runs: the first ends with its error result and completes with it; the
    // process dies while the second waits for its own, and the second fails as a process, with the
    // exit code and nothing of the first run's result. The client fixture's interrupted turn is
    // played as the first run's, without the interrupt's reply, since nothing here asks for it.
    [TestMethod]
    public async Task ARunWaitingWhenTheProcessDiesAfterAnotherRunsErrorResultFailsWithoutThatResult()
    {
        var client = Fixture.Load("client");
        var interrupted = client.Stdout[21..30].Where(line => Fixture.TypeOf(line) != "control_response").ToArray();
        Assert.AreEqual("result", Fixture.TypeOf(interrupted[^2]), "the recording's interrupted turn does not end in its result.");

        var script = new Script(
            [
                Script.AnswerInitialize,
                new AwaitPrompt(1),
                .. interrupted.Select(line => new EmitLine(line)),
                new AwaitPrompt(2),
                new HangUp(),
            ],
            ExitCode: 1)
        {
            Prompts = [Fixture.UuidOf(client.Stdin[7]), null],
        };

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        await using var session = await ConnectAsync(host);

        var first = await session.SendAsync("Count slowly from 1 to 200, one number per line, no other text.")
            .WaitForCompletionAsync()
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.AreEqual(ResultMessageSubtype.ErrorDuringExecution, first.Result.Subtype);

        var second = session.SendAsync("Say: two");
        var failure = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(
            () => second.WaitForCompletionAsync().WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.AreEqual(ErrorCodes.Process, failure.ErrorCode);
        Assert.AreEqual(1, failure.ExitCode);
        Assert.IsFalse(failure.Message.Contains("ede_diagnostic", StringComparison.Ordinal), failure.Message);
        Assert.IsNull(failure.Result);
        Assert.AreEqual(0, failure.Messages.OfType<ResultMessage>().Count());
    }

    // Decided 2026-09-20: the result decides what an exit means, and
    // a non-zero exit at a close the caller began is no failure of the session's. What no run reads
    // ends as it would have ended without the exit.
    [TestMethod]
    public async Task ASessionClosedByItsCallerEndsWhatNoRunReadsCleanlyThoughTheProcessExitsNonZero()
    {
        var script = new Script(
            [Script.AnswerInitialize, new EmitLine(Fixture.Line("trivial", "system"))],
            ExitCode: 1,
            AwaitStdinEnd: true);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        var session = await ConnectAsync(host);

        var reading = DrainAsync(session);
        await session.CloseAsync();
        var (seen, failure) = await reading;

        Assert.IsNull(failure, $"the caller's own close threw: {failure?.Message}");
        Assert.IsInstanceOfType<SystemInitMessage>(seen.Single());
    }

    // The same exit, with a run in its stretch: the run fails for the close, not for the exit code.
    [TestMethod]
    public async Task ARunInFlightAtItsCallersCloseFailsForTheCloseNotForTheProcessesExitCode()
    {
        var reset = Fixture.Load("conversation-reset");
        var script = new Script(
            [Script.AnswerInitialize, new AwaitPrompt(1), .. reset.Stdout[1..4].Select(line => new EmitLine(line))],
            ExitCode: 1,
            AwaitStdinEnd: true)
        {
            Prompts = Fixture.PromptUuids(reset.Stdin),
        };

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        var session = await ConnectAsync(host);

        var run = session.SendAsync("Say: one");
        await server.WaitForWrites(2, TimeSpan.FromSeconds(10));
        await session.CloseAsync();

        var failure = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(
            () => run.WaitForCompletionAsync().WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.AreEqual(ErrorCodes.Control, failure.ErrorCode);
        Assert.AreEqual("The session was closed before the run's result.", failure.Message);
        Assert.IsNull(failure.ExitCode);
    }

    // The same exit again: a prompt sent after the caller's close reports the closed session, as the
    // control verbs do, and not the process's exit code.
    [TestMethod]
    public async Task ASendAfterItsCallersCloseReportsTheClosedSessionAsTheControlVerbsDo()
    {
        var script = new Script(
            [Script.AnswerInitialize, new EmitLine(Fixture.Line("trivial", "system"))],
            ExitCode: 1,
            AwaitStdinEnd: true);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        var session = await ConnectAsync(host);

        await session.CloseAsync();

        var sent = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(
            () => session.SendAsync("Say: one").WaitForCompletionAsync().WaitAsync(TimeSpan.FromSeconds(10)));
        var verb = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => session.SetModelAsync("haiku"));

        foreach (var failure in new[] { sent, verb })
        {
            Assert.AreEqual(ErrorCodes.Control, failure.ErrorCode);
            Assert.AreEqual("session is closed", failure.Message);
            Assert.IsNull(failure.ExitCode);
        }
    }

    [TestMethod]
    public async Task AMalformedLineIsRejectedAndTheStreamCarriesOn()
    {
        // The client recording's first prompt, from its queued frame to its completed one, with a
        // malformed line after the init.
        var client = Fixture.Load("client");
        var recorded = client.Stdout[1..9];
        var script = new Script(
        [
            Script.AnswerInitialize,
            new AwaitPrompt(1),
            .. recorded.Take(3).Select(line => new EmitLine(line)),
            new EmitLine("""{"type": "assistant", "message": {"""),
            .. recorded.Skip(3).Select(line => new EmitLine(line)),
            new HangUp(),
        ])
        {
            Prompts = Fixture.PromptUuids(client.Stdin),
        };

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        await using var session = await ConnectAsync(host);

        var (seen, failure) = await DrainAsync(session.SendAsync("Say: one"));

        Assert.IsNull(failure);
        Assert.AreEqual(recorded.Length, seen.Length);
        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        Assert.AreEqual(1, session.Runtime.RejectedFrames.Count);
        Assert.AreEqual(ParseErrorKind.MalformedJson, session.Runtime.RejectedFrames[0].Kind);
    }

    private static Task<ClaudeAgentSession> ConnectAsync(TestHost host) => host.Client.StartSessionAsync();

    private static Task<(ImmutableArray<AgentMessage> Seen, ClaudeAgentException? Failure)> DrainAsync(
        ClaudeAgentSession session) => DrainAsync(session.ReceiveMessagesAsync());

    private static async Task<(ImmutableArray<AgentMessage> Seen, ClaudeAgentException? Failure)> DrainAsync(
        IAsyncEnumerable<AgentMessage> messages)
    {
        var seen = ImmutableArray.CreateBuilder<AgentMessage>();
        ClaudeAgentException? failure = null;

        try
        {
            await foreach (var message in messages)
            {
                seen.Add(message);
            }
        }
        catch (ClaudeAgentException caught)
        {
            failure = caught;
        }

        return (seen.ToImmutable(), failure);
    }
}
