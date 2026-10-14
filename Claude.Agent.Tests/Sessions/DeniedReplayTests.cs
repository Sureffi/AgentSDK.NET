using System.Text.Json;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Sessions;

[TestClass]
public sealed class DeniedReplayTests
{
    private const string Prompt =
        "Create a file named denied.txt in the working directory containing exactly: no. "
        + "If you cannot, reply with exactly the word: denied";

    private const string Refusal =
        "Claude requested permissions to write to /tmp/claude-agent-fixtures/cwd/denied.txt, "
        + "but you haven't granted it yet.";

    private static readonly string Write = new("Write");
    private static readonly string DeniedCall = new("toolu_01G66jXfHJfjE1uEp9qNP5df");
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task TheBinaryDeniesTheWriteOnItsOwnAndTheDenialArrivesBeforeTheToolResultItCaused()
    {
        await using var server = FixtureServer.Start("denied");
        using var host = StartAsync(server);

        var seen = (await ReplayAsync(host)).Written();

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        CollectionAssert.AreEqual(
            new[]
            {
                typeof(RateLimitMessage),
                typeof(SystemInitMessage),
                typeof(ThinkingTokensMessage),
                typeof(ThinkingTokensMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(PermissionDeniedMessage),
                typeof(UserMessage),
                typeof(AssistantMessage),
                typeof(AssistantMessage),
                typeof(ResultMessage),
            },
            seen.ConvertAll(message => message.GetType()));

        var call = seen.OfType<AssistantMessage>().SelectMany(message => message.ToolUses).Single();
        Assert.AreEqual(Write, call.Name);
        Assert.AreEqual(DeniedCall, call.Id);

        var denied = seen.OfType<PermissionDeniedMessage>().Single();
        Assert.AreEqual(Write, denied.ToolName);
        Assert.AreEqual(DeniedCall, denied.ToolUseId);
        Assert.AreEqual(Refusal, denied.Message);
    }

    [TestMethod]
    public async Task TheDeniedCallComesBackAsAToolResultMarkedAnErrorAndTheAssistantThenAnswersDenied()
    {
        await using var server = FixtureServer.Start("denied");
        using var host = StartAsync(server);

        var seen = await ReplayAsync(host);

        var result = (ToolResultBlock)seen.OfType<UserMessage>().Single().Content.Single();
        Assert.AreEqual(DeniedCall, result.ToolUseId);
        Assert.AreEqual<bool?>(true, result.IsError);
        Assert.AreEqual(Refusal, result.Content.Text());

        Assert.AreEqual("denied", seen.OfType<AssistantMessage>().Last().Text);
        Assert.AreEqual("denied", seen.OfType<ResultMessage>().Single().Result);
    }

    [TestMethod]
    public async Task TheResultFrameRepeatsTheRefusedCallInPermissionDenialsWithTheSameToolNameAndToolUseId()
    {
        await using var server = FixtureServer.Start("denied");
        using var host = StartAsync(server);

        var seen = await ReplayAsync(host);
        var result = seen.OfType<ResultMessage>().Single();

        Assert.AreEqual("success", result.Subtype);
        Assert.IsFalse(result.IsError);
        Assert.AreEqual(2, result.TurnCount);

        var denial = result.PermissionDenials.Single();
        Assert.AreEqual(Write, denial.ToolName);
        Assert.AreEqual(DeniedCall, denial.ToolUseId);
    }

    [TestMethod]
    public async Task TheDeniedCallStartsAfterTheAssistantMessageAndClosesFailedWithTheRefusalAfterTheToolResult()
    {
        await using var server = FixtureServer.Start("denied");
        using var host = StartAsync(server);

        var seen = await ReplayAsync(host);

        var snapshots = seen.OfType<ToolCallMessage>().Select(message => message.ToolCall).ToList();
        CollectionAssert.AreEqual(
            new[] { ToolCallStatus.Started, ToolCallStatus.Failed },
            snapshots.ConvertAll(call => call.Status));
        Assert.IsTrue(snapshots.All(call => call.Id == DeniedCall && call.Name == Write));

        var started = seen.FindIndex(message => message is ToolCallMessage);
        Assert.IsInstanceOfType<AssistantMessage>(seen[started - 1]);
        var closed = seen.FindLastIndex(message => message is ToolCallMessage);
        Assert.IsInstanceOfType<UserMessage>(seen[closed - 1]);

        var failed = snapshots[1];
        Assert.AreEqual(Refusal, failed.Error);
        Assert.AreEqual(Refusal, failed.Content.Text());
        Assert.IsNull(failed.Output);
        Assert.IsNull(failed.RawOutput);
        Assert.AreEqual("/tmp/claude-agent-fixtures/cwd/denied.txt", failed.RawInput.GetProperty("file_path").GetString());
    }

    [TestMethod]
    public async Task WithNoPermissionHandlerThisSideWritesTheInitializeAndThePromptAndNothingElse()
    {
        await using var server = FixtureServer.Start("denied");
        using var host = StartAsync(server);

        await ReplayAsync(host);
        await server.WaitForWrites(2, Patience);

        CollectionAssert.AreEqual(
            Fixture.Load("denied").Stdin.Anonymised(),
            server.Written.Anonymised());

        var requests = server.Written.Where(line => Fixture.TypeOf(line) == "control_request").ToArray();
        Assert.AreEqual(1, requests.Length);
        Assert.AreEqual("initialize", SubtypeOf(requests[0]));
    }

    [TestMethod]
    public async Task TheArgvThatAskedForTheDenialCarriesWriteAloneAndNoPermissionPromptTool()
    {
        await using var server = FixtureServer.Start("denied");
        using var host = StartAsync(server);

        await ReplayAsync(host);

        var asked = host.Transports.Last;
        Assert.IsNotNull(asked);
        Assert.IsFalse(asked.Needs.RoutePermissionsOverStdio);
        CollectionAssert.AreEqual(
            Fixture.Load("denied").Argv.ToArray(),
            asked.Argv.ToArray(),
            "denied.argv.json");
        CollectionAssert.DoesNotContain(asked.Argv.ToArray(), "--permission-prompt-tool");
    }

    private static TestHost StartAsync(FixtureServer server) => TestHost.Start(server, options: options =>
    {
        options.WorkingDirectory = "/tmp/claude-agent-fixtures/cwd";
        options.SettingSources = [];
        options.Model = "haiku";
        options.Tools = ["Write"];
        options.MaxTurns = 2;
        options.Effort = Effort.Low;
    });

    private static async Task<List<AgentMessage>> ReplayAsync(TestHost host)
    {
        var seen = new List<AgentMessage>();
        await foreach (var message in host.Client.QueryAsync(Prompt))
        {
            seen.Add(message);
        }

        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);
        return seen;
    }

    private static string? SubtypeOf(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.GetProperty("request").GetProperty("subtype").GetString();
    }
}
