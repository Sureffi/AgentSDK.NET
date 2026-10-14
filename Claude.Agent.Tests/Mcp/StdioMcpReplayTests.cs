using System.Collections.Immutable;
using Claude.Agent.Tests.Replay;
using System.Text.Json;
using OsProcess = System.Diagnostics.Process;
using OsProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace Claude.Agent.Tests.Mcp;

[TestClass]
public sealed class StdioMcpReplayTests
{
    private const string EchoPrompt = "Use the echo tool with the text pong, then reply with exactly what it returned.";
    private const string CalcPrompt = "Use the add tool to add 2 and 3, then reply with just the number.";
    private const string RecordedCwd = "/tmp/claude-agent-fixtures/cwd";
    private const string RecordedEchoServer = "/tmp/claude-agent-fixtures/cwd/echo-server";
    private const string NotFound = "Server not found: calc";

    private static readonly string EchoServer = new("echo");
    private static readonly string CalcServer = new("calc");
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task TheStdioServerIsConnectedWithItsServerInfoAndItsOneToolBeforeAnyPromptIsSent()
    {
        await using var server = FixtureServer.Start("stdio-mcp");
        using var host = StartWithEchoAsync(server);
        await using var session = await ConnectAsync(host);

        var run = await DriveEchoAsync(session);

        var echo = run.WhileConnected.Single();
        Assert.AreEqual(EchoServer, echo.Name);
        Assert.AreEqual("connected", echo.Status);
        Assert.AreEqual("1.0.0", echo.ServerInfo?.Version);
        Assert.AreEqual("echo", echo.ServerInfo?.Name);
        Assert.AreEqual("/tmp/claude-agent-fixtures/cwd/echo-server", ((McpStdioServerConfiguration)echo.Config!).Command);
        Assert.AreEqual(McpServerSource.Dynamic, echo.Scope);
        Assert.IsNull(echo.Error);

        Assert.AreEqual("echo", echo.Tools.Single().Name);
    }

    [TestMethod]
    public async Task TogglingTheStdioServerOffLeavesItDisabledWithNoServerInfoAndNoToolsInTheNextStatus()
    {
        await using var server = FixtureServer.Start("stdio-mcp");
        using var host = StartWithEchoAsync(server);
        await using var session = await ConnectAsync(host);

        var run = await DriveEchoAsync(session);

        var echo = run.WhileDisabled.Single();
        Assert.AreEqual(EchoServer, echo.Name);
        Assert.AreEqual("disabled", echo.Status);
        Assert.IsNull(echo.ServerInfo);
        Assert.AreEqual(0, echo.Tools.Count);
        Assert.IsInstanceOfType<McpStdioServerConfiguration>(echo.Config);
    }

    [TestMethod]
    public async Task TheAssistantCallsTheStdioServersToolAsMcpEchoEchoAndItsToolResultTextIsTheAnswer()
    {
        await using var server = FixtureServer.Start("stdio-mcp");
        using var host = StartWithEchoAsync(server);
        await using var session = await ConnectAsync(host);

        var run = await DriveEchoAsync(session);

        // The recording ends at the result, before a completed frame: the run completes with its
        // trace when the session's output ends.
        CollectionAssert.AreEqual(
            new[]
            {
                typeof(CommandLifecycleMessage),
                typeof(CommandLifecycleMessage),
                typeof(SystemInitMessage),
                typeof(ThinkingTokensMessage),
                typeof(ThinkingTokensMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(RateLimitMessage),
                typeof(UserMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(ResultMessage),
            },
            run.Messages.Select(message => message.GetType()).ToArray());

        // Each answer arrives as two assistant frames: a thinking block whose text the binary
        // withholds, then the block that does the work.
        var answers = run.Messages.OfType<AssistantMessage>().ToArray();
        Assert.IsInstanceOfType<ThinkingBlock>(answers[0].Content.Single());
        Assert.IsInstanceOfType<ThinkingBlock>(answers[2].Content.Single());

        var init = run.Messages.OfType<SystemInitMessage>().Single();
        Assert.AreEqual(EchoServer, init.McpServers.Single().Name);
        Assert.AreEqual("connected", init.McpServers.Single().Status);
        CollectionAssert.AreEqual(new[] { "mcp__echo__echo" }, init.Tools.ToArray());

        var toolUse = answers[1].ToolUses.Single();
        Assert.AreEqual("mcp__echo__echo", toolUse.Name);
        Assert.AreEqual("pong", toolUse.RawInput.GetProperty("text").GetString());

        var toolResult = run.Messages.OfType<UserMessage>().Single().Content.OfType<ToolResultBlock>().Single();
        Assert.AreEqual(toolUse.Id, toolResult.ToolUseId);
        Assert.AreEqual("pong", TextOf(toolResult));

        Assert.AreEqual("pong", answers[3].Text);
        Assert.AreEqual("pong", ((ResultMessage)run.Messages[^1]).Result);
    }

    [TestMethod]
    public async Task TheStdioMcpFixtureWritesTheSixControlRequestsAndThePromptInTheRecordedOrder()
    {
        await using var server = FixtureServer.Start("stdio-mcp");
        using var host = StartWithEchoAsync(server);
        await using var session = await ConnectAsync(host);

        await DriveEchoAsync(session);
        await server.WaitForWrites(7, Patience);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        CollectionAssert.AreEqual(
            Fixture.Load("stdio-mcp").Stdin.Anonymised(),
            server.Written.Anonymised());
    }

    [TestMethod]
    public async Task TheStdioMcpFixtureStartsTheTransportOnTheRecordedArgvWithTheServerInTheMcpConfig()
    {
        await using var server = FixtureServer.Start("stdio-mcp");
        using var host = StartWithEchoAsync(server);
        await using var session = await ConnectAsync(host);

        await DriveEchoAsync(session);

        AssertArgv(host, "stdio-mcp");
    }

    // The recording asked for the status before the in-process server's handshake finished: the
    // binary answered it ahead of notifications/initialized and tools/list, and the reply lists only
    // the account's claude.ai connector, still pending. payloads asked after the handshake, and its
    // reply lists the in-process server too.
    [TestMethod]
    public async Task AStatusAskedBeforeTheInProcessHandshakeFinishedListsOnlyTheConnector()
    {
        await using var server = FixtureServer.Start("sdk-mcp-client");
        using var host = StartWithCalcAsync(server);
        await using var session = await ConnectAsync(host);

        var run = await DriveCalcAsync(session);

        var listed = run.Status.Single();
        Assert.AreEqual("claude.ai Claude Docs", listed.Name);
        Assert.AreEqual("pending", listed.Status);
    }

    [TestMethod]
    public async Task ReconnectAndToggleOnTheInProcessServerAreBothRefusedBecauseTheBinaryDoesNotHostIt()
    {
        await using var server = FixtureServer.Start("sdk-mcp-client");
        using var host = StartWithCalcAsync(server);
        await using var session = await ConnectAsync(host);

        var run = await DriveCalcAsync(session);

        Assert.AreEqual(ErrorCodes.Control, run.Reconnect.ErrorCode);
        Assert.AreEqual(NotFound, run.Reconnect.Message);

        Assert.AreEqual(ErrorCodes.Control, run.Toggle.ErrorCode);
        Assert.AreEqual(NotFound, run.Toggle.Message);
    }

    [TestMethod]
    public async Task TheInProcessAddToolServesTheBinarysToolCallAndTheAnswerIsFive()
    {
        await using var server = FixtureServer.Start("sdk-mcp-client");
        using var host = StartWithCalcAsync(server);
        await using var session = await ConnectAsync(host);

        var run = await DriveCalcAsync(session);

        var init = run.Messages.OfType<SystemInitMessage>().Single();
        var calc = init.McpServers.Single(listed => listed.Name == CalcServer);
        Assert.AreEqual("connected", calc.Status);

        var toolUse = run.Messages.OfType<AssistantMessage>().SelectMany(message => message.ToolUses).Single();
        Assert.AreEqual("mcp__calc__add", toolUse.Name);

        var toolResult = run.Messages.OfType<UserMessage>()
            .SelectMany(message => message.Content)
            .OfType<ToolResultBlock>()
            .Single();
        Assert.AreEqual(toolUse.Id, toolResult.ToolUseId);
        Assert.AreEqual("5", TextOf(toolResult));
        Assert.AreEqual("5", ((ResultMessage)run.Messages[^1]).Result);
    }

    [TestMethod]
    public async Task TheSdkMcpClientFixtureWritesEveryRecordedLineIncludingTheFourJsonRpcRepliesFromTheBridge()
    {
        await using var server = FixtureServer.Start("sdk-mcp-client");
        using var host = StartWithCalcAsync(server);
        await using var session = await ConnectAsync(host);

        await DriveCalcAsync(session);
        await server.WaitForWrites(9, Patience);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        var recorded = Fixture.Load("sdk-mcp-client").Stdin;
        CollectionAssert.AreEqual(
            recorded.Anonymised(),
            server.Written.Anonymised());
    }

    [TestMethod]
    public async Task TheSdkMcpClientFixtureStartsTheTransportOnTheRecordedArgvWithTheSdkServerNamedInTheMcpConfig()
    {
        await using var server = FixtureServer.Start("sdk-mcp-client");
        using var host = StartWithCalcAsync(server);
        await using var session = await ConnectAsync(host);

        await DriveCalcAsync(session);

        AssertArgv(host, "sdk-mcp-client");
    }

    [TestMethod]
    public async Task TheEchoServerScriptAnswersInitializeListsItsOneToolAndEchoesTheTextItWasCalledWith()
    {
        using var echo = EchoServerProcess.Start();

        var initialize = await echo.CallAsync(
            0,
            "initialize",
            """{"protocolVersion": "2025-06-18", "capabilities": {}, "clientInfo": {"name": "replay", "version": "0"}}""");
        Assert.AreEqual("2025-06-18", initialize.GetProperty("protocolVersion").GetString());
        Assert.AreEqual("echo", initialize.GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.AreEqual("1.0.0", initialize.GetProperty("serverInfo").GetProperty("version").GetString());

        await echo.NotifyAsync("notifications/initialized");

        var tools = await echo.CallAsync(1, "tools/list", null);
        var tool = tools.GetProperty("tools").EnumerateArray().Single();
        Assert.AreEqual("echo", tool.GetProperty("name").GetString());
        Assert.AreEqual("text", tool.GetProperty("inputSchema").GetProperty("required").EnumerateArray().Single().GetString());

        var called = await echo.CallAsync(2, "tools/call", """{"name": "echo", "arguments": {"text": "pong"}}""");
        var content = called.GetProperty("content").EnumerateArray().Single();
        Assert.AreEqual("text", content.GetProperty("type").GetString());
        Assert.AreEqual("pong", content.GetProperty("text").GetString());
    }

    private static async Task<EchoRun> DriveEchoAsync(ClaudeAgentSession session)
    {
        var connected = await session.GetMcpStatusAsync();
        await session.ReconnectMcpServerAsync(EchoServer);
        await session.ToggleMcpServerAsync(EchoServer, enabled: false);
        var disabled = await session.GetMcpStatusAsync();
        await session.ToggleMcpServerAsync(EchoServer, enabled: true);

        return new EchoRun(connected, disabled, await PromptAsync(session, EchoPrompt));
    }

    private static async Task<CalcRun> DriveCalcAsync(ClaudeAgentSession session)
    {
        var status = await session.GetMcpStatusAsync();

        var reconnected = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(
            () => session.ReconnectMcpServerAsync(CalcServer));
        var toggled = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(
            () => session.ToggleMcpServerAsync(CalcServer, enabled: false));

        return new CalcRun(status, reconnected, toggled, await PromptAsync(session, CalcPrompt));
    }

    // The turn as the binary wrote it: the run's messages without the tool-call snapshots.
    private static async Task<ImmutableArray<AgentMessage>> PromptAsync(ClaudeAgentSession session, string prompt)
    {
        var trace = await session.SendAsync(prompt).WaitForCompletionAsync();
        return [.. trace.Messages.Where(message => message is not ToolCallMessage)];
    }

    private static TestHost StartWithEchoAsync(FixtureServer server) => TestHost.Start(
        server,
        options: options =>
        {
            Recorded(options);
            options.Model = "haiku";
            options.AllowedTools.Add("mcp__echo__echo");
            options.McpServers[EchoServer] = McpServerConfiguration.Stdio(RecordedEchoServer);
            options.OnlyConfiguredMcpServers = true;
        });

    private static TestHost StartWithCalcAsync(FixtureServer server) => TestHost.Start(
        server,
        options =>
        {
            Recorded(options);
            options.AllowedTools.Add("mcp__calc__add");
            options.McpServers[CalcServer] = McpServerConfiguration.InProcess(new Calc());
        });

    private static void Recorded(ClaudeAgentSessionOptions options)
    {
        options.WorkingDirectory = RecordedCwd;
        options.SettingSources = [];
        options.Model = "haiku";
        options.Tools = [];
        options.MaxTurns = 3;
        options.Effort = Effort.Low;
    }

    private static void AssertArgv(TestHost host, string fixture)
    {
        var asked = host.Transports.Last;
        Assert.IsNotNull(asked);
        CollectionAssert.AreEqual(Fixture.Load(fixture).Argv.ToArray(), asked.Argv.ToArray(), $"{fixture}.argv.json");
    }

    private static Task<ClaudeAgentSession> ConnectAsync(TestHost host) => host.Client.StartSessionAsync();

    private static string TextOf(ToolResultBlock block) => ((TextBlock)block.Content.Single()).Text;

    private sealed record EchoRun(
        IReadOnlyList<McpServerStatus> WhileConnected,
        IReadOnlyList<McpServerStatus> WhileDisabled,
        ImmutableArray<AgentMessage> Messages);

    private sealed record CalcRun(
        IReadOnlyList<McpServerStatus> Status,
        ClaudeAgentException Reconnect,
        ClaudeAgentException Toggle, ImmutableArray<AgentMessage> Messages);
}

internal sealed class EchoServerProcess : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly OsProcess _process;

    private EchoServerProcess(OsProcess process) => _process = process;

    public static EchoServerProcess Start()
    {
        var start = new OsProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "mcp", "echo-server"))
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        var process = OsProcess.Start(start);
        Assert.IsNotNull(process, "the echo server did not start.");
        return new EchoServerProcess(process);
    }

    public async Task NotifyAsync(string method)
    {
        await _process.StandardInput
            .WriteLineAsync($$"""{"jsonrpc": "2.0", "method": "{{method}}"}""")
            .ConfigureAwait(false);
        await _process.StandardInput.FlushAsync().ConfigureAwait(false);
    }

    public async Task<JsonElement> CallAsync(int id, string method, string? parameters)
    {
        var request = parameters is null
            ? $$"""{"jsonrpc": "2.0", "id": {{id}}, "method": "{{method}}"}"""
            : $$"""{"jsonrpc": "2.0", "id": {{id}}, "method": "{{method}}", "params": {{parameters}}}""";

        await _process.StandardInput.WriteLineAsync(request).ConfigureAwait(false);
        await _process.StandardInput.FlushAsync().ConfigureAwait(false);

        using var patience = new CancellationTokenSource(Patience);
        var line = await _process.StandardOutput.ReadLineAsync(patience.Token).ConfigureAwait(false);
        Assert.IsNotNull(line, $"the echo server answered nothing to {method}.");

        using var document = JsonDocument.Parse(line);
        Assert.AreEqual(id, document.RootElement.GetProperty("id").GetInt32());
        return document.RootElement.GetProperty("result").Clone();
    }

    public void Dispose()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }

        _process.Dispose();
    }
}
