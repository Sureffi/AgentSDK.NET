using System.Text.Json.Nodes;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Sessions;

// The session client a ClaudeAgentClient hands out reads the client's configuration directory and
// nothing of the user's own: every case seeds both, under the same project, and reads one.
[TestClass]
public sealed class SessionListingTests
{
    [TestMethod]
    public async Task TheConfigurationDirectorysSessionsListNewestFirstAndTheUsersAreNotListed()
    {
        using var client = new TemporaryClient();
        using var project = new TemporaryProject();
        var home = client.ConfigurationProject(project.Path);
        var older = Built.Session(home, firstPrompt: "older", mtimeEpochMs: 1_700_000_000_000);
        var newer = Built.Session(home, firstPrompt: "newer", mtimeEpochMs: 1_700_000_001_000);
        var users = Built.Session(client.UserProject(project.Path), firstPrompt: "the user's", mtimeEpochMs: 1_700_000_002_000);

        var listed = await client.Sessions.GetSessionsAsync(Listing(project));

        CollectionAssert.AreEqual(new[] { newer, older }, listed.Select(info => info.SessionId).ToArray());
        CollectionAssert.DoesNotContain(listed.Select(info => info.SessionId).ToArray(), users);
        Assert.AreEqual("newer", listed[0].Summary);
    }

    [TestMethod]
    public async Task LimitAndOffsetPageTheListing()
    {
        using var client = new TemporaryClient();
        using var project = new TemporaryProject();
        var home = client.ConfigurationProject(project.Path);
        var sessions = Enumerable.Range(0, 5)
            .Select(i => Built.Session(home, firstPrompt: "p" + i, mtimeEpochMs: 1_700_000_000_000 + (i * 1000)))
            .ToArray();

        var page = await client.Sessions.GetSessionsAsync(Listing(project, 2, 1));

        CollectionAssert.AreEqual(new[] { sessions[3], sessions[2] }, page.Select(info => info.SessionId).ToArray());
    }

    [TestMethod]
    public async Task SidechainsAreDroppedBeforeTheListingIsPaged()
    {
        using var client = new TemporaryClient();
        using var project = new TemporaryProject();
        var home = client.ConfigurationProject(project.Path);
        for (var i = 0; i < 3; i++)
        {
            Built.Session(home, firstPrompt: "side" + i, sidechain: true, mtimeEpochMs: 1_700_000_010_000 + (i * 1000));
        }

        for (var i = 0; i < 5; i++)
        {
            Built.Session(home, firstPrompt: "real" + i, mtimeEpochMs: 1_700_000_000_000 + (i * 1000));
        }

        var listed = await client.Sessions.GetSessionsAsync(Listing(project, 5));
        // The offset is what separates the two orderings at the tail: five real rows past three newer
        // sidechains leave three from the second one on, where paging first would leave none.
        var past = await client.Sessions.GetSessionsAsync(Listing(project, 5, 2));

        Assert.AreEqual(5, listed.Count);
        Assert.IsTrue(listed.All(info => info.Summary.StartsWith("real", StringComparison.Ordinal)));
        Assert.AreEqual(3, past.Count);
        Assert.IsTrue(past.All(info => info.Summary.StartsWith("real", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task WithNoWorkingDirectoryEveryProjectOfTheConfigurationDirectoryIsListed()
    {
        using var client = new TemporaryClient();
        using var one = new TemporaryProject();
        using var two = new TemporaryProject();
        Built.Session(client.ConfigurationProject(one.Path), firstPrompt: "from one");
        Built.Session(client.ConfigurationProject(two.Path), firstPrompt: "from two");
        Built.Session(client.UserProject(one.Path), firstPrompt: "the user's");

        var listed = await client.Sessions.GetSessionsAsync();

        CollectionAssert.AreEquivalent(new[] { "from one", "from two" }, listed.Select(info => info.Summary).ToArray());
    }

    [TestMethod]
    public async Task AConfigurationDirectoryThatIsNotThereListsNothingAndIsNotMade()
    {
        using var client = new TemporaryClient();
        using var project = new TemporaryProject();
        Built.Session(client.UserProject(project.Path), firstPrompt: "the user's");

        var everywhere = await client.Sessions.GetSessionsAsync();
        var here = await client.Sessions.GetSessionsAsync(Listing(project));

        Assert.AreEqual(0, everywhere.Count);
        Assert.AreEqual(0, here.Count);
        Assert.IsFalse(Directory.Exists(client.ConfigurationDirectory));
    }

    [TestMethod]
    public async Task ANamedConfigurationDirectoryIsTheOneRead()
    {
        using var named = new TemporaryProject();
        using var client = new TemporaryClient(Path.Combine(named.Path, "home"));
        using var project = new TemporaryProject();
        var session = Built.Session(client.ConfigurationProject(project.Path), firstPrompt: "named");
        var defaults = Path.Combine(TestHostEnvironments.DefaultConfigurationDirectory(client.Root), "projects");
        Directory.CreateDirectory(defaults);

        var listed = await client.Sessions.GetSessionsAsync(Listing(project));

        Assert.AreEqual(Path.Combine(named.Path, "home"), client.ConfigurationDirectory);
        CollectionAssert.AreEqual(new[] { session }, listed.Select(info => info.SessionId).ToArray());
    }

    [TestMethod]
    public async Task InfoIsTheConfigurationDirectorysAndNullForASessionOnlyTheUserHas()
    {
        using var client = new TemporaryClient();
        using var project = new TemporaryProject();
        var session = Built.Session(client.ConfigurationProject(project.Path), firstPrompt: "hello", customTitle: "A Title");
        var users = Built.Session(client.UserProject(project.Path), firstPrompt: "the user's");

        var found = await client.Sessions.GetSessionAsync(session, project.Path);
        var anywhere = await client.Sessions.GetSessionAsync(session);
        var missing = await client.Sessions.GetSessionAsync(users, project.Path);
        var missingAnywhere = await client.Sessions.GetSessionAsync(users);

        Assert.AreEqual("A Title", found?.Summary);
        Assert.AreEqual("A Title", found?.CustomTitle);
        Assert.AreEqual(session, anywhere?.SessionId);
        Assert.IsNull(missing);
        Assert.IsNull(missingAnywhere);
    }

    [TestMethod]
    public async Task OneSessionIdInBothDirectoriesIsReadFromTheConfigurationDirectory()
    {
        using var client = new TemporaryClient();
        using var project = new TemporaryProject();
        var session = Guid.NewGuid().ToString();
        Seed(client.ConfigurationProject(project.Path), session, "home");
        Seed(client.UserProject(project.Path), session, "user");

        var info = await client.Sessions.GetSessionAsync(session, project.Path);
        var messages = await client.Sessions.GetMessagesAsync(session, new SessionQueryOptions { WorkingDirectory = project.Path });
        var agents = await client.Sessions.GetSubagentsAsync(session, project.Path);
        var agent = await client.Sessions.GetSubagentMessagesAsync(session, "home", new SessionQueryOptions { WorkingDirectory = project.Path });
        var usersAgent = await client.Sessions.GetSubagentMessagesAsync(session, "user", new SessionQueryOptions { WorkingDirectory = project.Path });

        Assert.AreEqual("home prompt", info?.Summary);
        CollectionAssert.AreEqual(new[] { "home-a", "home-b" }, messages.Select(message => message.Uuid).ToArray());
        CollectionAssert.AreEqual(new[] { "home" }, agents.ToArray());
        CollectionAssert.AreEqual(new[] { "home-x" }, agent.Select(message => message.Uuid).ToArray());
        Assert.AreEqual(0, usersAgent.Count);
    }

    // The binary names a session's project directory after realpath of the directory it ran in, the
    // physical path. A working directory handed in under a linked ancestor is that directory, and
    // every verb that names it finds the session.
    [TestMethod]
    public async Task AWorkingDirectoryUnderALinkedAncestorFindsTheSessionTheBinaryWroteUnderThePhysicalPath()
    {
        using var client = new TemporaryClient();
        var physical = Path.Combine(client.Root, "physical");
        var linked = Path.Combine(client.Root, "linked");
        Directory.CreateDirectory(Path.Combine(physical, "work"));
        Directory.CreateSymbolicLink(linked, physical);
        var work = Path.Combine(linked, "work");
        var written = Path.Combine(
            client.ConfigurationDirectory,
            "projects",
            TranscriptPaths.Sanitize(Path.Combine(TranscriptPaths.Canonicalize(client.Root), "physical", "work")));
        Directory.CreateDirectory(written);
        var session = Guid.NewGuid().ToString();
        Seed(written, session, "home");

        var listed = await client.Sessions.GetSessionsAsync(new SessionQueryOptions { WorkingDirectory = work });
        var info = await client.Sessions.GetSessionAsync(session, work);
        var messages = await client.Sessions.GetMessagesAsync(session, new SessionQueryOptions { WorkingDirectory = work });
        var agents = await client.Sessions.GetSubagentsAsync(session, work);
        await client.Sessions.TagSessionAsync(session, "found", work);

        CollectionAssert.AreEqual(new[] { session }, listed.Select(row => row.SessionId).ToArray());
        Assert.AreEqual("home prompt", info?.Summary);
        CollectionAssert.AreEqual(new[] { "home-a", "home-b" }, messages.Select(message => message.Uuid).ToArray());
        CollectionAssert.AreEqual(new[] { "home" }, agents.ToArray());
        Assert.AreEqual("found", (await client.Sessions.GetSessionAsync(session, work))?.Tag);
    }

    private static SessionQueryOptions Listing(TemporaryProject project, int? limit = null, int offset = 0) =>
        new() { WorkingDirectory = project.Path, Limit = limit, Offset = offset };

    // A two-turn session whose uuids, prompt and one subagent are named for the directory it is in.
    private static void Seed(string projectDirectory, string session, string side)
    {
        Built.Transcript(
            projectDirectory,
            session,
            null,
            Message("user", side + "-a", null, side + " prompt"),
            Message("assistant", side + "-b", side + "-a", side + " answer"));
        Built.SubagentTranscript(
            projectDirectory,
            session,
            side,
            null,
            Message("user", side + "-x", null, side + " task"));
    }

    private static JsonObject Message(string type, string uuid, string? parent, string text) => new()
    {
        ["type"] = type,
        ["uuid"] = uuid,
        ["parentUuid"] = parent,
        ["message"] = new JsonObject { ["role"] = type, ["content"] = text },
    };
}
