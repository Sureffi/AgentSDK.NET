using System.Text;

namespace Claude.Agent.Tests.Sessions;

[TestClass]
public sealed class TranscriptPathTests
{
    [TestMethod]
    public void SanitizeReplacesEveryNonAlphanumericWithAHyphen()
    {
        Assert.AreEqual("-Users-foo-my-project", TranscriptPaths.Sanitize("/Users/foo/my-project"));
        Assert.AreEqual("plugin-name-server", TranscriptPaths.Sanitize("plugin:name:server"));
    }

    [TestMethod]
    public void SanitizeTruncatesALongNameAndAppendsTheHashOfTheUnsanitizedPath()
    {
        var path = "/" + new string('a', 250);

        var sanitized = TranscriptPaths.Sanitize(path);

        Assert.AreEqual(201 + TranscriptPaths.NameHash(path).Length, sanitized.Length);
        Assert.AreEqual($"-{new string('a', 199)}-{TranscriptPaths.NameHash(path)}", sanitized);
    }

    [TestMethod]
    public void SanitizeLeavesANameOfExactly200CharactersWhole()
    {
        var name = new string('a', 200);

        Assert.AreEqual(name, TranscriptPaths.Sanitize(name));
    }

    // The binary's hash: h * 31 + each UTF-16 unit in 32 bits, the absolute value in base 36.
    [TestMethod]
    public void TheNameHashIsTheBinarys()
    {
        Assert.AreEqual("0", TranscriptPaths.NameHash(""));
        Assert.AreEqual("2p", TranscriptPaths.NameHash("a"));
        Assert.AreEqual("2e9", TranscriptPaths.NameHash("ab"));
    }

    [TestMethod]
    public void FindProjectDirectoryTakesTheExactNameAndOtherwiseNothing()
    {
        using var config = Transcripts.Copy();

        Assert.AreEqual(
            config.ProjectDirectory,
            TranscriptPaths.FindProjectDirectory(config.Projects, Transcripts.ProjectDirectoryName));
        Assert.IsNull(TranscriptPaths.FindProjectDirectory(config.Projects, "-nothing-here"));
    }

    // Past 200 characters a directory under the cut and a dash is found when the exact one is not
    // there; at exactly 200 the name was never cut and there is no scan.
    [TestMethod]
    public void FindProjectDirectoryFallsBackToTheTwoHundredCharacterPrefixOnlyPastThatLength()
    {
        using var config = Transcripts.Empty();
        var prefix = new string('a', TranscriptPaths.MaxSanitizedLength);
        var written = Path.Combine(config.Projects, prefix + "-otherhash");
        Directory.CreateDirectory(written);

        Assert.AreEqual(written, TranscriptPaths.FindProjectDirectory(config.Projects, prefix + "-thishash"));
        Assert.IsNull(TranscriptPaths.FindProjectDirectory(config.Projects, prefix));
    }

    // The one lookup, from both sides. Past 200 characters the prefix can match more than one
    // directory, and nothing but the transcripts in them says which the binary wrote; readdir order
    // would let the editor verbs walk into one and the reader load from the other, so both take the
    // ordinally first.
    [TestMethod]
    public void TwoDirectoriesUnderOnePrefixResolveToTheOrdinalFirstForTheLayoutAndTheWorkspaceWalk()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var deep = Path.Combine(project.Path, new string('a', 220));
        Directory.CreateDirectory(deep);

        var layout = config.Layout;
        var cwd = deep;
        var key = layout.ProjectFor(cwd);
        Assert.IsTrue(key.Length > TranscriptPaths.MaxSanitizedLength, key);

        // Created last so that a filesystem handing back creation order hands back the other one.
        var stem = Path.Combine(config.Projects, key[..TranscriptPaths.MaxSanitizedLength]);
        var first = stem + "-0000";
        Directory.CreateDirectory(stem + "-zzzz");
        Directory.CreateDirectory(first);

        var walk = new TranscriptWorkspace(new TranscriptDirectory(layout))
            .Directories(Guid.NewGuid().ToString(), cwd);

        Assert.AreEqual(first, layout.ProjectDirectory(key));
        Assert.IsTrue(walk.IsOk);
        Assert.AreEqual(first, walk.Value[0]);
    }

    [TestMethod]
    public void TheUuidPatternAcceptsBothCasesAndRejectsAnythingElse()
    {
        Assert.IsTrue(TranscriptPaths.IsUuid("e67351d1-f522-421e-a7f3-45bfeef09aa5"));
        Assert.IsTrue(TranscriptPaths.IsUuid("E67351D1-F522-421E-A7F3-45BFEEF09AA5"));
        Assert.IsFalse(TranscriptPaths.IsUuid("not-a-uuid"));
        Assert.IsFalse(TranscriptPaths.IsUuid(""));
        Assert.IsFalse(TranscriptPaths.IsUuid("e67351d1-f522-421e-a7f3-45bfeef09aa"));
        Assert.IsFalse(TranscriptPaths.IsUuid(" e67351d1-f522-421e-a7f3-45bfeef09aa5"));
    }

    [TestMethod]
    public void TheProjectsDirectoryIsTheConfigurationDirectorysOwn()
    {
        var layout = new TranscriptLayout("/somewhere/else");

        Assert.AreEqual(Path.Combine("/somewhere/else", "projects"), layout.ProjectsDirectory);
    }

    [TestMethod]
    public void AnEmptyConfigurationDirectoryDoesNotConstruct() =>
        Assert.ThrowsExactly<ArgumentException>(() => new TranscriptLayout(""));

    [TestMethod]
    public void AProjectKeyIsTheSanitizedCanonicalPath()
    {
        var layout = Transcripts.Layout();

        Assert.AreEqual("-tmp-claude-agent-fixtures-cwd", layout.ProjectFor("/tmp/claude-agent-fixtures/cwd"));
    }

    [TestMethod]
    public void TheRecordedProjectDirectoryIsNamedByTheCwdItWasRecordedIn()
    {
        var layout = Transcripts.Layout();

        var directory = Path.Combine(layout.ProjectsDirectory, layout.ProjectFor(Transcripts.Cwd));

        Assert.IsTrue(Directory.Exists(directory), directory);
        Assert.AreEqual(23, Directory.GetFiles(directory, "*.jsonl").Length);
    }

    [TestMethod]
    public void AProjectKeyIsCanonicalisedBeforeItIsSanitized()
    {
        var layout = Transcripts.Layout();

        var key = layout.ProjectFor("/tmp/claude-agent-fixtures/../claude-agent-fixtures/cwd");

        Assert.AreEqual(Transcripts.ProjectDirectoryName, key);
    }

    // The binary names a project after realpath of the directory it runs in, which follows a link
    // wherever it stands: a directory under a linked ancestor is the physical directory's project,
    // whether the link's target is absolute or relative, and a ".." after the link is taken from
    // where the link led.
    [TestMethod]
    public void AProjectKeyFollowsASymbolicLinkAmongTheAncestorsAsRealpathDoes()
    {
        var root = Directory.CreateTempSubdirectory("claude-agent-links-").FullName;
        try
        {
            var physical = Path.Combine(TranscriptPaths.Canonicalize(root), "deep", "physical");
            Directory.CreateDirectory(Path.Combine(physical, "work"));
            Directory.CreateDirectory(Path.Combine(root, "deep", "sibling"));
            var absolute = Path.Combine(root, "absolute");
            var relative = Path.Combine(root, "relative");
            Directory.CreateSymbolicLink(absolute, physical);
            Directory.CreateSymbolicLink(relative, Path.Combine("deep", "physical"));
            var layout = Transcripts.Layout();

            Assert.AreEqual(TranscriptPaths.Sanitize(Path.Combine(physical, "work")), layout.ProjectFor(Path.Combine(absolute, "work")));
            Assert.AreEqual(TranscriptPaths.Sanitize(Path.Combine(physical, "work")), layout.ProjectFor(Path.Combine(relative, "work")));
            Assert.AreEqual(Path.Combine(physical, "work"), layout.CanonicalPath(Path.Combine(relative, "work")));
            Assert.AreEqual(
                Path.Combine(TranscriptPaths.Canonicalize(root), "deep", "sibling"),
                TranscriptPaths.Canonicalize(Path.Combine(absolute, "..", "sibling")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // Canonicalize follows at most MaxLinksFollowed links in a path. A path that needs more, a loop
    // among them, is taken as spelled and made full, as the binary takes its own directory when
    // realpath throws; so is one with a component that is not there.
    [TestMethod]
    public void APathThatNeedsMoreLinksThanTheBoundOrIsNotThereIsTakenAsSpelled()
    {
        var root = TranscriptPaths.Canonicalize(Directory.CreateTempSubdirectory("claude-agent-links-").FullName);
        try
        {
            var physical = Path.Combine(root, "physical");
            Directory.CreateDirectory(physical);
            var target = physical;
            for (var index = TranscriptPaths.MaxLinksFollowed; index >= 0; index--)
            {
                var link = Path.Combine(root, "link-" + index);
                Directory.CreateSymbolicLink(link, target);
                target = link;
            }

            Directory.CreateSymbolicLink(Path.Combine(root, "loop-a"), Path.Combine(root, "loop-b"));
            Directory.CreateSymbolicLink(Path.Combine(root, "loop-b"), Path.Combine(root, "loop-a"));

            Assert.AreEqual(physical, TranscriptPaths.Canonicalize(Path.Combine(root, "link-1")));
            Assert.AreEqual(Path.Combine(root, "link-0"), TranscriptPaths.Canonicalize(Path.Combine(root, "link-0")));
            Assert.AreEqual(Path.Combine(root, "loop-a", "work"), TranscriptPaths.Canonicalize(Path.Combine(root, "loop-a", "work")));
            Assert.AreEqual(Path.Combine(root, "missing"), TranscriptPaths.Canonicalize(Path.Combine(root, "link-1", "..", "missing")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // The layout NFC-normalises the config dir and the canonical cwd: this tree calls
    // string.Normalize in both places — but Directory.Build.props sets InvariantGlobalization, under
    // which Normalize is a silent no-op and IsNormalized always answers true. So the two spellings of
    // one directory still get two project keys here. The day the tree leaves invariant mode this test
    // fails and the two collapse into one.
    [TestMethod]
    public void NfcNormalisationIsANoOpWhileTheTreeBuildsInvariant()
    {
        var layout = Transcripts.Layout();
        const string Decomposed = "/tmp/cafe\u0301";

        Assert.AreEqual(
            Decomposed,
            Decomposed.Normalize(NormalizationForm.FormC),
            "string.Normalize did something: invariant globalization is off and the keys below collapse");
        Assert.AreNotEqual(
            layout.ProjectFor("/tmp/caf\u00e9"),
            layout.ProjectFor(Decomposed));
    }

    [TestMethod]
    public void WorktreePathsReadsEveryWorktreeLineAndNothingElse()
    {
        var layout = new TranscriptLayout(Transcripts.ConfigDirectory, git: _ => "worktree /tmp/main\nHEAD abc\nbranch refs/heads/main\n\nworktree /tmp/side\n");

        CollectionAssert.AreEqual(
            new[] { "/tmp/main", "/tmp/side" },
            layout.WorktreePaths(Transcripts.Cwd).ToArray());
    }

    // With no runner injected the layout reads the repository's own records: .git is the common
    // directory, and each <common>/worktrees/<name>/gitdir names a linked worktree's .git file.
    [TestMethod]
    public void WorktreePathsAreReadOffTheRepositorysOwnRecordsFromTheMainTreeAndFromALinkedOne()
    {
        var root = Directory.CreateTempSubdirectory("claude-agent-worktrees-").FullName;
        try
        {
            var main = Path.Combine(root, "main");
            var bee = Path.Combine(root, "bee");
            var record = Path.Combine(main, ".git", "worktrees", "bee");
            Directory.CreateDirectory(record);
            Directory.CreateDirectory(Path.Combine(bee, "src"));
            File.WriteAllText(Path.Combine(record, "gitdir"), Path.Combine(bee, ".git") + "\n");
            File.WriteAllText(Path.Combine(bee, ".git"), $"gitdir: {record}\n");

            var expected = new[] { TranscriptPaths.Normalize(main), TranscriptPaths.Normalize(bee) };
            CollectionAssert.AreEqual(expected, new TranscriptLayout(Transcripts.ConfigDirectory).WorktreePaths(main).ToArray());
            CollectionAssert.AreEqual(expected, new TranscriptLayout(Transcripts.ConfigDirectory).WorktreePaths(Path.Combine(bee, "src")).ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void WorktreePathsIsEmptyWhenTheProbeReportsNothing()
    {
        Assert.AreEqual(0, new TranscriptLayout(Transcripts.ConfigDirectory, git: _ => null).WorktreePaths(Transcripts.Cwd).Length);
        Assert.AreEqual(0, new TranscriptLayout(Transcripts.ConfigDirectory, git: _ => "").WorktreePaths(Transcripts.Cwd).Length);
        Assert.AreEqual(0, new TranscriptLayout(Transcripts.ConfigDirectory).WorktreePaths(Transcripts.Cwd).Length);
    }
}
