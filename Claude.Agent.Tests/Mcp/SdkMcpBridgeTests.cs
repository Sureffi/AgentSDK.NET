using System.Collections.Immutable;
using Claude.Agent.Tests.Replay;
using System.Text.Json;

namespace Claude.Agent.Tests.Mcp;

[TestClass]
public sealed class SdkMcpBridgeTests
{
    private const string GhostRequestId = "3a7c9d61-ghost";

    private const string GhostRequest =
        "{\"type\": \"control_request\", \"request_id\": \"" + GhostRequestId + "\", \"request\": "
        + "{\"subtype\": \"mcp_message\", \"server_name\": \"ghost\", \"message\": "
        + "{\"jsonrpc\": \"2.0\", \"method\": \"tools/list\", \"id\": 7}}}";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(6);

    private static readonly JsonElement Acknowledgement = Json("""{"jsonrpc":"2.0","result":{}}""");

    [TestMethod]
    public async Task TheInitializeHandshakeIsAnsweredWithTheRecordedReply()
    {
        await AssertAnswersFixtureLine(recorded: 0, stdinLine: 2);
    }

    [TestMethod]
    public async Task TheInitializedNotificationIsAnsweredWithTheAcknowledgementTheCallerSupplies()
    {
        await using var bridge = new SdkMcpBridge([Calculator()]);
        var (id, request) = Recorded()[1];

        var reply = await bridge.HandleAsync(request.ServerName, request.Message, CancellationToken.None);

        Assert.IsNull(reply, "a notification has no reply of its own.");
        Assert.AreEqual(
            Stdin(4),
            new FrameCodec().Encode(new OutboundControlResponse(id, new McpReply(Acknowledgement))));
    }

    [TestMethod]
    public async Task TheToolsListReplyCarriesTheRecordedSchema()
    {
        await AssertAnswersFixtureLine(recorded: 2, stdinLine: 5);
    }

    [TestMethod]
    public async Task TheToolsCallReplyCarriesTheRecordedContent()
    {
        await AssertAnswersFixtureLine(recorded: 3, stdinLine: 6);
    }

    [TestMethod]
    public async Task PingIsAnsweredWithAnEmptyResult()
    {
        await using var bridge = new SdkMcpBridge([Calculator()]);

        var reply = await bridge.HandleAsync(
            "calc",
            Json("""{"jsonrpc":"2.0","id":4,"method":"ping"}"""),
            CancellationToken.None);

        Assert.IsNotNull(reply);
        Assert.AreEqual("""{"jsonrpc":"2.0","id":4,"result":{}}""", reply.Value.GetRawText());
    }

    [TestMethod]
    public async Task AMethodTheServerDoesNotServeIsAnsweredWithMethodNotFound()
    {
        await using var bridge = new SdkMcpBridge([Calculator()]);

        var reply = await bridge.HandleAsync(
            "calc",
            Json("""{"jsonrpc":"2.0","id":5,"method":"resources/list"}"""),
            CancellationToken.None);

        Assert.IsNotNull(reply);
        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":5,"error":{"code":-32601,"message":"Method not found"}}""",
            reply.Value.GetRawText());
    }

    [TestMethod]
    public async Task AMessageWithoutTheJsonrpcKeyIsAnsweredWithInvalidRequest()
    {
        await using var bridge = new SdkMcpBridge([Calculator()]);

        var reply = await bridge.HandleAsync(
            "calc",
            Json("""{"id":6,"method":"tools/list"}"""),
            CancellationToken.None);

        Assert.IsNotNull(reply);
        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":6,"error":{"code":-32600,"message":"Invalid Request"}}""",
            reply.Value.GetRawText());
    }

    [TestMethod]
    public async Task AMessageForAServerNobodyRegisteredIsAnsweredWithMethodNotFoundInsideASuccessReply()
    {
        var script = new Script([Script.AnswerInitialize, new EmitLine(GhostRequest)]);

        await using var server = FixtureServer.Play(script);
        using var host = TestHost.Start(server, options => options.McpServers["calc"] = McpServerConfiguration.InProcess(new Calc()));
        await using var session = await ConnectAsync(host);

        await server.WaitForWrites(2, Patience);
        var reply = ReplyTo(server.Written, GhostRequestId);

        Assert.IsNotNull(reply);
        Assert.AreEqual("success", reply.Value.GetProperty("subtype").GetString());

        var error = reply.Value.GetProperty("response").GetProperty("mcp_response").GetProperty("error");
        Assert.AreEqual(-32601, error.GetProperty("code").GetInt32());
        Assert.AreEqual("This session serves no MCP server named 'ghost'.", error.GetProperty("message").GetString());
    }

    [TestMethod]
    public async Task ANotificationCarryingAnIdIsAnsweredAtThatId()
    {
        await using var bridge = new SdkMcpBridge([Calculator()]);

        var reply = await bridge.HandleAsync(
            "calc",
            Json("""{"jsonrpc":"2.0","id":9,"method":"notifications/initialized"}"""),
            CancellationToken.None);

        Assert.IsNotNull(reply);
        Assert.AreEqual(
            """{"jsonrpc":"2.0","id":9,"error":{"code":-32601,"message":"Method not found"}}""",
            reply.Value.GetRawText());
    }

    [TestMethod]
    public async Task ClosingTheBridgeCancelsAToolCallThatWouldNeverReturn()
    {
        var hanging = new HangingToolServer();
        await using var bridge = new SdkMcpBridge([hanging]);

        var call = bridge.HandleAsync(
                "hang",
                Json("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"wait","arguments":{}}}"""),
                CancellationToken.None)
            .AsTask();

        await hanging.Entered.WaitAsync(Patience);
        await bridge.CloseAsync().AsTask().WaitAsync(Patience);

        await Assert.ThrowsAsync<OperationCanceledException>(() => call);
    }

    // The binary gives up on a call with MCP's notifications/cancelled, naming the call's id; the
    // mcp-cancel fixture records it. The call's token is cancelled and the call ends unanswered.
    [TestMethod]
    public async Task ACancelledNotificationNamingARunningCallCancelsItsTokenAndTheCallIsNotAnswered()
    {
        var hanging = new HangingToolServer();
        await using var bridge = new SdkMcpBridge([hanging]);

        var call = bridge.HandleAsync(
                "hang",
                Json("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"wait","arguments":{}}}"""),
                CancellationToken.None)
            .AsTask();
        await hanging.Entered.WaitAsync(Patience);

        var notified = await bridge.HandleAsync(
            "hang",
            Json("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":2,"reason":"AbortError: remote-cancel"}}"""),
            CancellationToken.None);

        Assert.IsNull(notified, "a notification has no reply of its own.");
        var cancelled = await Assert.ThrowsAsync<McpCallCancelledException>(() => call.WaitAsync(Patience));
        Assert.AreEqual("hang", cancelled.ServerName);
        Assert.AreEqual("2", cancelled.RequestId);
    }

    // Each control request is handled on its own task, so the notification can be handled first.
    [TestMethod]
    public async Task ACancelledNotificationHandledBeforeItsCallStartsTheCallCancelled()
    {
        var hanging = new HangingToolServer();
        await using var bridge = new SdkMcpBridge([hanging]);

        await bridge.HandleAsync(
            "hang",
            Json("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":2}}"""),
            CancellationToken.None);
        var call = bridge.HandleAsync(
                "hang",
                Json("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"wait","arguments":{}}}"""),
                CancellationToken.None)
            .AsTask();

        await Assert.ThrowsAsync<McpCallCancelledException>(() => call.WaitAsync(Patience));
    }

    [TestMethod]
    public async Task ACancellationCallbackOfTheToolThatThrowsLeavesTheNotificationUnansweredAndTheCallCancelled()
    {
        var throwing = new HangingToolServer(onCancelled: () => throw new InvalidOperationException("the tool's callback fell over"));
        await using var bridge = new SdkMcpBridge([throwing]);

        var call = bridge.HandleAsync(
                "hang",
                Json("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"wait","arguments":{}}}"""),
                CancellationToken.None)
            .AsTask();
        await throwing.Entered.WaitAsync(Patience);

        var notified = await bridge.HandleAsync(
            "hang",
            Json("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":2}}"""),
            CancellationToken.None);

        Assert.IsNull(notified);
        await Assert.ThrowsAsync<McpCallCancelledException>(() => call.WaitAsync(Patience));
    }

    [TestMethod]
    public async Task ACancelledNotificationNamingAnotherIdOrAnotherServerLeavesTheCallRunning()
    {
        var hanging = new HangingToolServer();
        await using var bridge = new SdkMcpBridge([hanging, Calculator()]);

        var call = bridge.HandleAsync(
                "hang",
                Json("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"wait","arguments":{}}}"""),
                CancellationToken.None)
            .AsTask();
        await hanging.Entered.WaitAsync(Patience);

        await bridge.HandleAsync(
            "hang",
            Json("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":3}}"""),
            CancellationToken.None);
        await bridge.HandleAsync(
            "calc",
            Json("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":2}}"""),
            CancellationToken.None);
        await bridge.HandleAsync(
            "hang",
            Json("""{"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":"2"}}"""),
            CancellationToken.None);

        await Task.Delay(TimeSpan.FromMilliseconds(200));
        Assert.IsFalse(call.IsCompleted, "a cancellation naming another call ended this one.");

        await bridge.CloseAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => call.WaitAsync(Patience));
    }

    private static async Task AssertAnswersFixtureLine(int recorded, int stdinLine)
    {
        await using var bridge = new SdkMcpBridge([Calculator()]);
        var (id, request) = Recorded()[recorded];

        var reply = await bridge.HandleAsync(request.ServerName, request.Message, CancellationToken.None);

        Assert.IsNotNull(reply);
        Assert.AreEqual(
            Stdin(stdinLine),
            new FrameCodec().Encode(new OutboundControlResponse(id, new McpReply(reply.Value))),
            $"sdk-mcp.stdin.jsonl line {stdinLine}");
    }

    private static ReflectionToolServer Calculator() => new(new Calc(), "calc", "1.0.0");

    private static ImmutableArray<(RequestId Id, McpMessageRequest Request)> Recorded()
    {
        var codec = new FrameCodec();
        var requests = ImmutableArray.CreateBuilder<(RequestId, McpMessageRequest)>();

        foreach (var line in Fixture.Load("sdk-mcp").Stdout.Where(one => Fixture.TypeOf(one) == "control_request"))
        {
            var decoded = codec.Decode(line);
            Assert.IsTrue(decoded.IsOk, "sdk-mcp.stdout.jsonl carries a control request that does not decode.");

            var frame = (ControlRequestFrame)decoded.Value;
            requests.Add((frame.RequestId, (McpMessageRequest)frame.Request));
        }

        return requests.ToImmutable();
    }

    private static string Stdin(int lineNumber) => Fixture.Load("sdk-mcp").Stdin[lineNumber - 1];

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static Task<ClaudeAgentSession> ConnectAsync(TestHost host) => host.Client.StartSessionAsync();

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

internal sealed class HangingToolServer(Action? onCancelled = null) : IToolServer
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Name => new("hang");

    public string Version => "1.0.0";

    public IReadOnlyList<ToolDefinition> Tools => [];

    public Task Entered => _entered.Task;

    public async ValueTask<ToolResult> CallAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
    {
        _entered.TrySetResult();
        using var registration = onCancelled is null ? default : cancellationToken.Register(onCancelled);
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        return ToolResult.FromText("never");
    }
}
