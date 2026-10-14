using System.Text.Json.Nodes;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Sessions;

// One session copied from the user's own directory into the client's configuration directory, one
// way: the copy is byte for byte, lands in the project directory of the same name, and the user's
// directory is the same tree, byte for byte, after it as before.
[TestClass]
public sealed class SessionImportTests
{
    private const string Recorded = "17a5a07e-50c8-46aa-a329-da0fa774c768";
    private const string RecordedAgent = "af89b39897730c636";

    [TestMethod]
    public async Task ARecordedSessionIsCopiedByteForByteWithItsSubagentAndTheUsersDirectoryIsAsItWas()
    {
        using var client = new TemporaryClient();
        var users = UserCopyOfRecorded(client);
        File.WriteAllText(Path.Combine(client.UserDirectory, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(client.UserDirectory, "CLAUDE.md"), "the user's own");
        var before = TemporaryClient.Tree(client.UserDirectory);

        await client.Sessions.ImportSessionAsync(Recorded, Transcripts.Cwd);

        TemporaryClient.AssertSameTree(before, TemporaryClient.Tree(client.UserDirectory));
        var home = Path.Combine(client.ConfigurationDirectory, "projects", Transcripts.ProjectDirectoryName);
        TemporaryClient.AssertSameTree(TemporaryClient.Tree(users), TemporaryClient.Tree(home));
        CollectionAssert.AreEquivalent(
            new[] { "projects" },
            Directory.GetFileSystemEntries(client.ConfigurationDirectory).Select(Path.GetFileName).ToArray());
    }

    // A host a session of an SDK client started inherited that session's CLAUDE_CONFIG_DIR, the
    // default configuration directory, which is not the user's: the import reads from the
    // secure-storage directory the host inherited, else from ~/.claude.
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AHostThatInheritedTheConfigurationDirectoryImportsFromTheUsersDirectory(bool inheritedSecureStorage)
    {
        using var client = new TemporaryClient(inherited: root =>
        {
            var variables = new Dictionary<string, string>
            {
                ["CLAUDE_CONFIG_DIR"] = TestHostEnvironments.DefaultConfigurationDirectory(root),
            };
            if (inheritedSecureStorage)
            {
                variables["CLAUDE_SECURESTORAGE_CONFIG_DIR"] = Path.Combine(root, "storage");
            }

            return variables;
        });
        var user = inheritedSecureStorage ? Path.Combine(client.Root, "storage") : TestHostEnvironments.UserDirectory(client.Root);
        var users = Path.Combine(user, "projects", Transcripts.ProjectDirectoryName);
        Directory.CreateDirectory(users);
        var session = Seed(users);

        await client.Sessions.ImportSessionAsync(session, Transcripts.Cwd);

        CollectionAssert.AreEqual(
            File.ReadAllBytes(Path.Combine(users, session + ".jsonl")),
            File.ReadAllBytes(Path.Combine(TestHostEnvironments.DefaultConfigurationDirectory(client.Root), "projects", Transcripts.ProjectDirectoryName, session + ".jsonl")));
    }

    [TestMethod]
    public async Task AnImportedSessionIsTheClientsToReadWithItsSubagent()
    {
        using var client = new TemporaryClient();
        UserCopyOfRecorded(client);

        await client.Sessions.ImportSessionAsync(Recorded, Transcripts.Cwd);

        Assert.AreEqual(Recorded, (await client.Sessions.GetSessionAsync(Recorded, Transcripts.Cwd))?.SessionId);
        CollectionAssert.AreEqual(new[] { RecordedAgent }, (await client.Sessions.GetSubagentsAsync(Recorded, Transcripts.Cwd)).ToArray());
        Assert.AreNotEqual(0, (await client.Sessions.GetMessagesAsync(Recorded, new SessionQueryOptions { WorkingDirectory = Transcripts.Cwd })).Count);
    }

    [TestMethod]
    public async Task WithSubagentsExcludedOnlyTheTranscriptIsCopied()
    {
        using var client = new TemporaryClient();
        var users = UserCopyOfRecorded(client);

        await client.Sessions.ImportSessionAsync(Recorded, Transcripts.Cwd, includeSubagents: false);

        var home = Path.Combine(client.ConfigurationDirectory, "projects", Transcripts.ProjectDirectoryName);
        CollectionAssert.AreEqual(new[] { Recorded + ".jsonl" }, TemporaryClient.Tree(home).Keys.ToArray());
        CollectionAssert.AreEqual(
            File.ReadAllBytes(Path.Combine(users, Recorded + ".jsonl")),
            File.ReadAllBytes(Path.Combine(home, Recorded + ".jsonl")));
    }

    [TestMethod]
    public async Task EveryFileUnderTheSubagentsDirectoryIsCopiedNestedOnesIncluded()
    {
        using var client = new TemporaryClient();
        using var project = new TemporaryProject();
        var users = client.UserProject(project.Path);
        var session = Seed(users);
        Built.SubagentTranscript(users, session, "a1", null, Entry(1));
        Built.Sidecar(Path.Combine(users, session, "subagents", "agent-a1.jsonl"), """{"agentType":"general-purpose"}""");
        Built.SubagentTranscript(users, session, "deep", Path.Combine("workflows", "run-1"), Entry(2));

        await client.Sessions.ImportSessionAsync(session, project.Path);

        TemporaryClient.AssertSameTree(TemporaryClient.Tree(users), TemporaryClient.Tree(client.ConfigurationProject(project.Path)));
    }

    // The project directory is the one the transcript was found in, never one the cwd would compose.
    [TestMethod]
    public async Task WithNoWorkingDirectoryTheSessionLandsInTheProjectDirectoryItWasFoundIn()
    {
        using var client = new TemporaryClient();
        var users = Path.Combine(client.UserDirectory, "projects", "-elsewhere");
        Directory.CreateDirectory(users);
        var session = Seed(users);

        await client.Sessions.ImportSessionAsync(session);

        CollectionAssert.AreEqual(
            File.ReadAllBytes(Path.Combine(users, session + ".jsonl")),
            File.ReadAllBytes(Path.Combine(client.ConfigurationDirectory, "projects", "-elsewhere", session + ".jsonl")));
    }

    [TestMethod]
    public async Task ASessionTheUserDoesNotHaveIsNotFoundAndNothingIsWritten()
    {
        using var client = new TemporaryClient();
        using var project = new TemporaryProject();
        var session = Seed(client.ConfigurationProject(project.Path));
        client.UserProject(project.Path);
        var before = TemporaryClient.Tree(client.ConfigurationDirectory);

        var refused = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => client.Sessions.ImportSessionAsync(session, project.Path));
        var anywhere = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => client.Sessions.ImportSessionAsync(Guid.NewGuid().ToString()));

        Assert.AreEqual("SessionNotFound", refused.ErrorCode);
        Assert.AreEqual("SessionNotFound", anywhere.ErrorCode);
        TemporaryClient.AssertSameTree(before, TemporaryClient.Tree(client.ConfigurationDirectory));
    }

    // A zero-byte transcript is "not here" on every path, the import's included.
    [TestMethod]
    public async Task AZeroByteTranscriptIsNotFoundRatherThanImportedEmpty()
    {
        using var client = new TemporaryClient();
        using var project = new TemporaryProject();
        var session = Guid.NewGuid().ToString();
        File.WriteAllBytes(Path.Combine(client.UserProject(project.Path), session + ".jsonl"), []);

        var refused = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => client.Sessions.ImportSessionAsync(session, project.Path));

        Assert.AreEqual("SessionNotFound", refused.ErrorCode);
        Assert.IsFalse(Directory.Exists(client.ConfigurationDirectory));
    }

    [TestMethod]
    public async Task ASessionAlreadyInTheConfigurationDirectoryIsRefusedAndNothingIsWritten()
    {
        using var client = new TemporaryClient();
        using var project = new TemporaryProject();
        var users = client.UserProject(project.Path);
        var session = Seed(users);
        Built.SubagentTranscript(users, session, "a1", null, Entry(1));
        File.WriteAllText(Path.Combine(client.ConfigurationProject(project.Path), session + ".jsonl"), "the client's own\n");
        var before = TemporaryClient.Tree(client.ConfigurationDirectory);

        var refused = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => client.Sessions.ImportSessionAsync(session, project.Path));

        Assert.AreEqual("SessionExists", refused.ErrorCode);
        TemporaryClient.AssertSameTree(before, TemporaryClient.Tree(client.ConfigurationDirectory));
    }

    // One subagent file already there refuses the whole copy: the transcript is not written beside it.
    [TestMethod]
    public async Task ASubagentFileAlreadyInTheConfigurationDirectoryRefusesTheWholeCopy()
    {
        using var client = new TemporaryClient();
        using var project = new TemporaryProject();
        var users = client.UserProject(project.Path);
        var session = Seed(users);
        Built.SubagentTranscript(users, session, "a1", null, Entry(1));
        Built.SubagentTranscript(users, session, "a2", null, Entry(2));
        Built.SubagentTranscript(client.ConfigurationProject(project.Path), session, "a2", null, Entry(3));
        var before = TemporaryClient.Tree(client.ConfigurationDirectory);

        var refused = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => client.Sessions.ImportSessionAsync(session, project.Path));

        Assert.AreEqual("SessionExists", refused.ErrorCode);
        TemporaryClient.AssertSameTree(before, TemporaryClient.Tree(client.ConfigurationDirectory));
    }

    // Cancelled part-way through the transcript, after the subagent was copied whole, the copy leaves
    // nothing it made: not the subagent's copy, not the part of the transcript it wrote, not one of the
    // directories down to them. Asked again, the import copies the session.
    [TestMethod]
    public async Task AnImportCancelledPartWayLeavesNothingItMadeAndARetryCopiesTheSession()
    {
        using var client = new TemporaryClient();
        using var project = new TemporaryProject();
        var users = client.UserProject(project.Path);
        var session = Seed(users);
        Built.SubagentTranscript(users, session, "a1", null, Entry(1));
        var transcript = Path.Combine(users, session + ".jsonl");
        var copy = Path.Combine(client.ConfigurationDirectory, "projects", Path.GetFileName(users), session + ".jsonl");
        var copyWasThere = false;
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await ImportSessionHandler.Handle(
            new ImportSession(session, project.Path, IncludeSubagents: true),
            new TranscriptWorkspace(new TranscriptDirectory(new TranscriptLayout(client.UserDirectory))),
            new TranscriptDirectory(new TranscriptLayout(client.ConfigurationDirectory)),
            path => path == transcript
                ? new CancelledOnSecondRead(File.OpenRead(path), cancellation, () => copyWasThere = File.Exists(copy))
                : File.OpenRead(path),
            cancellation.Token));

        Assert.IsTrue(copyWasThere, "the transcript's copy was not begun when the copy was cancelled");
        Assert.IsFalse(Directory.Exists(Path.Combine(client.Root, "profile", ".config")), "a directory the copy made is there");

        await client.Sessions.ImportSessionAsync(session, project.Path);

        TemporaryClient.AssertSameTree(TemporaryClient.Tree(users), TemporaryClient.Tree(client.ConfigurationProject(project.Path)));
    }

    // A directory the configuration directory already had stays, with what was in it.
    [TestMethod]
    public async Task AnImportCancelledPartWayLeavesTheConfigurationDirectoryAsItWas()
    {
        using var client = new TemporaryClient();
        using var project = new TemporaryProject();
        var users = client.UserProject(project.Path);
        var session = Seed(users);
        File.WriteAllText(Path.Combine(client.ConfigurationProject(project.Path), Guid.NewGuid() + ".jsonl"), "the client's own\n");
        var before = TemporaryClient.Tree(client.ConfigurationDirectory);
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await ImportSessionHandler.Handle(
            new ImportSession(session, project.Path, IncludeSubagents: true),
            new TranscriptWorkspace(new TranscriptDirectory(new TranscriptLayout(client.UserDirectory))),
            new TranscriptDirectory(new TranscriptLayout(client.ConfigurationDirectory)),
            path => new CancelledOnSecondRead(File.OpenRead(path), cancellation, () => { }),
            cancellation.Token));

        TemporaryClient.AssertSameTree(before, TemporaryClient.Tree(client.ConfigurationDirectory));
        Assert.IsTrue(Directory.Exists(client.ConfigurationProject(project.Path)));
    }

    [TestMethod]
    public async Task TheCopiesAreReadableByTheirOwnerAlone()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("a file's mode is Unix's.");
            return;
        }

        using var client = new TemporaryClient();
        using var project = new TemporaryProject();
        var users = client.UserProject(project.Path);
        var session = Seed(users);
        Built.SubagentTranscript(users, session, "a1", null, Entry(1));

        await client.Sessions.ImportSessionAsync(session, project.Path);

        var home = client.ConfigurationProject(project.Path);
        var directories = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        var files = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        Assert.AreEqual(directories, File.GetUnixFileMode(client.ConfigurationDirectory));
        Assert.AreEqual(directories, File.GetUnixFileMode(home));
        Assert.AreEqual(directories, File.GetUnixFileMode(Path.Combine(home, session, "subagents")));
        Assert.AreEqual(files, File.GetUnixFileMode(Path.Combine(home, session + ".jsonl")));
        Assert.AreEqual(files, File.GetUnixFileMode(Path.Combine(home, session, "subagents", "agent-a1.jsonl")));
    }

    [TestMethod]
    public async Task ASessionIdThatIsNotAUuidIsRefusedBeforeEitherDirectoryIsAsked()
    {
        var editor = new SessionEditor(
            new TranscriptDirectory(new UntouchableLayout()),
            new TranscriptDirectory(new UntouchableLayout()));

        var refused = await editor.ImportAsync("../../etc/passwd", Transcripts.Cwd, true, CancellationToken.None);

        Assert.IsInstanceOfType<ValidationError>(refused.Error);
    }

    // The recorded session's transcript and subagent, copied into the user's directory under the
    // project directory they were recorded in, which is returned.
    private static string UserCopyOfRecorded(TemporaryClient client)
    {
        var users = Path.Combine(client.UserDirectory, "projects", Transcripts.ProjectDirectoryName);
        var subagents = Path.Combine(users, Recorded, "subagents");
        Directory.CreateDirectory(subagents);
        File.Copy(Transcripts.PathOf(Recorded), Path.Combine(users, Recorded + ".jsonl"));
        foreach (var file in Directory.GetFiles(Path.Combine(Transcripts.Directory, Recorded, "subagents")))
        {
            File.Copy(file, Path.Combine(subagents, Path.GetFileName(file)));
        }

        return users;
    }

    private static string Seed(string projectDirectory)
    {
        var session = Guid.NewGuid().ToString();
        Built.Transcript(projectDirectory, session, null, Entry(0), Entry(1));
        return session;
    }

    private static JsonObject Entry(int index) => new()
    {
        ["type"] = "user",
        ["uuid"] = "u" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["timestamp"] = $"2026-01-01T00:00:{index:00}.000Z",
        ["message"] = new JsonObject { ["role"] = "user", ["content"] = "prompt " + index.ToString(System.Globalization.CultureInfo.InvariantCulture) },
    };

    // One of the user's files, sixteen bytes a read: the second read runs `cancelling`, cancels the
    // copy and throws, so the copy stops part-way through the file.
    private sealed class CancelledOnSecondRead(Stream inner, CancellationTokenSource cancellation, Action cancelling) : Stream
    {
        private int _reads;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (++_reads > 1)
            {
                cancelling();
                await cancellation.CancelAsync();
                cancellationToken.ThrowIfCancellationRequested();
            }

            return await inner.ReadAsync(buffer[..Math.Min(buffer.Length, 16)], cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
