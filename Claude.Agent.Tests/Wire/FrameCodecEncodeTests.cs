using System.Collections.Immutable;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Wire;

[TestClass]
public sealed class FrameCodecEncodeTests
{
    private static readonly RequestId FirstRequest = new("req_1_00000000");

    [TestMethod]
    public void TheInitializeRequestWithoutHooksIsWhatEveryQueryFixtureOpensWith()
    {
        var frame = new OutboundControlRequest(
            FirstRequest,
            new InitializeRequest(null, null, null, null, ForwardSubagentText: false));

        foreach (var fixture in
                 new[] { "trivial", "tool-use", "streaming", "sdk-mcp", "fork-parent", "fork", "resume" })
        {
            AssertEncodes(frame, fixture, 1);
        }
    }

    [TestMethod]
    public void TheInitializeRequestOfAPresetPromptSaysItKeepsTheDynamicSections()
    {
        var frame = new OutboundControlRequest(
            FirstRequest,
            new InitializeRequest(null, null, ExcludeDynamicSections: false, null, ForwardSubagentText: false));

        AssertEncodes(frame, "client", 1);
    }

    [TestMethod]
    public void TheInitializeRequestCarriesTheHooksTheRegistryRegistered()
    {
        var hooks = new OrderedDictionary<HookEvent, ImmutableArray<HookMatcherConfig>>
        {
            [HookEvent.PreToolUse] = [new HookMatcherConfig("Read", ["hook_0"], null)],
            [HookEvent.PostToolUse] = [new HookMatcherConfig(null, ["hook_1"], null)],
            [HookEvent.UserPromptSubmit] = [new HookMatcherConfig(null, ["hook_2"], null)],
            [HookEvent.Stop] = [new HookMatcherConfig(null, ["hook_3"], null)],
        };

        var frame = new OutboundControlRequest(
            FirstRequest,
            new InitializeRequest(hooks, null, null, null, ForwardSubagentText: false));

        AssertEncodes(frame, "hooks", 1);
    }

    // A hook event the library does not name is registered and written out as the caller spelt it:
    // the binary knows events this library has no name for.
    [TestMethod]
    public void AHookEventTheLibraryDoesNotNameIsWrittenOnTheInitializeRequestUnchanged()
    {
        var options = new ClaudeAgentSessionOptions();
        options.AddHook("TeaBreak", null, (_, _) => Task.FromResult(new HookOutput()));

        var hooks = new HookTable(options.Hooks).ToInitializeConfig();
        Assert.IsNotNull(hooks);
        Assert.IsFalse(hooks.Keys.Single().IsDefined);

        var frame = new OutboundControlRequest(
            FirstRequest,
            new InitializeRequest(hooks, null, null, null, ForwardSubagentText: false));

        Assert.AreEqual(
            """{"type": "control_request", "request_id": "req_1_00000000", "request": {"subtype": "initialize", "hooks": {"TeaBreak": [{"matcher": null, "hookCallbackIds": ["hook_0"]}]}}}""",
            new FrameCodec().Encode(frame));
    }

    [TestMethod]
    public void TheOneShotUserFrameCarriesTheEmptySessionIdBeforeTheMessage()
    {
        AssertUserInput("trivial", 2, "Reply with exactly one word: pong");
        AssertUserInput("tool-use", 2, "Read notes.txt in the working directory and reply with its first line only.");
        AssertUserInput("hooks", 2, "Read notes.txt in the working directory and reply with its second line only.");
        AssertUserInput("streaming", 2, "What is a mint?");
        AssertUserInput("sdk-mcp", 3, "Use the add tool to add 2 and 3, then reply with just the number.");

        // The three verb fixtures are query() recordings, so the replay tests that drive a client session
        // over them cannot compare stdin: the client writes the session_id slot after parent_tool_use_id.
        // Their two lines are pinned here instead.
        AssertUserInput("fork-parent", 2, "Reply with exactly one word: pong");
        AssertUserInput("fork", 2, "Reply with exactly the word you replied last time.");
        AssertUserInput("resume", 2, "Reply with exactly the word you replied last time.");
    }

    [TestMethod]
    public void TheClientUserFrameCarriesTheDefaultSessionIdAfterTheMessageAndItsUuidLast()
    {
        AssertClientInput(2, "Say: one");
        AssertClientInput(7, "Say: two");
        AssertClientInput(8, "Count slowly from 1 to 200, one number per line, no other text.");
    }

    [TestMethod]
    public void TheSetModelRequestNamesTheModel()
    {
        var frame = new OutboundControlRequest(FirstRequest, new SetModelRequest("haiku"));

        AssertEncodes(frame, "client", 3);
    }

    [TestMethod]
    public void TheSetPermissionModeRequestUsesTheWireNameOfTheMode()
    {
        var frame = new OutboundControlRequest(
            FirstRequest,
            new SetPermissionModeRequest(PermissionMode.AcceptEdits));

        AssertEncodes(frame, "client", 4);
    }

    [TestMethod]
    public void TheContextUsageRequestIsASubtypeAndNothingElse()
    {
        AssertEncodes(new OutboundControlRequest(FirstRequest, new GetContextUsageRequest()), "client", 5);
    }

    [TestMethod]
    public void TheMcpStatusRequestIsASubtypeAndNothingElse()
    {
        AssertEncodes(new OutboundControlRequest(FirstRequest, new McpStatusRequest()), "client", 6);
    }

    [TestMethod]
    public void TheInterruptRequestIsASubtypeAndNothingElse()
    {
        AssertEncodes(new OutboundControlRequest(FirstRequest, new InterruptRequest()), "client", 9);
    }

    [TestMethod]
    public void TheUserPromptSubmitHookReplyCarriesItsAdditionalContext()
    {
        var reply = new HookReply(
            new HookOutput { AdditionalContext = "the file is small" },
            "UserPromptSubmit");

        AssertEncodes(new OutboundControlResponse(HookRequest(0), reply), "hooks", 3);
    }

    [TestMethod]
    public void ThePreToolUseHookReplyCarriesTheAllowDecisionAndItsReason()
    {
        var reply = new HookReply(
            new HookOutput { Decision = PermissionDecision.Allow, DecisionReason = "fixture allows" },
            "PreToolUse");

        AssertEncodes(new OutboundControlResponse(HookRequest(1), reply), "hooks", 4);
    }

    [TestMethod]
    public void ThePostToolUseHookReplyCarriesItsAdditionalContext()
    {
        var reply = new HookReply(
            new HookOutput { AdditionalContext = "fixture saw the read" },
            "PostToolUse");

        AssertEncodes(new OutboundControlResponse(HookRequest(2), reply), "hooks", 5);
    }

    [TestMethod]
    public void AHookThatDecidesNothingRepliesWithAnEmptyObject()
    {
        var reply = new HookReply(new HookOutput(), "Stop");

        AssertEncodes(new OutboundControlResponse(HookRequest(3), reply), "hooks", 6);
    }

    [TestMethod]
    public void TheMcpInitializeReplyWrapsTheJsonRpcResultUnderMcpResponse()
    {
        var reply = new McpReply(Recorded.Json(
            """
            {"jsonrpc": "2.0", "id": 0, "result": {"capabilities": {"experimental": {}, "tools": {"listChanged": false}}, "protocolVersion": "2025-11-25", "serverInfo": {"name": "calc", "version": "1.0.0"}}}
            """));

        AssertEncodes(new OutboundControlResponse(McpRequest(0), reply), "sdk-mcp", 2);
    }

    [TestMethod]
    public void TheMcpNotificationIsAcknowledgedWithAnEmptyResult()
    {
        var reply = new McpReply(Recorded.Json("""{"jsonrpc": "2.0", "result": {}}"""));

        AssertEncodes(new OutboundControlResponse(McpRequest(1), reply), "sdk-mcp", 4);
    }

    [TestMethod]
    public void TheMcpToolsListReplyCarriesTheToolSchema()
    {
        var reply = new McpReply(Recorded.Json(
            """
            {"jsonrpc": "2.0", "id": 1, "result": {"tools": [{"description": "Add two numbers", "inputSchema": {"properties": {"a": {"type": "number"}, "b": {"type": "number"}}, "required": ["a", "b"], "type": "object"}, "name": "add"}]}}
            """));

        AssertEncodes(new OutboundControlResponse(McpRequest(2), reply), "sdk-mcp", 5);
    }

    [TestMethod]
    public void TheMcpToolsCallReplyCarriesTheToolContent()
    {
        var reply = new McpReply(Recorded.Json(
            """
            {"jsonrpc": "2.0", "id": 2, "result": {"content": [{"text": "5", "type": "text"}], "isError": false}}
            """));

        AssertEncodes(new OutboundControlResponse(McpRequest(3), reply), "sdk-mcp", 6);
    }

    private static RequestId HookRequest(int index) => Recorded.RequestIds("hooks")[index];

    private static RequestId McpRequest(int index) => Recorded.RequestIds("sdk-mcp")[index];

    private static void AssertUserInput(string fixture, int lineNumber, string text)
    {
        var frame = new UserInputFrame(text, null, null, UserInputForm.OneShot);

        AssertEncodes(frame, fixture, lineNumber);
    }

    private static void AssertClientInput(int lineNumber, string text)
    {
        var uuid = Guid.Parse(Fixture.UuidOf(Recorded.Stdin("client", lineNumber))!);
        var frame = new UserInputFrame(text, null, null, UserInputForm.Client, uuid);

        AssertEncodes(frame, "client", lineNumber);
    }

    private static void AssertEncodes(OutboundFrame frame, string fixture, int lineNumber)
    {
        Assert.AreEqual(
            Fixture.Anonymise(Recorded.Stdin(fixture, lineNumber)),
            Fixture.Anonymise(new FrameCodec().Encode(frame)),
            $"{fixture}.stdin.jsonl line {lineNumber}");
    }
}
