using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Sessions;

// The suggestions fixture: a Bash command, a Read outside the cwd and a Write outside it, none of
// them allowed, so the binary asks three times and each ask says what would stop it asking.
[TestClass]
public sealed class SuggestionsReplayTests
{
    private const string RecordedCwd = "/tmp/claude-agent-fixtures/cwd";
    private const string Outside = "/tmp/claude-agent-fixtures/outside";
    private const string Prompt =
        "Do these three things in order, each with one tool call: run the shell command `git init --bare hive.git` with the " +
        $"Bash tool; read the file {Outside}/notes.txt with the Read tool; write a file {Outside}/asked.txt containing exactly: " +
        "yes. Then reply with exactly the word: done";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task ABashCommandIsAskedWithARuleForLocalSettings()
    {
        var asked = await AskedAsync();

        var bash = asked[0];
        Assert.AreEqual("Bash", bash.ToolCall.Name);
        var suggested = (AddRulesPermissionUpdate)bash.Suggestions.Single();
        Assert.AreEqual(PermissionRuleAction.Allow, suggested.Behavior);
        Assert.AreEqual("localSettings", suggested.Destination);
        Assert.AreEqual("Bash", suggested.Rules.Single().ToolName);
        Assert.AreEqual("git init *", suggested.Rules.Single().RuleContent);
    }

    [TestMethod]
    public async Task AReadOutsideTheWorkingDirectoryIsAskedWithARuleForTheSession()
    {
        var asked = await AskedAsync();

        var read = asked[1];
        Assert.AreEqual("Read", read.ToolCall.Name);
        var suggested = (AddRulesPermissionUpdate)read.Suggestions.Single();
        Assert.AreEqual(PermissionRuleAction.Allow, suggested.Behavior);
        Assert.AreEqual("session", suggested.Destination);
        Assert.AreEqual("Read", suggested.Rules.Single().ToolName);
        Assert.AreEqual($"/{Outside}/**", suggested.Rules.Single().RuleContent);
    }

    [TestMethod]
    public async Task AWriteOutsideTheWorkingDirectoryIsAskedWithAModeAndTheDirectory()
    {
        var asked = await AskedAsync();

        var write = asked[2];
        Assert.AreEqual("Write", write.ToolCall.Name);
        Assert.AreEqual(2, write.Suggestions.Count);
        Assert.AreEqual(PermissionMode.AcceptEdits, ((SetModePermissionUpdate)write.Suggestions[0]).Mode);
        var directories = (AddDirectoriesPermissionUpdate)write.Suggestions[1];
        Assert.AreEqual("session", directories.Destination);
        CollectionAssert.AreEqual(new[] { Outside }, directories.Directories.ToArray());
    }

    [TestMethod]
    public async Task ThreeAllowsAreWrittenAsRecordedOnTheRecordedArgv()
    {
        await using var server = FixtureServer.Start("suggestions");
        using var host = Start(server, (_, _) => Task.FromResult(ToolPermissionResult.Allow()));

        var result = await QueryAsync(host);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        Assert.AreEqual("done", result.Result);

        var fixture = Fixture.Load("suggestions");
        await server.WaitForWrites(fixture.Stdin.Length, Patience);
        CollectionAssert.AreEqual(
            fixture.Stdin.Anonymised(),
            server.Written.Anonymised());
        CollectionAssert.AreEqual(fixture.Argv.ToArray(), host.Transports.Requests.Single().Argv.ToArray());
    }

    private static async Task<List<ToolPermissionRequest>> AskedAsync()
    {
        var asked = new List<ToolPermissionRequest>();
        await using var server = FixtureServer.Start("suggestions");
        using var host = Start(server, (request, _) =>
        {
            asked.Add(request);
            return Task.FromResult(ToolPermissionResult.Allow());
        });

        await QueryAsync(host);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        Assert.AreEqual(3, asked.Count);
        return asked;
    }

    private static TestHost Start(
        FixtureServer server,
        Func<ToolPermissionRequest, CancellationToken, Task<ToolPermissionResult>> decide) =>
        TestHost.Start(server, options: options =>
        {
            options.WorkingDirectory = RecordedCwd;
            options.SettingSources = [];
            options.Model = "haiku";
            options.Tools = ["Bash", "Read", "Write"];
            options.MaxTurns = 5;
            options.Effort = Effort.Low;
            options.PermissionCallback = decide;
        });

    private static async Task<ResultMessage> QueryAsync(TestHost host)
    {
        ResultMessage? result = null;
        await foreach (var message in host.Client.QueryAsync(Prompt))
        {
            result = message as ResultMessage ?? result;
        }

        Assert.IsNotNull(result);
        return result;
    }
}
