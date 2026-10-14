using System.Collections.Immutable;
using Claude.Agent.Tests.Replay;
using System.Text.Json;

namespace Claude.Agent.Tests.Sessions;

[TestClass]
public sealed class ControlProtocolTests
{
    private const string PreToolUseRequestId = "65b2c8d6-a19d-41cb-b253-98679236f55c";
    private const string UnparsedRequestId = "e0a1c6a4-unparsed";
    private const string ToolCallRequestId = "9c2f5b30-toolcall";
    private const string PermissionRequestId = "b41d7ce2-permission";
    private const string SuggestedPermissionRequestId = "perm-1";
    private const string GizmoRequestId = "7d0f9a12-gizmo";

    private const string ToolCallRequest =
        "{\"type\": \"control_request\", \"request_id\": \"" + ToolCallRequestId + "\", \"request\": "
        + "{\"subtype\": \"mcp_message\", \"server_name\": \"calc\", \"message\": "
        + "{\"jsonrpc\": \"2.0\", \"id\": 11, \"method\": \"tools/call\", "
        + "\"params\": {\"name\": \"add\", \"arguments\": {}}}}}";

    private const string CanUseToolRequest =
        "{\"type\": \"control_request\", \"request_id\": \"" + PermissionRequestId + "\", \"request\": "
        + "{\"subtype\": \"can_use_tool\", \"tool_name\": \"Read\", "
        + "\"input\": {\"file_path\": \"/tmp/notes.txt\"}}}";

    private const string SuggestedCanUseToolRequest =
        "{\"type\":\"control_request\",\"request_id\":\"" + SuggestedPermissionRequestId + "\",\"request\":"
        + "{\"subtype\":\"can_use_tool\",\"tool_name\":\"Bash\",\"input\":{\"command\":\"ls\"},"
        + "\"permission_suggestions\":[{\"type\":\"addRules\","
        + "\"rules\":[{\"toolName\":\"Bash\",\"ruleContent\":\"ls:*\"}],"
        + "\"behavior\":\"allow\",\"destination\":\"session\"}],"
        + "\"blocked_path\":null,\"tool_use_id\":\"toolu_perm1\"}}";

    private const string GizmoRequest =
        "{\"type\": \"control_request\", \"request_id\": \"" + GizmoRequestId + "\", "
        + "\"request\": {\"subtype\": \"set_gizmo\"}}";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task AControlResponseWithSubtypeErrorFailsTheRequestWithItsErrorText()
    {
        var script = new Script([Script.AnswerInitialize, Script.AnswerWithError("model not available")]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        await using var session = await ConnectAsync(host);

        var refused = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => session.SetModelAsync("sonnet"));

        Assert.AreEqual(ErrorCodes.Control, refused.ErrorCode);
        Assert.AreEqual("model not available", refused.Message);
    }

    [TestMethod]
    public async Task AHookThatThrowsIsAnsweredWithAControlErrorCarryingTheExceptionMessage()
    {
        var script = new Script([Script.AnswerInitialize, new EmitLine(PreToolUseCallback)]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server, options => options.AddHook(
            HookEvent.PreToolUse,
            "Read",
            (_, _) => throw new InvalidOperationException("the hook fell over")));
        await using var session = await ConnectAsync(host);

        await server.WaitForWrites(2, Patience);
        var reply = ReplyTo(server.Written, PreToolUseRequestId);

        Assert.IsNotNull(reply);
        Assert.AreEqual("error", reply.Value.GetProperty("subtype").GetString());
        Assert.AreEqual("the hook fell over", reply.Value.GetProperty("error").GetString());
    }

    // A cancellation that is the hook's own — a timeout inside it, a token the host owns — is a
    // failure like any other. Only the request's own cancel is answered with silence.
    [TestMethod]
    public async Task AHookThatThrowsACancellationOfItsOwnIsStillAnsweredWithAControlError()
    {
        var script = new Script([Script.AnswerInitialize, new EmitLine(PreToolUseCallback)]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server, options => options.AddHook(
            HookEvent.PreToolUse,
            "Read",
            (_, _) => throw new TaskCanceledException("the hook timed out")));
        await using var session = await ConnectAsync(host);

        await server.WaitForWrites(2, Patience);
        var reply = ReplyTo(server.Written, PreToolUseRequestId);

        Assert.IsNotNull(reply);
        Assert.AreEqual("error", reply.Value.GetProperty("subtype").GetString());
        Assert.AreEqual("the hook timed out", reply.Value.GetProperty("error").GetString());
    }

    [TestMethod]
    public async Task APermissionCallbackThatThrowsACancellationOfItsOwnIsStillAnsweredWithAControlError()
    {
        var script = new Script([Script.AnswerInitialize, new EmitLine(CanUseToolRequest)]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(
            server,
            options => options.PermissionCallback = (_, _) => throw new OperationCanceledException("the gate timed out"));
        await using var session = await ConnectAsync(host);

        await server.WaitForWrites(2, Patience);
        var reply = ReplyTo(server.Written, PermissionRequestId);

        Assert.IsNotNull(reply);
        Assert.AreEqual("error", reply.Value.GetProperty("subtype").GetString());
        Assert.AreEqual("the gate timed out", reply.Value.GetProperty("error").GetString());
    }

    // The callback never suspends: it blocks its thread until its token fires. The cancel frame is
    // right behind the request, so it is only read — and the token only fires — if the callback is
    // not running on the read loop.
    [TestMethod]
    public async Task AHookThatBlocksItsThreadStillHasItsCancelFrameReadAndItsTokenCancelled()
    {
        var cancel = "{\"type\":\"control_cancel_request\",\"request_id\":\"" + PreToolUseRequestId + "\"}";
        var script = new Script([Script.AnswerInitialize, new EmitLine(PreToolUseCallback), new EmitLine(cancel)]);
        var sawItsCancel = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server, options => options.AddHook(
            HookEvent.PreToolUse,
            "Read",
            (_, cancellation) =>
            {
                sawItsCancel.TrySetResult(cancellation.WaitHandle.WaitOne(Patience));
                cancellation.ThrowIfCancellationRequested();
                return Task.FromResult(new HookOutput());
            }));
        await using var session = await ConnectAsync(host);

        Assert.IsTrue(await sawItsCancel.Task.WaitAsync(Patience + Patience), "the cancel frame was not read while the hook blocked");
        Assert.IsNull(ReplyTo(server.Written, PreToolUseRequestId), "a cancelled request was answered");
    }

    [TestMethod]
    public async Task AControlRequestThatDoesNotParseIsAnsweredWithAnErrorForItsRequestId()
    {
        var script = new Script([Script.AnswerInitialize, new EmitLine(CallbackWithoutId)]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        await using var session = await ConnectAsync(host);

        await server.WaitForWrites(2, Patience);
        var reply = ReplyTo(server.Written, UnparsedRequestId);

        Assert.IsNotNull(reply);
        Assert.AreEqual("error", reply.Value.GetProperty("subtype").GetString());
        StringAssert.Contains(reply.Value.GetProperty("error").GetString(), "callback_id");
    }

    [TestMethod]
    public async Task AToolThatThrowsIsAnsweredWithAToolResultMarkedAnErrorInsideASuccessReply()
    {
        var script = new Script([Script.AnswerInitialize, new EmitLine(ToolCallRequest)]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(
            server,
            options => options.McpServers["calc"] = McpServerConfiguration.InProcess(new FallingCalc()));
        await using var session = await ConnectAsync(host);

        await server.WaitForWrites(2, Patience);
        var reply = ReplyTo(server.Written, ToolCallRequestId);

        Assert.IsNotNull(reply);
        Assert.AreEqual("success", reply.Value.GetProperty("subtype").GetString());

        var result = reply.Value.GetProperty("response").GetProperty("mcp_response").GetProperty("result");
        Assert.IsTrue(result.GetProperty("isError").GetBoolean());
        StringAssert.Contains(result.GetProperty("content")[0].GetProperty("text").GetString(), "the tool fell over");
    }

    [TestMethod]
    public async Task APermissionCallbackThatThrowsIsAnsweredWithAControlErrorCarryingTheExceptionMessage()
    {
        var script = new Script([Script.AnswerInitialize, new EmitLine(CanUseToolRequest)]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(
            server,
            options => options.PermissionCallback = (_, _) => throw new InvalidOperationException("the gate fell over"));
        await using var session = await ConnectAsync(host);

        await server.WaitForWrites(2, Patience);
        var reply = ReplyTo(server.Written, PermissionRequestId);

        Assert.IsNotNull(reply);
        Assert.AreEqual("error", reply.Value.GetProperty("subtype").GetString());
        Assert.AreEqual("the gate fell over", reply.Value.GetProperty("error").GetString());
    }

    [TestMethod]
    public async Task APermissionHandlerThatAllowsIsAnsweredWithTheOriginalInputAndItsUpdatedPermissions()
    {
        ToolPermissionRequest? asked = null;

        await using var server = FixtureServer.Play(SuggestedPermissionScript());
        using var host = TestHost.Start(
            server,
            options => options.PermissionCallback = (request, _) =>
            {
                asked = request;
                return Task.FromResult(ToolPermissionResult.Allow(updatedPermissions: request.Suggestions));
            });
        await using var session = await ConnectAsync(host);

        await PromptAsync(session);
        await server.WaitForWrites(3, Patience);

        Assert.IsNotNull(asked);
        Assert.AreEqual("Bash", asked.ToolCall.Name);
        Assert.AreEqual("ls", asked.ToolCall.RawInput.GetProperty("command").GetString());
        Assert.AreEqual("toolu_perm1", asked.ToolCall.Id);

        var suggested = (AddRulesPermissionUpdate)asked.Suggestions.Single();
        Assert.AreEqual(PermissionRuleAction.Allow, suggested.Behavior);
        Assert.AreEqual("session", suggested.Destination);
        Assert.AreEqual("Bash", suggested.Rules.Single().ToolName);
        Assert.AreEqual("ls:*", suggested.Rules.Single().RuleContent);

        Assert.AreEqual(
            """
            {"type": "control_response", "response": {"subtype": "success", "request_id": "perm-1", "response": {"behavior": "allow", "updatedInput": {"command": "ls"}, "updatedPermissions": [{"type": "addRules", "destination": "session", "rules": [{"toolName": "Bash", "ruleContent": "ls:*"}], "behavior": "allow"}]}}}
            """,
            LineFor(server.Written, SuggestedPermissionRequestId));
    }

    [TestMethod]
    public async Task APermissionHandlerThatDeniesWithInterruptIsAnsweredWithTheMessageAndTheInterruptFlag()
    {
        await using var server = FixtureServer.Play(SuggestedPermissionScript());
        using var host = TestHost.Start(
            server,
            options => options.PermissionCallback = (_, _) =>
                Task.FromResult(ToolPermissionResult.Deny("not here", interrupt: true)));
        await using var session = await ConnectAsync(host);

        await PromptAsync(session);
        await server.WaitForWrites(3, Patience);

        Assert.AreEqual(
            """
            {"type": "control_response", "response": {"subtype": "success", "request_id": "perm-1", "response": {"behavior": "deny", "message": "not here", "interrupt": true}}}
            """,
            LineFor(server.Written, SuggestedPermissionRequestId));
    }

    [TestMethod]
    public async Task APermissionHandlerThatDeniesWithoutInterruptIsAnsweredWithNoInterruptKey()
    {
        await using var server = FixtureServer.Play(SuggestedPermissionScript());
        using var host = TestHost.Start(
            server,
            options => options.PermissionCallback = (_, _) =>
                Task.FromResult(ToolPermissionResult.Deny("not here")));
        await using var session = await ConnectAsync(host);

        await PromptAsync(session);
        await server.WaitForWrites(3, Patience);

        var reply = ReplyTo(server.Written, SuggestedPermissionRequestId);

        Assert.IsNotNull(reply);
        Assert.IsFalse(
            reply.Value.GetProperty("response").TryGetProperty("interrupt", out _),
            "a plain deny carried an interrupt key.");

        Assert.AreEqual(
            """
            {"type": "control_response", "response": {"subtype": "success", "request_id": "perm-1", "response": {"behavior": "deny", "message": "not here"}}}
            """,
            LineFor(server.Written, SuggestedPermissionRequestId));
    }

    [TestMethod]
    public async Task AControlRequestSubtypeNothingServesIsAnsweredWithAnErrorNamingTheSubtype()
    {
        var script = new Script([Script.AnswerInitialize, new EmitLine(GizmoRequest)]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server);
        await using var session = await ConnectAsync(host);

        await server.WaitForWrites(2, Patience);
        var reply = ReplyTo(server.Written, GizmoRequestId);

        Assert.IsNotNull(reply);
        Assert.AreEqual("error", reply.Value.GetProperty("subtype").GetString());
        Assert.AreEqual(
            "This session answers no control request of subtype 'set_gizmo'.",
            reply.Value.GetProperty("error").GetString());
    }

    private static string PreToolUseCallback => Fixture.Line("hooks", "control_request", 1);

    private static string CallbackWithoutId =>
        "{\"type\": \"control_request\", \"request_id\": \"" + UnparsedRequestId + "\", "
        + "\"request\": {\"subtype\": \"hook_callback\", \"input\": {}}}";

    // The client recording's first prompt said started, then the permission request, then that
    // prompt's result and its completed frame, each naming the prompt the test sends.
    private static Script SuggestedPermissionScript()
    {
        var client = Fixture.Load("client");
        return new Script(
        [
            Script.AnswerInitialize,
            new AwaitPrompt(1),
            new EmitLine(client.Stdout[1]),
            new EmitLine(client.Stdout[2]),
            new EmitLine(SuggestedCanUseToolRequest),
            new AwaitClientResponse(SuggestedPermissionRequestId),
            new EmitLine(client.Stdout[7]),
            new EmitLine(client.Stdout[8]),
            new HangUp(),
        ])
        {
            Prompts = Fixture.PromptUuids(client.Stdin),
        };
    }

    private static async Task PromptAsync(ClaudeAgentSession session)
    {
        await session.SendAsync("list the files").WaitForCompletionAsync();
    }

    private static Task<ClaudeAgentSession> ConnectAsync(TestHost host) => host.Client.StartSessionAsync();

    private static string? LineFor(ImmutableArray<string> written, string requestId) =>
        written.FirstOrDefault(line => Fixture.ResponseRequestIdOf(line) == requestId);

    private static JsonElement? ReplyTo(ImmutableArray<string> written, string requestId)
    {
        foreach (var line in written)
        {
            using var document = JsonDocument.Parse(line);
            if (document.RootElement.TryGetProperty("response", out var response)
                && response.TryGetProperty("request_id", out var id)
                && id.GetString() == requestId)
            {
                return response.Clone();
            }
        }

        return null;
    }
}

// One tool, and it falls over: the bridge turns the throw into a tool result marked an error, which is
// what the binary shows the model.
internal sealed class FallingCalc
{
    [McpTool("add", "Add two numbers")]
    public static double Add() => throw new InvalidOperationException("the tool fell over");
}
