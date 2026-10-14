using System.Collections.Immutable;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Sessions;

// The continuation recording, 2.1.276: the first prompt starts a background shell; when it ends the
// binary opens a turn of its own to report it, and the second prompt goes out as that turn runs.
// The third does as the first, and the fourth goes out as the binary's own turn makes its first
// tool call, and the binary folds it into that turn. Each run is checked by the uuids of the frames
// it holds, against the recorded stdout's line numbers.
[TestClass]
public sealed class ContinuationReplayTests
{
    private const string RecordedCwd = "/tmp/claude-agent-fixtures/cwd";
    private const string FirstPrompt = "Run the shell command `sleep 5` with Bash in the background. Then reply with exactly the word: started";
    private const string SecondPrompt = "Say: two";
    private const string ThirdPrompt =
        "Run the shell command `sleep 5` with Bash in the background. Then reply with exactly the word: started. "
        + "When it has finished, run the shell command `wc -l notes.txt` with Bash, then reply with exactly the word: counted";
    private const string FourthPrompt = "Say: four";
    private const string CountToolUseId = "toolu_013RV11Dm71zSZGy1sttXQ3w";

    // A prompt's stretch runs from its started frame to its result; its lifecycle frames are its
    // own, and the completed frame that follows the result is placed before it.
    [TestMethod]
    public async Task ASendsRunTakesItsPromptsStretchAndNothingElse()
    {
        var replay = await ReplayAsync();

        CollectionAssert.AreEqual(Lines(2, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 16, 15), Uuids(replay.First));
        CollectionAssert.AreEqual(Lines(33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 46, 45), Uuids(replay.Third));
        Assert.AreEqual("started", replay.First.Result.Result);
        Assert.AreEqual("started", replay.Third.Result.Result);

        // The shell each turn starts is paired within that turn.
        foreach (var trace in new[] { replay.First, replay.Third })
        {
            var shell = trace.ToolCalls.Single();
            Assert.AreEqual("Bash", shell.Name);
            Assert.AreEqual(ToolCallStatus.Answered, shell.Status);
        }
    }

    // The binary's own turn after the first shell ends names no prompt and goes to what no run
    // reads, its result there. The second prompt, written as that turn runs, is queued inside it and
    // its run starts only at its started frame, after that turn's result: the result of the binary's
    // own turn never ends it.
    [TestMethod]
    public async Task TheBinarysOwnTurnGoesToWhatNoRunReadsAndAPromptSentDuringItTakesOnlyItsOwnStretch()
    {
        var replay = await ReplayAsync();

        CollectionAssert.AreEqual(Lines(21, 27, 28, 29, 30, 32, 31), Uuids(replay.Second));
        Assert.AreEqual("two", replay.Second.Result.Result);

        var ownTurn = replay.Unclaimed.Where(message => message is not ToolCallMessage).Take(10).ToList();
        CollectionAssert.AreEqual(Lines(3, 17, 18, 19, 20, 22, 23, 24, 25, 26), Uuids(ownTurn));
        var ownResult = (ResultMessage)ownTurn[^1];
        StringAssert.StartsWith(ownResult.Result, "The background task has completed successfully.");
    }

    // The fold: the binary's own turn makes a tool call, the fourth prompt is queued, the tool round
    // ends, and only then does the prompt start. The run holds from its queued frame and from its
    // started frame to the result; the completed frame that comes before the result stays in its
    // place. The binary's own frames before the fold, the tool call among them, go to what no run
    // reads, and the call is paired there.
    [TestMethod]
    public async Task TheFoldedPromptsRunHoldsFromItsStartedFrameToTheResultAndTheCallBeforeTheFoldIsPairedInWhatNoRunReads()
    {
        var replay = await ReplayAsync();

        CollectionAssert.AreEqual(Lines(53, 55, 56, 57, 58, 59, 60, 61), Uuids(replay.Fourth));
        Assert.AreEqual("counted\n\nfour", replay.Fourth.Result.Result);
        Assert.AreEqual(0, replay.Fourth.ToolCalls.Count);

        var beforeTheFold = replay.Unclaimed.Where(message => message is not ToolCallMessage).Skip(10).ToList();
        CollectionAssert.AreEqual(Lines(47, 48, 49, 50, 51, 52, 54), Uuids(beforeTheFold));

        var calls = replay.Unclaimed.OfType<ToolCallMessage>().Select(message => message.ToolCall).ToArray();
        CollectionAssert.AreEqual(new[] { ToolCallStatus.Started, ToolCallStatus.Answered }, calls.Select(call => call.Status).ToArray());
        Assert.IsTrue(calls.All(call => call.Id == CountToolUseId && call.Name == "Bash"));

        // Each snapshot right after the message that caused it.
        var unclaimed = replay.Unclaimed;
        var started = unclaimed.FindIndex(message => message is ToolCallMessage);
        Assert.AreEqual(Line(52), unclaimed[started - 1].Uuid);
        var answered = unclaimed.FindLastIndex(message => message is ToolCallMessage);
        Assert.AreEqual(Line(54), unclaimed[answered - 1].Uuid);
    }

    [TestMethod]
    public async Task TheContinuationFixtureWritesTheInitializeAndTheFourPromptsInOrderOnTheRecordedArgv()
    {
        var replay = await ReplayAsync();

        var recorded = Fixture.Load("continuation");
        CollectionAssert.AreEqual(recorded.Stdin.Anonymised(), replay.Written.Anonymised());
        CollectionAssert.AreEqual(recorded.Argv.ToArray(), replay.Argv, "continuation.argv.json");
    }

    // The session the recorder drove, one prompt after another's run has ended. The play holds each
    // line that names a prompt until the client has written it, so the binary's own turns run as
    // recorded whenever the prompts go out.
    private static async Task<ContinuationReplay> ReplayAsync()
    {
        await using var server = FixtureServer.Start("continuation");
        using var host = TestHost.Start(server, options =>
        {
            options.WorkingDirectory = RecordedCwd;
            options.SettingSources = [];
            options.Model = "haiku";
            options.MaxTurns = 4;
            options.Effort = Effort.Low;
            options.Tools = ["Bash"];
            options.AllowedTools.Add("Bash");
        });

        List<AgentMessage> unclaimed = [];
        AgentTrace first, second, third, fourth;
        await using (var session = await host.Client.StartSessionAsync())
        {
            first = await session.SendAsync(FirstPrompt).WaitForCompletionAsync();
            second = await session.SendAsync(SecondPrompt).WaitForCompletionAsync();
            third = await session.SendAsync(ThirdPrompt).WaitForCompletionAsync();
            fourth = await session.SendAsync(FourthPrompt).WaitForCompletionAsync();
            await session.CloseAsync();

            await foreach (var message in session.ReceiveMessagesAsync())
            {
                unclaimed.Add(message);
            }
        }

        await server.WaitForWrites(Fixture.Load("continuation").Stdin.Length, TimeSpan.FromSeconds(10));
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        return new ContinuationReplay(first, second, third, fourth, unclaimed, server.Written, [.. host.Transports.Requests.Single().Argv]);
    }

    private static string[] Lines(params int[] numbers) => [.. numbers.Select(Line)];

    // The uuid of the recorded stdout line of that number.
    private static string Line(int number)
    {
        var line = Fixture.Load("continuation").Stdout[number - 1];
        return Fixture.UuidOf(line) ?? throw new AssertFailedException($"continuation.stdout.jsonl line {number} carries no uuid.");
    }

    private static string?[] Uuids(AgentTrace trace) => Uuids(trace.Messages);

    private static string?[] Uuids(IEnumerable<AgentMessage> messages) =>
        [.. messages.Where(message => message is not ToolCallMessage).Select(message => message.Uuid)];

    private sealed record ContinuationReplay(
        AgentTrace First,
        AgentTrace Second,
        AgentTrace Third,
        AgentTrace Fourth,
        List<AgentMessage> Unclaimed,
        ImmutableArray<string> Written,
        string[] Argv);
}
