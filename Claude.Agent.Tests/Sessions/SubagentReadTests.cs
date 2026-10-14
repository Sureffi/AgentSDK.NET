using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace Claude.Agent.Tests.Sessions;

[TestClass]
public sealed class SubagentReadTests
{
    private static readonly SessionQueryOptions All = new();

    [TestMethod]
    public async Task TheFourRecordedSessionsWithSubagentsEachListTheirOne()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["17a5a07e-50c8-46aa-a329-da0fa774c768"] = "af89b39897730c636",
            ["24ab0d44-a6b5-40c0-98fc-cc43b8ff3c9f"] = "ab1f0f48d03bd004b",
            ["b0bf1d40-2f2c-4f0c-a43d-6dfc8e992c24"] = "a4a70311520ab724b",
            ["dd69d1c6-e13f-4d37-8e72-8684030b744f"] = "a17ef52e7870878c6",
        };

        var reader = Built.Reader(Transcripts.Layout());
        foreach (var session in Transcripts.SessionIds())
        {
            var listed = await reader.ListSubagentsAsync(session, Transcripts.Cwd, CancellationToken.None);

            Assert.IsTrue(listed.IsOk, session);
            CollectionAssert.AreEqual(
                expected.TryGetValue(session, out var agent) ? new[] { agent } : [],
                listed.Value.Select(id => id).ToArray(),
                session);
        }
    }

    [TestMethod]
    public async Task ARecordedSubagentReadsBackWithTheToolUseThatSpawnedIt()
    {
        var expected = new (string Session, string Agent, int Count, string ToolUse, string FirstUuid)[]
        {
            ("17a5a07e-50c8-46aa-a329-da0fa774c768", "af89b39897730c636", 2, "toolu_01MDDxkGsfGUwziMLZVNkC9Q", "b9af4243-7566-4f01-9dda-d7aef8e3a428"),
            ("24ab0d44-a6b5-40c0-98fc-cc43b8ff3c9f", "ab1f0f48d03bd004b", 9, "toolu_011eCjwtxfUdMgFDisp4nrQX", "76a97dcc-186b-4241-a354-93a15432e158"),
            ("b0bf1d40-2f2c-4f0c-a43d-6dfc8e992c24", "a4a70311520ab724b", 2, "toolu_01GKdmeSSK7CmFuDtTJ9kq9U", "1e8de86b-57c7-45c5-98ff-d30c75cea15c"),
            ("dd69d1c6-e13f-4d37-8e72-8684030b744f", "a17ef52e7870878c6", 6, "toolu_012i81uaZWz8JLN9GXeARHhe", "39b0e60a-5c88-4967-bd00-3ec66ff272b1"),
        };

        var reader = Built.Reader(Transcripts.Layout());
        foreach (var (session, agent, count, toolUse, first) in expected)
        {
            var read = await reader.GetSubagentMessagesAsync(session, agent, Query.All(Transcripts.Cwd), CancellationToken.None);

            Assert.AreEqual(count, read.Value.Length, session);
            Assert.AreEqual(first, read.Value[0].Uuid, session);
            Assert.AreEqual("user", read.Value[0].Role, session);
            // Every line of a subagent transcript carries the parent session's id, and both parent
            // ids are stamped on every message from the one sidecar.
            Assert.IsTrue(read.Value.All(message => message.SessionId == session), session);
            Assert.IsTrue(read.Value.All(message => message.ParentToolUseId == toolUse), session);
            Assert.IsTrue(read.Value.All(message => message.ParentAgentId is null), session);
        }
    }

    [TestMethod]
    public async Task ARecordedSubagentChainPages()
    {
        var read = await Built.Reader(Transcripts.Layout()).GetSubagentMessagesAsync("24ab0d44-a6b5-40c0-98fc-cc43b8ff3c9f", "ab1f0f48d03bd004b", Query.Page(3, 2, Transcripts.Cwd), CancellationToken.None);

        Assert.AreEqual(3, read.Value.Length);
    }

    [TestMethod]
    public async Task AnIdThatIsNotAUuidIsEmptyBeforeAnythingIsRead()
    {
        var reader = Built.Reader(new UntouchableLayout());
        var session = "nope";

        var listed = await reader.ListSubagentsAsync(session, null, CancellationToken.None);
        var read = await reader.GetSubagentMessagesAsync(session, "a1", Query.All(), CancellationToken.None);

        Assert.IsTrue(listed.IsOk);
        Assert.IsTrue(listed.Value.IsEmpty);
        Assert.IsTrue(read.Value.IsEmpty);
    }

    [TestMethod]
    public void AnEmptyAgentIdDoesNotConstruct() =>
        // The "empty agent id returns nothing before any I/O" guard, kept by the type instead.
        Assert.IsFalse(Ids.IsName(string.Empty));

    [TestMethod]
    public async Task AnUnknownSessionListsNoSubagents()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        Built.ProjectDirectory(config, project.Path);

        var listed = await Built.Reader(config).ListSubagentsAsync(
            "00000000-0000-0000-0000-000000000000",
            project.Path,
            CancellationToken.None);

        Assert.IsTrue(listed.Value.IsEmpty);
    }

    [TestMethod]
    public async Task ASessionWithNoSubagentsDirectoryListsNone()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var session = Built.Session(Built.ProjectDirectory(config, project.Path));

        var listed = await Built.Reader(config).ListSubagentsAsync(
            session,
            project.Path,
            CancellationToken.None);

        Assert.IsTrue(listed.Value.IsEmpty);
    }

    [TestMethod]
    public async Task AnEmptySubagentsDirectoryListsNone()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        var session = Built.Session(directory);
        Directory.CreateDirectory(Path.Combine(directory, session, "subagents"));

        var listed = await Built.Reader(config).ListSubagentsAsync(
            session,
            project.Path,
            CancellationToken.None);

        Assert.IsTrue(listed.Value.IsEmpty);
    }

    [TestMethod]
    public async Task TwoSubagentsAreListedInOrdinalOrder()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        var session = Built.Session(directory);
        Built.SubagentTranscript(directory, session, "b2", null, User("x", null, "hi"));
        Built.SubagentTranscript(directory, session, "a1", null, User("y", null, "hi"));

        var listed = await Built.Reader(config).ListSubagentsAsync(
            session,
            project.Path,
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "a1", "b2" }, listed.Value.Select(id => id).ToArray());
    }

    [TestMethod]
    public async Task TheSidecarTheWrongPrefixAndTheMissingExtensionAreAllIgnored()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        var session = Built.Session(directory);
        var transcript = Built.SubagentTranscript(directory, session, "keep", null, User("x", null, "hi"));
        Built.Sidecar(transcript, """{"toolUseId":"toolu_1"}""");
        var subagents = Path.Combine(directory, session, "subagents");
        await File.WriteAllTextAsync(Path.Combine(subagents, "other.jsonl"), "{}\n", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(subagents, "agent-noext"), "{}\n", CancellationToken.None);

        var listed = await Built.Reader(config).ListSubagentsAsync(
            session,
            project.Path,
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "keep" }, listed.Value.Select(id => id).ToArray());
    }

    [TestMethod]
    public async Task ASubagentNestedUnderAWorkflowDirectoryIsFoundAndRead()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        var session = Built.Session(directory);
        var transcript = Built.SubagentTranscript(
            directory,
            session,
            "nested",
            "workflows/run-1",
            User("x", null, "hi"),
            Assistant("y", "x", "pong"));
        Built.Sidecar(transcript, """{"toolUseId":"toolu_nested"}""");

        var reader = Built.Reader(config);
        var listed = await reader.ListSubagentsAsync(
            session,
            project.Path,
            CancellationToken.None);
        var read = await reader.GetSubagentMessagesAsync(session, "nested", Query.All(project.Path), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "nested" }, listed.Value.Select(id => id).ToArray());
        Assert.AreEqual(2, read.Value.Length);
        Assert.AreEqual("toolu_nested", read.Value[0].ParentToolUseId);
        Assert.IsNull(read.Value[0].ParentAgentId);
    }

    [TestMethod]
    public async Task EveryProjectIsSearchedWhenNoDirectoryIsGiven()
    {
        using var config = Transcripts.Empty();
        Built.ProjectDirectory(config, "/some/empty/project");
        var directory = Built.ProjectDirectory(config, "/some/other/project");
        var session = Built.Session(directory);
        Built.SubagentTranscript(directory, session, "far", null, User("x", null, "hi"));

        var listed = await Built.Reader(config).ListSubagentsAsync(
            session,
            null,
            CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "far" }, listed.Value.Select(id => id).ToArray());
    }

    [TestMethod]
    public async Task AnAgentIdThatIsNotThereIsEmpty()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        var session = Built.Session(directory);
        Built.SubagentTranscript(directory, session, "present", null, User("x", null, "hi"));

        var read = await Built.Reader(config).GetSubagentMessagesAsync(session, "absent", Query.All(project.Path), CancellationToken.None);

        Assert.IsTrue(read.Value.IsEmpty);
    }

    [TestMethod]
    public async Task TheFourSidecarDegradationsAllLeaveTheParentIdsNullAndTheMessagesStanding()
    {
        var sidecars = new[]
        {
            null,
            "not json {",
            """{"agentType":"general-purpose"}""",
            """{"toolUseId":42,"parentAgentId":["x"]}""",
        };

        foreach (var sidecar in sidecars)
        {
            using var config = Transcripts.Empty();
            using var project = new TemporaryProject();
            var directory = Built.ProjectDirectory(config, project.Path);
            var session = Built.Session(directory);
            var transcript = Built.SubagentTranscript(directory, session, "a1", null, User("x", null, "hi"));
            if (sidecar is not null)
            {
                Built.Sidecar(transcript, sidecar);
            }

            var read = await Built.Reader(config).GetSubagentMessagesAsync(session, "a1", Query.All(project.Path), CancellationToken.None);

            Assert.AreEqual(1, read.Value.Length, sidecar ?? "absent");
            Assert.IsNull(read.Value[0].ParentToolUseId, sidecar ?? "absent");
            Assert.IsNull(read.Value[0].ParentAgentId, sidecar ?? "absent");
        }
    }

    [TestMethod]
    public async Task ASidecarThatIsADirectoryDegradesInsteadOfRaising()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        var session = Built.Session(directory);
        var transcript = Built.SubagentTranscript(directory, session, "a1", null, User("x", null, "hi"));
        Directory.CreateDirectory(transcript[..^".jsonl".Length] + ".meta.json");

        var read = await Built.Reader(config).GetSubagentMessagesAsync(session, "a1", Query.All(project.Path), CancellationToken.None);

        Assert.AreEqual(1, read.Value.Length);
        Assert.IsNull(read.Value[0].ParentToolUseId);
    }

    [TestMethod]
    public async Task BothParentIdsComeOffTheSidecarWhenBothAreStrings()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        var session = Built.Session(directory);
        var transcript = Built.SubagentTranscript(
            directory,
            session,
            "child",
            null,
            User("x", null, "hi"),
            Assistant("y", "x", "pong"));
        Built.Sidecar(transcript, """{"toolUseId":"toolu_9","parentAgentId":"parent1"}""");

        var read = await Built.Reader(config).GetSubagentMessagesAsync(session, "child", Query.All(project.Path), CancellationToken.None);

        Assert.IsTrue(read.Value.All(message => message.ParentToolUseId == "toolu_9"));
        Assert.IsTrue(read.Value.All(message => message.ParentAgentId == "parent1"));
    }

    // The sidecar is read as a transcript line is: half a surrogate pair escaped without its other
    // half in one of its strings reads as U+FFFD, and the parent ids beside it are read.
    [TestMethod]
    public async Task ASidecarCarryingHalfOfASurrogatePairStillNamesTheParents()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        var session = Built.Session(directory);
        var transcript = Built.SubagentTranscript(directory, session, "child", null, User("x", null, "hi"));
        Built.Sidecar(transcript, """{"agentType":"cut \ud83d","toolUseId":"toolu_9","parentAgentId":"parent1"}""");

        var read = await Built.Reader(config).GetSubagentMessagesAsync(session, "child", Query.All(project.Path), CancellationToken.None);

        Assert.AreEqual("toolu_9", read.Value.Single().ParentToolUseId);
        Assert.AreEqual("parent1", read.Value.Single().ParentAgentId);
    }

    [TestMethod]
    public async Task CorruptLinesInASubagentTranscriptAreSkipped()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        var session = Built.Session(directory);
        var transcript = Path.Combine(directory, session, "subagents");
        Directory.CreateDirectory(transcript);
        await File.WriteAllTextAsync(
            Path.Combine(transcript, "agent-a1.jsonl"),
            Built.Line(User("x", null, "hi")) + "\nnot json\n\n" + Built.Line(Assistant("y", "x", "pong")) + "\n",
            CancellationToken.None);

        var read = await Built.Reader(config).GetSubagentMessagesAsync(session, "a1", Query.All(project.Path), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "x", "y" }, read.Value.Select(message => message.Uuid).ToArray());
    }

    [TestMethod]
    public async Task AZeroByteAgentTranscriptIsEmpty()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        var session = Built.Session(directory);
        Built.SubagentTranscript(directory, session, "a1");

        var read = await Built.Reader(config).GetSubagentMessagesAsync(session, "a1", Query.All(project.Path), CancellationToken.None);

        Assert.IsTrue(read.Value.IsEmpty);
    }

    [TestMethod]
    public async Task ASubagentChainTakesTheLastMessageInFileOrderAsItsLeaf()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        var session = Built.Session(directory);
        Built.SubagentTranscript(
            directory,
            session,
            "a1",
            null,
            User("x", null, "hi"),
            Assistant("y", "x", "first answer"),
            Assistant("z", "x", "second answer"));

        var read = await Built.Reader(config).GetSubagentMessagesAsync(session, "a1", Query.All(project.Path), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "x", "z" }, read.Value.Select(message => message.Uuid).ToArray());
    }

    [TestMethod]
    public async Task AMetaLineInsideASubagentTranscriptIsKept()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        var session = Built.Session(directory);
        var meta = User("y", "x", "a meta line");
        meta["isMeta"] = true;
        meta["isSidechain"] = true;
        Built.SubagentTranscript(directory, session, "a1", null, User("x", null, "hi"), meta);

        var read = await Built.Reader(config).GetSubagentMessagesAsync(session, "a1", Query.All(project.Path), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "x", "y" }, read.Value.Select(message => message.Uuid).ToArray());
    }

    private static JsonObject User(string uuid, string? parent, string text) => Entry("user", uuid, parent, text);

    private static JsonObject Assistant(string uuid, string? parent, string text) =>
        Entry("assistant", uuid, parent, text);

    private static JsonObject Entry(string type, string uuid, string? parent, string text) => new()
    {
        ["type"] = type,
        ["uuid"] = uuid,
        ["parentUuid"] = parent,
        ["isSidechain"] = true,
        ["message"] = new JsonObject { ["role"] = type, ["content"] = text },
    };
}
