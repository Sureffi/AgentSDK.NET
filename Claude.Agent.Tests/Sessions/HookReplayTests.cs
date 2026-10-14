using System.Collections.Immutable;
using Claude.Agent.Tests.Replay;
using System.Text.Json;

namespace Claude.Agent.Tests.Sessions;

[TestClass]
public sealed class HookReplayTests
{
    private const string Prompt = "Read notes.txt in the working directory and reply with its second line only.";
    private const string ReadToolUseId = "toolu_01FS2BYgfJPaPgcCRmeoMwZK";
    private const string PreToolUseRequestId = "65b2c8d6-a19d-41cb-b253-98679236f55c";
    private const string RecordedCwd = "/tmp/claude-agent-fixtures/cwd";

    private const string ToolsPrompt =
        "Do these steps in order, one tool call each unless a step says otherwise, without explaining: "
        + "1. Glob the pattern *.txt. 2. Grep the pattern line in notes.txt with output mode content. "
        + "3. In one message, Read notes.txt and Read cells.ipynb together. 4. Write draft.txt containing exactly: alpha. "
        + "5. Edit draft.txt replacing alpha with beta. "
        + "6. NotebookEdit cells.ipynb: replace the source of the first cell with print(2). "
        + "7. TodoWrite a list with one item, content check the draft, status completed. "
        + "8. WebFetch https://example.com with the prompt: what is the page title? "
        + "9. Run the shell command `wc -l notes.txt` with Bash. 10. Read missing.txt. "
        + "Then reply with exactly the word: done";

    private const string MissingReadToolUseId = "toolu_019eHJkRnaabo71vnq3687Cx";

    private const string BackgroundStopPrompt =
        "Use the Bash tool with run_in_background set to true to run: sleep 5. Then reply with exactly the word: started";

    private static readonly string[] BuiltInTools =
        ["Glob", "Grep", "Read", "Write", "Edit", "NotebookEdit", "TodoWrite", "WebFetch", "Bash"];

    [TestMethod]
    public async Task HooksFixtureCallsEachRegisteredBehaviourOnceWithItsTypedInput()
    {
        var log = new HookLog();
        await using var server = FixtureServer.Start("hooks");
        using var host = TestHost.Start(server, options => RegisterFixtureHooks(options, log));

        await ReplayAsync(host);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        Assert.AreEqual(4, log.Inputs.Length);

        var submit = log.Only<UserPromptSubmitHookInput>();
        Assert.AreEqual(Prompt, submit.Prompt);

        var pre = log.Only<PreToolUseHookInput>();
        Assert.AreEqual("Read", pre.ToolCall.Name);
        Assert.AreEqual(ReadToolUseId, pre.ToolCall.Id);

        var post = log.Only<PostToolUseHookInput>();
        Assert.AreEqual("Read", post.ToolCall.Name);
        Assert.AreEqual(ReadToolUseId, post.ToolCall.Id);

        Assert.IsFalse(log.Only<StopHookInput>().IsStopHookActive);
    }

    // One client, and what differs is on the call: its model, its tools, its caps, its hooks and its
    // permission callback, over defaults that say otherwise. The argv is the recorded one, and the
    // server holds the stdin to the recording as it closes.
    [TestMethod]
    public async Task WhatACallAsksOverTheDefaultsIsWhatTheBinaryIsStartedWithAndToldOf()
    {
        var log = new HookLog();
        await using var server = FixtureServer.Start("hooks");
        using var host = TestHost.Start(server, defaults =>
        {
            defaults.WorkingDirectory = "/tmp/claude-agent-fixtures/cwd";
            defaults.SettingSources = [];
            defaults.Model = "opus";
            defaults.MaxTurns = 9;
            defaults.Tools = [];
            defaults.PermissionPromptTool = "mcp__calc__ask";
        });

        var asked = new ClaudeAgentSessionOptions
        {
            Model = "haiku",
            MaxTurns = 3,
            Tools = ["Read"],
            Effort = Effort.Low,
            IncludeHookEvents = true,
            PermissionCallback = (_, _) => Task.FromResult(ToolPermissionResult.Allow()),
        };
        RegisterFixtureHooks(asked, log);

        var seen = new List<AgentMessage>();
        await foreach (var message in host.Client.QueryAsync(Prompt, asked))
        {
            seen.Add(message);
        }

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        CollectionAssert.AreEqual(Fixture.Load("hooks").Argv.ToArray(), host.Transports.Last!.Argv.ToArray(), "hooks.argv.json");
        Assert.AreEqual(4, log.Inputs.Length);
    }

    // Hooks accumulate. The recording registered four; here the client's defaults bring the first two
    // and the call the last two, and the initialize that goes out is the recorded line: the defaults'
    // first, the call's after, each under the callback id the recording gave it. All four are called.
    [TestMethod]
    public async Task TheDefaultsHooksRunAndTheCallsRunToo()
    {
        var log = new HookLog();
        await using var server = FixtureServer.Start("hooks");
        using var host = TestHost.Start(server, defaults => RegisterFixtureHooks(defaults, log, take: 2));
        var asked = new ClaudeAgentSessionOptions();
        RegisterFixtureHooks(asked, log, skip: 2);

        var seen = new List<AgentMessage>();
        await foreach (var message in host.Client.QueryAsync(Prompt, asked))
        {
            seen.Add(message);
        }

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        Assert.AreEqual(4, host.Transports.Last!.Session.Hooks.Count);
        Assert.AreEqual(4, log.Inputs.Length);
        Assert.AreEqual("Read", log.Only<PreToolUseHookInput>().ToolCall.Name);
        Assert.IsFalse(log.Only<StopHookInput>().IsStopHookActive);
    }

    [TestMethod]
    public async Task HooksFixtureWritesTheInitializeAndTheFourRecordedReplies()
    {
        var log = new HookLog();
        await using var server = FixtureServer.Start("hooks");
        using var host = TestHost.Start(server, options => RegisterFixtureHooks(options, log));

        await ReplayAsync(host);
        await server.WaitForWrites(6, TimeSpan.FromSeconds(10));

        var recorded = Fixture.Load("hooks").Stdin;
        Assert.AreEqual(Fixture.Anonymise(recorded[0]), Fixture.Anonymise(server.Written[0]));
        Assert.AreEqual(recorded[1], server.Written[1]);
        CollectionAssert.AreEqual(
            recorded.Skip(2).ToArray(),
            server.Written.Where(IsControlResponse).ToArray());
    }

    [TestMethod]
    public async Task HooksFixtureClosesStdinOnceTheResultArrives()
    {
        var log = new HookLog();
        await using var server = FixtureServer.Start("hooks", endAfterLastLine: false);
        using var host = TestHost.Start(server, options => RegisterFixtureHooks(options, log));

        await ReplayAsync(host);
        await server.Completed.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        Assert.IsTrue(server.StdinEnded);
    }

    [TestMethod]
    public async Task APreToolUseHookThatDeniesWritesTheDenyAndIsCalledOnce()
    {
        var calls = 0;

        // One hook where the recording registered four, answering deny where it answered nothing.
        await using var server = FixtureServer.Start("hooks", stdinDiffers: true);
        using var host = TestHost.Start(server, options => options.AddHook(HookEvent.PreToolUse, "Read", (_, _) =>
        {
            calls++;
            return Task.FromResult(new HookOutput { Decision = PermissionDecision.Deny, DecisionReason = "fixture denies" });
        }));

        await ReplayAsync(host);
        await server.WaitForWrites(6, TimeSpan.FromSeconds(10));

        Assert.AreEqual(1, calls);
        Assert.AreEqual("deny", PermissionDecisionIn(server.Written, PreToolUseRequestId));
    }

    // The turn ends with the shell the model started in the background still sleeping, and the
    // binary lists it on the Stop hook's input.
    [TestMethod]
    public async Task AStopHookIsToldOfTheBackgroundShellStillRunningAsTheTurnEnds()
    {
        var log = new HookLog();
        await using var server = FixtureServer.Start("background-stop");
        using var host = TestHost.Start(server, options =>
        {
            options.WorkingDirectory = RecordedCwd;
            options.SettingSources = [];
            options.Model = "haiku";
            options.MaxTurns = 3;
            options.Effort = Effort.Low;
            options.Tools = ["Bash"];
            options.AllowedTools.Add("Bash");
            options.AddHook(HookEvent.Stop, null, (input, _) => log.Record(input, new HookOutput()));
        });

        var seen = new List<AgentMessage>();
        await foreach (var message in host.Client.QueryAsync(BackgroundStopPrompt))
        {
            seen.Add(message);
        }

        Assert.AreEqual("started", ((ResultMessage)seen[^1]).Result);

        var task = (ShellStopHookBackgroundTask)log.Only<StopHookInput>().BackgroundTasks.Single();
        Assert.AreEqual("b37r3lb38", task.Id);
        Assert.AreEqual(StopHookBackgroundTaskType.Shell, task.TaskType);
        Assert.AreEqual(BackgroundTaskStatus.Running, task.Status);
        Assert.AreEqual("Sleep for 5 seconds in background", task.Description);
        Assert.AreEqual("sleep 5", task.Command);

        var recorded = Fixture.Load("background-stop");
        await server.WaitForWrites(recorded.Stdin.Length, TimeSpan.FromSeconds(10));
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        CollectionAssert.AreEqual(recorded.Stdin.Anonymised(), server.Written.Anonymised());
        CollectionAssert.AreEqual(recorded.Argv.ToArray(), host.Transports.Last!.Argv.ToArray(), "background-stop.argv.json");
    }

    // Nine built-in tools asked for and allowed. 2.1.276 did not offer TodoWrite, and the model
    // said so and went on to the next step.
    [TestMethod]
    public async Task TheToolsFixtureCallsTheBuiltInToolsInTheOrderAskedAndSkipsTheTodoWriteItWasNotOffered()
    {
        var (seen, _, _) = await ReplayToolsAsync();

        var init = seen.OfType<SystemInitMessage>().Single();
        CollectionAssert.IsSubsetOf(
            new[] { "Bash", "Edit", "Glob", "Grep", "NotebookEdit", "Read", "WebFetch", "Write" },
            init.Tools.ToArray());
        CollectionAssert.DoesNotContain(init.Tools.ToArray(), "TodoWrite");
        Assert.IsTrue(seen.OfType<AssistantMessage>().Any(
            message => message.Text == "**Step 7: TodoWrite** - This tool is not available, skipping.\n\n**Step 8: WebFetch**"));

        var calls = seen.OfType<AssistantMessage>().SelectMany(message => message.ToolUses).ToArray();
        CollectionAssert.AreEqual(
            new[] { "Glob", "Grep", "Read", "Read", "Write", "Edit", "NotebookEdit", "WebFetch", "Bash", "Read" },
            calls.Select(call => call.Name).ToArray());

        // Two reads in one message: one API message, streamed as two assistant frames.
        var reads = seen.OfType<AssistantMessage>().Where(message => message.ToolUses.Any(call => call.Name == "Read")).ToArray();
        Assert.AreEqual("msg_011CfAih3HDoWhbBL5ZxZraG", reads[0].ApiMessageId);
        Assert.AreEqual("msg_011CfAih3HDoWhbBL5ZxZraG", reads[1].ApiMessageId);
        Assert.AreEqual($"{RecordedCwd}/cells.ipynb", reads[1].ToolUses.Single().RawInput.GetProperty("file_path").GetString());

        var results = seen.OfType<UserMessage>()
            .SelectMany(message => message.Content)
            .OfType<ToolResultBlock>()
            .ToArray();
        CollectionAssert.AreEqual(calls.Select(call => call.Id).ToArray(), results.Select(block => block.ToolUseId).ToArray());
        Assert.AreEqual(MissingReadToolUseId, results[^1].ToolUseId);
        Assert.AreEqual(true, results[^1].IsError);
        Assert.IsFalse(results[..^1].Any(block => block.IsError is true));

        var result = (ResultMessage)seen[^1];
        Assert.AreEqual("done", result.Result);
        Assert.AreEqual(11, result.TurnCount);
    }

    // Each call starts right after the assistant message that asks for it and closes once, right
    // after the user message that answers it; the two reads of one API message are two calls.
    [TestMethod]
    public async Task EveryToolCallOfTheToolsFixtureStartsAfterItsAssistantMessageAndClosesOnceAfterItsAnswer()
    {
        var (seen, _, _) = await ReplayToolsAsync();

        var calls = seen.OfType<AssistantMessage>().SelectMany(message => message.ToolUses).ToArray();
        var snapshots = seen.OfType<ToolCallMessage>().Select(message => message.ToolCall).ToArray();
        Assert.AreEqual(2 * calls.Length, snapshots.Length);

        foreach (var call in calls)
        {
            var own = snapshots.Where(snapshot => snapshot.Id == call.Id).ToArray();
            Assert.AreEqual(2, own.Length, call.Id);
            Assert.AreEqual(ToolCallStatus.Started, own[0].Status, call.Id);
            Assert.AreNotEqual(ToolCallStatus.Started, own[1].Status, call.Id);
            Assert.AreNotEqual(ToolCallStatus.Unanswered, own[1].Status, call.Id);
            Assert.IsTrue(own.All(snapshot => snapshot.Name == call.Name && snapshot.ParentToolUseId is null), call.Id);
            Assert.AreEqual(call.RawInput.GetRawText(), own[1].RawInput.GetRawText(), call.Id);
        }

        for (var index = 0; index < seen.Count; index++)
        {
            if (seen[index] is not ToolCallMessage { ToolCall: var snapshot })
            {
                continue;
            }

            var before = seen.Take(index).Last(message => message is not ToolCallMessage);
            if (snapshot.Status == ToolCallStatus.Started)
            {
                Assert.IsTrue(((AssistantMessage)before).ToolUses.Any(use => use.Id == snapshot.Id), snapshot.Id);
            }
            else
            {
                Assert.IsTrue(((UserMessage)before).Content.OfType<ToolResultBlock>().Any(answer => answer.ToolUseId == snapshot.Id), snapshot.Id);
            }
        }

        var reads = snapshots.Where(snapshot => snapshot.Name == "Read" && snapshot.Status == ToolCallStatus.Answered).ToArray();
        CollectionAssert.AreEqual(
            new[] { "text", "notebook" },
            reads.Select(read => read.RawOutput?.GetProperty("type").GetString()).ToArray());
        Assert.AreEqual("1\tfirst line\n2\tsecond line\n3\t", reads[0].Content.Text());
        Assert.IsTrue(reads.All(read => read.Error is null));
        Assert.IsInstanceOfType<ReadTextToolOutput>(reads[0].Output);
        Assert.IsInstanceOfType<ReadNotebookToolOutput>(reads[1].Output);

        var failed = snapshots.Single(snapshot => snapshot.Status == ToolCallStatus.Failed);
        Assert.AreEqual(MissingReadToolUseId, failed.Id);
        Assert.AreEqual($"File does not exist. Note: your current working directory is {RecordedCwd}.", failed.Error);
        Assert.IsNull(failed.RawOutput);
        Assert.IsNull(failed.Output);

        var answering = seen.OfType<UserMessage>().Last();
        Assert.AreEqual(
            $"Error: File does not exist. Note: your current working directory is {RecordedCwd}.",
            answering.RawToolOutput?.GetString());
        Assert.IsNull(answering.ToolOutput);
    }

    [TestMethod]
    public async Task EveryToolCallReachesThePreToolUseHookAndItsCloseThePostToolUseOrThePostToolUseFailureHook()
    {
        var (seen, log, _) = await ReplayToolsAsync();

        CollectionAssert.AreEqual(
            new[]
            {
                "PreToolUse Glob", "PostToolUse Glob",
                "PreToolUse Grep", "PostToolUse Grep",
                "PreToolUse Read", "PostToolUse Read",
                "PreToolUse Read", "PostToolUse Read",
                "PreToolUse Write", "PostToolUse Write",
                "PreToolUse Edit", "PostToolUse Edit",
                "PreToolUse NotebookEdit", "PostToolUse NotebookEdit",
                "PreToolUse WebFetch", "PostToolUse WebFetch",
                "PreToolUse Bash", "PostToolUse Bash",
                "PreToolUse Read", "PostToolUseFailure Read",
            },
            log.Inputs.Select(input => $"{input.Event} {HookLog.Tool(input)}").ToArray());

        var calls = seen.OfType<AssistantMessage>().SelectMany(message => message.ToolUses).ToArray();
        CollectionAssert.AreEqual(
            calls.SelectMany(call => new[] { call.Id, call.Id }).ToArray(),
            log.Inputs.Select(HookLog.ToolUseId).ToArray());

        var failed = log.Only<PostToolUseFailureHookInput>();
        Assert.AreEqual(MissingReadToolUseId, failed.ToolCall.Id);
        Assert.AreEqual($"File does not exist. Note: your current working directory is {RecordedCwd}.", failed.ToolCall.Error);
        Assert.AreEqual(false, failed.IsInterrupt);
        Assert.AreEqual($"{RecordedCwd}/missing.txt", failed.ToolCall.RawInput.GetProperty("file_path").GetString());
    }

    [TestMethod]
    public async Task TheToolsFixtureWritesTheInitializeThePromptAndTwentyEmptyHookReplies()
    {
        var (_, _, written) = await ReplayToolsAsync();

        var recorded = Fixture.Load("tools").Stdin;
        Assert.AreEqual(22, recorded.Length);
        CollectionAssert.AreEqual(
            recorded.Anonymised(),
            written.Anonymised());
    }

    private static async Task<(List<AgentMessage> Seen, HookLog Log, ImmutableArray<string> Written)> ReplayToolsAsync()
    {
        var log = new HookLog();
        await using var server = FixtureServer.Start("tools");
        using var host = TestHost.Start(server, options =>
        {
            options.WorkingDirectory = RecordedCwd;
            options.SettingSources = [];
            options.Model = "haiku";
            options.MaxTurns = 16;
            options.Effort = Effort.Low;
            options.Tools = [.. BuiltInTools];
            foreach (var tool in BuiltInTools)
            {
                options.AllowedTools.Add(tool);
            }

            foreach (var hookEvent in new[] { HookEvent.PreToolUse, HookEvent.PostToolUse, HookEvent.PostToolUseFailure })
            {
                options.AddHook(hookEvent, null, (input, _) => log.Record(input, new HookOutput()));
            }
        });

        var seen = new List<AgentMessage>();
        await foreach (var message in host.Client.QueryAsync(ToolsPrompt))
        {
            seen.Add(message);
        }

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        await server.WaitForWrites(Fixture.Load("tools").Stdin.Length, TimeSpan.FromSeconds(10));
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        return (seen, log, server.Written);
    }

    private static async Task ReplayAsync(TestHost host)
    {
        var agent = host.Client;
        var seen = new List<AgentMessage>();
        await foreach (var message in agent.QueryAsync(Prompt))
        {
            seen.Add(message);
        }

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
    }

    // The four the recording registered, in its order; `skip` and `take` split them between two
    // options without changing that order.
    private static void RegisterFixtureHooks(ClaudeAgentSessionOptions options, HookLog log, int skip = 0, int take = 4)
    {
        (HookEvent Event, string? Matcher, HookOutput Output)[] recorded =
        [
            (HookEvent.PreToolUse, "Read", new HookOutput { Decision = PermissionDecision.Allow, DecisionReason = "fixture allows" }),
            (HookEvent.PostToolUse, null, new HookOutput { AdditionalContext = "fixture saw the read" }),
            (HookEvent.UserPromptSubmit, null, new HookOutput { AdditionalContext = "the file is small" }),
            (HookEvent.Stop, null, new HookOutput()),
        ];

        foreach (var (hookEvent, matcher, output) in recorded.Skip(skip).Take(take))
        {
            options.AddHook(hookEvent, matcher, (input, ct) => log.Record(input, output));
        }
    }

    private static bool IsControlResponse(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.TryGetProperty("type", out var type)
            && type.GetString() == "control_response";
    }

    private static string? PermissionDecisionIn(ImmutableArray<string> written, string requestId)
    {
        foreach (var line in written)
        {
            using var document = JsonDocument.Parse(line);
            if (!document.RootElement.TryGetProperty("response", out var response)
                || !response.TryGetProperty("request_id", out var id)
                || id.GetString() != requestId)
            {
                continue;
            }

            return response.GetProperty("response")
                .GetProperty("hookSpecificOutput")
                .GetProperty("permissionDecision")
                .GetString();
        }

        return null;
    }
}

internal sealed class HookLog
{
    private readonly Lock _gate = new();
    private readonly List<HookInput> _inputs = [];

    public ImmutableArray<HookInput> Inputs
    {
        get
        {
            lock (_gate)
            {
                return [.. _inputs];
            }
        }
    }

    public Task<HookOutput> Record(HookInput input, HookOutput output)
    {
        lock (_gate)
        {
            _inputs.Add(input);
        }

        return Task.FromResult(output);
    }

    public HookInput Only(HookEvent hookEvent)
    {
        var matching = Inputs.Where(input => input.Event == hookEvent).ToArray();
        Assert.AreEqual(1, matching.Length, $"expected exactly one {hookEvent}");
        return matching[0];
    }

    public T Only<T>()
        where T : HookInput
    {
        var matching = Inputs.OfType<T>().ToArray();
        Assert.AreEqual(1, matching.Length, $"expected exactly one {typeof(T).Name}");
        return matching[0];
    }

    // The tool a hook input is about, where a test only wants the name.
    public static string? Tool(HookInput input) => input switch
    {
        PreToolUseHookInput pre => pre.ToolCall.Name,
        PostToolUseHookInput post => post.ToolCall.Name,
        PostToolUseFailureHookInput failed => failed.ToolCall.Name,
        PermissionRequestHookInput asked => asked.ToolCall.Name,
        _ => null,
    };

    public static string? ToolUseId(HookInput input) => input switch
    {
        PreToolUseHookInput pre => pre.ToolCall.Id,
        PostToolUseHookInput post => post.ToolCall.Id,
        PostToolUseFailureHookInput failed => failed.ToolCall.Id,
        _ => null,
    };
}
