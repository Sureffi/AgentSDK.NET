using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Sessions;

[TestClass]
public sealed class CanUseToolReplayTests
{
    private const string Prompt = "Create a file named hello.txt in the working directory containing exactly: hello";
    private const string RecordedCwd = "/tmp/claude-agent-fixtures/cwd";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task TheBinaryAsksForWriteAndTheHandlerSeesTheToolItsInputAndTheSuggestedMode()
    {
        ToolPermissionRequest? asked = null;

        await using var server = FixtureServer.Start("can-use-tool");
        using var host = StartAsync(server, (request, _) =>
        {
            asked = request;
            return Task.FromResult(ToolPermissionResult.Allow());
        });

        var result = await QueryAsync(host);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        Assert.IsFalse(result.IsError, result.Result);
        StringAssert.StartsWith(result.Result, "Done! I've created a file named `hello.txt`");

        Assert.IsNotNull(asked);
        Assert.AreEqual("Write", asked.ToolCall.Name);
        Assert.AreEqual($"{RecordedCwd}/hello.txt", asked.ToolCall.RawInput.GetProperty("file_path").GetString());
        Assert.AreEqual("hello", asked.ToolCall.RawInput.GetProperty("content").GetString());
        Assert.AreEqual("toolu_018h9W35FPLkh58UqfxQftJk", asked.ToolCall.Id);
        Assert.AreEqual("Write", asked.DisplayName);
        Assert.AreEqual("hello.txt", asked.Description);

        var suggested = (SetModePermissionUpdate)asked.Suggestions.Single();
        Assert.AreEqual(PermissionMode.AcceptEdits, suggested.Mode);
        Assert.AreEqual("session", suggested.Destination);
    }

    [TestMethod]
    public async Task AnAllowIsWrittenAsRecordedAndTheTurnRunsToItsResult()
    {
        await using var server = FixtureServer.Start("can-use-tool");
        using var host = StartAsync(server, (_, _) => Task.FromResult(ToolPermissionResult.Allow()));

        var result = await QueryAsync(host);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        Assert.AreEqual("success", result.Subtype);

        await server.WaitForWrites(3, Patience);
        CollectionAssert.AreEqual(
            Fixture.Load("can-use-tool").Stdin.Anonymised(),
            server.Written.Anonymised());
    }

    // The guard of one job: the client has no callback, the call brings one, and it is the one the
    // binary's request reaches. The reply is held to the recorded stdin as the server closes.
    [TestMethod]
    public async Task APermissionCallbackGivenOnTheCallAloneAnswersTheBinary()
    {
        var asked = 0;
        await using var server = FixtureServer.Start("can-use-tool");
        using var host = TestHost.Start(server, defaults =>
        {
            defaults.WorkingDirectory = RecordedCwd;
            defaults.SettingSources = [];
        });

        var job = new ClaudeAgentSessionOptions
        {
            Model = "haiku",
            Tools = ["Write"],
            MaxTurns = 3,
            Effort = Effort.Low,
            PermissionCallback = (_, _) =>
            {
                Interlocked.Increment(ref asked);
                return Task.FromResult(ToolPermissionResult.Allow());
            },
        };

        ResultMessage? result = null;
        await foreach (var message in host.Client.QueryAsync(Prompt, job))
        {
            result = message as ResultMessage ?? result;
        }

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        Assert.AreEqual("success", result?.Subtype);
        Assert.AreEqual(1, asked);
        CollectionAssert.AreEqual(Fixture.Load("can-use-tool").Argv.ToArray(), host.Transports.Last!.Argv.ToArray());
    }

    private static TestHost StartAsync(
        FixtureServer server,
        Func<ToolPermissionRequest, CancellationToken, Task<ToolPermissionResult>> decide) =>
        TestHost.Start(server, options: options =>
        {
            options.WorkingDirectory = RecordedCwd;
            options.SettingSources = [];
            options.Model = "haiku";
            options.Tools = ["Write"];
            options.MaxTurns = 3;
            options.Effort = Effort.Low;
            options.PermissionCallback = decide;
        });

    private static async Task<ResultMessage> QueryAsync(TestHost host)
    {
        var agent = host.Client;
        ResultMessage? result = null;
        await foreach (var message in agent.QueryAsync(Prompt))
        {
            result = message as ResultMessage ?? result;
        }

        Assert.IsNotNull(result);
        return result;
    }
}
