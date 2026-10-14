using System.Text.Json;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Wire;

[TestClass]
public sealed class FrameCodecMessageTests
{
    private const string RecordedCwd =
        "/tmp/claude-agent-fixtures/cwd";

    private static readonly string[] ConnectorTools =
    [
        "mcp__claude_ai_Claude_Docs__batch", "mcp__claude_ai_Claude_Docs__create", "mcp__claude_ai_Claude_Docs__delete",
        "mcp__claude_ai_Claude_Docs__export", "mcp__claude_ai_Claude_Docs__guide", "mcp__claude_ai_Claude_Docs__query",
        "mcp__claude_ai_Claude_Docs__read", "mcp__claude_ai_Claude_Docs__update",
    ];

    [TestMethod]
    public void TheInitFrameCarriesTheModelWorkingDirectoryToolsAndCliVersion()
    {
        var init = Recorded.Messages<SystemInitMessage>("trivial").Single();

        Assert.AreEqual("init", init.Subtype);
        Assert.AreEqual("claude-haiku-4-5-20251001", init.Model);
        Assert.AreEqual(RecordedCwd, init.WorkingDirectory);
        Assert.AreEqual(33, init.Tools.Count);
        Assert.AreEqual("2.1.276", init.ClaudeCodeVersion);
        Assert.AreEqual("default", init.PermissionMode);
    }

    // The three keys the 2.1.283 init frame carries and the 2.1.276 one does not: where the session's
    // scratch files go, whether effort goes out per turn for its model, and its view.
    [TestMethod]
    public void TheInitFrameCarriesTheScratchpadThePerTurnEffortAndTheViewWhereTheBinaryWritesThem()
    {
        var init = Recorded.Messages<SystemInitMessage>("schema-type").Single();

        Assert.AreEqual("2.1.283", init.ClaudeCodeVersion);
        Assert.AreEqual("/tmp/claude-1000/-tmp-claude-agent-fixtures-cwd/c732cbba-a19c-4fe5-8e7b-f3714b2dcb5b/scratchpad", init.ScratchpadDirectory);
        Assert.AreEqual(false, init.IsPerTurnEffortActive);
        Assert.AreEqual(ViewMode.Default, init.ViewMode);
        Assert.IsTrue(init.ViewMode!.Value.IsDefined);

        var earlier = Recorded.Messages<SystemInitMessage>("trivial").Single();
        Assert.AreEqual("2.1.276", earlier.ClaudeCodeVersion);
        Assert.IsNull(earlier.ScratchpadDirectory);
        Assert.IsNull(earlier.IsPerTurnEffortActive);
        Assert.IsNull(earlier.ViewMode);
    }

    [TestMethod]
    public void TheStatusFrameIsItsOwnSystemMessage()
    {
        var status = Recorded.Messages<SystemMessage>("streaming").Single(notice => notice.Subtype == "status");

        Assert.AreEqual("requesting", Text(status.Line(), "status"));
        Assert.IsNull(Text(status.Line(), "permissionMode"));
    }

    [TestMethod]
    public void TheStatusFrameThatFollowsAModeChangeCarriesTheNewMode()
    {
        var status = Recorded.Messages<SystemMessage>("client").Single(notice => notice.Subtype == "status");

        Assert.IsNull(Text(status.Line(), "status"));
        Assert.AreEqual("acceptEdits", Text(status.Line(), "permissionMode"));
    }

    // Each frame names a prompt the client wrote, by the uuid it was written with, and carries a uuid
    // of its own and the session's id. The four states the recordings hold: continuation's three,
    // and cancel's cancelled after each interrupted result.
    [TestMethod]
    public void TheCommandLifecycleFrameNamesThePromptItsStateItsOwnUuidAndTheSession()
    {
        var lifecycles = Recorded.Messages<CommandLifecycleMessage>("continuation").ToList();
        var prompts = Fixture.PromptUuids(Fixture.Load("continuation").Stdin);

        var first = lifecycles[0];
        Assert.AreEqual(prompts[0], first.CommandUuid);
        Assert.AreEqual(CommandLifecycleState.Queued, first.State);
        Assert.AreEqual(Fixture.UuidOf(Fixture.Load("continuation").Stdout[1]), first.Uuid);
        Assert.AreEqual("1544a9e0-91dc-4dcd-ae8f-f7d416397ffc", first.SessionId);
        Assert.IsTrue(lifecycles.All(lifecycle => prompts.Contains(lifecycle.CommandUuid)));
        CollectionAssert.AreEqual(
            new[] { "completed", "queued", "started" },
            lifecycles.Select(lifecycle => lifecycle.State.ToString()).Distinct().Order(StringComparer.Ordinal).ToArray());
        CollectionAssert.AreEqual(
            new[] { CommandLifecycleState.Cancelled, CommandLifecycleState.Cancelled },
            Recorded.Messages<CommandLifecycleMessage>("cancel")
                .Where(lifecycle => lifecycle.State != CommandLifecycleState.Queued && lifecycle.State != CommandLifecycleState.Started)
                .Select(lifecycle => lifecycle.State)
                .ToArray());
    }

    [TestMethod]
    public void TheRateLimitEventCarriesTheWindowTheAccountIsOn()
    {
        var info = Recorded.Messages<RateLimitMessage>("client").First();

        Assert.AreEqual("allowed", info.Status);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1789822800), info.ResetsAt);
        Assert.AreEqual("five_hour", info.LimitType);
    }

    [TestMethod]
    public void TheRateLimitEventCarriesTheOverageTheAccountIsRefused()
    {
        var info = Recorded.Messages<RateLimitMessage>("client").First();

        Assert.AreEqual("rejected", info.OverageStatus);
        Assert.AreEqual("org_level_disabled", info.OverageDisabledReason);
    }

    [TestMethod]
    public void TheRateLimitEventKeepsTheUnifiedWindowsTheRecordDoesNotType()
    {
        var info = Recorded.Messages<RateLimitMessage>("client").First();

        var windows = info.Line().GetProperty("rate_limit_info").GetProperty("unifiedWindows");
        Assert.AreEqual(0.01, windows.GetProperty("five_hour").GetProperty("utilization").GetDouble());
        Assert.AreEqual(0.72, windows.GetProperty("seven_day").GetProperty("utilization").GetDouble());
    }

    [TestMethod]
    public void TheUserPromptSubmitHookInputCarriesThePrompt()
    {
        var input = HookInputs<UserPromptSubmitHookInput>().Single();

        Assert.AreEqual(
            "Read notes.txt in the working directory and reply with its second line only.",
            input.Prompt);
        Assert.AreEqual(HookEvent.UserPromptSubmit, input.Event);
        Assert.AreEqual(RecordedCwd, input.WorkingDirectory);
    }

    [TestMethod]
    public void ThePreToolUseHookInputCarriesTheToolNameAndToolUseId()
    {
        var input = HookInputs<PreToolUseHookInput>().Single();

        Assert.AreEqual("Read", input.ToolCall.Name);
        Assert.AreEqual("toolu_01FS2BYgfJPaPgcCRmeoMwZK", input.ToolCall.Id);
        Assert.AreEqual($"{RecordedCwd}/notes.txt", input.ToolCall.RawInput.GetProperty("file_path").GetString());
    }

    [TestMethod]
    public void ThePostToolUseHookInputCarriesTheToolResponse()
    {
        var input = HookInputs<PostToolUseHookInput>().Single();

        Assert.AreEqual("Read", input.ToolCall.Name);
        Assert.AreEqual("toolu_01FS2BYgfJPaPgcCRmeoMwZK", input.ToolCall.Id);
        Assert.AreEqual("text", input.ToolCall.RawOutput?.GetProperty("type").GetString());
        Assert.AreEqual(TimeSpan.FromMilliseconds(7), input.Duration);
    }

    [TestMethod]
    public void TheStopHookInputCarriesStopHookActive()
    {
        var input = HookInputs<StopHookInput>().Single();

        Assert.IsFalse(input.IsStopHookActive);
        Assert.AreEqual(HookEvent.Stop, input.Event);
    }

    [TestMethod]
    public void TheFourHookCallbacksNameTheIdsTheInitializeRequestRegistered()
    {
        var callbacks = Recorded.Requests<HookCallbackRequest>("hooks").ToArray();

        CollectionAssert.AreEqual(
            new[] { "hook_2", "hook_0", "hook_1", "hook_3" },
            callbacks.Select(callback => callback.CallbackId).ToArray());
    }

    [TestMethod]
    public void TheInterruptedTurnEndsInAnErrorDuringExecutionResult()
    {
        var result = Recorded.Messages<ResultMessage>("client").Last();

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("error_during_execution", result.Subtype);
        Assert.AreEqual("aborted_streaming", result.TerminalReason);
        Assert.AreEqual(1, result.Errors.Count);
        Assert.AreEqual(2, result.TurnCount);
    }

    [TestMethod]
    public void TheReplayedUserMessageKeepsTheIsReplayFlagTheRecordDoesNotType()
    {
        var echoed = Recorded.Messages<UserMessage>("rewind-files").First();

        Assert.AreEqual(JsonValueKind.String, echoed.Line().GetProperty("message").GetProperty("content").ValueKind);
        Assert.IsInstanceOfType<TextBlock>(echoed.Content.Single());
        Assert.IsTrue(echoed.Line().GetProperty("isReplay").GetBoolean());
    }

    [TestMethod]
    public void TheInterruptedAssistantMessageKeepsTheAbortedFlagTheRecordDoesNotType()
    {
        var aborted = Recorded.Messages<AssistantMessage>("client")
            .Single(message => message.Line().TryGetProperty("aborted", out var flag) && flag.GetBoolean());

        Assert.IsNotNull(aborted.Uuid);
    }

    [TestMethod]
    public void TheThinkingBlockKeepsItsSignature()
    {
        var thinking = Recorded.Messages<AssistantMessage>("client")
            .SelectMany(message => message.Content)
            .OfType<ThinkingBlock>()
            .First();

        Assert.AreEqual(string.Empty, thinking.Thinking);
        Assert.IsTrue(
            thinking.Signature.StartsWith("EoYDCrIBCBEYAipA", StringComparison.Ordinal),
            thinking.Signature);
    }

    [TestMethod]
    public void TheSetPermissionModeReplyParsesAsTheModeTheBinaryConfirmed()
    {
        var parsed = new PayloadParser().PermissionMode(Recorded.SuccessPayload("client", "req_3_7972eb2f"));

        Assert.IsTrue(parsed.IsOk, Recorded.Reason(parsed));
        Assert.AreEqual(PermissionMode.AcceptEdits, parsed.Value);
    }

    [TestMethod]
    public void TheContextUsageReplyParsesWithItsTotalsAndCategories()
    {
        var parsed = new PayloadParser().ContextUsage(Recorded.SuccessPayload("client", "req_4_7f5fa2d8"));

        Assert.IsTrue(parsed.IsOk, Recorded.Reason(parsed));

        var usage = parsed.Value;
        Assert.AreEqual(29260, usage.TotalTokens);
        Assert.AreEqual(200000, usage.MaxTokens);
        Assert.AreEqual("claude-haiku-4-5-20251001", usage.Model);
        Assert.AreEqual(6, usage.Categories.Count);
        Assert.IsTrue(usage.IsAutoCompactEnabled);
    }

    [TestMethod]
    public void TheMcpStatusReplyParsesAsTheAccountsOneConnectorWithItsTools()
    {
        var parsed = new PayloadParser().McpStatus(Recorded.SuccessPayload("client", "req_5_1135df8a"));

        Assert.IsTrue(parsed.IsOk, Recorded.Reason(parsed));
        var server = parsed.Value.Single();
        Assert.AreEqual("claude.ai Claude Docs", server.Name);
        Assert.AreEqual(8, server.Tools.Count);
    }

    [TestMethod]
    public void TheInitializeReplyParsesIntoTheServerInfoTheSessionOpensWith()
    {
        var parsed = new PayloadParser().ServerInfo(Recorded.SuccessPayload("client", "req_1_7a1526d9"));

        Assert.IsTrue(parsed.IsOk, Recorded.Reason(parsed));

        var info = parsed.Value;
        Assert.AreEqual(56, info.Commands.Count);
        Assert.AreEqual("deep-research", info.Commands[0].Name);
        Assert.AreEqual(5, info.Models.Count);
        Assert.AreEqual("default", info.Models[0].Value);
        Assert.AreEqual("claude-opus-5[1m]", info.Models[0].ResolvedModel);
        Assert.AreEqual("Claude Max", info.Account?.SubscriptionType);
        Assert.AreEqual(1474847, info.ProcessId);
        Assert.AreEqual("idle", info.SessionState);
    }

    [TestMethod]
    public void TheStreamingFixtureCarriesTwentyFiveStreamEventsInWireOrder()
    {
        var events = Recorded.Messages<StreamEventMessage>("streaming").ToArray();

        Assert.AreEqual(25, events.Length);
        CollectionAssert.AreEqual(
            new[]
            {
                "message_start",
                "content_block_start",
                "content_block_delta",
                "content_block_delta",
                "content_block_delta",
                "content_block_stop",
                "content_block_start",
                "content_block_delta",
                "content_block_delta",
                "content_block_delta",
                "content_block_delta",
                "content_block_delta",
                "content_block_delta",
                "content_block_delta",
                "content_block_delta",
                "content_block_delta",
                "content_block_delta",
                "content_block_delta",
                "content_block_delta",
                "content_block_delta",
                "content_block_delta",
                "content_block_delta",
                "content_block_stop",
                "message_delta",
                "message_stop",
            },
            events.Select(message => message.EventType.ToString()).ToArray());
    }

    [TestMethod]
    public void TheSdkMcpFixtureCarriesTheFourJsonRpcCallsTheBridgeMustAnswer()
    {
        var calls = Recorded.Requests<McpMessageRequest>("sdk-mcp").ToArray();

        CollectionAssert.AreEqual(
            new[] { "initialize", "notifications/initialized", "tools/list", "tools/call" },
            calls.Select(call => call.Message.GetProperty("method").GetString()).ToArray());
        Assert.IsTrue(calls.All(call => call.ServerName == "calc"));
    }

    [TestMethod]
    public void TheSdkMcpInitFrameNamesTheConnectedInProcessServer()
    {
        var init = Recorded.Messages<SystemInitMessage>("sdk-mcp").Single();

        Assert.AreEqual(2, init.McpServers.Count);
        Assert.AreEqual("claude.ai Claude Docs", init.McpServers[0].Name);
        Assert.AreEqual("calc", init.McpServers[1].Name);
        Assert.AreEqual("connected", init.McpServers[1].Status);
    }

    // How a caller says "no tools": an empty list, which goes out as `--tools ""`. The binary started
    // that way lists no tool of its own, only what an MCP server brought; started with no flag it
    // lists all of its own, and started with one name it lists that one. The login's own connector,
    // where it had connected when the init frame went out, brought its eight.
    [TestMethod]
    public void TheBinaryStartedWithAnEmptyToolListHasNoToolOfItsOwn()
    {
        CollectionAssert.AreEqual(new[] { "--tools", "" }, Fixture.Load("sdk-mcp").Argv.SkipWhile(flag => flag != "--tools").Take(2).ToArray());
        CollectionAssert.AreEqual(new[] { "mcp__calc__add" }.Concat(ConnectorTools).ToArray(), Recorded.Messages<SystemInitMessage>("sdk-mcp").Single().Tools.ToArray());

        CollectionAssert.AreEqual(new[] { "--tools", "" }, Fixture.Load("stdio-mcp").Argv.SkipWhile(flag => flag != "--tools").Take(2).ToArray());
        CollectionAssert.AreEqual(new[] { "mcp__echo__echo" }, Recorded.Messages<SystemInitMessage>("stdio-mcp").First().Tools.ToArray());

        CollectionAssert.AreEqual(new[] { "--tools", "Read" }, Fixture.Load("tool-use").Argv.SkipWhile(flag => flag != "--tools").Take(2).ToArray());
        CollectionAssert.AreEqual(new[] { "Read" }.Concat(ConnectorTools).ToArray(), Recorded.Messages<SystemInitMessage>("tool-use").Single().Tools.ToArray());

        CollectionAssert.DoesNotContain(Fixture.Load("trivial").Argv.ToArray(), "--tools");
        CollectionAssert.Contains(Recorded.Messages<SystemInitMessage>("trivial").Single().Tools.ToArray(), "Bash");
    }

    // Every recorded value of a closed set is one the library names, so these lines are ours, in
    // our own JSON: a value the library has no name for is kept as the binary wrote it, and the set
    // says it does not name it. Nothing is skipped and nothing throws.
    [TestMethod]
    public void AResultsSubtypeStopReasonAndTerminalReasonAreKeptWhenTheLibraryNamesNoneOfThem()
    {
        var result = (ResultMessage)Decoded(
            """
            {"type":"result","subtype":"error_tea_break","duration_ms":1,"duration_api_ms":1,"is_error":true,"num_turns":1,"session_id":"s-1","stop_reason":"tea","terminal_reason":"kettle_boiled"}
            """);

        Assert.AreEqual("error_tea_break", result.Subtype.ToString());
        Assert.IsFalse(result.Subtype.IsDefined);
        Assert.AreEqual("tea", result.StopReason.ToString());
        Assert.IsFalse(result.StopReason!.Value.IsDefined);
        Assert.AreEqual("kettle_boiled", result.TerminalReason.ToString());
        Assert.IsFalse(result.TerminalReason!.Value.IsDefined);
    }

    [TestMethod]
    public void AnInitFramesPermissionModeAndCredentialSourceAreKeptWhenTheLibraryNamesNeither()
    {
        var init = (SystemInitMessage)Decoded(
            """
            {"type":"system","subtype":"init","session_id":"s-1","uuid":"u-1","permissionMode":"ponderMode","apiKeySource":"a-vault"}
            """);

        Assert.AreEqual("ponderMode", init.PermissionMode.ToString());
        Assert.IsFalse(init.PermissionMode!.Value.IsDefined);
        Assert.AreEqual("a-vault", init.ApiKeySource.ToString());
        Assert.IsFalse(init.ApiKeySource!.Value.IsDefined);
    }

    // focus is the view the binary's own init schema names beside default, and no recording carries
    // it: the library does not name it, and keeps it.
    [TestMethod]
    public void AnInitFramesViewIsKeptWhenTheLibraryDoesNotNameIt()
    {
        var init = (SystemInitMessage)Decoded(
            """
            {"type":"system","subtype":"init","session_id":"s-1","uuid":"u-1","view_mode":"focus"}
            """);

        Assert.AreEqual("focus", init.ViewMode.ToString());
        Assert.IsFalse(init.ViewMode!.Value.IsDefined);
    }

    [TestMethod]
    public void ALifecycleStateAndARateLimitStandingAreKeptWhenTheLibraryNamesNeither()
    {
        var lifecycle = (CommandLifecycleMessage)Decoded(
            """
            {"type":"command_lifecycle","command_uuid":"c-1","state":"steeping","session_id":"s-1","uuid":"u-1"}
            """);

        Assert.AreEqual("steeping", lifecycle.State.ToString());
        Assert.IsFalse(lifecycle.State.IsDefined);

        var limit = (RateLimitMessage)Decoded(
            """
            {"type":"rate_limit_event","session_id":"s-1","uuid":"u-1","rate_limit_info":{"status":"throttled","rateLimitType":"one_minute","overageDisabledReason":"teapot"}}
            """);

        Assert.AreEqual("throttled", limit.Status.ToString());
        Assert.IsFalse(limit.Status.IsDefined);
        Assert.AreEqual("one_minute", limit.LimitType.ToString());
        Assert.IsFalse(limit.LimitType!.Value.IsDefined);
        Assert.AreEqual("teapot", limit.OverageDisabledReason.ToString());
        Assert.IsFalse(limit.OverageDisabledReason!.Value.IsDefined);
    }

    // A hook the library has no name for: the input still reaches a callback whole, with the event
    // as the binary wrote it and the permission suggestion's destination and behavior beside it.
    [TestMethod]
    public void AHookCallbackForAnEventTheLibraryDoesNotNameIsReadWhole()
    {
        var frame = (ControlRequestFrame)Decode(
            """
            {"type":"control_request","request_id":"req_9_0000000a","request":{"subtype":"hook_callback","callback_id":"hook_0","input":{"session_id":"s-1","hook_event_name":"TeaBreak","permission_mode":"ponderMode","permission_suggestions":[{"type":"addRules","behavior":"brew","destination":"teapotSettings","rules":[{"toolName":"Kettle","ruleContent":null}]}]}}}
            """);

        var input = ((HookCallbackRequest)frame.Request).Input;
        Assert.AreEqual("TeaBreak", input.Event.ToString());
        Assert.IsFalse(input.Event.IsDefined);
        Assert.AreEqual("ponderMode", input.PermissionMode.ToString());
        Assert.IsFalse(input.PermissionMode!.Value.IsDefined);
    }

    [TestMethod]
    public void APermissionSuggestionsBehaviorAndDestinationAreKeptWhenTheLibraryNamesNeither()
    {
        var suggestion = PermissionUpdateParser.Parse(Recorded.Json(
            """
            {"type":"addRules","behavior":"brew","destination":"teapotSettings","rules":[{"toolName":"Kettle","ruleContent":null}]}
            """));

        var rules = (AddRulesPermissionUpdate)suggestion;
        Assert.AreEqual("brew", rules.Behavior.ToString());
        Assert.IsFalse(rules.Behavior.IsDefined);
        Assert.AreEqual("teapotSettings", rules.Destination.ToString());
        Assert.IsFalse(rules.Destination!.Value.IsDefined);
        Assert.AreEqual("Kettle", rules.Rules.Single().ToolName);
        Assert.IsNull(rules.Rules.Single().RuleContent);
    }

    // A closed set is closed against no value at all, so the factories a caller uses refuse an empty
    // one. Reading is not calling: a suggestion whose mode, behavior or destination is empty is kept
    // whole as the unknown, written back as it came, and the stream goes on.
    [TestMethod]
    public void APermissionSuggestionWithAnEmptyModeBehaviorOrDestinationIsKeptWholeAsTheUnknown()
    {
        string[] lines =
        [
            """{"type":"setMode","mode":""}""",
            """{"type":"setMode","mode":"plan","destination":""}""",
            """{"type":"addRules","behavior":"","destination":"session","rules":[{"toolName":"Kettle"}]}""",
            """{"type":"addDirectories","directories":["/tea"],"destination":""}""",
        ];

        foreach (var line in lines)
        {
            var suggestion = PermissionUpdateParser.Parse(Recorded.Json(line));

            var unknown = (UnknownPermissionUpdate)suggestion;
            Assert.AreEqual(line, unknown.Raw.GetRawText(), line);
        }
    }

    // The same line inside a real control request: the codec reads it, and nothing throws out of the
    // read loop to end the session.
    [TestMethod]
    public void AControlRequestCarryingASuggestionWithAnEmptyModeIsReadAndNotRejected()
    {
        var frame = Decode(
            """
            {"type":"control_request","request_id":"r1","request":{"subtype":"can_use_tool","tool_name":"Bash","input":{},"permission_suggestions":[{"type":"setMode","mode":""}]}}
            """);

        var request = (CanUseToolRequest)((ControlRequestFrame)frame).Request;
        Assert.AreEqual(1, request.PermissionSuggestions.Length);
        Assert.IsInstanceOfType<UnknownPermissionUpdate>(request.PermissionSuggestions[0]);
    }

    private static AgentMessage Decoded(string line) => ((MessageFrame)Decode(line)).Message;

    private static InboundFrame Decode(string line)
    {
        var decoded = new FrameCodec().Decode(line);
        Assert.IsTrue(decoded.IsOk, Recorded.Reason(decoded));
        return decoded.Value;
    }

    private static string? Text(JsonElement raw, string name) =>
        raw.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static IEnumerable<T> HookInputs<T>()
        where T : HookInput =>
        Recorded.Requests<HookCallbackRequest>("hooks").Select(request => request.Input).OfType<T>();
}
