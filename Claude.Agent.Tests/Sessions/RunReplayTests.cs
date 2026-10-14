using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Sessions;

// The run a call hands back: read one way, kept whether or not it is read, ended by its result or
// failed where it stands.
[TestClass]
public sealed class RunReplayTests
{
    private const string TrivialPrompt = "Reply with exactly one word: pong";
    private const string ToolUsePrompt = "Read notes.txt in the working directory and reply with its first line only.";
    private const string CancelledReadPrompt = "Read notes.txt in the working directory and reply with its first line only.";
    private const string CancelledReadToolUseId = "toolu_01EzYnLSq1PpXu4DCwqP16fw";
    private const string DefaultsPrompt = "Reply with exactly one word: ok";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    // How long a call that asks for a turn is given to hand back its run. The binary has not
    // answered when the call is made, so a call that waits on it fails the test here.
    private static readonly TimeSpan AtOnce = TimeSpan.FromSeconds(2);

    [TestMethod]
    public async Task EnumeratingARunAndThenAwaitingItsCompletionGiveTheSameMessagesTheResultLast()
    {
        await using var server = FixtureServer.Start("tool-use");
        using var host = TestHost.Start(server);

        var run = host.Client.QueryAsync(ToolUsePrompt);
        var seen = new List<AgentMessage>();
        await foreach (var message in run)
        {
            seen.Add(message);
        }

        var completion = run.WaitForCompletionAsync();
        Assert.IsTrue(completion.IsCompletedSuccessfully, "the completion was not there when the stream ended.");
        var trace = await completion;

        CollectionAssert.AreEqual(seen, trace.Messages.ToList());
        Assert.AreSame(seen[^1], trace.Result);
        Assert.AreEqual("first line", trace.Text);
        Assert.AreEqual(ToolCallStatus.Answered, trace.ToolCalls.Single().Status);
    }

    // The text is a view over the messages and not the result's own words: the main thread's
    // assistant text, a subagent's left out, whatever the result says.
    [TestMethod]
    public void ATracesTextIsTheMainThreadsAssistantTextAndNotTheResults()
    {
        var trace = ClaudeAgentModelFactory.AgentTrace(
        [
            ClaudeAgentModelFactory.AssistantMessage(ClaudeAgentModelFactory.ApiMessage([ClaudeAgentModelFactory.TextBlock("first")])),
            ClaudeAgentModelFactory.AssistantMessage(ClaudeAgentModelFactory.ApiMessage([ClaudeAgentModelFactory.TextBlock("aside")]), parentToolUseId: "toolu_01"),
            ClaudeAgentModelFactory.AssistantMessage(ClaudeAgentModelFactory.ApiMessage([ClaudeAgentModelFactory.TextBlock("second")])),
            ClaudeAgentModelFactory.ResultMessage(result: "the result's own words"),
        ]);

        Assert.AreEqual("first\nsecond", trace.Text);
    }

    [TestMethod]
    public async Task AwaitingARunsCompletionAloneDrainsTheStreamIntoTheTrace()
    {
        await using var server = FixtureServer.Start("tool-use");
        using var host = TestHost.Start(server);

        var trace = await host.Client.QueryAsync(ToolUsePrompt).WaitForCompletionAsync().WaitAsync(Patience);

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
            trace.Messages.Select(message => message.GetType()).ToArray());
        Assert.AreEqual("success", trace.Result.Subtype);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
    }

    [TestMethod]
    public async Task ASecondEnumerationOfARunThrowsAndSoDoesEnumeratingAfterItsCompletionWasAwaitedAlone()
    {
        await using var enumerated = FixtureServer.Start("trivial");
        await using var awaited = FixtureServer.Start("trivial");
        using var host = TestHost.Start([enumerated, awaited]);

        var once = host.Client.QueryAsync(TrivialPrompt);
        await foreach (var message in once)
        {
            _ = message;
        }

        Assert.ThrowsExactly<InvalidOperationException>(() => once.GetAsyncEnumerator());
        Assert.IsInstanceOfType<ResultMessage>((await once.WaitForCompletionAsync()).Messages[^1]);

        var alone = host.Client.QueryAsync(TrivialPrompt);
        var trace = await alone.WaitForCompletionAsync();
        Assert.ThrowsExactly<InvalidOperationException>(() => alone.GetAsyncEnumerator());
        Assert.AreEqual("pong", trace.Result.Result);
    }

    // A query hands back its run at once, its session starting behind it: here the binary reads the
    // initialize and never answers it. The query's token, cancelled, ends the start and the run.
    [TestMethod]
    public async Task AQueryHandsBackItsRunAtOnceWhileItsSessionIsStillStarting()
    {
        await using var server = FixtureServer.Play(new Script([], AwaitStdinEnd: true));
        using var host = TestHost.Start(server);
        using var cancel = new CancellationTokenSource();

        var run = await HandedBackAsync(() => host.Client.QueryAsync(TrivialPrompt, cancel.Token));

        await server.WaitForWrites(1, Patience);
        Assert.AreEqual("control_request", Fixture.TypeOf(server.Written[0]));
        await cancel.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => run.WaitForCompletionAsync().WaitAsync(Patience));
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
    }

    // A session's send hands back its run at once: here the binary answers the initialize and
    // nothing after it, and reads the prompt. The run fails for the close.
    [TestMethod]
    public async Task ASessionsSendHandsBackItsRunAtOnceWhileTheBinaryHasNotAnsweredItsPrompt()
    {
        await using var server = FixtureServer.Play(new Script([Script.AnswerInitialize], AwaitStdinEnd: true));
        using var host = TestHost.Start(server);
        await using var session = await host.Client.StartSessionAsync();

        var run = await HandedBackAsync(() => session.SendAsync(TrivialPrompt));

        await server.WaitForWrites(2, Patience);
        Assert.AreEqual("user", Fixture.TypeOf(server.Written[1]));
        await session.CloseAsync();
        var failure = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => run.WaitForCompletionAsync().WaitAsync(Patience));
        Assert.AreEqual("The session was closed before the run's result.", failure.Message);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
    }

    // Rule B. A break out of a query's loop part-way interrupts the turn: the interrupt goes out,
    // the binary answers with the interrupted turn's result, and the loop is left once the run has
    // ended with that trace, its session closed. The script is the test's own: the trivial turn's
    // init, then the interrupt answered and the client recording's interrupted result.
    [TestMethod]
    public async Task BreakingOutOfAQuerysLoopPartWayInterruptsItsTurnAndTheLoopIsLeftWithTheRunEndedWithItsInterruptedTrace()
    {
        await using var server = FixtureServer.Play(InterruptedTrivialTurn());
        using var host = TestHost.Start(server);

        var run = host.Client.QueryAsync(TrivialPrompt);
        AgentMessage? first = null;
        await foreach (var message in run)
        {
            first = message;
            break;
        }

        var completion = run.WaitForCompletionAsync();
        Assert.IsTrue(completion.IsCompletedSuccessfully, "the run had not ended when the loop was left.");
        Assert.IsTrue(server.StdinEnded, "the query's session had not closed when the loop was left.");

        var trace = await completion;
        Assert.IsInstanceOfType<SystemInitMessage>(first);
        Assert.AreSame(first, trace.Messages[0]);
        Assert.AreEqual("aborted_streaming", trace.Result.TerminalReason);
        Assert.AreSame(trace.Messages[^1], trace.Result);
        Assert.AreEqual("interrupt", RequestSubtype(server.Written[2]));
        Assert.ThrowsExactly<InvalidOperationException>(() => run.GetAsyncEnumerator());
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
    }

    // The same break against a recording that has no interrupt: the binary played back finishes the
    // turn as recorded and never answers the interrupt, and the loop is left once the run has ended
    // with the recorded trace. stdinDiffers: the break writes the interrupt the recording has not.
    [TestMethod]
    public async Task ALoopLeftPartWayWhoseInterruptIsNeverAnsweredIsLeftOnceTheTurnEndsAsRecorded()
    {
        await using var server = FixtureServer.Start("tool-use", stdinDiffers: true);
        using var host = TestHost.Start(server);

        var run = host.Client.QueryAsync(ToolUsePrompt);
        AgentMessage? first = null;
        await foreach (var message in run)
        {
            first = message;
            break;
        }

        var completion = run.WaitForCompletionAsync();
        Assert.IsTrue(completion.IsCompletedSuccessfully, "the run had not ended when the loop was left.");

        var trace = await completion;
        Assert.AreSame(first, trace.Messages[0]);
        Assert.AreEqual(12, trace.Messages.Count);
        Assert.AreSame(trace.Messages[^1], trace.Result);
        Assert.AreEqual("interrupt", RequestSubtype(server.Written[2]));
    }

    // Rule B: a loop that reads a run to its end, or leaves it at its last message, interrupts
    // nothing. What goes out is the recording's stdin, and nothing after it.
    [TestMethod]
    public async Task EnumeratingARunToItsEndAndDisposingItsEnumeratorSendsNoInterrupt()
    {
        await using var server = FixtureServer.Start("conversation-reset");
        using var host = TestHost.Start(server, AsTheConversationResetFixture);
        await using var session = await host.Client.StartSessionAsync();

        var enumerator = session.SendAsync("Say: one").GetAsyncEnumerator();
        while (await enumerator.MoveNextAsync())
        {
        }

        await enumerator.DisposeAsync();

        await foreach (var message in session.SendAsync("/clear"))
        {
            if (message is ResultMessage)
            {
                break;
            }
        }

        await session.SendAsync("Say: two").WaitForCompletionAsync().WaitAsync(Patience);

        await server.WaitForWrites(4, Patience);
        CollectionAssert.AreEqual(Fixture.Load("conversation-reset").Stdin.Anonymised(), server.Written.Anonymised());
    }

    [TestMethod]
    public async Task EnumeratingAQueryToItsEndAndDisposingItsEnumeratorSendsNoInterrupt()
    {
        await using var server = FixtureServer.Start("trivial");
        using var host = TestHost.Start(server);

        var enumerator = host.Client.QueryAsync(TrivialPrompt).GetAsyncEnumerator();
        while (await enumerator.MoveNextAsync())
        {
        }

        await enumerator.DisposeAsync();

        CollectionAssert.AreEqual(Fixture.Load("trivial").Stdin.Anonymised(), server.Written.Anonymised());
    }

    // Rule C. A query asked for no hook, no permission callback and no tool server keeps its stdin
    // open until the result that ends its turn, so its own interrupt is written, and the turn it
    // stops completes with its trace.
    [TestMethod]
    public async Task AnInterruptOnAPlainQueryIsWrittenAndItsTurnCompletesWithItsTrace()
    {
        await using var server = FixtureServer.Play(InterruptedTrivialTurn());
        using var host = TestHost.Start(server);

        var run = host.Client.QueryAsync(TrivialPrompt);
        var seen = new List<AgentMessage>();
        await foreach (var message in run)
        {
            seen.Add(message);
            if (message is SystemInitMessage)
            {
                await run.InterruptAsync().WaitAsync(Patience);
            }
        }

        var trace = await run.WaitForCompletionAsync();
        CollectionAssert.AreEqual(seen, trace.Messages.ToList());
        Assert.AreEqual("aborted_streaming", trace.Result.TerminalReason);
        Assert.AreEqual(3, server.Written.Length);
        Assert.AreEqual("interrupt", RequestSubtype(server.Written[2]));
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
    }

    // Rule A. A tool call started after a query's last result is the run's: it is closed
    // unanswered, and the result is still last, so the run completes with its trace.
    [TestMethod]
    public async Task AToolUseAfterAQuerysLastResultIsClosedUnansweredBeforeTheResultAndTheRunCompletes()
    {
        var trivial = Fixture.MessageLines("trivial");
        var toolUse = Fixture.MessageLines("tool-use");
        var script = new Script(
        [
            Script.AnswerInitialize,
            new AwaitPrompt(1),
            .. trivial.Select(line => new EmitLine(line)),
            new EmitLine(toolUse[5]),
        ]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);

        var trace = await host.Client.QueryAsync(TrivialPrompt).WaitForCompletionAsync().WaitAsync(Patience);

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(SystemInitMessage),
                typeof(RateLimitMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(ToolCallMessage),
                typeof(ToolCallMessage),
                typeof(ResultMessage),
            },
            trace.Messages.Select(message => message.GetType()).ToArray());
        CollectionAssert.AreEqual(
            new[] { ToolCallStatus.Started, ToolCallStatus.Unanswered },
            trace.Messages.OfType<ToolCallMessage>().Select(message => message.ToolCall.Status).ToArray());
        Assert.AreEqual("pong", trace.Result.Result);
        Assert.AreSame(trace.Messages[^1], trace.Result);
    }

    // A Failed call's Error is the text blocks of its tool result joined as the binary wrote them,
    // a leading "Error: " kept: no recorded block carries that prefix, only the frame's
    // tool_use_result, so nothing founds stripping it. The answer is tool-use's own, made an error
    // with two text blocks.
    [TestMethod]
    public async Task AFailedCallsErrorIsItsToolResultsTextBlocksJoinedAsWrittenALeadingErrorPrefixKept()
    {
        var toolUse = Fixture.MessageLines("tool-use");
        var answer = JsonNode.Parse(toolUse[6])!;
        var block = answer["message"]!["content"]![0]!;
        block["is_error"] = true;
        block["content"] = new JsonArray(
            new JsonObject { ["type"] = "text", ["text"] = "Error: no such file" },
            new JsonObject { ["type"] = "text", ["text"] = " (notes.txt)" });
        var script = new Script(
        [
            Script.AnswerInitialize,
            new AwaitPrompt(1),
            .. toolUse.Take(6).Select(line => new EmitLine(line)),
            new EmitLine(answer.ToJsonString()),
            .. toolUse.Skip(7).Select(line => new EmitLine(line)),
        ]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);

        var trace = await host.Client.QueryAsync(ToolUsePrompt).WaitForCompletionAsync().WaitAsync(Patience);

        var failed = trace.ToolCalls.Single();
        Assert.AreEqual(ToolCallStatus.Failed, failed.Status);
        Assert.AreEqual("Error: no such file (notes.txt)", failed.Error);
    }

    // The read loop is never held by a consumer: a run nobody reads keeps what arrives, and a
    // session's next run is served all the same. The three prompts are sent at once and written
    // one at a time, each once the run before it has ended, as the recording has them.
    [TestMethod]
    public async Task ARunNobodyReadsKeepsItsMessagesAndTheSessionsNextRunIsServed()
    {
        await using var server = FixtureServer.Start("conversation-reset");
        using var host = TestHost.Start(server, options: options =>
        {
            options.WorkingDirectory = "/tmp/claude-agent-fixtures/cwd";
            options.SettingSources = [];
            options.Model = "haiku";
            options.MaxTurns = 1;
            options.Effort = Effort.Low;
        });
        await using var session = await host.Client.StartSessionAsync();

        var unread = session.SendAsync("Say: one");
        var cleared = session.SendAsync("/clear");
        var last = await session.SendAsync("Say: two").WaitForCompletionAsync().WaitAsync(Patience);

        Assert.AreEqual("two", last.Result.Result);
        Assert.AreEqual("one", (await unread.WaitForCompletionAsync()).Result.Result);
        Assert.IsInstanceOfType<ConversationResetMessage>((await cleared.WaitForCompletionAsync()).Messages[2]);
    }

    // One prompt in flight: each send hands back its run at once, the second while the first prompt
    // is in flight and its run cannot end until the test resumes the play. The first run has its
    // result, and its prompt's completed frame has not come, so the run has not ended and the prompt
    // sent after it is not written. Once it comes the run ends with its trace and the next prompt
    // goes out.
    [TestMethod]
    public async Task ASessionWritesItsNextPromptOnlyOnceTheRunBeforeItHasEnded()
    {
        await using var server = FixtureServer.Play(FirstResultHeld(), "conversation-reset");
        using var host = TestHost.Start(server, AsTheConversationResetFixture);
        await using var session = await host.Client.StartSessionAsync();

        var first = await HandedBackAsync(() => session.SendAsync("Say: one"));
        var cleared = await HandedBackAsync(() => session.SendAsync("/clear"));
        await AwaitTheMarkAsync(session, server);

        Assert.IsFalse(first.Stream.IsEnded);
        Assert.AreEqual(2, server.Written.Length, "the initialize and the first prompt only");

        server.Resume();
        Assert.AreEqual("one", (await first.WaitForCompletionAsync().WaitAsync(Patience)).Result.Result);
        Assert.IsInstanceOfType<ConversationResetMessage>((await cleared.WaitForCompletionAsync().WaitAsync(Patience)).Messages[2]);
        await server.WaitForWrites(3, Patience);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
    }

    // Finding run:3. A run's interrupt goes out only while its turn is under way: once its result
    // has been read nothing is sent, though the run has not yet ended.
    [TestMethod]
    public async Task ARunsInterruptOnceItsResultHasBeenReadSendsNothing()
    {
        await using var server = FixtureServer.Play(FirstResultHeld(), "conversation-reset");
        using var host = TestHost.Start(server, AsTheConversationResetFixture);
        await using var session = await host.Client.StartSessionAsync();

        var first = session.SendAsync("Say: one");
        await AwaitTheMarkAsync(session, server);

        await first.InterruptAsync().WaitAsync(Patience);
        Assert.AreEqual(2, server.Written.Length, "no interrupt went out");

        server.Resume();
        Assert.AreEqual("one", (await first.WaitForCompletionAsync().WaitAsync(Patience)).Result.Result);
        Assert.IsInstanceOfType<ConversationResetMessage>((await session.SendAsync("/clear").WaitForCompletionAsync().WaitAsync(Patience)).Messages[2]);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
    }

    // An interrupt after the result does nothing: a session's run interrupted where the loop holds
    // its result, and again once the loop is done, throws nothing and keeps the trace it had, and
    // the session's next turn goes as recorded. What goes out is the recording's stdin to that turn.
    [TestMethod]
    public async Task ASessionRunsInterruptAfterItsResultWritesNothingAndLeavesItsTraceAsItWas()
    {
        await using var server = FixtureServer.Start("conversation-reset");
        using var host = TestHost.Start(server, AsTheConversationResetFixture);
        await using var session = await host.Client.StartSessionAsync();

        var (seen, before, after) = await InterruptedAfterItsResultAsync(session.SendAsync("Say: one"));

        Assert.AreSame(before, after);
        CollectionAssert.AreEqual(seen, after.Messages.ToList());
        Assert.AreSame(seen[^1], after.Result);

        Assert.IsInstanceOfType<ConversationResetMessage>((await session.SendAsync("/clear").WaitForCompletionAsync().WaitAsync(Patience)).Messages[2]);
        await session.CloseAsync();
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        CollectionAssert.AreEqual(Fixture.Load("conversation-reset").Stdin.Take(3).Anonymised(), server.Written.Anonymised());
    }

    // A prompt whose sender's token is cancelled while it waits behind the run before it is taken
    // out: nothing is written, its run ends cancelled, and the next prompt takes its place. What goes
    // out is the recording's stdin.
    [TestMethod]
    public async Task APromptWhoseTokenIsCancelledWhileItWaitsIsNeverWrittenAndItsRunEndsCancelled()
    {
        await using var server = FixtureServer.Start("conversation-reset", pauseAfterLine: 8);
        using var host = TestHost.Start(server, AsTheConversationResetFixture);
        await using var session = await host.Client.StartSessionAsync();

        var first = session.SendAsync("Say: one");
        using var gaveUp = new CancellationTokenSource();
        var withdrawn = session.SendAsync("/clear", gaveUp.Token);

        // The first result is on the wire and its completed frame is not: the first run holds.
        await server.Paused.WaitAsync(Patience);
        await gaveUp.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => withdrawn.WaitForCompletionAsync().WaitAsync(Patience));

        await PlayTheRestAsync(session, server, first);
    }

    // A pending run's own interrupt does as its token: the prompt is taken out, nothing is written,
    // and the run ends cancelled, since no turn ran.
    [TestMethod]
    public async Task APendingRunsInterruptTakesItsPromptOutAndTheRunEndsCancelled()
    {
        await using var server = FixtureServer.Start("conversation-reset", pauseAfterLine: 8);
        using var host = TestHost.Start(server, AsTheConversationResetFixture);
        await using var session = await host.Client.StartSessionAsync();

        var first = session.SendAsync("Say: one");
        var withdrawn = session.SendAsync("/clear");

        await server.Paused.WaitAsync(Patience);
        await withdrawn.InterruptAsync().WaitAsync(Patience);
        await Assert.ThrowsAsync<OperationCanceledException>(() => withdrawn.WaitForCompletionAsync().WaitAsync(Patience));

        await PlayTheRestAsync(session, server, first);
    }

    // Finding run:2. Once a prompt is written its sender's token stops nothing: its run takes its
    // turn to the result, and the next run takes its own.
    [TestMethod]
    public async Task ATokenCancelledAfterItsPromptWasWrittenStopsNothing()
    {
        await using var server = FixtureServer.Start("conversation-reset", pauseAfterLine: 13);
        using var host = TestHost.Start(server, AsTheConversationResetFixture);
        await using var session = await host.Client.StartSessionAsync();

        await session.SendAsync("Say: one").WaitForCompletionAsync().WaitAsync(Patience);
        using var late = new CancellationTokenSource();
        var cleared = session.SendAsync("/clear", late.Token);

        // The /clear turn's init is on the wire, and its result is not.
        await server.Paused.WaitAsync(Patience);
        await late.CancelAsync();
        server.Resume();

        var reset = await cleared.WaitForCompletionAsync().WaitAsync(Patience);
        Assert.IsInstanceOfType<ConversationResetMessage>(reset.Messages[2]);
        Assert.AreEqual(string.Empty, reset.Result.Result);
        Assert.AreEqual("two", (await session.SendAsync("Say: two").WaitForCompletionAsync().WaitAsync(Patience)).Result.Result);

        await server.WaitForWrites(4, Patience);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        CollectionAssert.AreEqual(Fixture.Load("conversation-reset").Stdin.Anonymised(), server.Written.Anonymised());
    }

    // Finding run:4, as finding end:1 settles it. One pairing for the session, and a call belongs to
    // where its start went: a call started in what no run reads and answered inside a run's stretch
    // closes answered where it started, though the message that answers it is the run's, and nothing
    // closes it unanswered. No recording has a call cross that line; the script is task-background's
    // with a second prompt whose turn starts while the background agent is in its first tool round:
    // its start (line 19) between turns, its answer (line 21) inside the second prompt's stretch,
    // which ends at a result and its completed frame.
    [TestMethod]
    public async Task ACallStartedInWhatNoRunReadsClosesThereWhenItsAnswerComesInsideARunsStretch()
    {
        const string AgentsToolUseId = "toolu_013W1o4VLeELuh1Dt6qjq6sv";
        var background = Fixture.Load("task-background");
        var recorded = Fixture.PromptUuids(background.Stdin).Single()!;
        var second = Guid.NewGuid().ToString();
        string OfTheSecond(string line) => line.Replace(recorded, second, StringComparison.Ordinal);

        var script = new Script(
        [
            Script.AnswerInitialize,
            new AwaitPrompt(1),
            .. background.Stdout[1..19].Select(line => new EmitLine(line)),
            new AwaitPrompt(2),
            new EmitLine(OfTheSecond(background.Stdout[1])),
            new EmitLine(OfTheSecond(background.Stdout[2])),
            new EmitLine(background.Stdout[20]),
            new EmitLine(background.Stdout[37]),
            new EmitLine(OfTheSecond(background.Stdout[15])),
            new HangUp(),
        ])
        {
            Prompts = [recorded, second],
        };

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        await using var session = await host.Client.StartSessionAsync();

        var first = await session.SendAsync("Start the agent").WaitForCompletionAsync().WaitAsync(Patience);
        var answered = await session.SendAsync("Say: two").WaitForCompletionAsync().WaitAsync(Patience);
        var unclaimed = new List<AgentMessage>();
        using var patience = new CancellationTokenSource(Patience);
        await foreach (var message in session.ReceiveMessagesAsync(patience.Token))
        {
            unclaimed.Add(message);
        }

        Assert.AreEqual("started", first.Result.Result);
        var snapshots = unclaimed.OfType<ToolCallMessage>().Select(message => message.ToolCall).ToArray();
        CollectionAssert.AreEqual(
            new[] { ToolCallStatus.Started, ToolCallStatus.Answered },
            snapshots.Select(call => call.Status).ToArray());
        Assert.IsTrue(snapshots.All(call => call.Id == AgentsToolUseId));

        // The message that answered it is the run's; the close is not.
        Assert.IsTrue(answered.Messages.Any(message => message is UserMessage));
        Assert.IsFalse(answered.Messages.Any(message => message is ToolCallMessage));
    }

    // A lifecycle frame that names a prompt other than the run's, inside the run's stretch, is not
    // the run's and closes nothing of it: it goes to what no run reads. The frame is continuation's
    // completed for its first prompt, which this session never sent.
    [TestMethod]
    public async Task ALifecycleFrameNamingAnotherPromptGoesToWhatNoRunReadsAndClosesNothing()
    {
        var reset = Fixture.Load("conversation-reset");
        var foreign = Fixture.Load("continuation").Stdout[15];
        var script = new Script(
        [
            Script.AnswerInitialize,
            new AwaitPrompt(1),
            .. reset.Stdout[1..6].Select(line => new EmitLine(line)),
            new EmitLine(foreign),
            .. reset.Stdout[6..9].Select(line => new EmitLine(line)),
            new HangUp(),
        ])
        {
            Prompts = Fixture.PromptUuids(reset.Stdin),
        };

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        await using var session = await host.Client.StartSessionAsync();

        var trace = await session.SendAsync("Say: one").WaitForCompletionAsync().WaitAsync(Patience);
        var unclaimed = new List<AgentMessage>();
        using var patience = new CancellationTokenSource(Patience);
        await foreach (var message in session.ReceiveMessagesAsync(patience.Token))
        {
            unclaimed.Add(message);
        }

        Assert.AreEqual(Fixture.UuidOf(foreign), ((CommandLifecycleMessage)unclaimed.Single()).Uuid);
        CollectionAssert.AreEqual(
            new[] { "queued", "started", "completed" },
            trace.Messages.OfType<CommandLifecycleMessage>().Select(lifecycle => lifecycle.State.ToString()).ToArray());
        Assert.AreEqual("one", trace.Result.Result);
        Assert.AreSame(trace.Result, trace.Messages[^1]);
    }

    // The conversation-reset recording's first prompt: its frames up to its result, then the rate
    // limit event again, which no run reads and so marks, once it reaches what no run reads, that
    // every line before it has been routed; the play pauses there, then goes on with the completed
    // frame and the /clear prompt's turn.
    private static Script FirstResultHeld()
    {
        var reset = Fixture.Load("conversation-reset");
        return new Script(
        [
            Script.AnswerInitialize,
            new AwaitPrompt(1),
            .. reset.Stdout[1..8].Select(line => new EmitLine(line)),
            new EmitLine(reset.Stdout[6]),
            new Pause(),
            new EmitLine(reset.Stdout[8]),
            new AwaitPrompt(2),
            .. reset.Stdout[9..15].Select(line => new EmitLine(line)),
        ])
        {
            Prompts = Fixture.PromptUuids(reset.Stdin),
        };
    }

    private static async Task AwaitTheMarkAsync(ClaudeAgentSession session, FixtureServer server)
    {
        await using var unclaimed = session.ReceiveMessagesAsync().GetAsyncEnumerator();
        Assert.IsTrue(await unclaimed.MoveNextAsync().AsTask().WaitAsync(Patience));
        Assert.IsInstanceOfType<RateLimitMessage>(unclaimed.Current);
        await server.Paused.WaitAsync(Patience);
    }

    // The call made off the test's thread, so a call that blocks fails on AtOnce instead of stalling
    // the test.
    private static Task<ClaudeAgentRun> HandedBackAsync(Func<ClaudeAgentRun> call) => Task.Run(call).WaitAsync(AtOnce);

    // The run enumerated to its end, interrupted where the loop holds its result and again once the
    // loop is done: what the loop saw, the trace before the interrupts, and the trace after them.
    private static async Task<(List<AgentMessage> Seen, AgentTrace Before, AgentTrace After)> InterruptedAfterItsResultAsync(ClaudeAgentRun run)
    {
        var seen = new List<AgentMessage>();
        AgentTrace? before = null;
        await foreach (var message in run)
        {
            seen.Add(message);
            if (message is ResultMessage)
            {
                before = await run.WaitForCompletionAsync().WaitAsync(Patience);
                await run.InterruptAsync().WaitAsync(Patience);
            }
        }

        await run.InterruptAsync().WaitAsync(Patience);
        Assert.IsNotNull(before, "the run handed out no result.");
        return (seen, before, await run.WaitForCompletionAsync().WaitAsync(Patience));
    }

    // The recording from the first prompt's completed frame on, driven as it was recorded: the
    // withdrawn prompt is not among what went out.
    private static async Task PlayTheRestAsync(ClaudeAgentSession session, FixtureServer server, ClaudeAgentRun first)
    {
        var cleared = session.SendAsync("/clear");
        server.Resume();

        Assert.AreEqual("one", (await first.WaitForCompletionAsync().WaitAsync(Patience)).Result.Result);
        Assert.IsInstanceOfType<ConversationResetMessage>((await cleared.WaitForCompletionAsync().WaitAsync(Patience)).Messages[2]);
        Assert.AreEqual("two", (await session.SendAsync("Say: two").WaitForCompletionAsync().WaitAsync(Patience)).Result.Result);

        await server.WaitForWrites(4, Patience);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        CollectionAssert.AreEqual(Fixture.Load("conversation-reset").Stdin.Anonymised(), server.Written.Anonymised());
    }

    // Finding routing:2. A query's turn is over once the result that ends it has been read, though
    // the run has not ended until its session closes: its interrupt then sends nothing and does not
    // throw. What goes out is the recording's stdin.
    [TestMethod]
    public async Task AQuerysInterruptOnceItsTurnsResultHasBeenReadSendsNothing()
    {
        await using var server = FixtureServer.Start("trivial");
        using var host = TestHost.Start(server);

        var run = host.Client.QueryAsync(TrivialPrompt);
        await run.Stream.Ending.WaitAsync(Patience);
        await run.InterruptAsync().WaitAsync(Patience);

        Assert.AreEqual("pong", (await run.WaitForCompletionAsync().WaitAsync(Patience)).Result.Result);
        CollectionAssert.AreEqual(Fixture.Load("trivial").Stdin.Anonymised(), server.Written.Anonymised());
    }

    // An interrupt after the result does nothing: a query's run interrupted where the loop holds its
    // result, its session already closed, and again once the loop is done, throws nothing and keeps
    // the trace it had. What goes out is the recording's stdin.
    [TestMethod]
    public async Task AQuerysInterruptAfterItsResultWritesNothingAndLeavesItsTraceAsItWas()
    {
        await using var server = FixtureServer.Start("defaults");
        using var host = TestHost.Start(server, options =>
        {
            options.WorkingDirectory = "/tmp/claude-agent-fixtures/cwd";
            options.SettingSources = [];
            options.Model = "haiku";
            options.MaxTurns = 1;
        });

        var (seen, before, after) = await InterruptedAfterItsResultAsync(host.Client.QueryAsync(DefaultsPrompt));

        Assert.AreSame(before, after);
        CollectionAssert.AreEqual(seen, after.Messages.ToList());
        Assert.AreSame(seen[^1], after.Result);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        CollectionAssert.AreEqual(Fixture.Load("defaults").Stdin.Anonymised(), server.Written.Anonymised());
    }

    // Findings page:1 and routing:3. What no run reads has no run's end: the result of a turn there
    // closes nothing, and a call started there closes answered when its answer comes after that
    // result. No recording shows this order; the script is written by hand from the tool-use and
    // trivial recordings.
    [TestMethod]
    public async Task AResultInWhatNoRunReadsClosesNothingAndACallStartedThereClosesAnsweredWhenItsAnswerComes()
    {
        var toolUse = Fixture.MessageLines("tool-use");
        var script = new Script(
        [
            Script.AnswerInitialize,
            new EmitLine(toolUse[1]),
            new EmitLine(toolUse[5]),
            new EmitLine(Fixture.MessageLines("trivial")[^1]),
            new EmitLine(toolUse[6]),
            new HangUp(),
        ]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        await using var session = await host.Client.StartSessionAsync();

        var unclaimed = new List<AgentMessage>();
        using var patience = new CancellationTokenSource(Patience);
        await foreach (var message in session.ReceiveMessagesAsync(patience.Token))
        {
            unclaimed.Add(message);
        }

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(SystemInitMessage),
                typeof(AssistantMessage),
                typeof(ToolCallMessage),
                typeof(ResultMessage),
                typeof(UserMessage),
                typeof(ToolCallMessage),
            },
            unclaimed.Select(message => message.GetType()).ToArray());
        CollectionAssert.AreEqual(
            new[] { ToolCallStatus.Started, ToolCallStatus.Answered },
            unclaimed.OfType<ToolCallMessage>().Select(message => message.ToolCall.Status).ToArray());
    }

    // The routing report's pairing gap, settled by the page: a call still open when its run ends is
    // closed unanswered before the run's result, and every call closes once, so its answer, coming
    // after that result to what no run reads, is handed out as its message and nothing more. No
    // recording shows this order; the script is the conversation-reset recording's first turn with
    // the tool-use recording's call and answer written into it.
    [TestMethod]
    public async Task ACallAnsweredAfterItsRunsResultIsClosedUnansweredThereAndItsLateAnswerHandsOutNoSecondSnapshot()
    {
        var reset = Fixture.Load("conversation-reset");
        var toolUse = Fixture.MessageLines("tool-use");
        var script = new Script(
        [
            Script.AnswerInitialize,
            new AwaitPrompt(1),
            .. reset.Stdout[1..4].Select(line => new EmitLine(line)),
            new EmitLine(toolUse[5]),
            .. reset.Stdout[7..9].Select(line => new EmitLine(line)),
            new EmitLine(toolUse[6]),
            new HangUp(),
        ])
        {
            Prompts = Fixture.PromptUuids(reset.Stdin),
        };

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        await using var session = await host.Client.StartSessionAsync();

        var trace = await session.SendAsync("Say: one").WaitForCompletionAsync().WaitAsync(Patience);
        var unclaimed = new List<AgentMessage>();
        using var patience = new CancellationTokenSource(Patience);
        await foreach (var message in session.ReceiveMessagesAsync(patience.Token))
        {
            unclaimed.Add(message);
        }

        CollectionAssert.AreEqual(
            new[] { ToolCallStatus.Started, ToolCallStatus.Unanswered },
            trace.Messages.OfType<ToolCallMessage>().Select(message => message.ToolCall.Status).ToArray());
        Assert.IsInstanceOfType<ToolCallMessage>(trace.Messages[^2]);
        Assert.AreSame(trace.Messages[^1], trace.Result);
        Assert.IsInstanceOfType<UserMessage>(unclaimed.Single());
    }

    // Finding run:6. A run nobody awaits or reads, failed by its session's close, is observed all the
    // same: its failure never reaches TaskScheduler.UnobservedTaskException.
    [TestMethod]
    public async Task ARunNobodyObservesFailsWhenItsSessionClosesWithoutAnUnobservedTaskException()
    {
        var unobserved = new ConcurrentQueue<Exception>();
        void Watch(object? sender, UnobservedTaskExceptionEventArgs raised)
        {
            foreach (var inner in raised.Exception.InnerExceptions)
            {
                unobserved.Enqueue(inner);
            }
        }

        TaskScheduler.UnobservedTaskException += Watch;
        try
        {
            await FailARunNobodyObservesAsync();
            for (var round = 0; round < 3; round++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Watch;
        }

        Assert.IsFalse(
            unobserved.OfType<ClaudeAgentException>().Any(failure => failure.Message == "The session was closed before the run's result."),
            "a run's failure went unobserved.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task FailARunNobodyObservesAsync()
    {
        var reset = Fixture.Load("conversation-reset");
        var script = new Script(
            [Script.AnswerInitialize, new AwaitPrompt(1), .. reset.Stdout[1..3].Select(line => new EmitLine(line))],
            AwaitStdinEnd: true)
        {
            Prompts = Fixture.PromptUuids(reset.Stdin),
        };

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        var session = await host.Client.StartSessionAsync();

        _ = session.SendAsync("Say: one");
        await server.WaitForWrites(2, Patience);
        await session.CloseAsync();
    }

    // The trivial turn's init, then the interrupt the client writes answered, and the client
    // recording's interrupted result.
    private static Script InterruptedTrivialTurn() => new(
    [
        Script.AnswerInitialize,
        new AwaitPrompt(1),
        new EmitLine(Fixture.MessageLines("trivial")[0]),
        new AnswerControlRequest(Script.SuccessResponse),
        new EmitLine(Fixture.Line("client", "result", 2)),
    ]);

    private static string? RequestSubtype(string line)
    {
        using var document = System.Text.Json.JsonDocument.Parse(line);
        return document.RootElement.GetProperty("request").GetProperty("subtype").GetString();
    }

    private static void AsTheConversationResetFixture(ClaudeAgentSessionOptions options)
    {
        options.WorkingDirectory = "/tmp/claude-agent-fixtures/cwd";
        options.SettingSources = [];
        options.Model = "haiku";
        options.MaxTurns = 1;
        options.Effort = Effort.Low;
    }

    // A frame of a type the codec has no type for, and a known kind that does not fit its type, reach
    // the run as the unknown message, in their place; a keep_alive never reaches it.
    [TestMethod]
    public async Task AnUnknownTypeAndAMisfitKnownKindReachTheRunAsUnknownMessagesAndAKeepAliveDoesNot()
    {
        // The client recording's first prompt: queued, started, its init, then the frames of the
        // test's own, then its result and its completed frame.
        var client = Fixture.Load("client");
        var script = new Script(
        [
            Script.AnswerInitialize,
            new AwaitPrompt(1),
            new EmitLine(client.Stdout[1]),
            new EmitLine(client.Stdout[2]),
            new EmitLine(client.Stdout[3]),
            new EmitLine("""{"type": "keep_alive"}"""),
            new EmitLine("""{"type": "sparkle", "subtype": "glitter", "session_id": "s-1"}"""),
            new EmitLine("""{"type": "assistant", "message": {"content": "not blocks"}, "session_id": "s-1"}"""),
            new EmitLine("""{"type": "keep_alive"}"""),
            new EmitLine(client.Stdout[7]),
            new EmitLine(client.Stdout[8]),
            new HangUp(),
        ])
        {
            Prompts = Fixture.PromptUuids(client.Stdin),
        };

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        await using var session = await host.Client.StartSessionAsync();

        var trace = await session.SendAsync("Say: one").WaitForCompletionAsync().WaitAsync(Patience);

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(CommandLifecycleMessage),
                typeof(CommandLifecycleMessage),
                typeof(SystemInitMessage),
                typeof(UnknownMessage),
                typeof(UnknownMessage),
                typeof(CommandLifecycleMessage),
                typeof(ResultMessage),
            },
            trace.Messages.Select(message => message.GetType()).ToArray());
        CollectionAssert.AreEqual(
            new[] { "sparkle", "assistant" },
            trace.Messages.OfType<UnknownMessage>().Select(unknown => unknown.Type).ToArray());
        Assert.AreEqual("glitter", trace.Messages.OfType<UnknownMessage>().First().Subtype);
        Assert.AreEqual(0, session.Runtime.RejectedFrames.Count);
    }

    // The cancel fixture's first turn, cut after the Read's tool_use: the binary dies with the call
    // open, and the run closes it unanswered at the end of what arrived, then throws. The rate
    // limit event between the prompt's queued and started frames is not the run's.
    [TestMethod]
    public async Task ARunThatEndsWithoutItsResultClosesItsOpenCallUnansweredAtTheEndOfTheMessagesSoFar()
    {
        var cancel = Fixture.Load("cancel");
        var script = new Script(
            [Script.AnswerInitialize, new AwaitPrompt(1), .. cancel.Stdout[1..7].Select(line => new EmitLine(line)), new HangUp()],
            ExitCode: 1)
        {
            Prompts = Fixture.PromptUuids(cancel.Stdin),
        };

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        await using var session = await host.Client.StartSessionAsync();

        var run = session.SendAsync(CancelledReadPrompt);
        var seen = new List<AgentMessage>();
        var thrown = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(async () =>
        {
            await foreach (var message in run)
            {
                seen.Add(message);
            }
        });

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(CommandLifecycleMessage),
                typeof(CommandLifecycleMessage),
                typeof(SystemInitMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(ToolCallMessage),
                typeof(ToolCallMessage),
            },
            seen.Select(message => message.GetType()).ToArray());
        CollectionAssert.AreEqual(seen, thrown.Messages.ToList());
        Assert.IsNull(thrown.Result);

        var closed = ((ToolCallMessage)seen[^1]).ToolCall;
        Assert.AreEqual(CancelledReadToolUseId, closed.Id);
        Assert.AreEqual(ToolCallStatus.Unanswered, closed.Status);
        Assert.IsNull(seen[^1].Raw);
        Assert.AreEqual(ToolCallStatus.Started, ((ToolCallMessage)seen[^2]).ToolCall.Status);

        var completion = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => run.WaitForCompletionAsync());
        CollectionAssert.AreEqual(seen, completion.Messages.ToList());
    }

    // The call's token closes the one-shot session: stdin, which a query keeps open
    // until its turn's result, ends, and the fixture server ends the connection, before the run ends
    // cancelled from its stream and from its completion.
    [TestMethod]
    public async Task CancellingAQuerysTokenClosesItsSessionAndEndsItsRunCancelledFromItsStreamAndItsCompletion()
    {
        var trivial = Fixture.MessageLines("trivial");
        var script = new Script([Script.AnswerInitialize, new AwaitPrompt(1), new EmitLine(trivial[0]), new Pause()]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        using var cancel = new CancellationTokenSource();

        var run = host.Client.QueryAsync(TrivialPrompt, cancel.Token);
        var seen = new List<AgentMessage>();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var message in run)
            {
                seen.Add(message);
                await server.Paused.WaitAsync(Patience);
                await cancel.CancelAsync();
            }
        });

        Assert.IsTrue(server.StdinEnded, "stdin was open when the cancelled run ended.");
        Assert.IsTrue(server.HungUp.IsCompleted, "the connection was open when the cancelled run ended.");
        Assert.IsInstanceOfType<SystemInitMessage>(seen.Single());
        await Assert.ThrowsAsync<OperationCanceledException>(() => run.WaitForCompletionAsync());
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
    }

    // Finding pairing:2. A query cancelled with a call open closes it unanswered: a reader meets the
    // close before the cancellation.
    [TestMethod]
    public async Task ACancelledQueryClosesItsOpenCallUnansweredBeforeItsStreamThrows()
    {
        var toolUse = Fixture.MessageLines("tool-use");
        var script = new Script([Script.AnswerInitialize, new AwaitPrompt(1), .. toolUse[..6].Select(line => new EmitLine(line)), new Pause()]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        using var cancel = new CancellationTokenSource();

        var run = host.Client.QueryAsync(ToolUsePrompt, cancel.Token);
        var seen = new List<AgentMessage>();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var message in run)
            {
                seen.Add(message);
                if (message is ToolCallMessage { ToolCall.Status: ToolCallStatus.Started })
                {
                    await cancel.CancelAsync();
                }
            }
        });

        CollectionAssert.AreEqual(
            new[] { ToolCallStatus.Started, ToolCallStatus.Unanswered },
            seen.OfType<ToolCallMessage>().Select(message => message.ToolCall.Status).ToArray());
        Assert.IsInstanceOfType<ToolCallMessage>(seen[^1]);
    }

    // Findings end:1 and findings:3. A call belongs to where its start went: a query cancelled with
    // a call open, whose answer the read loop takes after the run was withdrawn, still hands that
    // run the call's close. No recording shows this order; the script is the tool-use recording's,
    // with the answer written once the client's stdin has ended, which is after the withdrawal.
    [TestMethod]
    public async Task ACancelledQuerysCallAnsweredAfterItsRunWasWithdrawnStillClosesInThatRun()
    {
        var toolUse = Fixture.MessageLines("tool-use");
        var script = new Script(
        [
            Script.AnswerInitialize,
            new AwaitPrompt(1),
            .. toolUse[..6].Select(line => new EmitLine(line)),
            new AwaitClientStdinEnd(),
            new EmitLine(toolUse[6]),
        ]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        using var cancel = new CancellationTokenSource();

        var run = host.Client.QueryAsync(ToolUsePrompt, cancel.Token);
        var seen = new List<AgentMessage>();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var message in run)
            {
                seen.Add(message);
                if (message is ToolCallMessage { ToolCall.Status: ToolCallStatus.Started })
                {
                    await cancel.CancelAsync();
                }
            }
        });

        CollectionAssert.AreEqual(
            new[] { ToolCallStatus.Started, ToolCallStatus.Answered },
            seen.OfType<ToolCallMessage>().Select(message => message.ToolCall.Status).ToArray());
        Assert.IsInstanceOfType<ToolCallMessage>(seen[^1]);
    }

    // Findings end:5 and tests:2. An enumerator disposed before its first move has left the
    // enumeration just as a break does: the turn is interrupted and the run has ended when the
    // dispose returns.
    [TestMethod]
    public async Task AnEnumeratorDisposedBeforeItsFirstMoveInterruptsTheTurnAndTheRunHasEndedWhenItReturns()
    {
        await using var server = FixtureServer.Play(InterruptedTrivialTurn());
        using var host = TestHost.Start(server);

        var run = host.Client.QueryAsync(TrivialPrompt);
        var enumerator = run.GetAsyncEnumerator();
        await enumerator.DisposeAsync().AsTask().WaitAsync(Patience);

        var completion = run.WaitForCompletionAsync();
        Assert.IsTrue(completion.IsCompletedSuccessfully, "the run had not ended when the enumerator was disposed.");
        Assert.AreEqual("aborted_streaming", (await completion).Result.TerminalReason);
        Assert.AreEqual("interrupt", RequestSubtype(server.Written[2]));
        Assert.IsTrue(server.StdinEnded, "the query's session had not closed when the enumerator was disposed.");
    }

    // Finding findings:2. A result line whose duration no TimeSpan holds is read as the unknown of
    // the message set, so the turn it ended carries no result: the run fails for that line where the
    // frame was read, and its session is closed, instead of a query waiting with its stdin open for
    // a result that will not come.
    [TestMethod]
    public async Task AQueryWhoseResultLineDoesNotFitItsTypeFailsForTheTurnWithoutAResultAndItsSessionCloses()
    {
        var trivial = Fixture.MessageLines("trivial");
        var script = new Script(
            [Script.AnswerInitialize, new AwaitPrompt(1), .. trivial[..^1].Select(line => new EmitLine(line)), new EmitLine(Misfit(trivial[^1])), new Pause()]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);

        var failure = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(
            () => host.Client.QueryAsync(TrivialPrompt).WaitForCompletionAsync().WaitAsync(Patience));

        Assert.AreEqual(ErrorCodes.Parse, failure.ErrorCode);
        StringAssert.Contains(failure.Message, "duration_ms");
        Assert.AreEqual(FrameTypes.Result, ((UnknownMessage)failure.Messages[^1]).Type);
        await server.StdinEnd.WaitAsync(Patience);
    }

    // The same line in a session: the run fails for it, and the prompt waiting behind it is written
    // all the same, with its own turn read as recorded.
    [TestMethod]
    public async Task ASessionRunWhoseResultLineDoesNotFitItsTypeFailsAndTheNextPromptIsWrittenAllTheSame()
    {
        var reset = Fixture.Load("conversation-reset");
        var script = new Script(
        [
            Script.AnswerInitialize,
            new AwaitPrompt(1),
            .. reset.Stdout[1..7].Select(line => new EmitLine(line)),
            new EmitLine(Misfit(reset.Stdout[7])),
            new EmitLine(reset.Stdout[8]),
            new AwaitPrompt(2),
            .. reset.Stdout[9..15].Select(line => new EmitLine(line)),
            new HangUp(),
        ])
        {
            Prompts = Fixture.PromptUuids(reset.Stdin),
        };

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server, AsTheConversationResetFixture);
        await using var session = await host.Client.StartSessionAsync();

        var first = session.SendAsync("Say: one");
        var cleared = session.SendAsync("/clear");

        var failure = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => first.WaitForCompletionAsync().WaitAsync(Patience));
        Assert.AreEqual(ErrorCodes.Parse, failure.ErrorCode);
        Assert.AreEqual(FrameTypes.Result, ((UnknownMessage)failure.Messages[^1]).Type);
        Assert.IsInstanceOfType<ConversationResetMessage>((await cleared.WaitForCompletionAsync().WaitAsync(Patience)).Messages[2]);
        await server.WaitForWrites(3, Patience);
    }

    // The line with a duration no TimeSpan holds.
    private static string Misfit(string result) => Fixture.WithDurationOutOfRange(result);

    // The read loop is never held by a consumer: a run nobody reads keeps three
    // hundred messages, and what the binary writes after them reaches what no run reads all the
    // same. A buffer that held the loop once full would keep the marker from coming.
    [TestMethod]
    public async Task ARunNobodyReadsKeepsThreeHundredMessagesWhileTheReadLoopGoesOnToWhatComesAfterThem()
    {
        const int Unread = 300;
        var reset = Fixture.Load("conversation-reset");
        var script = new Script(
        [
            Script.AnswerInitialize,
            new AwaitPrompt(1),
            .. reset.Stdout[1..4].Select(line => new EmitLine(line)),
            .. Enumerable.Repeat(reset.Stdout[5], Unread).Select(line => new EmitLine(line)),
            .. reset.Stdout[6..9].Select(line => new EmitLine(line)),
            new EmitLine(reset.Stdout[6]),
        ])
        {
            Prompts = Fixture.PromptUuids(reset.Stdin),
        };

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        await using var session = await host.Client.StartSessionAsync();

        var unread = session.SendAsync("Say: one");
        await using (var unclaimed = session.ReceiveMessagesAsync().GetAsyncEnumerator())
        {
            Assert.IsTrue(await unclaimed.MoveNextAsync().AsTask().WaitAsync(Patience));
            Assert.IsInstanceOfType<RateLimitMessage>(unclaimed.Current);
        }

        var trace = await unread.WaitForCompletionAsync().WaitAsync(Patience);
        Assert.AreEqual(Unread, trace.Messages.OfType<AssistantMessage>().Count());
        Assert.AreEqual("one", trace.Result.Result);
    }

    // What the query's run is handed back before anything is started: a start that fails fails the
    // run, not the call.
    [TestMethod]
    public async Task AQueryWhoseStartFailsHandsBackARunThatThrowsTheFailure()
    {
        var script = new Script([Script.AnswerWithError("the binary refused to start")]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);

        var run = host.Client.QueryAsync(TrivialPrompt);
        var failure = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => run.WaitForCompletionAsync().WaitAsync(Patience));

        StringAssert.Contains(failure.Message, "the binary refused to start");
        Assert.AreEqual(0, failure.Messages.Count);
    }
}
