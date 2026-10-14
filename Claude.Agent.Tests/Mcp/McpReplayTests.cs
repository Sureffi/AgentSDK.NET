using System.Collections.Immutable;
using Claude.Agent.Tests.Replay;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Claude.Agent.Tests.Mcp;

[TestClass]
public sealed class McpReplayTests
{
    private const string Prompt = "Use the add tool to add 2 and 3, then reply with just the number.";

    private const string RecordedCwd =
        "/tmp/claude-agent-fixtures/cwd";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task TheSdkMcpFixtureWritesEveryRecordedLineInTheSameOrder()
    {
        await using var server = FixtureServer.Start("sdk-mcp");
        using var host = StartAsync(server);

        await ReplayAsync(host);
        await server.WaitForWrites(6, Patience);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        CollectionAssert.AreEqual(
            Fixture.Load("sdk-mcp").Stdin.Anonymised(),
            server.Written.Anonymised());
    }

    [TestMethod]
    public async Task TheSdkMcpFixtureEndsWithTheResultTheInProcessToolProduced()
    {
        await using var server = FixtureServer.Start("sdk-mcp");
        using var host = StartAsync(server);

        var seen = await ReplayAsync(host);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        var result = seen[^1] as ResultMessage;
        Assert.IsNotNull(result);
        Assert.AreEqual("5", result.Result);
    }

    [TestMethod]
    public async Task TheSdkMcpFixtureStartsTheTransportOnTheRecordedArgv()
    {
        await using var server = FixtureServer.Start("sdk-mcp");
        using var host = StartAsync(server);

        await ReplayAsync(host);

        var request = host.Transports.Last;
        Assert.IsNotNull(request);
        CollectionAssert.AreEqual(Fixture.Load("sdk-mcp").Argv.ToArray(), request.Argv.ToArray());
    }

    // The client names no server. The call does, and the launcher finds it per session: the tool
    // call is answered in this process and every line written is the recorded one.
    [TestMethod]
    public async Task AnInProcessServerGivenOnTheCallAloneIsServed()
    {
        await using var server = FixtureServer.Start("sdk-mcp");
        using var host = TestHost.Start(server);
        var asked = new ClaudeAgentSessionOptions
        {
            WorkingDirectory = RecordedCwd,
            SettingSources = [],
            Model = "haiku",
            Tools = [],
            MaxTurns = 3,
            Effort = Effort.Low,
        };
        asked.AllowedTools.Add("mcp__calc__add");
        asked.McpServers["calc"] = McpServerConfiguration.InProcess(new Calc());

        ResultMessage? result = null;
        await foreach (var message in host.Client.QueryAsync(Prompt, asked))
        {
            result = message as ResultMessage ?? result;
        }

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        Assert.AreEqual("5", result?.Result);
        CollectionAssert.AreEqual(Fixture.Load("sdk-mcp").Argv.ToArray(), host.Transports.Last!.Argv.ToArray());
    }

    // AddClaudeTools puts the server on the defaults, under the name it was given, and a session
    // asked those defaults serves it.
    [TestMethod]
    public async Task AddClaudeToolsPutsTheServerOnTheDefaultsAndASessionServesIt()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TestHostEnvironments.Unmade);
        services.AddClaudeTools<Calc>("calc");
        services.AddClaudeAgentClient(options =>
        {
            options.Defaults.WorkingDirectory = RecordedCwd;
            options.Defaults.SettingSources = [];
            options.Defaults.Model = "haiku";
            options.Defaults.Tools = [];
            options.Defaults.AllowedTools.Add("mcp__calc__add");
            options.Defaults.MaxTurns = 3;
            options.Defaults.Effort = Effort.Low;
        });
        await using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ClaudeAgentClientOptions>>().Value;

        Assert.IsInstanceOfType<McpSdkServerConfiguration>(options.Defaults.McpServers["calc"]);

        await using var server = FixtureServer.Start("sdk-mcp");
        using var host = TestHost.Start(options, server);
        var seen = await ReplayAsync(host);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        Assert.AreEqual("5", (seen[^1] as ResultMessage)?.Result);
    }

    // The call names a server of its own. The one AddClaudeTools put on the defaults is still there:
    // both are in the --mcp-config, and the in-process one answers the recorded tool call.
    [TestMethod]
    public async Task TheServerAddClaudeToolsRegistersSurvivesACallThatNamesItsOwn()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TestHostEnvironments.Unmade);
        services.AddClaudeTools<Calc>("calc");
        services.AddClaudeAgentClient(options =>
        {
            options.Defaults.WorkingDirectory = RecordedCwd;
            options.Defaults.SettingSources = [];
            options.Defaults.Model = "haiku";
            options.Defaults.Tools = [];
            options.Defaults.AllowedTools.Add("mcp__calc__add");
            options.Defaults.MaxTurns = 3;
            options.Defaults.Effort = Effort.Low;
        });
        await using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ClaudeAgentClientOptions>>().Value;

        await using var server = FixtureServer.Start("sdk-mcp");
        using var host = TestHost.Start(options, server);
        var job = new ClaudeAgentSessionOptions();
        job.McpServers["hive"] = McpServerConfiguration.Stdio("hive-server");

        ResultMessage? result = null;
        await foreach (var message in host.Client.QueryAsync(Prompt, job))
        {
            result = message as ResultMessage ?? result;
        }

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        Assert.AreEqual("5", result?.Result);
        CollectionAssert.AreEqual(new[] { "calc", "hive" }, host.Transports.Last!.Session.McpServers.Keys.ToArray());
        var argv = host.Transports.Last.Argv;
        var config = argv[argv.IndexOf("--mcp-config") + 1];
        StringAssert.Contains(config, "\"calc\"");
        StringAssert.Contains(config, "\"hive\"");
    }

    private static TestHost StartAsync(FixtureServer server) => TestHost.Start(
        server,
        options =>
        {
            options.WorkingDirectory = RecordedCwd;
            options.SettingSources = [];
            options.Model = "haiku";
            options.Tools = [];
            options.AllowedTools.Add("mcp__calc__add");
            options.MaxTurns = 3;
            options.Effort = Effort.Low;
            options.McpServers["calc"] = McpServerConfiguration.InProcess(new Calc());
        });

    private static async Task<ImmutableArray<AgentMessage>> ReplayAsync(TestHost host)
    {
        var agent = host.Client;
        var seen = ImmutableArray.CreateBuilder<AgentMessage>();

        await foreach (var message in agent.QueryAsync(Prompt))
        {
            seen.Add(message);
        }

        return seen.ToImmutable();
    }
}
