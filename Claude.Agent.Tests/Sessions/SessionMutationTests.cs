using System.Collections.Immutable;
using System.ComponentModel;
using System.Text;
using System.Text.Json.Nodes;
using Claude.Agent.Tests.Replay;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Claude.Agent.Tests.Sessions;

// rename, tag and delete over a transcript directory, the registration that hands the session client
// out, and a session client that edits the client's configuration directory and leaves the user's
// own as it was. The append itself — at EOF, never creating, refusing a zero-byte stub — is the
// transcript directory's and is pinned in TranscriptDirectoryStoreTests; these are the verbs above
// it.
[TestClass]
public sealed class SessionMutationTests
{
    private const string Session = "c0000000-0000-4000-8000-000000000001";

    // A recorded session, whose lines carry the uuids a fork needs.
    private const string Recorded = "17a5a07e-50c8-46aa-a329-da0fa774c768";

    [TestMethod]
    public async Task RenameRefusesASessionIdThatIsNotAUuidAndWritesNothing()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, Transcripts.ProjectDirectoryName);
        var before = File.ReadAllText(PathOf(config, Transcripts.ProjectDirectoryName, session));

        var refused = await Editor(config).RenameAsync(
            "not-a-uuid", "title", Transcripts.Cwd, CancellationToken.None);

        Assert.IsInstanceOfType<ValidationError>(refused.Error);
        Assert.AreEqual(before, File.ReadAllText(PathOf(config, Transcripts.ProjectDirectoryName, session)));
    }

    [TestMethod]
    public async Task RenameRefusesATitleThatIsEmptyOrNothingButWhitespace()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, Transcripts.ProjectDirectoryName);
        var editor = Editor(config);

        foreach (var title in new[] { "", "   ", "\n\t", "\u001c", "\u001c\u001f", "\u200b\u2029" })
        {
            var refused = await editor.RenameAsync(session, title, Transcripts.Cwd, CancellationToken.None);

            Assert.IsInstanceOfType<ValidationError>(refused.Error, title);
        }
    }

    [TestMethod]
    public async Task RenameOfASessionThatIsNotInTheProjectDirectoryFailsWithThatScope()
    {
        using var config = Transcripts.Empty();
        Seed(config, Transcripts.ProjectDirectoryName);

        var refused = await Editor(config).RenameAsync(
            Session, "title", Transcripts.Cwd, CancellationToken.None);

        Assert.AreEqual(SessionSearchScope.ProjectDirectory, NotFound(refused).Scope);
    }

    [TestMethod]
    public async Task RenameWithNoProjectDirectoryForTheCwdIsNotFoundInThatProjectDirectory()
    {
        using var config = Transcripts.Empty();

        var refused = await Editor(config).RenameAsync(
            Session, "title", Transcripts.Cwd, CancellationToken.None);

        Assert.AreEqual(SessionSearchScope.ProjectDirectory, NotFound(refused).Scope);
    }

    [TestMethod]
    public async Task RenameWithNoProjectsDirectoryAtAllSaysSo()
    {
        var absent = Path.Combine(Path.GetTempPath(), "claude-agent-tests-" + Guid.NewGuid().ToString("n"));
        var editor = Editor(Transcripts.Layout(absent));

        var refused = await editor.RenameAsync(Session, "title", null, CancellationToken.None);

        Assert.AreEqual(SessionSearchScope.NoProjectsDirectory, NotFound(refused).Scope);
    }

    [TestMethod]
    public async Task RenameWithNoCwdAndNoMatchAnywhereNamesEveryProjectDirectory()
    {
        using var config = Transcripts.Empty();
        Seed(config, "-aaa-project");

        var refused = await Editor(config).RenameAsync(Session, "title", null, CancellationToken.None);

        Assert.AreEqual(SessionSearchScope.AnyProjectDirectory, NotFound(refused).Scope);
    }

    [TestMethod]
    public async Task RenameAppendsTheCustomTitleLineTheCliWrites()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, Transcripts.ProjectDirectoryName);

        var renamed = await Editor(config).RenameAsync(session, "Title", Transcripts.Cwd, CancellationToken.None);

        Assert.IsTrue(renamed.IsOk, Text(renamed));
        Assert.AreEqual(
            $$"""{"type":"custom-title","customTitle":"Title","sessionId":"{{session}}"}""",
            Last(config, Transcripts.ProjectDirectoryName, session));
    }

    [TestMethod]
    public async Task ATitleIsTrimmedBeforeItIsWritten()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, Transcripts.ProjectDirectoryName);

        await Editor(config).RenameAsync(session, "  Trimmed Title  ", Transcripts.Cwd, CancellationToken.None);

        Assert.AreEqual(
            "Trimmed Title",
            SessionEntry.Parse(Last(config, Transcripts.ProjectDirectoryName, session)).Value.Json["customTitle"]!
                .GetValue<string>());
    }

    // As the binary's /rename takes a name: trimmed, each run of controls, format characters and
    // line or paragraph separators made one space, cut at 200 code points and trimmed again. A tag is
    // trimmed of JavaScript's whitespace, which holds none of the C0 separators U+001C to U+001F.
    [TestMethod]
    public async Task ATitleTurnsEachRunOfInvisibleCharactersIntoASpaceAndATagKeepsTheC0Separators()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, Transcripts.ProjectDirectoryName);
        var editor = Editor(config);

        await editor.RenameAsync(session, "\u001ca\u0000\u200b\u2028b\u001f", Transcripts.Cwd, CancellationToken.None);
        await editor.TagAsync(session, "\u001db\u001e", Transcripts.Cwd, CancellationToken.None);

        var lines = File.ReadAllLines(PathOf(config, Transcripts.ProjectDirectoryName, session));
        Assert.AreEqual(
            $$"""{"type":"custom-title","customTitle":"a b","sessionId":"{{session}}"}""",
            lines[^2]);
        Assert.AreEqual($$"""{"type":"tag","tag":"\u001db\u001e","sessionId":"{{session}}"}""", lines[^1]);
    }

    // The cut counts code points, so a pair past the 200th unit is kept whole.
    [TestMethod]
    public async Task ATitleIsCutAtTwoHundredCodePoints()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, Transcripts.ProjectDirectoryName);

        await Editor(config).RenameAsync(
            session,
            string.Concat(Enumerable.Repeat("\U0001F600", 150)) + " " + new string('x', 60),
            Transcripts.Cwd,
            CancellationToken.None);

        Assert.AreEqual(
            string.Concat(Enumerable.Repeat("\U0001F600", 150)) + " " + new string('x', 49),
            SessionEntry.Parse(Last(config, Transcripts.ProjectDirectoryName, session)).Value.Json["customTitle"]!
                .GetValue<string>());
    }

    // A title or a tag carrying half a surrogate pair without its other half, as a caller's Substring
    // through an emoji leaves one, is written escaped, as JSON.stringify writes it, and reads back
    // over the title and the tag before it, the half as U+FFFD.
    [TestMethod]
    public async Task ATitleAndATagCarryingHalfOfASurrogatePairReadBackWithTheHalfAsAReplacementCharacter()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, Transcripts.ProjectDirectoryName);
        var editor = Editor(config);
        var cut = "ab\U0001F600"[..3];

        await editor.RenameAsync(session, "Old title", Transcripts.Cwd, CancellationToken.None);
        await editor.TagAsync(session, "old", Transcripts.Cwd, CancellationToken.None);
        await editor.RenameAsync(session, cut, Transcripts.Cwd, CancellationToken.None);
        await editor.TagAsync(session, cut, Transcripts.Cwd, CancellationToken.None);

        var lines = File.ReadAllLines(PathOf(config, Transcripts.ProjectDirectoryName, session));
        Assert.AreEqual($$"""{"type":"custom-title","customTitle":"ab\ud83d","sessionId":"{{session}}"}""", lines[^2]);
        Assert.AreEqual($$"""{"type":"tag","tag":"ab\ud83d","sessionId":"{{session}}"}""", lines[^1]);
        var listed = Listed(config).Single();
        var info = (await Built.Reader(config).GetInfoAsync(session, Transcripts.Cwd, CancellationToken.None)).Value;
        foreach (var read in new[] { listed, info })
        {
            Assert.AreEqual("ab\uFFFD", read?.CustomTitle);
            Assert.AreEqual("ab\uFFFD", read?.Summary);
            Assert.AreEqual("ab\uFFFD", read?.Tag);
        }
    }

    [TestMethod]
    public async Task TheLastOfThreeRenamesIsTheOneTheListingReports()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, Transcripts.ProjectDirectoryName);
        var editor = Editor(config);

        foreach (var title in new[] { "First Title", "Second Title", "Final Title" })
        {
            var renamed = await editor.RenameAsync(session, title, Transcripts.Cwd, CancellationToken.None);
            Assert.IsTrue(renamed.IsOk, Text(renamed));
        }

        var info = Listed(config).Single();

        Assert.AreEqual("Final Title", info?.CustomTitle);
        Assert.AreEqual("Final Title", info?.Summary);
    }

    [TestMethod]
    public async Task WithNoCwdTheSessionIsLookedForInEveryProjectDirectory()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, "-some-project");

        var renamed = await Editor(config).RenameAsync(session, "Found Without Cwd", null, CancellationToken.None);

        Assert.IsTrue(renamed.IsOk, Text(renamed));
        StringAssert.Contains(File.ReadAllText(PathOf(config, "-some-project", session)), "Found Without Cwd");
    }

    // The zero-byte stub is the "keep searching" signal: without it the walk would stop at an empty
    // file in one project directory while the session lives in another.
    [TestMethod]
    public async Task AZeroByteStubIsSteppedOverAndTheRealTranscriptElsewhereTakesTheLine()
    {
        using var config = Transcripts.Empty();
        var session = Session;
        Directory.CreateDirectory(Path.Combine(config.Projects, "-aaa-project"));
        File.WriteAllBytes(PathOf(config, "-aaa-project", session), []);
        Seed(config, "-zzz-project", session);

        var renamed = await Editor(config).RenameAsync(session, "New Title", null, CancellationToken.None);

        Assert.IsTrue(renamed.IsOk, Text(renamed));
        Assert.AreEqual(0, new FileInfo(PathOf(config, "-aaa-project", session)).Length);
        StringAssert.Contains(File.ReadAllText(PathOf(config, "-zzz-project", session)), """"customTitle":"New Title"""");
    }

    [TestMethod]
    public async Task TagRefusesASessionIdThatIsNotAUuid()
    {
        using var config = Transcripts.Empty();
        Seed(config, Transcripts.ProjectDirectoryName);

        var refused = await Editor(config).TagAsync(
            "not-a-uuid", "tag", Transcripts.Cwd, CancellationToken.None);

        Assert.IsInstanceOfType<ValidationError>(refused.Error);
    }

    [TestMethod]
    public async Task TagRefusesAnEmptyTagBecauseNullIsHowATagIsCleared()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, Transcripts.ProjectDirectoryName);
        var editor = Editor(config);

        foreach (var tag in new[] { "", "   ", "\u2028\ufeff" })
        {
            var refused = await editor.TagAsync(session, tag, Transcripts.Cwd, CancellationToken.None);

            Assert.IsInstanceOfType<ValidationError>(refused.Error, tag);
        }
    }

    [TestMethod]
    public async Task TagOfASessionThatIsNotThereIsNotFound()
    {
        using var config = Transcripts.Empty();
        Seed(config, Transcripts.ProjectDirectoryName);

        var refused = await Editor(config).TagAsync(
            Session, "tag", Transcripts.Cwd, CancellationToken.None);

        Assert.AreEqual(SessionSearchScope.ProjectDirectory, NotFound(refused).Scope);
    }

    [TestMethod]
    public async Task TagAppendsTheTagLineTheCliWrites()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, Transcripts.ProjectDirectoryName);

        var tagged = await Editor(config).TagAsync(session, "mytag", Transcripts.Cwd, CancellationToken.None);

        Assert.IsTrue(tagged.IsOk, Text(tagged));
        Assert.AreEqual(
            $$"""{"type":"tag","tag":"mytag","sessionId":"{{session}}"}""",
            Last(config, Transcripts.ProjectDirectoryName, session));
    }

    [TestMethod]
    public async Task ATagIsTrimmedBeforeItIsWritten()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, Transcripts.ProjectDirectoryName);

        await Editor(config).TagAsync(session, "  my-tag  ", Transcripts.Cwd, CancellationToken.None);

        Assert.AreEqual(
            $$"""{"type":"tag","tag":"my-tag","sessionId":"{{session}}"}""",
            Last(config, Transcripts.ProjectDirectoryName, session));
    }

    [TestMethod]
    public async Task ANullTagClearsItWithTheEmptyStringAndNeverWithNull()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, Transcripts.ProjectDirectoryName);
        var editor = Editor(config);

        await editor.TagAsync(session, "original-tag", Transcripts.Cwd, CancellationToken.None);
        await editor.TagAsync(session, null, Transcripts.Cwd, CancellationToken.None);

        Assert.AreEqual(
            $$"""{"type":"tag","tag":"","sessionId":"{{session}}"}""",
            Last(config, Transcripts.ProjectDirectoryName, session));
    }

    [TestMethod]
    public async Task ThreeTagsAllLandAndTheLastIsTheOneTheListingReports()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, Transcripts.ProjectDirectoryName);
        var editor = Editor(config);

        foreach (var tag in new[] { "first", "second", "third" })
        {
            await editor.TagAsync(session, tag, Transcripts.Cwd, CancellationToken.None);
        }

        var lines = File.ReadAllLines(PathOf(config, Transcripts.ProjectDirectoryName, session));

        Assert.AreEqual(3, lines.Count(line => SessionEntry.Parse(line).Value.Type == "tag"));
        Assert.AreEqual("third", Listed(config).Single().Tag);
    }

    [TestMethod]
    public async Task ATagIsUnicodeSanitisedBeforeItIsWritten()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, Transcripts.ProjectDirectoryName);

        await Editor(config).TagAsync(session, "clean\u200btag\ufeff", Transcripts.Cwd, CancellationToken.None);

        Assert.AreEqual(
            $$"""{"type":"tag","tag":"cleantag","sessionId":"{{session}}"}""",
            Last(config, Transcripts.ProjectDirectoryName, session));
    }

    [TestMethod]
    public async Task ATagOfNothingButInvisibleCharactersIsRefused()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, Transcripts.ProjectDirectoryName);

        var refused = await Editor(config).TagAsync(
            session, "\u200b\u200c\ufeff", Transcripts.Cwd, CancellationToken.None);

        Assert.IsInstanceOfType<ValidationError>(refused.Error);
    }

    [TestMethod]
    public void ACleanStringPassesThroughTheSanitiser()
    {
        Assert.AreEqual("hello", UnicodeText.Sanitize("hello"));
        Assert.AreEqual("tag-with-dashes_123", UnicodeText.Sanitize("tag-with-dashes_123"));
    }

    [TestMethod]
    public void TheSanitiserStripsZeroWidthSpacesJoinersAndNonJoiners()
    {
        Assert.AreEqual("ab", UnicodeText.Sanitize("a\u200bb"));
        Assert.AreEqual("ab", UnicodeText.Sanitize("a\u200cb"));
        Assert.AreEqual("ab", UnicodeText.Sanitize("a\u200db"));
    }

    [TestMethod]
    public void TheSanitiserStripsTheByteOrderMark() =>
        Assert.AreEqual("hello", UnicodeText.Sanitize("\ufeffhello"));

    [TestMethod]
    public void TheSanitiserStripsDirectionalMarksAndIsolates()
    {
        Assert.AreEqual("abc", UnicodeText.Sanitize("a\u202ab\u202cc"));
        Assert.AreEqual("abc", UnicodeText.Sanitize("a\u2066b\u2069c"));
    }

    [TestMethod]
    public void TheSanitiserStripsPrivateUseCharacters()
    {
        Assert.AreEqual("ab", UnicodeText.Sanitize("a\ue000b"));
        Assert.AreEqual("ab", UnicodeText.Sanitize("a\uf8ffb"));
    }

    // NFKC folds a fullwidth \uff21 to A. string.Normalize does that on
    // an ordinary host and nothing at all under InvariantGlobalization, which Directory.Build.props sets
    // — for this test host and the sample, not for the shipped library, which emits no runtimeconfig and
    // takes whatever mode its host is in. So a consumer gets the fold and this suite does not,
    // and the first assertion says the sanitiser routes through Normalize either way while the second
    // says which of the two modes is being looked at.
    [TestMethod]
    public void TheNfkcFoldFollowsTheHostAndThisTestHostIsInvariant()
    {
        var folds = string.Equals("\uff21".Normalize(NormalizationForm.FormKC), "A", StringComparison.Ordinal);

        Assert.AreEqual(folds ? "A" : "\uff21", UnicodeText.Sanitize("\uff21"));
        Assert.IsFalse(folds, "invariant globalization is off: the fold is live and UnicodeText's comment is stale");
    }

    // An unpaired surrogate is a code point of category Surrogate, which is in none of the three
    // stripped categories, so the sanitiser keeps it. Enumerating runes would have
    // rewritten it to U+FFFD.
    [TestMethod]
    public void TheSanitiserKeepsAnUnpairedSurrogate() =>
        Assert.AreEqual("a\ud800b", UnicodeText.Sanitize("a\ud800b"));

    // A private-use code point above the BMP is two surrogates to a char loop and neither half
    // carries the category the pair has.
    [TestMethod]
    public void TheSanitiserStripsAPrivateUseCodePointAboveTheBmp() =>
        Assert.AreEqual("ab", UnicodeText.Sanitize("a\udbc0\udc00b"));

    [TestMethod]
    public void TwentyZeroWidthsConvergeInsideTheTenPasses() =>
        Assert.AreEqual("ab", UnicodeText.Sanitize("a" + new string('\u200b', 20) + "b"));

    [TestMethod]
    public async Task DeleteRefusesASessionIdThatIsNotAUuid()
    {
        using var config = Transcripts.Empty();
        Seed(config, Transcripts.ProjectDirectoryName);

        var refused = await Editor(config).DeleteAsync(
            "not-a-uuid", Transcripts.Cwd, CancellationToken.None);

        Assert.IsInstanceOfType<ValidationError>(refused.Error);
    }

    // Nothing removed is not a delete: the verb says not found.
    [TestMethod]
    public async Task DeletingASessionThatIsNotThereIsNotFound()
    {
        using var config = Transcripts.Empty();
        Seed(config, Transcripts.ProjectDirectoryName);

        var refused = await Editor(config).DeleteAsync(Session, null, CancellationToken.None);

        Assert.AreEqual(SessionSearchScope.AnyProjectDirectory, NotFound(refused).Scope);
    }

    [TestMethod]
    public async Task DeleteRemovesTheTranscript()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, Transcripts.ProjectDirectoryName);

        var deleted = await Editor(config).DeleteAsync(session, Transcripts.Cwd, CancellationToken.None);

        Assert.IsTrue(deleted.IsOk, Text(deleted));
        Assert.IsFalse(File.Exists(PathOf(config, Transcripts.ProjectDirectoryName, session)));
    }

    [TestMethod]
    public async Task DeleteTakesTheSiblingSubagentDirectoryWithIt()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, Transcripts.ProjectDirectoryName);
        var subagents = Path.Combine(config.ProjectDirectory, session, "subagents");
        Directory.CreateDirectory(subagents);
        File.WriteAllText(Path.Combine(subagents, "agent-abc.jsonl"), "{}\n");

        var deleted = await Editor(config).DeleteAsync(session, Transcripts.Cwd, CancellationToken.None);

        Assert.IsTrue(deleted.IsOk, Text(deleted));
        Assert.IsFalse(Directory.Exists(Path.Combine(config.ProjectDirectory, session)));
    }

    [TestMethod]
    public async Task DeleteFindsTheSessionWithNoCwd()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, "-any-project");

        var deleted = await Editor(config).DeleteAsync(session, null, CancellationToken.None);

        Assert.IsTrue(deleted.IsOk, Text(deleted));
        Assert.IsFalse(File.Exists(PathOf(config, "-any-project", session)));
    }

    [TestMethod]
    public async Task ADeletedSessionIsGoneFromTheListing()
    {
        using var config = Transcripts.Empty();
        var session = Seed(config, Transcripts.ProjectDirectoryName);

        Assert.AreEqual(1, Listed(config).Length);
        await Editor(config).DeleteAsync(session, Transcripts.Cwd, CancellationToken.None);

        Assert.AreEqual(0, Listed(config).Length);
    }

    // The worktree probe is guarded on every candidate walk: a git that is missing or refuses
    // means no worktrees,
    // not a verb that throws instead of answering.
    [TestMethod]
    public async Task AWorktreeProbeThatThrowsLeavesEveryVerbAnswering()
    {
        using var home = Transcripts.Empty();
        using var user = Transcripts.Copy();
        var session = Recorded;
        var probed = 0;
        Func<string, string?> git = _ =>
        {
            probed++;
            throw new Win32Exception("git is not on PATH");
        };
        var editor = new SessionEditor(
            new TranscriptDirectory(new TranscriptLayout(home.Root, git)),
            new TranscriptDirectory(new TranscriptLayout(user.Root, git)));

        var imported = await editor.ImportAsync(session, Transcripts.Cwd, false, CancellationToken.None);
        var renamed = await editor.RenameAsync(session, "Title", Transcripts.Cwd, CancellationToken.None);
        var tagged = await editor.TagAsync(session, "tag", Transcripts.Cwd, CancellationToken.None);
        var forked = await editor.ForkAsync(session, null, null, Transcripts.Cwd, CancellationToken.None);
        var deleted = await editor.DeleteAsync(session, Transcripts.Cwd, CancellationToken.None);

        Assert.IsTrue(imported.IsOk, Text(imported));
        Assert.IsTrue(renamed.IsOk, Text(renamed));
        Assert.IsTrue(tagged.IsOk, Text(tagged));
        Assert.IsTrue(forked.IsOk, Text(forked));
        Assert.IsTrue(deleted.IsOk, Text(deleted));
        Assert.AreEqual(5, probed);
    }

    [TestMethod]
    public async Task AWorktreeProbeThatThrowsStillAnswersNotFoundForASessionThatIsNotThere()
    {
        using var config = Transcripts.Empty();
        Seed(config, Transcripts.ProjectDirectoryName);
        var editor = Editor(new TranscriptLayout(config.Root, _ => throw new Win32Exception("git is not on PATH")));

        var refused = await editor.RenameAsync(
            Session, "Title", Transcripts.Cwd, CancellationToken.None);

        Assert.AreEqual(SessionSearchScope.ProjectDirectory, NotFound(refused).Scope);
    }

    [TestMethod]
    public async Task TheSessionClientEditsTheConfigurationDirectoryAndLeavesTheUsersAsItWas()
    {
        using var client = new TemporaryClient();
        using var project = new TemporaryProject();
        var home = client.ConfigurationProject(project.Path);
        var users = client.UserProject(project.Path);
        var session = Guid.NewGuid().ToString();
        File.WriteAllText(Path.Combine(home, session + ".jsonl"), Transcript("home"));
        File.WriteAllText(Path.Combine(users, session + ".jsonl"), Transcript("user"));
        Directory.CreateDirectory(Path.Combine(users, session, "subagents"));
        File.WriteAllText(Path.Combine(users, session, "subagents", "agent-a1.jsonl"), Transcript("agent"));
        var before = TemporaryClient.Tree(client.UserDirectory);

        await client.Sessions.RenameSessionAsync(session, "Renamed", project.Path);
        await client.Sessions.TagSessionAsync(session, "tagged");
        var edited = File.ReadAllLines(Path.Combine(home, session + ".jsonl"));
        var fork = await client.Sessions.ForkSessionAsync(session, workingDirectory: project.Path);
        var forked = File.ReadAllText(Path.Combine(home, fork + ".jsonl"));
        await client.Sessions.DeleteSessionAsync(session, project.Path);

        TemporaryClient.AssertSameTree(before, TemporaryClient.Tree(client.UserDirectory));
        Assert.AreEqual($$"""{"type":"custom-title","customTitle":"Renamed","sessionId":"{{session}}"}""", edited[^2]);
        Assert.AreEqual($$"""{"type":"tag","tag":"tagged","sessionId":"{{session}}"}""", edited[^1]);
        StringAssert.Contains(forked, "home prompt");
        Assert.IsFalse(forked.Contains("user prompt", StringComparison.Ordinal));
        Assert.IsFalse(File.Exists(Path.Combine(home, session + ".jsonl")));
        Assert.IsFalse(File.Exists(Path.Combine(users, fork + ".jsonl")));
    }

    [TestMethod]
    public async Task TheSessionClientCannotEditASessionOnlyTheUserHas()
    {
        using var client = new TemporaryClient();
        using var project = new TemporaryProject();
        var users = client.UserProject(project.Path);
        var session = Guid.NewGuid().ToString();
        File.WriteAllText(Path.Combine(users, session + ".jsonl"), Transcript("user"));
        var before = TemporaryClient.Tree(client.UserDirectory);

        var renamed = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => client.Sessions.RenameSessionAsync(session, "Renamed", project.Path));
        var tagged = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => client.Sessions.TagSessionAsync(session, "tagged"));
        var forked = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => client.Sessions.ForkSessionAsync(session, workingDirectory: project.Path));
        var deleted = await Assert.ThrowsExactlyAsync<ClaudeAgentException>(() => client.Sessions.DeleteSessionAsync(session));

        Assert.AreEqual("SessionNotFound", renamed.ErrorCode);
        Assert.AreEqual("SessionNotFound", tagged.ErrorCode);
        Assert.AreEqual("SessionNotFound", forked.ErrorCode);
        Assert.AreEqual("SessionNotFound", deleted.ErrorCode);
        TemporaryClient.AssertSameTree(before, TemporaryClient.Tree(client.UserDirectory));
        Assert.IsFalse(Directory.Exists(client.ConfigurationDirectory));
    }

    // The registration resolves, and what it hands back reads the configuration directory the options
    // name.
    [TestMethod]
    public async Task TheRegistrationHandsBackAClientAndASessionClientOverItsConfigurationDirectory()
    {
        using var home = Transcripts.Empty();
        using var project = new TemporaryProject();
        var directory = Path.Combine(home.Projects, home.Layout.ProjectFor(project.Path));
        Directory.CreateDirectory(directory);
        var session = Built.Session(directory);
        var services = new ServiceCollection();
        services.AddSingleton(TestHostEnvironments.Unmade);
        services.AddClaudeAgentClient(options => options.ConfigurationDirectory = home.Root);
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<ClaudeAgentClient>();
        var listed = await provider.GetRequiredService<ClaudeSessionClient>().GetSessionsAsync(new SessionQueryOptions { WorkingDirectory = project.Path });

        Assert.AreEqual(home.Root, client.Options.ConfigurationDirectory);
        Assert.AreEqual(home.Root, provider.GetRequiredService<IOptions<ClaudeAgentClientOptions>>().Value.ConfigurationDirectory);
        CollectionAssert.AreEqual(new[] { session }, listed.Select(info => info.SessionId).ToArray());
    }

    [TestMethod]
    [DataRow("ConfigurationDirectory", " ", null)]
    [DataRow("EnvironmentVariables", null, "CLAUDE_CONFIG_DIR")]
    [DataRow("EnvironmentVariables", null, "CLAUDE_SECURESTORAGE_CONFIG_DIR")]
    public async Task TheRegistrationRefusesTheOptionsAtStart(string member, string? configurationDirectory, string? variable)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(TestHostEnvironments.Unmade);
        builder.Services.AddClaudeAgentClient(options =>
        {
            options.ConfigurationDirectory = configurationDirectory ?? options.ConfigurationDirectory;
            if (variable is not null)
            {
                options.EnvironmentVariables[variable] = "/somewhere";
            }
        });
        using var host = builder.Build();

        var refused = await Assert.ThrowsExactlyAsync<OptionsValidationException>(() => host.StartAsync());

        StringAssert.Contains(refused.Message, member);
    }

    // The validator and the client read the user's directory and the default configuration directory
    // off a host registered beside the registration: the client's directories are that host's; a host
    // that inherited CLAUDE_CONFIG_DIR naming its own default configuration directory, as one a session
    // of an SDK client started does, starts, and its user's directory is its ~/.claude; and that
    // ~/.claude named as the configuration directory is refused at start.
    [TestMethod]
    public async Task TheRegistrationResolvesTheDirectoriesAgainstAHostRegisteredBesideIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "claude-agent-registration-" + Guid.NewGuid().ToString("n"));
        var services = new ServiceCollection();
        services.AddSingleton(TestHostEnvironments.Under(root));
        services.AddClaudeAgentClient();
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<ClaudeAgentClient>();

        Assert.AreEqual(TestHostEnvironments.DefaultConfigurationDirectory(root), client.Directories.Configuration);
        Assert.AreEqual(TestHostEnvironments.UserDirectory(root), client.Directories.User);

        var nested = Host.CreateApplicationBuilder();
        nested.Services.AddSingleton(TestHostEnvironments.Under(
            root,
            new Dictionary<string, string> { ["CLAUDE_CONFIG_DIR"] = TestHostEnvironments.DefaultConfigurationDirectory(root) }));
        nested.Services.AddClaudeAgentClient();
        using (var started = nested.Build())
        {
            await started.StartAsync();

            var inner = started.Services.GetRequiredService<ClaudeAgentClient>();
            Assert.AreEqual(TestHostEnvironments.DefaultConfigurationDirectory(root), inner.Directories.Configuration);
            Assert.AreEqual(TestHostEnvironments.UserDirectory(root), inner.Directories.User);
            await started.StopAsync();
        }

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(TestHostEnvironments.Under(root));
        builder.Services.AddClaudeAgentClient(options => options.ConfigurationDirectory = TestHostEnvironments.UserDirectory(root));
        using var host = builder.Build();

        var refused = await Assert.ThrowsExactlyAsync<OptionsValidationException>(() => host.StartAsync());

        StringAssert.Contains(refused.Message, "is the user's own");
        Assert.IsFalse(Directory.Exists(root), "resolving the directories made one");
    }

    internal static SessionEntry Line(string type, string text) =>
        new(new JsonObject
        {
            ["type"] = type,
            ["message"] = new JsonObject { ["role"] = "user", ["content"] = text },
        });

    private static string Text<T>(Result<T, Error> result) => result.IsError ? result.Error.Message : "";

    private static SessionNotFoundError NotFound<T>(Result<T, Error> result)
    {
        Assert.IsTrue(result.IsError, "the call succeeded");
        Assert.IsInstanceOfType<SessionNotFoundError>(result.Error, result.Error.Message);
        return (SessionNotFoundError)result.Error;
    }

    private static SessionEditor Editor(TemporaryConfig config) => Editor(config.Layout);

    private static SessionEditor Editor(ITranscriptLayout layout) =>
        new(new TranscriptDirectory(layout), new TranscriptDirectory(new UntouchableLayout()));

    private static ImmutableArray<SessionInfo> Listed(TemporaryConfig config) =>
        Built.Reader(config).ListProject(Transcripts.Project, CancellationToken.None);

    // Two turns whose uuids and prompt are named for the directory the transcript is in.
    private static string Transcript(string side) =>
        Built.Line(Message("user", side + "-a", null, side + " prompt")) + "\n"
        + Built.Line(Message("assistant", side + "-b", side + "-a", side + " answer")) + "\n";

    private static JsonObject Message(string type, string uuid, string? parent, string text) => new()
    {
        ["type"] = type,
        ["uuid"] = uuid,
        ["parentUuid"] = parent,
        ["message"] = new JsonObject { ["role"] = type, ["content"] = text },
    };

    private static string PathOf(TemporaryConfig config, string project, string session) =>
        Path.Combine(config.Projects, project, session + ".jsonl");

    private static string Last(TemporaryConfig config, string project, string session) =>
        File.ReadAllLines(PathOf(config, project, session))[^1];

    private static string Seed(TemporaryConfig config, string project, string? session = null)
    {
        var directory = Path.Combine(config.Projects, project);
        Directory.CreateDirectory(directory);
        var id = session ?? Guid.NewGuid().ToString("D");
        File.WriteAllText(
            Path.Combine(directory, id + ".jsonl"),
            Line("user", "Hello Claude").ToCompactJson() + "\n" + Line("assistant", "Hi!").ToCompactJson() + "\n");
        return id;
    }
}
