using System.Text;
using System.Text.Json.Nodes;

namespace Claude.Agent.Tests.Sessions;

// The configuration directory's transcripts as TranscriptDirectory reads, appends to and deletes them.
[TestClass]
public sealed class TranscriptDirectoryStoreTests
{
    private const string Recorded = "e67351d1-f522-421e-a7f3-45bfeef09aa5";

    private const string WithSubagents = "17a5a07e-50c8-46aa-a329-da0fa774c768";

    private static readonly byte[] Tag = Line(new JsonObject
    {
        ["type"] = "tag",
        ["tag"] = "release",
        ["sessionId"] = Recorded,
    });

    [TestMethod]
    public async Task AnAppendLandsAtTheEndAndTheLineIsByteExact()
    {
        using var config = Transcripts.Copy();
        var path = Path.Combine(config.ProjectDirectory, Recorded + ".jsonl");
        var before = await File.ReadAllBytesAsync(path, CancellationToken.None);

        Assert.IsTrue(TranscriptDirectory.Append(config.ProjectDirectory, Recorded, Tag));

        var after = await File.ReadAllBytesAsync(path, CancellationToken.None);
        CollectionAssert.AreEqual(before, after[..before.Length]);
        CollectionAssert.AreEqual(
            Encoding.UTF8.GetBytes("""{"type":"tag","tag":"release","sessionId":"e67351d1-f522-421e-a7f3-45bfeef09aa5"}""" + "\n"),
            after[before.Length..]);
    }

    [TestMethod]
    public async Task ThreeAppendsKeepTheirOrder()
    {
        using var config = Transcripts.Copy();

        foreach (var tag in new[] { "one", "two", "three" })
        {
            Assert.IsTrue(TranscriptDirectory.Append(
                config.ProjectDirectory,
                Recorded,
                Line(new JsonObject { ["type"] = "tag", ["tag"] = tag, ["sessionId"] = Recorded })));
        }

        var lines = await File.ReadAllLinesAsync(
            Path.Combine(config.ProjectDirectory, Recorded + ".jsonl"),
            CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "one", "two", "three" },
            lines[^3..].Select(line => SessionEntry.Parse(line).Value.Field("tag")).ToArray());
    }

    [TestMethod]
    public void AMissingTranscriptIsNotThereAndIsNotCreated()
    {
        using var config = Transcripts.Copy();
        var absent = "00000000-0000-0000-0000-000000000000";

        Assert.IsFalse(TranscriptDirectory.Append(config.ProjectDirectory, absent, Tag));

        Assert.IsFalse(File.Exists(Path.Combine(config.ProjectDirectory, absent + ".jsonl")));
        Assert.IsNull(TranscriptDirectory.Transcript(config.ProjectDirectory, absent));
    }

    [TestMethod]
    public async Task AZeroByteStubIsNotThereAndIsLeftByteIdentical()
    {
        using var config = Transcripts.Copy();
        var stub = "11111111-1111-1111-1111-111111111111";
        var path = Path.Combine(config.ProjectDirectory, stub + ".jsonl");
        await File.WriteAllBytesAsync(path, [], CancellationToken.None);

        Assert.IsFalse(TranscriptDirectory.Append(config.ProjectDirectory, stub, Tag));

        Assert.AreEqual(0, new FileInfo(path).Length);
        Assert.IsNull(TranscriptDirectory.Transcript(config.ProjectDirectory, stub));
    }

    // The open is the existence check for a write of any size, so an empty one answers the same way
    // a full one does.
    [TestMethod]
    public async Task AnEmptyAppendToATranscriptThatIsNotThereIsStillNotThere()
    {
        using var config = Transcripts.Copy();
        var absent = "00000000-0000-0000-0000-000000000000";
        var path = Path.Combine(config.ProjectDirectory, Recorded + ".jsonl");
        var before = await File.ReadAllBytesAsync(path, CancellationToken.None);

        Assert.IsFalse(TranscriptDirectory.Append(config.ProjectDirectory, absent, []));
        Assert.IsTrue(TranscriptDirectory.Append(config.ProjectDirectory, Recorded, []));

        Assert.IsFalse(File.Exists(Path.Combine(config.ProjectDirectory, absent + ".jsonl")));
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(path, CancellationToken.None));
    }

    [TestMethod]
    public void EntriesParsesEveryLineOfARecordedSession()
    {
        var entries = TranscriptDirectory.Entries(Transcripts.PathOf(Recorded));

        var lines = Transcripts.LinesOf(Recorded);
        Assert.AreEqual(lines.Length, entries.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(lines[i]), entries[i].Json), lines[i]);
        }
    }

    // A duplicate key is an ArgumentException out of JsonNode; the last occurrence is kept, and the
    // line reads.
    [TestMethod]
    public async Task ALineWithADuplicateKeyReadsInsteadOfThrowing()
    {
        using var config = Transcripts.Empty();
        Directory.CreateDirectory(config.ProjectDirectory);
        var session = "77777777-7777-7777-7777-777777777777";
        var path = Path.Combine(config.ProjectDirectory, session + ".jsonl");
        await File.WriteAllTextAsync(
            path,
            """{"type":"user","uuid":"a","type":"tag","message":{"role":"user","content":"kept"}}""" + "\n",
            CancellationToken.None);

        Assert.AreEqual("tag", TranscriptDirectory.Entries(path).Single().Type);
        Assert.AreEqual(session, TranscriptDirectory.Transcripts(config.ProjectDirectory, CancellationToken.None).Single().Session);
    }

    [TestMethod]
    public void ProjectsListsTheRecordedProjectAndNoneWithoutAProjectsDirectory()
    {
        CollectionAssert.AreEqual(
            new[] { Transcripts.ProjectDirectoryName },
            new TranscriptDirectory(Transcripts.Layout()).Projects().ToArray());

        var missing = new TranscriptDirectory(new TranscriptLayout(Path.Combine(Path.GetTempPath(), "claude-agent-tests-" + Guid.NewGuid().ToString("n"))));
        Assert.IsTrue(missing.Projects().IsEmpty);
        Assert.IsNull(missing.ProjectDirectories());
    }

    [TestMethod]
    public void TranscriptsListsTheTwentyThreeRecordedSessionsWithTheirWriteTimes()
    {
        var listed = TranscriptDirectory.Transcripts(Transcripts.Directory, CancellationToken.None).ToArray();

        Assert.AreEqual(23, listed.Length);
        CollectionAssert.AreEqual(Transcripts.SessionIds().ToArray(), listed.Select(entry => entry.Session).ToArray());
        foreach (var (session, file) in listed)
        {
            Assert.AreEqual(
                DateTimeOffset.FromUnixTimeMilliseconds(new DateTimeOffset(File.GetLastWriteTimeUtc(Transcripts.PathOf(session)), TimeSpan.Zero).ToUnixTimeMilliseconds()),
                TranscriptDirectory.Modified(file),
                session);
        }
    }

    [TestMethod]
    public void TranscriptsOfADirectoryThatIsNotThereIsEmpty() =>
        Assert.AreEqual(0, TranscriptDirectory.Transcripts(Path.Combine(Transcripts.ConfigDirectory, "projects", "-nothing-here"), CancellationToken.None).Count());

    [TestMethod]
    public async Task TranscriptsIgnoresANonUuidStemAndAZeroByteFile()
    {
        using var config = Transcripts.Copy();
        await File.WriteAllTextAsync(Path.Combine(config.ProjectDirectory, "README.md"), "not a transcript", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(config.ProjectDirectory, "notes.jsonl"), "{}\n", CancellationToken.None);
        await File.WriteAllBytesAsync(
            Path.Combine(config.ProjectDirectory, "22222222-2222-2222-2222-222222222222.jsonl"),
            [],
            CancellationToken.None);

        Assert.AreEqual(23, TranscriptDirectory.Transcripts(config.ProjectDirectory, CancellationToken.None).Count());
    }

    // Two files stamped the same millisecond come in ordinal order, so a page boundary does not move
    // with the filesystem's enumeration order.
    [TestMethod]
    public async Task SessionsStampedTheSameMillisecondListInOrdinalOrder()
    {
        using var config = Transcripts.Empty();
        Directory.CreateDirectory(config.ProjectDirectory);
        var stamp = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var sessions = new[] { "d", "b", "e", "a", "c" }
            .Select(letter => new string(letter[0], 8) + "-1111-1111-1111-111111111111")
            .ToArray();
        foreach (var session in sessions)
        {
            var path = Path.Combine(config.ProjectDirectory, session + ".jsonl");
            await File.WriteAllTextAsync(path, """{"type":"x"}""" + "\n", CancellationToken.None);
            File.SetLastWriteTimeUtc(path, stamp);
        }

        CollectionAssert.AreEqual(
            sessions.Order(StringComparer.Ordinal).ToArray(),
            TranscriptDirectory.Transcripts(config.ProjectDirectory, CancellationToken.None).Select(entry => entry.Session).ToArray());
    }

    // Three recorded files, 0851c866, 474e0e86 and dd69d1c6, run past 64 KiB, and their windows
    // still reach every field the whole file gives; every other is one window.
    [TestMethod]
    public void EveryRecordedSessionsInfoIsTheReadOfTheWholeFile()
    {
        var past = 0;
        foreach (var (session, file) in TranscriptDirectory.Transcripts(Transcripts.Directory, CancellationToken.None))
        {
            var entries = Transcripts.EntriesOf(session);
            var whole = TranscriptScan.Info(session, entries, entries, TranscriptDirectory.Modified(file), file.Length, Transcripts.Cwd);
            past += file.Length > TranscriptScan.WindowSize ? 1 : 0;

            var info = TranscriptDirectory.Info(session, file, Transcripts.Cwd);

            Assert.AreEqual(whole is null, info is null, session);
            if (whole is not null)
            {
                Assert.AreEqual(Shape(whole), Shape(info!), session);
            }
        }

        Assert.AreEqual(3, past);
    }

    [TestMethod]
    public async Task PastSixtyFourKibibytesTheHeadAndTheTailAreDifferentWindows()
    {
        using var config = Transcripts.Empty();
        Directory.CreateDirectory(config.ProjectDirectory);
        var session = "33333333-3333-3333-3333-333333333333";

        var lines = new List<string>
        {
            """{"type":"user","timestamp":"2024-01-01T00:00:00.000Z","cwd":"/head/only","message":{"role":"user","content":"the head prompt"}}""",
        };
        while (lines.Sum(line => line.Length + 1) < 3 * TranscriptScan.WindowSize)
        {
            lines.Add("""{"type":"attachment","filler":"FILL"}""".Replace("FILL", new string('f', 4000), StringComparison.Ordinal));
        }

        lines.Add("""{"type":"ai-title","aiTitle":"the tail title","sessionId":"33333333-3333-3333-3333-333333333333"}""");
        await File.WriteAllTextAsync(
            Path.Combine(config.ProjectDirectory, session + ".jsonl"),
            string.Join('\n', lines) + "\n",
            CancellationToken.None);

        var info = TranscriptDirectory.Info(session, TranscriptDirectory.Transcript(config.ProjectDirectory, session)!, null);

        Assert.IsNotNull(info);
        Assert.AreEqual("the tail title", info.CustomTitle);
        Assert.AreEqual("the head prompt", info.FirstPrompt);
        Assert.AreEqual("/head/only", info.WorkingDirectory);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeMilliseconds(1704067200000L), info.CreatedAt);
    }

    // A window reads the lines that lie wholly inside it. The head's last line here ends on the
    // window's last byte, its line feed one past it, and is read; the tail window opens inside a
    // custom-title line, which is dropped, so the title is the head's generated one.
    [TestMethod]
    public async Task ALineAWindowCutsIsDroppedAndOneEndingOnItsLastByteIsRead()
    {
        using var config = Transcripts.Empty();
        Directory.CreateDirectory(config.ProjectDirectory);
        var session = "66666666-6666-6666-6666-666666666666";
        var path = Path.Combine(config.ProjectDirectory, session + ".jsonl");

        var first = """{"type":"user","message":{"role":"user","content":"first"}}""" + "\n";
        var edge = """{"type":"ai-title","aiTitle":"edge title","pad":"PAD"}""";
        edge = edge.Replace("PAD", new string('p', TranscriptScan.WindowSize - first.Length - edge.Length + 3), StringComparison.Ordinal);
        var text = new StringBuilder(first).Append(edge).Append('\n');
        while (text.Length < 2 * TranscriptScan.WindowSize)
        {
            text.Append("""{"type":"attachment","filler":"FILL"}""".Replace("FILL", new string('f', 4000), StringComparison.Ordinal)).Append('\n');
        }

        text.Append("""{"type":"custom-title","customTitle":"cut title","pad":"PAD"}""".Replace("PAD", new string('p', TranscriptScan.WindowSize + 100), StringComparison.Ordinal)).Append('\n');
        text.Append("""{"type":"mode","mode":"normal"}""").Append('\n');
        await File.WriteAllTextAsync(path, text.ToString(), CancellationToken.None);

        Assert.AreEqual(TranscriptScan.WindowSize, first.Length + edge.Length, "the edge line must end on the head window's last byte");

        var info = TranscriptDirectory.Info(session, new FileInfo(path), null);

        Assert.IsNotNull(info);
        Assert.AreEqual("first", info.FirstPrompt);
        Assert.AreEqual("edge title", info.CustomTitle);
        Assert.IsNull(info.RenamedTitle);
    }

    // A character the tail window's first byte falls inside is in the line that window cuts, which is
    // dropped and never decoded.
    [TestMethod]
    public async Task ACharacterStraddlingTheTailBoundaryIsInADroppedLine()
    {
        using var config = Transcripts.Empty();
        Directory.CreateDirectory(config.ProjectDirectory);
        var session = "44444444-4444-4444-4444-444444444444";
        var path = Path.Combine(config.ProjectDirectory, session + ".jsonl");

        var head = Encoding.UTF8.GetBytes(
            """{"type":"user","timestamp":"2024-01-01T00:00:00.000Z","message":{"role":"user","content":"prompt"}}"""
            + "\n"
            + new string('f', 1000)
            + "\n");
        var dash = Encoding.UTF8.GetBytes("—");
        // The tail window opens at size - 65536. With 65534 bytes after the dash's three, it opens one
        // byte into the dash, so the decoder sees a continuation byte with no lead.
        var trailer = Encoding.UTF8.GetBytes(new string('t', 65533) + "\n");
        await File.WriteAllBytesAsync(path, [.. head, .. dash, .. trailer], CancellationToken.None);

        Assert.AreEqual(
            head.Length + 1,
            new FileInfo(path).Length - TranscriptScan.WindowSize,
            "the tail window must open one byte into the dash");

        Assert.AreEqual("prompt", TranscriptDirectory.Info(session, new FileInfo(path), null)?.FirstPrompt);
    }

    // A transcript this process may not read is listed all the same, with no summary.
    [TestMethod]
    public async Task ATranscriptThatCannotBeReadIsARowWithNoSummary()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("a file's mode is Unix's.");
            return;
        }

        using var config = Transcripts.Empty();
        Directory.CreateDirectory(config.ProjectDirectory);
        var session = "55555555-5555-5555-5555-555555555555";
        var path = Path.Combine(config.ProjectDirectory, session + ".jsonl");
        await File.WriteAllTextAsync(path, """{"type":"user","message":{"role":"user","content":"hidden"}}""" + "\n", CancellationToken.None);
        File.SetUnixFileMode(path, UnixFileMode.None);
        try
        {
            if (CanRead(path))
            {
                Assert.Inconclusive("this process reads a file whatever its mode.");
            }

            var info = TranscriptDirectory.Info(session, new FileInfo(path), null);

            Assert.IsNotNull(info);
            Assert.AreEqual(string.Empty, info.Summary);
            Assert.AreEqual(TranscriptDirectory.Modified(new FileInfo(path)), info.LastModified);
        }
        finally
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [TestMethod]
    public void SubagentsFindsTheFourRecordedSubagentTranscripts()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WithSubagents] = "subagents/agent-af89b39897730c636",
            ["24ab0d44-a6b5-40c0-98fc-cc43b8ff3c9f"] = "subagents/agent-ab1f0f48d03bd004b",
            ["b0bf1d40-2f2c-4f0c-a43d-6dfc8e992c24"] = "subagents/agent-a4a70311520ab724b",
            ["dd69d1c6-e13f-4d37-8e72-8684030b744f"] = "subagents/agent-a17ef52e7870878c6",
        };

        foreach (var (session, subagent) in expected)
        {
            CollectionAssert.AreEqual(new[] { subagent }, TranscriptDirectory.Subagents(Transcripts.Directory, session).ToArray(), session);
            Assert.IsTrue(File.Exists(TranscriptDirectory.SubagentTranscript(Transcripts.Directory, session, subagent)), session);
        }
    }

    [TestMethod]
    public void SubagentsIgnoresTheSidecarAndASessionWithoutSubagents()
    {
        Assert.AreEqual(0, TranscriptDirectory.Subagents(Transcripts.Directory, Recorded).Length);
        Assert.IsTrue(File.Exists(Path.Combine(
            Transcripts.Directory,
            WithSubagents,
            "subagents",
            "agent-af89b39897730c636.meta.json")));
    }

    [TestMethod]
    public async Task SubagentsWalksANestedSubagentDirectoryInOrdinalOrder()
    {
        using var config = Transcripts.Copy();
        var nested = Path.Combine(config.ProjectDirectory, Recorded, "subagents", "workflows", "run-1");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(nested, "agent-b.jsonl"), "{}\n", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(nested, "agent-a.jsonl"), "{}\n", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(nested, "other.jsonl"), "{}\n", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(nested, "agent-c.meta.json"), "{}", CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "subagents/workflows/run-1/agent-a", "subagents/workflows/run-1/agent-b" },
            TranscriptDirectory.Subagents(config.ProjectDirectory, Recorded).ToArray());
    }

    // The sidecar beside a subagent's transcript is where the tool use that started it is; every line
    // of the transcript itself is a sidechain line.
    [TestMethod]
    public void ASubagentsMetadataIsItsSidecar()
    {
        var path = TranscriptDirectory.SubagentTranscript(Transcripts.Directory, WithSubagents, "subagents/agent-af89b39897730c636");

        Assert.IsTrue(TranscriptDirectory.Entries(path).All(entry => entry.Flag("isSidechain")));
        Assert.AreEqual("toolu_01MDDxkGsfGUwziMLZVNkC9Q", SessionEntry.Text(TranscriptDirectory.Metadata(path)!, "toolUseId"));
        Assert.IsNull(TranscriptDirectory.Metadata(Transcripts.PathOf(Recorded)));
    }

    [TestMethod]
    public void DeleteRemovesTheTranscriptAndTheSiblingDirectory()
    {
        using var config = Transcripts.Copy();
        var transcript = Path.Combine(config.ProjectDirectory, WithSubagents + ".jsonl");
        var sibling = Path.Combine(config.ProjectDirectory, WithSubagents);

        TranscriptDirectory.Delete(config.ProjectDirectory, WithSubagents);

        Assert.IsFalse(File.Exists(transcript));
        Assert.IsFalse(Directory.Exists(sibling));
        Assert.IsTrue(File.Exists(Path.Combine(config.ProjectDirectory, Recorded + ".jsonl")));
    }

    // A key the sanitiser cut whose exact directory is not there finds one under its cut and a dash,
    // whatever hash that one carries.
    [TestMethod]
    public async Task AProjectKeyPastTwoHundredCharactersFindsADirectoryUnderItsCut()
    {
        using var config = Transcripts.Empty();
        var prefix = new string('a', TranscriptPaths.MaxSanitizedLength);
        var written = Path.Combine(config.Projects, prefix + "-otherhash");
        Directory.CreateDirectory(written);
        var session = "88888888-8888-8888-8888-888888888888";
        await File.WriteAllTextAsync(
            Path.Combine(written, session + ".jsonl"),
            """{"type":"x"}""" + "\n",
            CancellationToken.None);

        var directory = new TranscriptDirectory(config.Layout).ProjectDirectory(prefix + "-thishash");

        Assert.AreEqual(written, directory);
        Assert.AreEqual(session, TranscriptDirectory.Transcripts(directory!, CancellationToken.None).Single().Session);
    }

    private static bool CanRead(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    // SessionInfo is a class without value equality; the tuple is the comparison.
    private static (string, string, DateTimeOffset, long?, string?, string?, string?, string?, string?, DateTimeOffset?) Shape(SessionInfo info) =>
        (info.SessionId, info.Summary, info.LastModified, info.SizeBytes, info.CustomTitle, info.FirstPrompt, info.GitBranch, info.WorkingDirectory, info.Tag, info.CreatedAt);

    private static byte[] Line(JsonObject json) => Encoding.UTF8.GetBytes(new SessionEntry(json).ToCompactJson() + "\n");
}
