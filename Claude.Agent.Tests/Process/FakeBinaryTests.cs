using System.Collections.Concurrent;
using Claude.Agent.Tests.Replay;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Claude.Agent.Tests.Process;

[TestClass]
[UnsupportedOSPlatform("windows")]
public sealed class FakeBinaryTests
{
    private const string Prompt = "Reply with exactly one word: pong";
    private static readonly string FakeClaude = Path.Combine(AppContext.BaseDirectory, "fake", "claude");
    private static readonly string AppHost = Path.Combine(AppContext.BaseDirectory, "Claude.Agent.Tests");
    private static readonly string TestExecutable = File.Exists(AppHost) ? AppHost : "dotnet";
    private static readonly string[] TestExecutableArguments = File.Exists(AppHost) ? [] : [typeof(FakeBinaryTests).Assembly.Location];
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    // The environment the client's host is taken to have inherited; a test sets it before it starts.
    private readonly Dictionary<string, string> _inherited = new(StringComparer.Ordinal);

    private string _cwd = string.Empty;

    [TestInitialize]
    public void MakeACwd()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("the fake binary traps SIGTERM; there is no Windows machine here.");
        }

        _cwd = Directory.CreateTempSubdirectory("claude-agent-fake").FullName;
    }

    [TestCleanup]
    public void RemoveTheCwd() => Directory.Delete(_cwd, recursive: true);

    [TestMethod]
    public async Task ACleanFakePlaysTheTrivialTurnThroughTheRealProcessTransport()
    {
        var log = new LogCapture();
        using var host = StartAsync(log, cue: null);

        var seen = await QueryAsync(host);

        CollectionAssert.AreEqual(
            new[]
            {
                typeof(SystemInitMessage),
                typeof(RateLimitMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(ResultMessage),
            },
            seen.Select(message => message.GetType()).ToArray());
        Assert.IsFalse(log.Warnings.Any(), string.Join("\n", log.Warnings));
    }

    [TestMethod]
    public async Task AVersionBelowTheMinimumIsWarnedAboutAndTheTurnStillRuns()
    {
        var log = new LogCapture();
        using var host = StartAsync(log, cue: "wrong-version");

        var seen = await QueryAsync(host);

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        var warning = log.Warnings.SingleOrDefault(line => line.Contains("1.0.0", StringComparison.Ordinal));
        Assert.IsNotNull(warning, "the version probe did not warn about 1.0.0.");
        StringAssert.Contains(warning, "A version before 2.1.276 does not name the prompts it takes");
    }

    // 2.1.276 is the first version recorded naming each prompt; the clean fake says it is that one
    // and is not warned about.
    [TestMethod]
    public async Task TheVersionBeforeTheFirstThatNamesPromptsIsWarnedAbout()
    {
        var log = new LogCapture();
        using var host = StartAsync(log, cue: "version-before-floor");

        var seen = await QueryAsync(host);

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        var warning = log.Warnings.SingleOrDefault(line => line.Contains("2.1.275", StringComparison.Ordinal));
        Assert.IsNotNull(warning, "the version probe did not warn about 2.1.275.");
        StringAssert.Contains(warning, "A version before 2.1.276 does not name the prompts it takes");
    }

    // No variable of the host's turns the probe off: the one another SDK reads for that is read here
    // by nothing, and the probe still warns.
    [TestMethod]
    public async Task TheVersionProbeRunsWhateverTheHostsEnvironmentHolds()
    {
        var log = new LogCapture();
        // Spelt in two halves, as the tree's check for another SDK's names spells it.
        var variable = "CLAUDE_AGENT_SDK_" + "SKIP_VERSION_CHECK";
        Environment.SetEnvironmentVariable(variable, "1");
        try
        {
            using var host = StartAsync(log, cue: "wrong-version");

            var seen = await QueryAsync(host);

            Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }

        Assert.IsTrue(
            log.Warnings.Any(line => line.Contains("1.0.0", StringComparison.Ordinal)),
            "the version probe did not run.");
    }

    // The session start reads the options as they go out, the mode and the allow rules, and logs what
    // the binary leaves the permission callback; under dontAsk, nothing.
    [TestMethod]
    public async Task APermissionCallbackUnderDontAskIsWarnedAboutAsTheSessionStarts()
    {
        var log = new LogCapture();
        using var host = StartAsync(log, cue: null, options =>
        {
            options.Defaults.PermissionMode = PermissionMode.DontAsk;
            options.Defaults.PermissionCallback = (_, _) => Task.FromResult(ToolPermissionResult.Allow());
        });

        var seen = await QueryAsync(host);

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        Assert.IsTrue(
            log.Warnings.Any(line => line.Contains("PermissionMode is dontAsk", StringComparison.Ordinal)),
            string.Join("\n", log.Warnings));
    }

    [TestMethod]
    public async Task AProbeThatHangsIsKilledAfterItsGraceAndTheTurnStillRuns()
    {
        var log = new LogCapture();
        using var host = StartAsync(log, cue: "probe-hangs", Brisk);

        var seen = await QueryAsync(host);

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        Assert.IsFalse(log.Warnings.Any(), string.Join("\n", log.Warnings));
    }

    // The binary writes nothing on stdout but JSON.stringify's text, a message a line; a line that is
    // not one goes to the codec all the same, as it was written, and is rejected where it stands.
    [TestMethod]
    public async Task EveryLineOfStdoutReachesTheCodecAsWrittenAndOneThatDoesNotDecodeIsRejectedWithTheStreamGoingOn()
    {
        var log = new LogCapture();
        using var host = StartAsync(log, cue: "garbage-stdout");
        await using var session = await ConnectAsync(host);

        var seen = await DrainAsync(session.SendAsync(Prompt));

        CollectionAssert.AreEqual(
            new[] { "queued", "started", "completed" },
            seen.Messages.OfType<CommandLifecycleMessage>().Select(lifecycle => lifecycle.State.ToString()).ToArray());
        Assert.AreEqual(8, seen.Messages.Length);
        Assert.IsInstanceOfType<ResultMessage>(seen.Messages[^1]);
        Assert.IsNull(seen.Failure);
        CollectionAssert.AreEqual(
            new[] { "not json at all", "{\"type\": \"assistant\", \"message\": {" },
            session.Runtime.RejectedFrames.Select(rejected => rejected.Line).ToArray());
        Assert.IsTrue(session.Runtime.RejectedFrames.All(rejected => rejected.Kind == ParseErrorKind.MalformedJson));
    }

    // The recorder's tap is handed each line of stdout as the binary wrote it, before the codec reads
    // it: a blank line, one of whitespace and one with whitespace and a carriage return around it
    // included. Each line with '\n' after it, as the recorder writes it, is the binary's stdout.
    [TestMethod]
    public async Task TheTapIsHandedEachStdoutLineAsTheBinaryWroteItBlankOnesIncluded()
    {
        var log = new LogCapture();
        var pidfile = Path.Combine(_cwd, "fake.pid");
        var tapped = new ConcurrentQueue<string>();
        var tap = new TransportTap(_ => { }, tapped.Enqueue, _ => { });
        using var host = StartAsync(
            log,
            cue: "stdout-as-written",
            options => options.EnvironmentVariables["CLAUDE_FAKE_PIDFILE"] = pidfile,
            tap);

        var seen = await QueryAsync(host);

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        var written = await File.ReadAllTextAsync(pidfile + ".stdout");
        await WaitUntilAsync(() => string.Concat(tapped.Select(line => line + "\n")) == written);
        Assert.AreEqual(written, string.Concat(tapped.Select(line => line + "\n")));
        CollectionAssert.IsSubsetOf(new[] { string.Empty, " \t ", "  not json, padded  \r" }, tapped.ToArray());
    }

    // A line is read as its newline is, however many bytes the pipe has: a control request of 8192
    // bytes, which fills a StreamReader's 4096-byte buffer twice exactly, is answered while the binary
    // waits on it with nothing more written.
    [TestMethod]
    public async Task AControlRequestThatFillsTheReadsExactlyIsAnsweredWithoutWaitingForMoreOutput()
    {
        var log = new LogCapture();
        using var host = StartAsync(log, cue: "request-filling-reads");

        var trace = await host.Client.QueryAsync(Prompt).WaitForCompletionAsync().WaitAsync(Patience);

        Assert.AreEqual("pong", trace.Result.Result);
    }

    // The binary writes a message and its newline in one write; a process cut off between the two
    // leaves the message whole with nothing after it, and it is taken.
    [TestMethod]
    public async Task AResultTheProcessEndedWithNoNewlineAfterItIsTakenWhole()
    {
        var log = new LogCapture();
        using var host = StartAsync(log, cue: "result-without-newline");

        var trace = await host.Client.QueryAsync(Prompt).WaitForCompletionAsync().WaitAsync(Patience);

        Assert.AreEqual("pong", trace.Result.Result);
        Assert.AreSame(trace.Messages[^1], trace.Result);
    }

    [TestMethod]
    public async Task ATwoMebibyteLineIsPastMaxMessageSizeAndEndsTheStreamAsAProcessError()
    {
        var log = new LogCapture();
        using var host = StartAsync(log, cue: "huge-line", Brisk);
        await using var session = await ConnectAsync(host);

        var seen = await DrainAsync(session.SendAsync(Prompt));

        Assert.IsNotNull(seen.Failure);
        Assert.AreEqual(ErrorCodes.Process, seen.Failure.ErrorCode);
        StringAssert.Contains(seen.Failure.Message, "The binary wrote a line longer than MaxMessageSize, 1048576 characters.");
    }

    // The text the process wrote after its last newline is one more line, and the codec rejects it
    // where it stands, as any line that does not decode.
    [TestMethod]
    public async Task AProcessThatDiesMidFrameEndsTheStreamWithItsExitCodeAndItsUnfinishedLineIsRejected()
    {
        var log = new LogCapture();
        using var host = StartAsync(log, cue: "die-mid-frame");
        await using var session = await ConnectAsync(host);

        var seen = await DrainAsync(session.SendAsync(Prompt));

        Assert.AreEqual(3, seen.Messages.Length);
        Assert.AreEqual("started", ((CommandLifecycleMessage)seen.Messages[1]).State);
        Assert.IsInstanceOfType<SystemInitMessage>(seen.Messages[2]);
        Assert.IsNotNull(seen.Failure);
        Assert.AreEqual(ErrorCodes.Process, seen.Failure.ErrorCode);
        Assert.AreEqual(1, seen.Failure.ExitCode);
        var rejected = session.Runtime.RejectedFrames.Single();
        Assert.AreEqual(ParseErrorKind.MalformedJson, rejected.Kind);
        Assert.AreEqual("{\"type\": \"assistant\", \"message\": {\"content\": [{\"type\": \"text\", \"text\": \"half", rejected.Line);
    }

    [TestMethod]
    public async Task AProcessThatDiesMidTurnFailsTheRunFromItsStreamAndItsCompletionWithTheMessagesSoFar()
    {
        var log = new LogCapture();
        using var host = StartAsync(log, cue: "die-mid-frame");
        await using var session = await ConnectAsync(host);

        var run = session.SendAsync(Prompt);
        var seen = await DrainAsync(run);
        var completion = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => run.WaitForCompletionAsync());

        foreach (var failure in new[] { seen.Failure!, completion })
        {
            Assert.AreEqual(ErrorCodes.Process, failure.ErrorCode);
            Assert.AreEqual(1, failure.ExitCode);
            Assert.AreEqual(3, failure.Messages.Count);
            Assert.IsInstanceOfType<SystemInitMessage>(failure.Messages[^1]);
            Assert.IsNull(failure.Result);
        }

        Assert.AreSame(seen.Messages[2], completion.Messages[2]);
    }

    [TestMethod]
    public async Task AQueryWhoseProcessDiesMidTurnFailsItsCompletionAloneWithTheMessagesSoFar()
    {
        var log = new LogCapture();
        using var host = StartAsync(log, cue: "die-mid-frame");

        var run = host.Client.QueryAsync(Prompt);
        var failure = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => run.WaitForCompletionAsync());

        Assert.AreEqual(ErrorCodes.Process, failure.ErrorCode);
        Assert.AreEqual(1, failure.ExitCode);
        Assert.IsInstanceOfType<SystemInitMessage>(failure.Messages.Single());
        Assert.ThrowsExactly<InvalidOperationException>(() => run.GetAsyncEnumerator());
    }

    // Finding D. The close reads stderr to its end instead of stopping the read first, so everything
    // the process wrote before it exited has reached the writer when the query's completion is there:
    // the second line the fake writes after its result.
    [TestMethod]
    public async Task StderrReachesTheWriterAndHasAllOfItWhenTheQuerysCompletionIsThere()
    {
        var log = new LogCapture();
        var stderr = new TextCapture();
        using var host = StartAsync(log, cue: "stderr", options => options.StandardError = stderr);

        var seen = await QueryAsync(host);

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        Assert.AreEqual("fake: line one\nfake: line two\n", stderr.Text);
    }

    // Rule D. The fake writes its line once its stdin has
    // ended, just before it exits, so only a close that reads stderr to its end — rather than
    // stopping the reader first — has it when the query's completion is there.
    [TestMethod]
    public async Task AStderrLineWrittenAsTheProcessGoesReachesTheWriterBeforeTheQuerysCompletion()
    {
        var log = new LogCapture();
        var stderr = new TextCapture();
        using var host = StartAsync(log, cue: "stderr-at-exit", options => options.StandardError = stderr);

        var seen = await QueryAsync(host);

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        Assert.AreEqual("fake: at exit\n", stderr.Text);
    }

    // stderr is free text and reaches the writer as the binary wrote it: a blank line, trailing
    // spaces and text no newline ended included.
    [TestMethod]
    public async Task StderrReachesTheWriterAsTheBinaryWroteItBlankLinesIncluded()
    {
        var log = new LogCapture();
        var stderr = new TextCapture();
        using var host = StartAsync(log, cue: "stderr-as-written", options => options.StandardError = stderr);

        var seen = await QueryAsync(host);

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        Assert.AreEqual("fake: a paragraph\n\nfake: another, ending in two spaces  \nfake: no newline", stderr.Text);
    }

    // MaxMessageSize is stdout's: stderr text longer than it with no newline reaches the writer whole
    // and uncut, a character whose bytes two reads split included.
    [TestMethod]
    public async Task StderrPastMaxMessageSizeWithNoNewlineReachesTheWriterWholeACharacterSplitBetweenReadsIncluded()
    {
        var log = new LogCapture();
        var stderr = new TextCapture();
        using var host = StartAsync(log, cue: "stderr-past-bound", options => options.StandardError = stderr);

        var seen = await QueryAsync(host);

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        var expected = new string('x', (1024 * 1024) + 1) + "\u00e4, and no newline after it";
        var text = stderr.Text;
        Assert.IsTrue(
            string.Equals(expected, text, StringComparison.Ordinal),
            $"the writer has {text.Length} characters, not {expected.Length}, ending \"{text[^Math.Min(40, text.Length)..]}\".");
    }

    // Decided 2026-09-20: the result decides
    // what an exit means. A query whose turn has its result completes with its trace though the
    // binary then exits non-zero of its own accord; the exit disagrees with a successful result, so
    // it is logged as a warning.
    [TestMethod]
    public async Task AQueryWhoseProcessExitsNonZeroAfterItsResultCompletesWithItsTraceAndTheDisagreementIsWarnedAbout()
    {
        var log = new LogCapture();
        using var host = StartAsync(log, cue: "exit-nonzero-after-result");

        var trace = await host.Client.QueryAsync(Prompt).WaitForCompletionAsync().WaitAsync(Patience);

        Assert.AreEqual("pong", trace.Result.Result);
        Assert.IsFalse(trace.Result.IsError);
        Assert.AreSame(trace.Messages[^1], trace.Result);
        var warning = log.Warnings.SingleOrDefault(line => line.Contains("exited with code 3", StringComparison.Ordinal));
        Assert.IsNotNull(warning, $"the exit after a successful result was not warned about: {string.Join("\n", log.Warnings)}");
        StringAssert.Contains(warning, "disagrees");
    }

    // The same rule from the other side: a turn the binary did not finish ends in an error result,
    // and the binary exits 1 after it. The query completes with its trace, the error result last;
    // the exit says nothing that result does not, so nothing is warned about.
    [TestMethod]
    public async Task AQueryWhoseProcessExitsNonZeroAfterAnErrorResultCompletesWithItsTraceAndNoWarning()
    {
        var log = new LogCapture();
        using var host = StartAsync(log, cue: "exit-nonzero-after-error-result");

        var trace = await host.Client.QueryAsync(Prompt).WaitForCompletionAsync().WaitAsync(Patience);

        Assert.IsTrue(trace.Result.IsError);
        Assert.AreEqual(ResultMessageSubtype.ErrorDuringExecution, trace.Result.Subtype);
        Assert.AreSame(trace.Messages[^1], trace.Result);
        Assert.IsFalse(log.Warnings.Any(), string.Join("\n", log.Warnings));
    }

    // Finding routing:1. Once a query's turn has its result its session is closing, and its
    // interrupt returns at once instead of spinning until the close ends: here the close waits out a
    // two-second grace for a binary that stays.
    [TestMethod]
    public async Task AQuerysInterruptOnceItsTurnHasItsResultReturnsAtOnceWhileItsSessionCloses()
    {
        var log = new LogCapture();
        var pidfile = Path.Combine(_cwd, "pid");
        using var host = StartAsync(log, cue: "hang-after-result", options =>
        {
            Tracked(pidfile)(options);
            options.ProcessGracePeriod = TimeSpan.FromSeconds(2);
        });

        var run = host.Client.QueryAsync(Prompt);
        await run.Stream.Ending.WaitAsync(Patience);
        await run.InterruptAsync().WaitAsync(TimeSpan.FromMilliseconds(500));

        Assert.IsFalse(run.Stream.IsEnded, "the session had closed before the interrupt returned, so it proves nothing.");
        Assert.AreEqual("pong", (await run.WaitForCompletionAsync().WaitAsync(Patience)).Result.Result);
    }

    // Finding routing:1, as the review found it: a query cancelled mid-turn has its prompt withdrawn
    // and its session closing, and its interrupt returns at once, sending nothing, instead of
    // spinning until the close ends: here the close waits out a two-second grace for a binary that
    // never finishes its turn.
    [TestMethod]
    public async Task ACancelledQuerysInterruptReturnsAtOnceWhileItsSessionCloses()
    {
        var log = new LogCapture();
        var pidfile = Path.Combine(_cwd, "pid");
        using var host = StartAsync(log, cue: "hang-before-result", options =>
        {
            Tracked(pidfile)(options);
            options.ProcessGracePeriod = TimeSpan.FromSeconds(2);
        });

        using var cancel = new CancellationTokenSource();
        var run = host.Client.QueryAsync(Prompt, cancel.Token);
        var runtime = await run.RuntimeAsync(CancellationToken.None);
        Assert.IsNotNull(runtime);

        // The init the fake writes once it has read the prompt: the turn is under way.
        await WaitUntilAsync(() => run.Stream.Read(0, out _) is SystemInitMessage);
        Assert.IsInstanceOfType<SystemInitMessage>(run.Stream.Read(0, out _));

        await cancel.CancelAsync();
        await WaitUntilAsync(() => runtime.IsClosed);
        Assert.IsTrue(runtime.IsClosed, "the cancelled query's session never began its close.");
        await run.InterruptAsync().WaitAsync(TimeSpan.FromMilliseconds(500));

        Assert.IsFalse(run.Stream.IsEnded, "the session had closed before the interrupt returned, so it proves nothing.");
        await Assert.ThrowsAsync<OperationCanceledException>(() => run.WaitForCompletionAsync().WaitAsync(Patience));
    }

    // Rule A. A binary that stays after its stdin closes is ended by the close sequence, and the
    // query still completes with its trace, its result having come: after the grace, with the
    // process gone, and the forced close logged.
    [TestMethod]
    public async Task AQueryWhoseBinaryStaysAfterItsStdinClosesCompletesWithItsTraceOnceTheCloseSequenceEndsIt()
    {
        var log = new LogCapture();
        var pidfile = Path.Combine(_cwd, "pid");
        using var host = StartAsync(log, cue: "hang-after-result", Tracked(pidfile));

        var stopwatch = Stopwatch.StartNew();
        var trace = await host.Client.QueryAsync(Prompt).WaitForCompletionAsync().WaitAsync(Patience);
        stopwatch.Stop();

        Assert.AreEqual("pong", trace.Result.Result);
        Assert.AreSame(trace.Messages[^1], trace.Result);
        Assert.IsTrue(stopwatch.Elapsed >= Grace, $"the completion came after {stopwatch.Elapsed}, inside the grace.");
        Assert.IsFalse(IsAlive(int.Parse(await File.ReadAllTextAsync(pidfile))), "the process outlived the query's completion.");
        Assert.IsTrue(
            log.Warnings.Any(line => line.Contains("did not exit", StringComparison.Ordinal)),
            "the forced close was not logged.");
    }

    // The same close, read by enumerating: the result is the last thing handed out, and the process
    // has exited by the time the loop holds it, not only by the time the completion is awaited.
    [TestMethod]
    public async Task AQueryReadByEnumeratingHandsOutItsResultOnlyOnceTheProcessHasExited()
    {
        var log = new LogCapture();
        var pidfile = Path.Combine(_cwd, "pid");
        using var host = StartAsync(log, cue: "hang-after-result", Tracked(pidfile));

        var run = host.Client.QueryAsync(Prompt);
        bool? aliveAtResult = null;
        await foreach (var message in run)
        {
            if (message is ResultMessage)
            {
                aliveAtResult = IsAlive(int.Parse(await File.ReadAllTextAsync(pidfile)));
            }
        }

        Assert.IsFalse(aliveAtResult ?? true, "the result was handed out while the process was still alive, or never.");
    }

    [TestMethod]
    public async Task AProcessThatHangsOnStdinIsKilledWhenTheConnectIsCancelled()
    {
        var log = new LogCapture();
        var pidfile = Path.Combine(_cwd, "pid");
        using var host = StartAsync(log, cue: "hang-stdin", Tracked(pidfile));
        var agent = host.Client;

        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var stopwatch = Stopwatch.StartNew();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () => await agent.StartSessionAsync(cancel.Token));
        stopwatch.Stop();

        Assert.IsTrue(stopwatch.Elapsed < Patience, $"the cancelled connect took {stopwatch.Elapsed}.");
        await WaitUntilAsync(() => File.Exists(pidfile));
        await AssertGoneAsync(int.Parse(await File.ReadAllTextAsync(pidfile)));
    }

    [TestMethod]
    public async Task AProcessThatIgnoresStdinClosingIsKilledOnCloseWithinItsGrace()
    {
        var log = new LogCapture();
        var pidfile = Path.Combine(_cwd, "pid");
        using var host = StartAsync(log, cue: "hang-after-result", Tracked(pidfile));
        var session = await ConnectAsync(host);
        var pid = await RunToResultAsync(session, pidfile);

        var stopwatch = Stopwatch.StartNew();
        await session.CloseAsync();
        stopwatch.Stop();

        Assert.IsTrue(stopwatch.Elapsed < Patience, $"the close took {stopwatch.Elapsed}.");
        await AssertGoneAsync(pid);
    }

    [TestMethod]
    public async Task AProcessThatTrapsSigtermGetsToExitOnItAndIsNotKilled()
    {
        var log = new LogCapture();
        var pidfile = Path.Combine(_cwd, "pid");
        using var host = StartAsync(log, cue: "trap-term", Tracked(pidfile));
        var session = await ConnectAsync(host);
        var pid = await RunToResultAsync(session, pidfile);

        await session.CloseAsync();

        await AssertGoneAsync(pid);
        Assert.AreEqual("15", await File.ReadAllTextAsync(pidfile + ".term"), "SIGTERM did not reach the fake.");
        Assert.IsTrue(File.Exists(pidfile + ".exit"), "the fake did not get to exit on its own after SIGTERM.");
    }

    [TestMethod]
    public async Task AProcessThatIgnoresSigtermIsKilledAfterTheSecondGrace()
    {
        var log = new LogCapture();
        var pidfile = Path.Combine(_cwd, "pid");
        using var host = StartAsync(log, cue: "ignore-term", Tracked(pidfile));
        var session = await ConnectAsync(host);
        var pid = await RunToResultAsync(session, pidfile);

        await session.CloseAsync();

        await AssertGoneAsync(pid);
        Assert.AreEqual("15", await File.ReadAllTextAsync(pidfile + ".term"), "SIGTERM was not sent before the kill.");
        Assert.IsFalse(File.Exists(pidfile + ".exit"), "the fake exited on its own, so the kill proved nothing.");
    }

    [TestMethod]
    public async Task TheChildGetsTheSdksEnvironmentOverTheHostsAndTheCallersOverThat()
    {
        var log = new LogCapture();
        var pidfile = Path.Combine(_cwd, "pid");
        Environment.SetEnvironmentVariable("CLAUDECODE", "1");
        Environment.SetEnvironmentVariable("TRACESTATE", "stale=1");
        using var span = new Activity("mint").SetIdFormat(ActivityIdFormat.W3C).Start();
        try
        {
            using var host = StartAsync(log, cue: null, options =>
            {
                options.EnvironmentVariables["CLAUDE_FAKE_PIDFILE"] = pidfile;
                options.EnvironmentVariables["CLAUDE_CODE_ENTRYPOINT"] = "sdk-mint";
                options.Defaults.EnableFileCheckpointing = true;
            });

            var seen = await QueryAsync(host);
            Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CLAUDECODE", null);
            Environment.SetEnvironmentVariable("TRACESTATE", null);
        }

        var env = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(pidfile + ".env"))!;

        Assert.IsFalse(env.ContainsKey("CLAUDECODE"), "CLAUDECODE leaked into the child.");
        Assert.AreEqual(span.Id, env["TRACEPARENT"], "the active span did not reach the child.");
        Assert.IsFalse(env.ContainsKey("TRACESTATE"), "a stale TRACESTATE was paired with the fresh TRACEPARENT.");
        Assert.AreEqual("sdk-mint", env["CLAUDE_CODE_ENTRYPOINT"], "the caller's Env did not win over the SDK's entrypoint.");
        Assert.AreEqual(SdkVersion(), env["CLAUDE_AGENT_SDK_VERSION"], "the child was not told the assembly's own version.");
        Assert.AreEqual("true", env["CLAUDE_CODE_ENABLE_SDK_FILE_CHECKPOINTING"]);
        Assert.AreEqual(_cwd, env["PWD"]);
    }

    [TestMethod]
    public async Task TheChildIsToldItIsTheDotnetSdkWhenTheCallerSaysNothing()
    {
        var log = new LogCapture();
        var pidfile = Path.Combine(_cwd, "pid");
        using var host = StartAsync(log, cue: null, options => options.EnvironmentVariables["CLAUDE_FAKE_PIDFILE"] = pidfile);

        var seen = await QueryAsync(host);

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        var env = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(pidfile + ".env"))!;
        Assert.AreEqual("sdk-dotnet", env["CLAUDE_CODE_ENTRYPOINT"]);
        Assert.IsFalse(env.ContainsKey("CLAUDE_CODE_ENABLE_SDK_FILE_CHECKPOINTING"));
    }

    // The host resolves under the test's directory: the user's own is <cwd>/profile/.claude, and the
    // client's default <cwd>/profile/.config/claude-agent, which is not there until the client makes it.
    // The login is ~/.claude's, and the child is told CLAUDE_SECURESTORAGE_CONFIG_DIR set to nothing,
    // which the binary reads as ~/.claude under the credential store's own name: the variable reaches
    // the child's environment, set to nothing, and is not dropped.
    [TestMethod]
    public async Task TheChildRunsInTheDefaultConfigurationDirectoryMadeForItOnTheUsersLogin()
    {
        var log = new LogCapture();
        var pidfile = Path.Combine(_cwd, "pid");
        var configuration = TestHostEnvironments.DefaultConfigurationDirectory(_cwd);
        Assert.IsFalse(Directory.Exists(configuration));
        using var host = StartAsync(log, cue: null, options => options.EnvironmentVariables["CLAUDE_FAKE_PIDFILE"] = pidfile);

        var seen = await QueryAsync(host);

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        var env = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(pidfile + ".env"))!;
        Assert.AreEqual(configuration, env["CLAUDE_CONFIG_DIR"]);
        Assert.IsTrue(env.TryGetValue("CLAUDE_SECURESTORAGE_CONFIG_DIR", out var secureStorage), "the child was not told CLAUDE_SECURESTORAGE_CONFIG_DIR");
        Assert.AreEqual(string.Empty, secureStorage);
        Assert.AreEqual(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(configuration));
        Assert.IsFalse(Directory.Exists(TestHostEnvironments.UserDirectory(_cwd)), "the user's directory was made");
    }

    // An inherited CLAUDE_CONFIG_DIR names the user's directory and never the child's: the child runs in
    // the configured directory and finds the login in the inherited one, told it as the host inherited
    // it, spelled as it was spelled, since the binary names the credential store by that text as the
    // user's own claude does.
    [TestMethod]
    [DataRow("user")]
    [DataRow("user/")]
    [DataRow("elsewhere/../user")]
    public async Task AnInheritedConfigurationDirectoryIsTheUsersAndTheChildRunsInTheConfiguredOne(string spelling)
    {
        var log = new LogCapture();
        var pidfile = Path.Combine(_cwd, "pid");
        var user = Path.Combine(_cwd, "user");
        var inherited = _cwd + Path.DirectorySeparatorChar + spelling;
        var configured = Path.Combine(_cwd, "configured");
        _inherited["CLAUDE_CONFIG_DIR"] = inherited;
        using var host = StartAsync(log, cue: null, options =>
        {
            options.EnvironmentVariables["CLAUDE_FAKE_PIDFILE"] = pidfile;
            options.ConfigurationDirectory = configured;
        });

        var seen = await QueryAsync(host);

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        var env = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(pidfile + ".env"))!;
        Assert.AreEqual(configured, env["CLAUDE_CONFIG_DIR"]);
        Assert.AreEqual(inherited, env["CLAUDE_SECURESTORAGE_CONFIG_DIR"]);
        Assert.IsTrue(Directory.Exists(configured));
        Assert.IsFalse(Directory.Exists(user), "the user's directory was made");
    }

    // A secure-storage directory the host inherited is where the login is, and the child is told it as
    // the host inherited it: a path as it is spelled, and the variable set to nothing, which names
    // ~/.claude to the binary, set to nothing.
    [TestMethod]
    [DataRow("storage")]
    [DataRow("storage/")]
    [DataRow("elsewhere/../storage")]
    [DataRow(null)]
    public async Task AnInheritedSecureStorageDirectoryPassesThroughToTheChild(string? name)
    {
        var log = new LogCapture();
        var pidfile = Path.Combine(_cwd, "pid");
        var storage = name is null ? string.Empty : _cwd + Path.DirectorySeparatorChar + name;
        _inherited["CLAUDE_CONFIG_DIR"] = Path.Combine(_cwd, "user");
        _inherited["CLAUDE_SECURESTORAGE_CONFIG_DIR"] = storage;
        using var host = StartAsync(log, cue: null, options => options.EnvironmentVariables["CLAUDE_FAKE_PIDFILE"] = pidfile);

        var seen = await QueryAsync(host);

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        var env = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(pidfile + ".env"))!;
        Assert.IsTrue(env.TryGetValue("CLAUDE_SECURESTORAGE_CONFIG_DIR", out var secureStorage), "the child was not told CLAUDE_SECURESTORAGE_CONFIG_DIR");
        Assert.AreEqual(storage, secureStorage);
        Assert.AreEqual(TestHostEnvironments.DefaultConfigurationDirectory(_cwd), env["CLAUDE_CONFIG_DIR"]);
    }

    // A host a session of an SDK client started, which inherited that session's CLAUDE_CONFIG_DIR, the
    // default configuration directory: the client is made and its child runs there, on the login in
    // the secure-storage directory the host inherited, else in ~/.claude, which the child is told as the
    // variable set to nothing.
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AHostThatInheritedTheDefaultConfigurationDirectoryRunsItsChildThereOnTheUsersLogin(bool inheritedSecureStorage)
    {
        var log = new LogCapture();
        var pidfile = Path.Combine(_cwd, "pid");
        var configuration = TestHostEnvironments.DefaultConfigurationDirectory(_cwd);
        var storage = Path.Combine(_cwd, "storage");
        _inherited["CLAUDE_CONFIG_DIR"] = configuration;
        if (inheritedSecureStorage)
        {
            _inherited["CLAUDE_SECURESTORAGE_CONFIG_DIR"] = storage;
        }

        using var host = StartAsync(log, cue: null, options => options.EnvironmentVariables["CLAUDE_FAKE_PIDFILE"] = pidfile);

        var seen = await QueryAsync(host);

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        var env = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(pidfile + ".env"))!;
        Assert.AreEqual(configuration, env["CLAUDE_CONFIG_DIR"]);
        Assert.IsTrue(env.TryGetValue("CLAUDE_SECURESTORAGE_CONFIG_DIR", out var secureStorage), "the child was not told CLAUDE_SECURESTORAGE_CONFIG_DIR");
        Assert.AreEqual(inheritedSecureStorage ? storage : string.Empty, secureStorage);
    }

    [TestMethod]
    public async Task AConfigurationDirectoryThatCannotBeMadeIsAConnectionFailureBeforeAnyProcessStarts()
    {
        var log = new LogCapture();
        var pidfile = Path.Combine(_cwd, "pid");
        var file = Path.Combine(_cwd, "a-file");
        await File.WriteAllTextAsync(file, string.Empty);
        using var host = StartAsync(log, cue: null, options =>
        {
            options.EnvironmentVariables["CLAUDE_FAKE_PIDFILE"] = pidfile;
            options.ConfigurationDirectory = Path.Combine(file, "home");
        });

        var (_, failure) = await DrainAsync(host.Client.QueryAsync(Prompt));

        Assert.AreEqual("Connection", failure?.ErrorCode);
        StringAssert.Contains(failure!.Message, Path.Combine(file, "home"));
        Assert.IsFalse(File.Exists(pidfile), "a process started");
    }

    // A session that takes up another, asked to or from a live one, runs in the same configuration
    // directory, where the transcript it takes up is kept.
    [TestMethod]
    public async Task AResumedContinuedOrForkedSessionRunsInTheConfigurationDirectory()
    {
        var log = new LogCapture();
        var pidfile = Path.Combine(_cwd, "pid");
        var configuration = TestHostEnvironments.DefaultConfigurationDirectory(_cwd);
        using var host = StartAsync(log, cue: null, options => options.EnvironmentVariables["CLAUDE_FAKE_PIDFILE"] = pidfile);
        var earlier = Guid.NewGuid().ToString();

        foreach (var (asked, argument) in new (ClaudeAgentSessionOptions, string)[]
        {
            (new() { ResumeSessionId = earlier }, "--resume=" + earlier),
            (new() { ContinueLatest = true }, "--continue"),
            (new() { ResumeSessionId = earlier, Fork = true }, "--fork-session"),
        })
        {
            var (seen, failure) = await DrainAsync(host.Client.QueryAsync(Prompt, asked));

            Assert.IsNull(failure, argument);
            Assert.IsInstanceOfType<ResultMessage>(seen[^1], argument);
            Assert.AreEqual(configuration, (await ChildEnvironmentAsync(pidfile))["CLAUDE_CONFIG_DIR"], argument);
            Assert.IsTrue((await ChildArgumentsAsync(pidfile)).Any(value => value.StartsWith(argument, StringComparison.Ordinal)), argument);
        }

        await using var session = await ConnectAsync(host);
        Assert.IsInstanceOfType<ResultMessage>((await DrainAsync(session.SendAsync(Prompt))).Messages[^1]);
        foreach (var (takeUp, argument) in new (Func<Task<ClaudeAgentSession>>, string)[]
        {
            (() => session.ForkAsync(), "--fork-session"),
            (() => session.ResumeAsync(), "--resume="),
        })
        {
            await using var taken = await takeUp();
            var (seen, failure) = await DrainAsync(taken.SendAsync(Prompt));

            Assert.IsNull(failure, argument);
            Assert.IsInstanceOfType<ResultMessage>(seen[^1], argument);

            Assert.AreEqual(configuration, (await ChildEnvironmentAsync(pidfile))["CLAUDE_CONFIG_DIR"], argument);
            Assert.IsTrue((await ChildArgumentsAsync(pidfile)).Any(value => value.StartsWith(argument, StringComparison.Ordinal)), argument);
        }
    }

    [TestMethod]
    public async Task AWorkingDirectoryThatDoesNotExistIsItsOwnErrorBeforeAnyProcessStarts()
    {
        var log = new LogCapture();
        var missing = Path.Combine(_cwd, "gone");
        using var host = StartAsync(log, cue: null, options =>
        {
            options.ExecutablePath = Path.Combine(_cwd, "no-such-claude");
            options.Defaults.WorkingDirectory = missing;
        });
        var agent = host.Client;

        var refused = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => agent.StartSessionAsync());

        Assert.AreEqual(ErrorCodes.WorkingDirectoryNotFound, refused.ErrorCode);
        StringAssert.Contains(refused.Message, missing);

        var thrown = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(async () =>
        {
            await foreach (var _ in agent.QueryAsync(Prompt))
            {
            }
        });
        Assert.AreEqual(ErrorCodes.WorkingDirectoryNotFound, thrown.ErrorCode);
    }

    [TestMethod]
    public async Task AHostThatExitsWithoutClosingLeavesNoClaudeBehind() => await AssertReapedAsync(Leaving.Returns);

    [TestMethod]
    public async Task AHostThatCrashesLeavesNoClaudeBehind() => await AssertReapedAsync(Leaving.Crashes);

    [TestMethod]
    public async Task AHostStoppedBySigtermThroughItsLifetimeLeavesNoClaudeBehind() => await AssertReapedAsync(Leaving.Terminated);

    private enum Leaving
    {
        Returns,
        Crashes,
        Terminated,
    }

    private async Task AssertReapedAsync(Leaving leaving)
    {
        var pidfile = Path.Combine(_cwd, "pid");
        var startInfo = new ProcessStartInfo
        {
            FileName = TestExecutable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in TestExecutableArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add(OrphanHost.Flag);
        startInfo.ArgumentList.Add(Cued("hang-after-result"));
        startInfo.ArgumentList.Add(_cwd);
        startInfo.ArgumentList.Add(pidfile);
        switch (leaving)
        {
            case Leaving.Crashes:
                startInfo.ArgumentList.Add(OrphanHost.Crash);
                break;
            case Leaving.Terminated:
                startInfo.ArgumentList.Add(OrphanHost.Linger);
                break;
            default:
                break;
        }

        // The fake inherits the child's stdout and stderr, so a fake that outlives the child
        // holds both pipes open: every read here is bounded by the patience, never awaited bare.
        using var child = System.Diagnostics.Process.Start(startInfo)!;
        var stderr = Bounded(child.StandardError.ReadToEndAsync());
        if (leaving == Leaving.Terminated)
        {
            string? started;
            do
            {
                started = await Bounded(child.StandardOutput.ReadLineAsync());
            }
            while (started is not null && started != OrphanHost.Lingering && !started.StartsWith('<'));

            Assert.AreEqual(OrphanHost.Lingering, started, $"the child host never got to linger.\n{await stderr}");
            Assert.IsTrue(Posix.Terminate(child.Id), "SIGTERM could not be sent to the child host.");
        }

        var stdout = Bounded(child.StandardOutput.ReadToEndAsync());
        using var deadline = new CancellationTokenSource(Patience);
        await child.WaitForExitAsync(deadline.Token);

        Assert.IsTrue(File.Exists(pidfile), $"the fake never started under the child host.\n{await stdout}\n{await stderr}");
        var pid = int.Parse(await File.ReadAllTextAsync(pidfile));
        if (leaving == Leaving.Crashes)
        {
            Assert.AreNotEqual(0, child.ExitCode, "the child host did not crash.");
        }
        else
        {
            Assert.AreEqual(0, child.ExitCode, $"the child host failed.\n{await stdout}\n{await stderr}");
        }

        await AssertGoneAsync(pid);
    }

    private static async Task<string?> Bounded<T>(Task<T> read)
        where T : class?
    {
        try
        {
            return await read.WaitAsync(Patience) as string;
        }
        catch (TimeoutException)
        {
            return "<the pipe stayed open past the patience>";
        }
    }

    private static readonly TimeSpan Grace = TimeSpan.FromMilliseconds(200);

    private static void Brisk(ClaudeAgentClientOptions options) => options.ProcessGracePeriod = Grace;

    private static Action<ClaudeAgentClientOptions> Tracked(string pidfile) => options =>
    {
        Brisk(options);
        options.EnvironmentVariables["CLAUDE_FAKE_PIDFILE"] = pidfile;
    };

    // The real process transport, not a fixture: the client is built on its defaults with the
    // fake as the executable, and its transports hand what they read and write to the tap when one
    // is given.
    private TestHost StartAsync(
        LogCapture log,
        string? cue,
        Action<ClaudeAgentClientOptions>? more = null,
        TransportTap? tap = null)
    {
        var options = new ClaudeAgentClientOptions { ExecutablePath = Cued(cue) };
        options.Defaults.WorkingDirectory = _cwd;
        options.Defaults.MaxTurns = 1;
        more?.Invoke(options);

        return TestHost.Real(options, log, TestHostEnvironments.Under(_cwd, _inherited), tap);
    }

    private string Cued(string? cue)
    {
        if (cue is null)
        {
            return FakeClaude;
        }

        var link = Path.Combine(_cwd, $"claude-{cue}");
        if (!Path.Exists(link))
        {
            File.CreateSymbolicLink(link, FakeClaude);
        }

        return link;
    }

    private static async Task<ImmutableArray<AgentMessage>> QueryAsync(TestHost host)
    {
        var agent = host.Client;
        var seen = ImmutableArray.CreateBuilder<AgentMessage>();
        await foreach (var message in agent.QueryAsync(Prompt))
        {
            seen.Add(message);
        }

        return seen.ToImmutable();
    }

    private static async Task<int> RunToResultAsync(ClaudeAgentSession session, string pidfile)
    {
        var seen = new List<AgentMessage>();
        await foreach (var message in session.SendAsync(Prompt))
        {
            seen.Add(message);
        }

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        var pid = int.Parse(await File.ReadAllTextAsync(pidfile));
        Assert.IsTrue(IsAlive(pid), "the fake exited on its own, so the close proved nothing.");
        return pid;
    }

    private static Task<ClaudeAgentSession> ConnectAsync(TestHost host) => host.Client.StartSessionAsync();

    private static async Task<Dictionary<string, string>> ChildEnvironmentAsync(string pidfile) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(pidfile + ".env"))!;

    private static async Task<string[]> ChildArgumentsAsync(string pidfile) =>
        (await File.ReadAllTextAsync(pidfile + ".args")).Split('\n');

    private static async Task<(ImmutableArray<AgentMessage> Messages, ClaudeAgentException? Failure)> DrainAsync(
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

    private static bool IsAlive(int pid) => Directory.Exists($"/proc/{pid}") && !IsZombie(pid);

    private static bool IsZombie(int pid)
    {
        try
        {
            return File.ReadAllText($"/proc/{pid}/stat").Contains(") Z ", StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return true;
        }
    }

    private static async Task AssertGoneAsync(int pid)
    {
        await WaitUntilAsync(() => !IsAlive(pid));
        Assert.IsFalse(IsAlive(pid), $"process {pid} is still alive.");
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }

    // What the transport tells the child: the library's informational version without its build
    // metadata. Read off the assembly here so a version bump in Directory.Build.props moves both sides.
    private static string SdkVersion()
    {
        var informational = typeof(SubprocessCliTransport).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        Assert.IsFalse(string.IsNullOrEmpty(informational), "the library carries no informational version.");
        var plus = informational!.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? informational : informational[..plus];
    }
}

internal sealed class LogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<(LogLevel Level, string Message)> _lines = new();

    public IEnumerable<string> Warnings =>
        _lines.Where(line => line.Level >= LogLevel.Warning).Select(line => line.Message).ToArray();

    public ILogger CreateLogger(string categoryName) => new Logger(_lines);

    public void Dispose()
    {
    }

    private sealed class Logger(ConcurrentQueue<(LogLevel Level, string Message)> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            lines.Enqueue((logLevel, formatter(state, exception)));
    }
}

// A TextWriter that keeps all the text the transport writes to it.
internal sealed class TextCapture : TextWriter
{
    private readonly Lock _lock = new();
    private readonly StringBuilder _text = new();

    public override Encoding Encoding => Encoding.UTF8;

    public string Text
    {
        get
        {
            lock (_lock)
            {
                return _text.ToString();
            }
        }
    }

    public override void Write(char value)
    {
        lock (_lock)
        {
            _text.Append(value);
        }
    }

    public override void Write(char[] buffer, int index, int count)
    {
        lock (_lock)
        {
            _text.Append(buffer, index, count);
        }
    }

    public override void Write(string? value)
    {
        lock (_lock)
        {
            _text.Append(value);
        }
    }
}
