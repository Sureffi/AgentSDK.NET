using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace Claude.Agent.Tests.Sessions;

[TestClass]
public sealed class TranscriptReadTests
{
    private static readonly string Elsewhere = new("/tmp/claude-agent-fixtures/elsewhere");

    [TestMethod]
    public async Task TheRecordedDirectoryListsItsTwentyThreeSessions()
    {
        var listed = await Reader().ListAsync(new SessionQueryOptions { WorkingDirectory = Transcripts.Cwd }, CancellationToken.None);

        Assert.IsTrue(listed.IsOk);
        // The order is the write times', and those are whatever the checkout stamped on the recorded
        // files — the set and the descent are what the recording pins; the built listings below pin
        // the order against stamps a test set itself.
        CollectionAssert.AreEqual(
            Transcripts.SessionIds().ToArray(),
            listed.Value.Select(info => info.SessionId).Order(StringComparer.Ordinal).ToArray());
        Assert.IsTrue(listed.Value.Zip(listed.Value.Skip(1)).All(
            pair => pair.First.LastModified >= pair.Second.LastModified));
    }

    [TestMethod]
    public async Task EveryRecordedSessionListsTheSummaryTheBinaryLeftBehind()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["0851c866-1c1b-4661-b60b-b734c45296dd"] = "Pong",
            ["0f467c9c-7240-4183-8657-66c5dc288e2a"] = "Read notes.txt first line",
            ["17a5a07e-50c8-46aa-a329-da0fa774c768"] = "File operations and subagent test",
            ["24ab0d44-a6b5-40c0-98fc-cc43b8ff3c9f"] = "Background subagent launch test",
            ["258cdee4-0fd6-4b5e-a1fe-22601197b9a2"] = "Pong",
            ["3c542678-a922-4eab-82c3-a5bb3440454b"] = "Word pong letter count",
            ["40bf1e72-e8c2-4684-acd0-1b40fd59f3f1"] = "Say: one",
            ["41603023-fa17-4d91-a02b-10c70e1bca09"] = "Read notes.txt first line",
            ["474e0e86-4835-4b31-9f46-487185450a42"] = "kept title",
            ["4ca46a1b-0db6-4e0b-8cdb-0c98b59472c8"] = "Say: two",
            ["599be1d3-f7a1-4a55-bdd1-7bc714707377"] = "hello.txt file creation",
            ["5a264882-daef-4898-ba93-b67e298c4705"] = "Add 2 and 3",
            ["8a1c5516-dd2e-4951-878f-f9eeb6ed54ad"] = "Create denied.txt file",
            ["91afa0ce-07f3-4941-b411-d39b8f7fbaf3"] = "Create denied.txt file",
            ["a4ee9042-2e6c-4f32-8237-94b1609b06af"] = "Read notes.txt first line",
            ["a5258a12-f5f1-4f16-b9ff-a6048b59abb3"] = "Oulu founding year",
            ["b0bf1d40-2f2c-4f0c-a43d-6dfc8e992c24"] = "Background sleep process",
            ["b190f94e-916b-402a-9016-91266e02c461"] = "Say: two",
            ["d10f4163-5fab-46d6-948f-8fc4e19b3315"] = "Pong",
            ["d80ad26e-cac0-4fba-87bf-e2d2a7882a30"] = "Echo pong test",
            ["dd69d1c6-e13f-4d37-8e72-8684030b744f"] = "Agent tool with subagent reader",
            ["e67351d1-f522-421e-a7f3-45bfeef09aa5"] = "Pong",
            ["ffeb75c3-4ba5-4849-9688-85e6193729ca"] = "rewound.txt file creation",
        };

        var listed = await Reader().ListAsync(new SessionQueryOptions { WorkingDirectory = Transcripts.Cwd }, CancellationToken.None);

        foreach (var info in listed.Value)
        {
            Assert.AreEqual(expected[info.SessionId], info.Summary, info.SessionId);
        }
    }

    [TestMethod]
    public async Task ARecordedListingCarriesTheByteCountOfEachTranscript()
    {
        // The byte count is the length of the file the listing found.
        var listed = await Reader().ListAsync(new SessionQueryOptions { WorkingDirectory = Transcripts.Cwd }, CancellationToken.None);

        foreach (var info in listed.Value)
        {
            Assert.AreEqual(
                new FileInfo(Transcripts.PathOf(info.SessionId)).Length,
                info.SizeBytes,
                info.SessionId);
        }
    }

    [TestMethod]
    public async Task ARecordedSessionIsFoundByIdInItsOwnProject()
    {
        var info = await Reader().GetInfoAsync(
            "e67351d1-f522-421e-a7f3-45bfeef09aa5",
            Transcripts.Cwd,
            CancellationToken.None);

        Assert.IsNotNull(info.Value);
        Assert.AreEqual("Pong", info.Value.Summary);
        Assert.AreEqual("Pong", info.Value.CustomTitle);
        Assert.AreEqual("Reply with exactly one word: pong", info.Value.FirstPrompt);
        Assert.AreEqual("/tmp/claude-agent-fixtures/cwd", info.Value.WorkingDirectory);
        Assert.AreEqual("HEAD", info.Value.GitBranch);
        Assert.IsNull(info.Value.Tag);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeMilliseconds(1_789_084_120_759), info.Value.CreatedAt);
    }

    [TestMethod]
    public async Task ARecordedSessionIsAlsoFoundWithNoWorkingDirectoryAtAll()
    {
        var info = await Reader().GetInfoAsync(
            "e67351d1-f522-421e-a7f3-45bfeef09aa5",
            null,
            CancellationToken.None);

        Assert.IsNotNull(info.Value);
        Assert.AreEqual("Pong", info.Value.Summary);
    }

    [TestMethod]
    public async Task ASessionAskedForInAProjectThatDoesNotHoldItIsNull()
    {
        var info = await Reader().GetInfoAsync(
            "e67351d1-f522-421e-a7f3-45bfeef09aa5",
            Elsewhere,
            CancellationToken.None);

        Assert.IsNull(info.Value);
    }

    [TestMethod]
    public async Task AnIdThatIsNotAUuidIsNullBeforeAnythingIsRead()
    {
        var reader = Built.Reader(new UntouchableLayout());

        var info = await reader.GetInfoAsync("not-a-uuid", null, CancellationToken.None);

        Assert.IsTrue(info.IsOk);
        Assert.IsNull(info.Value);
    }

    [TestMethod]
    public async Task AnUnknownSessionIsNull()
    {
        var info = await Reader().GetInfoAsync(
            "00000000-0000-0000-0000-000000000000",
            Transcripts.Cwd,
            CancellationToken.None);

        Assert.IsNull(info.Value);
    }

    [TestMethod]
    public async Task AnEmptyProjectsDirectoryListsNothing()
    {
        using var config = Transcripts.Empty();

        var listed = await Built.Reader(config).ListAsync(new SessionQueryOptions(), CancellationToken.None);

        Assert.IsTrue(listed.IsOk);
        Assert.IsTrue(listed.Value.IsEmpty);
    }

    [TestMethod]
    public async Task AConfigDirectoryThatIsNotThereListsNothingAndIsNotAnError()
    {
        var layout = Transcripts.Layout(Path.Combine(Path.GetTempPath(), "claude-agent-tests-" + Guid.NewGuid().ToString("n")));

        var listed = await Built.Reader(layout).ListAsync(new SessionQueryOptions(), CancellationToken.None);

        Assert.IsTrue(listed.IsOk);
        Assert.IsTrue(listed.Value.IsEmpty);
    }

    [TestMethod]
    public async Task ABuiltSessionCarriesItsPromptBranchAndCwdThrough()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        var session = Built.Session(directory, firstPrompt: "What is 2+2?", gitBranch: "main", cwd: project.Path);

        var listed = await Built.Reader(config).ListAsync(Listing(project), CancellationToken.None);

        Assert.AreEqual(1, listed.Value.Length);
        var info = listed.Value[0];
        Assert.AreEqual(session, info.SessionId);
        Assert.AreEqual("What is 2+2?", info.FirstPrompt);
        Assert.AreEqual("What is 2+2?", info.Summary);
        Assert.AreEqual("main", info.GitBranch);
        Assert.AreEqual(project.Path, info.WorkingDirectory);
        Assert.IsNull(info.CustomTitle);
        Assert.IsTrue(info.LastModified > DateTimeOffset.UnixEpoch);
    }

    [TestMethod]
    public async Task ACustomTitleBeatsTheSummaryAndTheFirstPrompt()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        Built.Session(
            Built.ProjectDirectory(config, project.Path),
            firstPrompt: "original question",
            summary: "auto summary",
            customTitle: "My Custom Title");

        var listed = await Built.Reader(config).ListAsync(Listing(project), CancellationToken.None);

        Assert.AreEqual(1, listed.Value.Length);
        Assert.AreEqual("My Custom Title", listed.Value[0].Summary);
        Assert.AreEqual("My Custom Title", listed.Value[0].CustomTitle);
        Assert.AreEqual("original question", listed.Value[0].FirstPrompt);
    }

    [TestMethod]
    public async Task ASummaryBeatsTheFirstPromptWhenThereIsNoCustomTitle()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        Built.Session(
            Built.ProjectDirectory(config, project.Path),
            firstPrompt: "question",
            summary: "better summary");

        var listed = await Built.Reader(config).ListAsync(Listing(project), CancellationToken.None);

        Assert.AreEqual("better summary", listed.Value[0].Summary);
        Assert.IsNull(listed.Value[0].CustomTitle);
    }

    [TestMethod]
    public async Task SessionsSortNewestFirstAndTheirStampsAreTheFilesWriteTimes()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        var old = Built.Session(directory, firstPrompt: "old", mtimeEpochMs: 1_000_000);
        var newest = Built.Session(directory, firstPrompt: "new", mtimeEpochMs: 3_000_000);
        var middle = Built.Session(directory, firstPrompt: "mid", mtimeEpochMs: 2_000_000);

        var listed = await Built.Reader(config).ListAsync(Listing(project), CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { newest, middle, old },
            listed.Value.Select(info => info.SessionId).ToArray());
        CollectionAssert.AreEqual(
            new[] { 3_000_000L, 2_000_000L, 1_000_000L },
            listed.Value.Select(info => info.LastModified.ToUnixTimeMilliseconds()).ToArray());
    }

    [TestMethod]
    public async Task ALimitTakesTheNewestAndAnOffsetPagesPastThem()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        for (var i = 0; i < 5; i++)
        {
            Built.Session(directory, firstPrompt: $"prompt {i}", mtimeEpochMs: 1_000_000 + i);
        }

        var reader = Built.Reader(config);
        var first = await reader.ListAsync(Listing(project, limit: 2), CancellationToken.None);
        var second = await reader.ListAsync(Listing(project, limit: 2, offset: 2), CancellationToken.None);
        var past = await reader.ListAsync(Listing(project, offset: 100), CancellationToken.None);

        Assert.AreEqual(2, first.Value.Length);
        Assert.AreEqual(2, second.Value.Length);
        Assert.IsFalse(first.Value.Select(info => info.SessionId).Intersect(second.Value.Select(info => info.SessionId)).Any());
        Assert.IsTrue(first.Value[0].LastModified > second.Value[0].LastModified);
        Assert.IsTrue(past.Value.IsEmpty);
    }

    [TestMethod]
    public async Task ALimitOfZeroReturnsEverything()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        for (var i = 0; i < 3; i++)
        {
            Built.Session(directory, firstPrompt: $"p{i}");
        }

        var listed = await Built.Reader(config).ListAsync(Listing(project, limit: 0), CancellationToken.None);

        Assert.AreEqual(3, listed.Value.Length);
    }

    [TestMethod]
    public async Task ASessionWhoseFirstLineIsASidechainIsFilteredOut()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        Built.Session(directory, firstPrompt: "normal");
        Built.Session(directory, firstPrompt: "sidechain", sidechain: true);

        var listed = await Built.Reader(config).ListAsync(Listing(project), CancellationToken.None);

        Assert.AreEqual(1, listed.Value.Length);
        Assert.AreEqual("normal", listed.Value[0].FirstPrompt);
    }

    [TestMethod]
    public async Task ASessionWhoseOnlyUserEntryIsMetaIsDroppedWithNoPlaceholder()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        Built.Session(directory, firstPrompt: "ignored meta", metaOnly: true);
        Built.Session(directory, firstPrompt: "real content");

        var listed = await Built.Reader(config).ListAsync(Listing(project), CancellationToken.None);

        Assert.AreEqual(1, listed.Value.Length);
        Assert.AreEqual("real content", listed.Value[0].FirstPrompt);
    }

    [TestMethod]
    public async Task AFilenameThatIsNotAUuidIsIgnored()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        Built.Raw(directory, "not-a-uuid", Built.Line(new JsonObject
        {
            ["type"] = "user",
            ["message"] = new JsonObject { ["content"] = "x" },
        }) + "\n");
        Built.Session(directory, firstPrompt: "valid session");

        var listed = await Built.Reader(config).ListAsync(Listing(project), CancellationToken.None);

        Assert.AreEqual(1, listed.Value.Length);
        Assert.AreEqual("valid session", listed.Value[0].FirstPrompt);
    }

    [TestMethod]
    public async Task AFileThatIsNotAJsonlIsIgnored()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        await File.WriteAllTextAsync(Path.Combine(directory, "README.md"), "not a session", CancellationToken.None);
        Built.Session(directory, firstPrompt: "session");

        var listed = await Built.Reader(config).ListAsync(Listing(project), CancellationToken.None);

        Assert.AreEqual(1, listed.Value.Length);
    }

    [TestMethod]
    public async Task AZeroByteTranscriptIsDropped()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        Built.Raw(directory, Guid.NewGuid().ToString(), string.Empty);

        var listed = await Built.Reader(config).ListAsync(Listing(project), CancellationToken.None);

        Assert.IsTrue(listed.Value.IsEmpty);
    }

    [TestMethod]
    public async Task AWorkingDirectoryWithNoProjectDirectoryListsNothing()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();

        var listed = await Built.Reader(config).ListAsync(Listing(project), CancellationToken.None);

        Assert.IsTrue(listed.Value.IsEmpty);
    }

    [TestMethod]
    public async Task WithNoWorkingDirectoryEveryProjectIsScanned()
    {
        using var config = Transcripts.Empty();
        var one = Built.ProjectDirectory(config, "/some/path/one");
        var two = Built.ProjectDirectory(config, "/some/path/two");
        Built.Session(one, firstPrompt: "from one", mtimeEpochMs: 1_000_000);
        Built.Session(two, firstPrompt: "from two", mtimeEpochMs: 2_000_000);

        var listed = await Built.Reader(config).ListAsync(new SessionQueryOptions(), CancellationToken.None);

        Assert.AreEqual(2, listed.Value.Length);
        Assert.AreEqual("from two", listed.Value[0].FirstPrompt);
        Assert.AreEqual("from one", listed.Value[1].FirstPrompt);
    }

    [TestMethod]
    public async Task TheSameSessionInTwoProjectsKeepsTheNewer()
    {
        using var config = Transcripts.Empty();
        var one = Built.ProjectDirectory(config, "/path/one");
        var two = Built.ProjectDirectory(config, "/path/two");
        var shared = Guid.NewGuid().ToString();
        Built.Session(one, session: shared, firstPrompt: "older", mtimeEpochMs: 1_000_000);
        Built.Session(two, session: shared, firstPrompt: "newer", mtimeEpochMs: 2_000_000);

        var listed = await Built.Reader(config).ListAsync(new SessionQueryOptions(), CancellationToken.None);

        Assert.AreEqual(1, listed.Value.Length);
        Assert.AreEqual("newer", listed.Value[0].FirstPrompt);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeMilliseconds(2_000_000), listed.Value[0].LastModified);
    }

    [TestMethod]
    public async Task TheCwdFallsBackToTheCanonicalProjectPath()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        Built.Session(Built.ProjectDirectory(config, project.Path), firstPrompt: "no cwd field");

        var listed = await Built.Reader(config).ListAsync(Listing(project), CancellationToken.None);

        Assert.AreEqual(project.Path, listed.Value[0].WorkingDirectory);
    }

    [TestMethod]
    public async Task ATailGitBranchBeatsTheHeadOne()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        Built.Transcript(
            directory,
            Guid.NewGuid().ToString(),
            null,
            new JsonObject
            {
                ["type"] = "user",
                ["message"] = new JsonObject { ["content"] = "hello" },
                ["gitBranch"] = "old-branch",
            },
            new JsonObject { ["type"] = "summary", ["gitBranch"] = "new-branch" });

        var listed = await Built.Reader(config).ListAsync(Listing(project), CancellationToken.None);

        Assert.AreEqual("new-branch", listed.Value[0].GitBranch);
    }

    [TestMethod]
    public async Task ATagSurfacesThroughTheSameRead()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Built.ProjectDirectory(config, project.Path);
        var session = Guid.NewGuid().ToString();
        Built.Transcript(
            directory,
            session,
            null,
            new JsonObject
            {
                ["type"] = "user",
                ["message"] = new JsonObject { ["content"] = "hello" },
            },
            new JsonObject { ["type"] = "tag", ["tag"] = "release", ["sessionId"] = session });

        var info = await Built.Reader(config).GetInfoAsync(
            session,
            project.Path,
            CancellationToken.None);

        Assert.AreEqual("release", info.Value?.Tag);
    }

    [TestMethod]
    public async Task ASidechainSessionIsNullThroughGetInfoToo()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var session = Built.Session(Built.ProjectDirectory(config, project.Path), sidechain: true);

        var info = await Built.Reader(config).GetInfoAsync(
            session,
            project.Path,
            CancellationToken.None);

        Assert.IsTrue(info.IsOk);
        Assert.IsNull(info.Value);
    }

    [TestMethod]
    public async Task WithoutWorktreesOnlyTheNamedDirectoryIsScanned()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var canonical = new TranscriptLayout(Transcripts.ConfigDirectory).CanonicalPath(project.Path);
        Built.Session(Built.ProjectDirectory(config, project.Path), firstPrompt: "main session");
        Directory.CreateDirectory(Path.Combine(config.Projects, ProjectName(canonical + "-worktree")));
        Built.Session(
            Path.Combine(config.Projects, ProjectName(canonical + "-worktree")),
            firstPrompt: "worktree session");

        var listed = await Built.Reader(config).ListAsync(
            Listing(project, includeWorktrees: false),
            CancellationToken.None);

        Assert.AreEqual(1, listed.Value.Length);
        Assert.AreEqual("main session", listed.Value[0].FirstPrompt);
    }

    [TestMethod]
    public async Task OneWorktreePathIsNoWorktreeScanningAtAll()
    {
        // A repository with a single worktree short-circuits to the one directory,
        // so a sibling project named like a worktree of it is never reached.
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var canonical = new TranscriptLayout(Transcripts.ConfigDirectory).CanonicalPath(project.Path);
        Built.Session(Built.ProjectDirectory(config, project.Path), firstPrompt: "main session");
        Directory.CreateDirectory(Path.Combine(config.Projects, ProjectName(canonical + "-worktree")));
        Built.Session(
            Path.Combine(config.Projects, ProjectName(canonical + "-worktree")),
            firstPrompt: "worktree session");

        var listed = await Built.Reader(Git(config, canonical)).ListAsync(Listing(project), CancellationToken.None);

        Assert.AreEqual(1, listed.Value.Length);
        Assert.AreEqual("main session", listed.Value[0].FirstPrompt);
    }

    [TestMethod]
    public async Task TwoWorktreePathsScanBothProjectsAndTheWorktreePathBecomesTheCwdFallback()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var canonical = new TranscriptLayout(Transcripts.ConfigDirectory).CanonicalPath(project.Path);
        var second = canonical + "-wt";
        Built.Session(Built.ProjectDirectory(config, project.Path), firstPrompt: "main session", mtimeEpochMs: 1_000_000);
        Directory.CreateDirectory(Path.Combine(config.Projects, ProjectName(second)));
        Built.Session(
            Path.Combine(config.Projects, ProjectName(second)),
            firstPrompt: "worktree session",
            mtimeEpochMs: 2_000_000);

        var listed = await Built.Reader(Git(config, canonical, second)).ListAsync(
            Listing(project),
            CancellationToken.None);

        Assert.AreEqual(2, listed.Value.Length);
        Assert.AreEqual("worktree session", listed.Value[0].FirstPrompt);
        Assert.AreEqual(second, listed.Value[0].WorkingDirectory);
        Assert.AreEqual(canonical, listed.Value[1].WorkingDirectory);
    }

    [TestMethod]
    public async Task ASessionUnderASiblingWorktreeIsFoundByIdFromTheMainDirectory()
    {
        // A single-session read widens a named directory to its worktree paths,
        // the way the listing and every editor verb already do. Before this the listing found such a
        // session and GetSessionAsync then answered null on the same directory.
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        using var sibling = new TemporaryProject();
        var canonical = new TranscriptLayout(Transcripts.ConfigDirectory).CanonicalPath(project.Path);
        var other = new TranscriptLayout(Transcripts.ConfigDirectory).CanonicalPath(sibling.Path);
        Built.ProjectDirectory(config, project.Path);
        var session = Built.Session(Built.ProjectDirectory(config, sibling.Path), firstPrompt: "over there");

        var info = await Built.Reader(Git(config, canonical, other)).GetInfoAsync(
            session,
            project.Path,
            CancellationToken.None);

        Assert.IsNotNull(info.Value);
        Assert.AreEqual("over there", info.Value.FirstPrompt);
        Assert.AreEqual(other, info.Value.WorkingDirectory);
    }

    [TestMethod]
    public async Task ASessionUnderASiblingWorktreeAnswersItsMessagesFromTheMainDirectoryToo()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        using var sibling = new TemporaryProject();
        var canonical = new TranscriptLayout(Transcripts.ConfigDirectory).CanonicalPath(project.Path);
        var other = new TranscriptLayout(Transcripts.ConfigDirectory).CanonicalPath(sibling.Path);
        Built.ProjectDirectory(config, project.Path);
        var session = Guid.NewGuid().ToString();
        Built.Transcript(
            Built.ProjectDirectory(config, sibling.Path),
            session,
            null,
            Chained("user", "a", null, "first"),
            Chained("assistant", "b", "a", "second"));

        var read = await Built.Reader(Git(config, canonical, other)).GetMessagesAsync(
            session,
            Query.All(project.Path),
            CancellationToken.None);

        Assert.IsTrue(read.IsOk);
        CollectionAssert.AreEqual(
            new[] { "a", "b" },
            read.Value.Select(message => message.Uuid).ToArray());
    }

    [TestMethod]
    public async Task AProjectDirectoryNamedAfterAWorktreeAndADashIsScannedAsThatWorktree()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var canonical = new TranscriptLayout(Transcripts.ConfigDirectory).CanonicalPath(project.Path);
        var second = canonical + "-wt";
        Built.Session(Built.ProjectDirectory(config, project.Path), firstPrompt: "main session");
        Directory.CreateDirectory(Path.Combine(config.Projects, ProjectName(second)));
        Built.Session(Path.Combine(config.Projects, ProjectName(second)), firstPrompt: "worktree session");

        // As the binary lists a repository's sessions, a name shorter than 200 characters also takes
        // every directory under it and a dash, so /repo-wt-other is read as /repo-wt's.
        Directory.CreateDirectory(Path.Combine(config.Projects, ProjectName(second + "-other")));
        Built.Session(Path.Combine(config.Projects, ProjectName(second + "-other")), firstPrompt: "under the name");
        Directory.CreateDirectory(Path.Combine(config.Projects, ProjectName(canonical + "x")));
        Built.Session(Path.Combine(config.Projects, ProjectName(canonical + "x")), firstPrompt: "stranger");

        var listed = await Built.Reader(Git(config, canonical, second)).ListAsync(
            Listing(project),
            CancellationToken.None);

        CollectionAssert.AreEquivalent(
            new[] { "main session", "worktree session", "under the name" },
            listed.Value.Select(info => info.FirstPrompt).ToArray());
    }

    [TestMethod]
    public async Task TheSameSessionInTwoWorktreesIsListedOnce()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var canonical = new TranscriptLayout(Transcripts.ConfigDirectory).CanonicalPath(project.Path);
        var second = canonical + "-wt";
        var shared = Guid.NewGuid().ToString();
        Built.Session(
            Built.ProjectDirectory(config, project.Path),
            session: shared,
            firstPrompt: "older",
            mtimeEpochMs: 1_000_000);
        Directory.CreateDirectory(Path.Combine(config.Projects, ProjectName(second)));
        Built.Session(
            Path.Combine(config.Projects, ProjectName(second)),
            session: shared,
            firstPrompt: "newer",
            mtimeEpochMs: 2_000_000);

        var listed = await Built.Reader(Git(config, canonical, second)).ListAsync(
            Listing(project),
            CancellationToken.None);

        Assert.AreEqual(1, listed.Value.Length);
        Assert.AreEqual("newer", listed.Value[0].FirstPrompt);
    }

    [TestMethod]
    public void AGitRunnerThatFindsNothingReportsNoWorktrees()
    {
        var layout = new TranscriptLayout(Transcripts.ConfigDirectory, _ => null);

        Assert.IsTrue(layout.WorktreePaths("/tmp").IsEmpty);
    }

    [TestMethod]
    public void TheDefaultWorktreePathsAreReadOffTheRepositoryItself()
    {
        using var repository = new TemporaryProject();
        var main = Path.Combine(repository.Path, "main");
        var linked = Path.Combine(repository.Path, "linked");
        Git("init", "-q", main);
        File.WriteAllText(Path.Combine(main, "a.txt"), "a\n");
        Git("-C", main, "add", "a.txt");
        Git("-C", main, "-c", "user.email=t@t", "-c", "user.name=t", "commit", "-q", "-m", "one");
        Git("-C", main, "worktree", "add", "-q", linked);

        var layout = new TranscriptLayout(Transcripts.ConfigDirectory);
        var fromMain = layout.WorktreePaths(main);
        var fromLinked = layout.WorktreePaths(linked);

        var expected = new[] { layout.CanonicalPath(main), layout.CanonicalPath(linked) };
        CollectionAssert.AreEqual(expected, fromMain.ToArray());
        CollectionAssert.AreEqual(expected, fromLinked.ToArray());
    }

    [TestMethod]
    public void APathWithNoRepositoryAnywhereAboveItHasNoWorktrees()
    {
        // Named off the filesystem root and not off the temp directory: TMPDIR can be pointed inside a
        // checkout, and the walk up from there finds that checkout's worktrees and is right to.
        var plain = Path.Combine(
            Path.GetPathRoot(Path.GetTempPath()) ?? "/",
            "claude-agent-no-repository-" + Guid.NewGuid().ToString("n"));

        Assert.IsTrue(new TranscriptLayout(Transcripts.ConfigDirectory).WorktreePaths(plain).IsEmpty);
    }

    [TestMethod]
    public void AGitRunnerThatThrowsReportsNoWorktrees()
    {
        var layout = new TranscriptLayout(Transcripts.ConfigDirectory, _ => throw new InvalidOperationException("git blew up."));

        Assert.IsTrue(layout.WorktreePaths("/tmp").IsEmpty);
    }

    [TestMethod]
    public async Task AListingWhoseLayoutThrowsForWorktreesScansTheSingleDirectoryAnyway()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        Built.Session(Built.ProjectDirectory(config, project.Path), firstPrompt: "here");

        var listed = await Built.Reader(new BrokenWorktrees(config.Layout)).ListAsync(
            Listing(project),
            CancellationToken.None);

        Assert.IsTrue(listed.IsOk);
        Assert.AreEqual(1, listed.Value.Length);
        Assert.AreEqual("here", listed.Value[0].FirstPrompt);
    }

    [TestMethod]
    public async Task AWorktreePathThatIsNotAbsoluteStillNamesItsProjectDirectory()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var canonical = new TranscriptLayout(Transcripts.ConfigDirectory).CanonicalPath(project.Path);
        Built.Session(Built.ProjectDirectory(config, project.Path), firstPrompt: "here");
        // The sanitizer is what turns a path into a directory name, and it resolves nothing: the
        // relative "sibling/checkout" git printed names sibling-checkout, and is not dropped for
        // being relative.
        var sibling = Path.Combine(config.Projects, "sibling-checkout");
        Directory.CreateDirectory(sibling);
        Built.Session(sibling, firstPrompt: "over there");

        var listed = await Built.Reader(Git(config, canonical, "sibling/checkout")).ListAsync(
            Listing(project),
            CancellationToken.None);

        CollectionAssert.AreEquivalent(
            new[] { "here", "over there" },
            listed.Value.Select(info => info.FirstPrompt).ToArray());
    }

    [TestMethod]
    public async Task AConfigDirectoryThatIsNotThereHasNoInfoForASessionAndIsNotAnError()
    {
        var layout = Transcripts.Layout(Path.Combine(Path.GetTempPath(), "claude-agent-tests-" + Guid.NewGuid().ToString("n")));

        var info = await Built.Reader(layout).GetInfoAsync(
            "e67351d1-f522-421e-a7f3-45bfeef09aa5",
            null,
            CancellationToken.None);

        Assert.IsTrue(info.IsOk);
        Assert.IsNull(info.Value);
    }

    private static SessionQueryOptions Listing(TemporaryProject project, int? limit = null, int offset = 0, bool includeWorktrees = true) =>
        new() { WorkingDirectory = project.Path, Limit = limit, Offset = offset, IncludeWorktrees = includeWorktrees };

    private static string ProjectName(string path) => new TranscriptLayout(Transcripts.ConfigDirectory).ProjectFor(path);

    private static JsonObject Chained(string type, string uuid, string? parent, string text) => new()
    {
        ["type"] = type,
        ["uuid"] = uuid,
        ["parentUuid"] = parent,
        ["message"] = new JsonObject { ["role"] = type, ["content"] = text },
    };

    private static ITranscriptLayout Git(TemporaryConfig config, params string[] worktrees) =>
        Built.Layout(config, worktrees);

    // The test project may start git; the library may not, which is why the layout's default reads the
    // repository's own records instead of asking it.
    private static void Git(params string[] arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(start)!;
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.AreEqual(0, process.ExitCode, "git " + string.Join(' ', arguments) + ": " + error);
    }

    private static SessionReader Reader() => Built.Reader(Transcripts.Layout());

    // A layout that is not this tree's: the port says WorktreePaths answers empty for anything it
    // cannot read, and the reader holds the listing together when one does not.
    private sealed class BrokenWorktrees(ITranscriptLayout inner) : ITranscriptLayout
    {
        public string ProjectsDirectory => inner.ProjectsDirectory;

        public string ProjectFor(string cwd) => inner.ProjectFor(cwd);

        public string? ProjectDirectory(string project) => inner.ProjectDirectory(project);

        public string CanonicalPath(string cwd) => inner.CanonicalPath(cwd);

        public string? ProjectForSpelling(string path) => inner.ProjectForSpelling(path);

        public ImmutableArray<string> WorktreePaths(string cwd) =>
            throw new InvalidOperationException("this layout cannot say.");
    }
}
