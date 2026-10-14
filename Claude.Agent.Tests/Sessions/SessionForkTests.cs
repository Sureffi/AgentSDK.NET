using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace Claude.Agent.Tests.Sessions;

// The fork transform on both paths: a recorded transcript copied to a temp directory, and the same
// transform over entries that never touched a disk.
[TestClass]
public sealed class SessionForkTests
{
    // 22 recorded lines, 17 of them the conversation, one chain; the other five are queue-operation,
    // atis-latch, ai-title and last-prompt, which carry no uuid and are not copied. The fork is the 17
    // copies, then the latch, the title and the agent name.
    private const string Recorded = "17a5a07e-50c8-46aa-a329-da0fa774c768";
    private const string Absent = "c0000000-0000-4000-8000-000000000009";

    // The recorded session's first prompt, 332 characters, as a derived title's base takes it: cut at
    // 100 with the trailing space trimmed.
    private const string RecordedBase =
        "Do these three things in order, each with one tool call: read the file missing.txt in the working di";

    private static readonly string[] ConversationTypes = ["user", "assistant", "attachment", "system"];

    [TestMethod]
    public async Task ForkRefusesASourceIdThatIsNotAUuid()
    {
        using var config = Transcripts.Copy();

        var refused = await Editor(config).ForkAsync(
            "not-a-uuid", null, null, Transcripts.Cwd, CancellationToken.None);

        Assert.IsInstanceOfType<ValidationError>(refused.Error);
    }

    // The cutoff is checked before the source is looked for, so a bad one fails the same way
    // whether or not the session is here.
    [TestMethod]
    public async Task ForkRefusesACutoffThatIsNotAUuidBeforeTheSourceIsLookedFor()
    {
        using var config = Transcripts.Empty();

        var refused = await Editor(config).ForkAsync(
            Absent, "not-valid", null, Transcripts.Cwd, CancellationToken.None);

        Assert.AreEqual("upTo", ((ValidationError)refused.Error).Member);
    }

    // The refusal says there is no such session to fork, and where it was looked for.
    [TestMethod]
    public async Task ForkOfASessionThatIsNotThereIsNotFoundWhereItWasLookedFor()
    {
        using var config = Transcripts.Copy();

        var here = await Editor(config).ForkAsync(Absent, null, null, Transcripts.Cwd, CancellationToken.None);
        var anywhere = await Editor(config).ForkAsync(Absent, null, null, null, CancellationToken.None);

        var inProject = (SessionNotFoundError)here.Error;
        Assert.AreEqual(SessionSearchScope.ProjectDirectory, inProject.Scope);
        Assert.AreEqual($"There is no session {Absent} to fork in the project directory for {Transcripts.Cwd}.", inProject.Message);
        var inAny = (SessionNotFoundError)anywhere.Error;
        Assert.AreEqual(SessionSearchScope.AnyProjectDirectory, inAny.Scope);
        Assert.AreEqual($"There is no session {Absent} to fork in any project directory.", inAny.Message);
    }

    [TestMethod]
    public async Task TheForkIsANewTranscriptBesideTheSourceUnderADifferentId()
    {
        using var config = Transcripts.Copy();

        var forked = await Fork(config, Recorded);

        Assert.AreNotEqual(Recorded, forked);
        Assert.IsTrue(File.Exists(Path.Combine(config.ProjectDirectory, forked + ".jsonl")));
        Assert.IsTrue(File.Exists(Path.Combine(config.ProjectDirectory, Recorded + ".jsonl")));
    }

    // One copy per entry of the conversation, in its order, then the latch, the title line and the
    // agent-name line. forkedFrom.messageUuid says which source entry each copy is.
    [TestMethod]
    public async Task TheForkCopiesTheConversationInOrderAndEndsOnTheLatchTheTitleAndTheAgentName()
    {
        using var config = Transcripts.Copy();

        var forked = await Fork(config, Recorded);
        var lines = Lines(config, forked);

        CollectionAssert.AreEqual(
            SourceUuids(Transcripts.EntriesOf(Recorded)).ToArray(),
            lines[..^3].Select(line => line.Json["forkedFrom"]!["messageUuid"]!.GetValue<string>()).ToArray());
        Assert.AreEqual(20, lines.Length);
        CollectionAssert.AreEqual(new[] { "atis-latch", "custom-title", "agent-name" }, lines[^3..].Select(line => line.Type).ToArray());
    }

    // A copy keeps the uuid and the timestamp the binary wrote; its parent is the copy before it.
    [TestMethod]
    public async Task EveryCopyKeepsItsUuidAndTimestampAndIsChainedToTheCopyBeforeIt()
    {
        using var config = Transcripts.Copy();
        var source = Transcripts.EntriesOf(Recorded).Where(entry => entry.Uuid is not null && ConversationTypes.Contains(entry.Type)).ToArray();

        var forked = await Fork(config, Recorded);
        var copies = Lines(config, forked)[..^3];

        Assert.AreEqual(source.Length, copies.Length);
        for (var i = 0; i < copies.Length; i++)
        {
            Assert.AreEqual(source[i].Uuid, copies[i].Uuid);
            Assert.AreEqual(source[i].Uuid, copies[i].Json["forkedFrom"]!["messageUuid"]!.GetValue<string>());
            Assert.AreEqual(source[i].Json["timestamp"]?.GetValue<string>(), copies[i].Json["timestamp"]?.GetValue<string>());
            Assert.AreEqual(i == 0 ? null : copies[i - 1].Uuid, copies[i].Json["parentUuid"]?.GetValue<string>(), $"copy {i}");
        }
    }

    // A key the binary already wrote keeps its position and only its value changes; forkedFrom, which
    // the source line does not carry, comes last.
    [TestMethod]
    public async Task AForkedEntryKeepsTheBinarysKeyOrderAndAppendsForkedFrom()
    {
        using var config = Transcripts.Copy();
        var source = Transcripts.EntriesOf(Recorded).First(entry => entry.Type == "user");

        var forked = await Fork(config, Recorded);
        var line = Lines(config, forked)[0];

        CollectionAssert.AreEqual(
            source.Json.Select(property => property.Key).Append("forkedFrom").ToArray(),
            line.Json.Select(property => property.Key).ToArray());
    }

    [TestMethod]
    public async Task ACutoffSlicesTheConversationInclusively()
    {
        using var config = Transcripts.Copy();
        var originals = SourceUuids(Transcripts.EntriesOf(Recorded));
        var cutoff = originals[2];

        var forked = await Editor(config).ForkAsync(
            Recorded, cutoff, null, Transcripts.Cwd, CancellationToken.None);

        Assert.IsTrue(forked.IsOk, Text(forked));
        CollectionAssert.AreEqual(
            originals.Take(3).ToArray(),
            Lines(config, forked.Value)[..^3]
                .Select(line => line.Json["forkedFrom"]!["messageUuid"]!.GetValue<string>()).ToArray());
    }

    [TestMethod]
    public async Task ACutoffThatIsNotInTheConversationIsRefusedNamingIt()
    {
        using var config = Transcripts.Copy();

        var refused = await Editor(config).ForkAsync(
            Recorded,
            Absent,
            null,
            Transcripts.Cwd,
            CancellationToken.None);

        Assert.IsInstanceOfType<ForkError>(refused.Error);
        Assert.AreEqual($"Session {Recorded} holds no message {Absent} for a fork to end at.", refused.Error.Message);
    }

    // The fork copies the conversation the source resolves to, as the binary branches the one it has
    // open: an abandoned branch and a sidechain are not in it.
    [TestMethod]
    public async Task TheForkCopiesTheConversationTheSourceResolvesToAndNothingBesideIt()
    {
        using var config = Transcripts.Empty();
        Directory.CreateDirectory(config.ProjectDirectory);
        var root = Message("user", "11111111-1111-4111-8111-111111111111", null, Absent);
        var abandoned = Message("assistant", "22222222-2222-4222-8222-222222222222", "11111111-1111-4111-8111-111111111111", Absent);
        var side = Message("assistant", "44444444-4444-4444-8444-444444444444", "11111111-1111-4111-8111-111111111111", Absent).Json;
        side["isSidechain"] = true;
        var kept = Message("assistant", "33333333-3333-4333-8333-333333333333", "11111111-1111-4111-8111-111111111111", Absent);
        Write(config, Absent, root, abandoned, kept, new SessionEntry(side));

        var forked = await Fork(config, Absent);

        CollectionAssert.AreEqual(
            new[] { root.Uuid, kept.Uuid },
            Lines(config, forked)[..^2].Select(line => line.Uuid).ToArray());
    }

    // A line carrying half a surrogate pair escaped without its other half is read, the half as
    // U+FFFD, so the fork copies the turns before it, chained as they were, and its title is derived
    // from the conversation's first prompt.
    [TestMethod]
    public async Task TheForkCopiesTheTurnsBeforeALineCarryingHalfOfASurrogatePair()
    {
        using var config = Transcripts.Empty();
        var first = Message("user", "11111111-1111-4111-8111-111111111111", null, Absent);
        var halved = Message("assistant", "22222222-2222-4222-8222-222222222222", "11111111-1111-4111-8111-111111111111", Absent).Json;
        halved["message"] = new JsonObject { ["role"] = "assistant", ["content"] = "cut \ud83d here" };
        var third = Message("user", "33333333-3333-4333-8333-333333333333", "22222222-2222-4222-8222-222222222222", Absent);
        var fourth = Message("assistant", "44444444-4444-4444-8444-444444444444", "33333333-3333-4333-8333-333333333333", Absent);
        Write(config, Absent, first, new SessionEntry(halved), third, fourth);

        var forked = await Fork(config, Absent);
        var lines = Lines(config, forked);

        CollectionAssert.AreEqual(
            new[] { first.Uuid, "22222222-2222-4222-8222-222222222222", third.Uuid, fourth.Uuid },
            lines[..^2].Select(line => line.Uuid).ToArray());
        CollectionAssert.AreEqual(
            new[] { null, first.Uuid, "22222222-2222-4222-8222-222222222222", third.Uuid },
            lines[..^2].Select(line => line.Field("parentUuid")).ToArray());
        Assert.AreEqual("cut \uFFFD here", lines[1].Json["message"]!["content"]!.GetValue<string>());
        Assert.AreEqual("user 11111111-1111-4111-8111-111111111111 (Branch)", Title(lines));
    }

    // A given title is the binary's: its whitespace runs made one space, its ends trimmed, and no
    // branch suffix.
    [TestMethod]
    public async Task AGivenTitleIsWrittenWithItsWhitespaceCollapsedAndNoBranchSuffix()
    {
        using var config = Transcripts.Copy();

        var forked = await Editor(config).ForkAsync(
            Recorded, null, " My \t Fork\n ", Transcripts.Cwd, CancellationToken.None);

        Assert.AreEqual("My Fork", Title(Lines(config, forked.Value)));
    }

    [TestMethod]
    public async Task AWhitespaceOnlyTitleIsNoTitleAndTheTitleIsDerived()
    {
        using var config = Transcripts.Copy();

        var forked = await Editor(config).ForkAsync(
            Recorded, null, " \t\n", Transcripts.Cwd, CancellationToken.None);

        Assert.AreEqual(RecordedBase + " (Branch)", Title(Lines(config, forked.Value)));
    }

    // The recorded session carries an ai-title line, which the derived title does not read: it is
    // built on the fork's first prompt.
    [TestMethod]
    public async Task ADerivedTitleIsTheForksFirstPromptCutAtAHundredWithTheBranchSuffix()
    {
        using var config = Transcripts.Copy();

        var forked = await Fork(config, Recorded);

        Assert.AreEqual(RecordedBase + " (Branch)", Title(Lines(config, forked)));
    }

    // Each fork lands in the project directory, so the next one finds its title there.
    [TestMethod]
    public async Task EachFurtherDerivedForkOfASessionTakesTheLeastFreeBranchNumber()
    {
        using var config = Transcripts.Copy();

        var first = await Fork(config, Recorded);
        var second = await Fork(config, Recorded);
        var third = await Fork(config, Recorded);

        Assert.AreEqual(RecordedBase + " (Branch)", Title(Lines(config, first)));
        Assert.AreEqual(RecordedBase + " (Branch 2)", Title(Lines(config, second)));
        Assert.AreEqual(RecordedBase + " (Branch 3)", Title(Lines(config, third)));
    }

    // The clash is read as the binary reads it, lowercased and trimmed; the numbers are read off
    // the titles exactly, and 1 is always held.
    [TestMethod]
    public void TheBranchNumberIsTheLeastFromTwoThatNoTitleOfTheSameBaseHolds()
    {
        Assert.AreEqual("Base (Branch)", SessionFork.BranchTitle("Base", []));
        Assert.AreEqual("Base (Branch)", SessionFork.BranchTitle("Base", Renamed("Base (Branch 2)", null, "")));
        Assert.AreEqual("Base (Branch 2)", SessionFork.BranchTitle("Base", Renamed("  base (BRANCH) ")));
        Assert.AreEqual(
            "Base (Branch 3)",
            SessionFork.BranchTitle("Base", Renamed("Base (Branch)", "Base (Branch 2)", "Base (Branch 4)", "base (Branch 3)", "Base (Branch 3) ", "Base (Branch 3x)")));
        Assert.AreEqual("Base (Branch 2)", SessionFork.BranchTitle("Base", Renamed("Base (Branch)", "Base (Branch 99999999999)")));
    }

    // A session listed under a generated title clashes as a renamed one does, but holds no number:
    // the binary reads the numbers off renamed titles alone.
    [TestMethod]
    public void AGeneratedTitleClashesButHoldsNoBranchNumber()
    {
        Assert.AreEqual("Base (Branch 2)", SessionFork.BranchTitle("Base", [("Base (Branch)", null)]));
        Assert.AreEqual(
            "Base (Branch 2)",
            SessionFork.BranchTitle("Base", [("Base (Branch)", "Base (Branch)"), ("Base (Branch 2)", null)]));
    }

    private static (string? Listed, string? Renamed)[] Renamed(params string?[] titles) =>
        [.. titles.Select(title => (title, title))];

    [TestMethod]
    public void ABaseTitleWithNoPromptIsTheCommandNameAndWithNeitherBranchedConversation()
    {
        var command = Message("user", "11111111-1111-4111-8111-111111111111", null, Absent).Json;
        command["message"] = new JsonObject { ["role"] = "user", ["content"] = "<command-name>/clear</command-name>" };

        Assert.AreEqual("Branched conversation", SessionFork.BaseTitle([]));
        Assert.AreEqual("/clear", SessionFork.BaseTitle([new SessionEntry(command)]));
    }

    // The base is cut after the first-prompt rule has cut at 200 with its ellipsis and the whitespace
    // has been collapsed, so a prompt whose first 200 characters are one letter and spaces keeps the
    // ellipsis; a cut through a surrogate pair drops the high half, and a space left at the cut goes.
    [TestMethod]
    public void ABaseTitleCollapsesWhitespaceAndIsCutAtAHundredWithoutAnEllipsis()
    {
        Assert.AreEqual("a b c", Base("a\t\tb   c"));
        Assert.AreEqual("a…", Base("a" + new string(' ', 300) + "b"));
        Assert.AreEqual(new string('x', 100), Base(new string('x', 150)));
        Assert.AreEqual(new string('x', 99), Base(new string('x', 99) + "\U0001F600tail"));
        Assert.AreEqual(new string('x', 99), Base(new string('x', 99) + " yyyy"));
    }

    [TestMethod]
    public async Task EveryLineOfTheForkCarriesTheNewSessionIdAndEveryCopyPointsBackAtTheSource()
    {
        using var config = Transcripts.Copy();

        var forked = await Fork(config, Recorded);
        var lines = Lines(config, forked);

        foreach (var line in lines)
        {
            Assert.AreEqual(forked, line.Json["sessionId"]!.GetValue<string>());
        }

        foreach (var line in lines[..^3])
        {
            Assert.AreEqual(Recorded, line.Json["forkedFrom"]!["sessionId"]!.GetValue<string>());
            Assert.IsFalse(line.Json["isSidechain"]!.GetValue<bool>());
        }
    }

    [TestMethod]
    public async Task ForkFindsTheSourceWithNoCwd()
    {
        using var config = Transcripts.Copy();

        var forked = await Editor(config).ForkAsync(
            Recorded, null, null, null, CancellationToken.None);

        Assert.IsTrue(forked.IsOk, Text(forked));
        Assert.IsTrue(File.Exists(Path.Combine(config.ProjectDirectory, forked.Value + ".jsonl")));
    }

    // The binary's copy is the source line over again with its four keys rewritten: every other key
    // stays, logicalParentUuid as it stands, and only sessionKind goes.
    [TestMethod]
    public void ACopyKeepsEveryKeyButSessionKind()
    {
        var json = Message("user", "11111111-1111-4111-8111-111111111111", null, Absent).Json;
        json["teamName"] = "test-team";
        json["agentName"] = "test-agent";
        json["slug"] = "test-slug";
        json["sourceToolAssistantUUID"] = "22222222-2222-4222-8222-222222222222";
        json["logicalParentUuid"] = "99999999-9999-4999-8999-999999999999";
        json["sessionKind"] = "bg";

        var copy = SessionFork.Copy(Absent, [new SessionEntry(json)], Recorded)[0].Json;

        foreach (var key in new[] { "teamName", "agentName", "slug", "sourceToolAssistantUUID" })
        {
            Assert.AreEqual(json[key]!.GetValue<string>(), copy[key]!.GetValue<string>(), key);
        }

        Assert.AreEqual("99999999-9999-4999-8999-999999999999", copy["logicalParentUuid"]!.GetValue<string>());
        Assert.IsFalse(copy.ContainsKey("sessionKind"));
    }

    // A refusal fallback the model gave is marked as the fork's, so the fork does not act on it again.
    [TestMethod]
    public void ARefusalFallbackSystemEntryIsMarkedNeutralizedByTheFork()
    {
        var fallback = Message("system", "11111111-1111-4111-8111-111111111111", null, Absent).Json;
        fallback["subtype"] = "model_refusal_fallback";
        var other = Message("system", "22222222-2222-4222-8222-222222222222", "11111111-1111-4111-8111-111111111111", Absent).Json;
        other["subtype"] = "local_command";

        var copies = SessionFork.Copy(Absent, [new SessionEntry(fallback), new SessionEntry(other)], Recorded);

        Assert.IsTrue(copies[0].Json["neutralizedByFork"]!.GetValue<bool>());
        Assert.IsFalse(copies[1].Json.ContainsKey("neutralizedByFork"));
    }

    // The latch the fork carries is the source's last, as the binary takes one on load: printable
    // ASCII with no space, the empty one included, on a line of the source's own session. A source
    // with none gives the fork none.
    [TestMethod]
    public void TheForkCarriesTheSourcesLastLatch()
    {
        static SessionEntry Latch(string atis, string session) =>
            new(new JsonObject { ["type"] = "atis-latch", ["atis"] = atis, ["sessionId"] = session });

        var trailer = SessionFork.Trailer([Latch("first", Absent), Latch("kept", Absent), Latch("has space", Absent), Latch("other", Recorded)], Absent, "t", Recorded);
        var none = SessionFork.Trailer([Message("user", "11111111-1111-4111-8111-111111111111", null, Absent)], Absent, "t", Recorded);

        Assert.AreEqual(
            $$"""{"type":"atis-latch","sessionId":"{{Recorded}}","atis":"kept"}""",
            trailer[0].ToCompactJson());
        CollectionAssert.AreEqual(new[] { "custom-title", "agent-name" }, none.Select(line => line.Type).ToArray());
    }

    // The recorded source's latch is the empty one.
    [TestMethod]
    public async Task TheRecordedForkCarriesTheEmptyLatch()
    {
        using var config = Transcripts.Copy();

        var forked = await Fork(config, Recorded);

        Assert.AreEqual(
            $$"""{"type":"atis-latch","sessionId":"{{forked}}","atis":""}""",
            RawLines(config, forked)[^3]);
    }

    // The two lines the fork writes last, byte for byte: the custom-title line and the agent-name
    // line the binary writes, each type, then the title, then sessionId, and nothing else.
    [TestMethod]
    public async Task TheTitleAndAgentNameLinesAreTheBinarysInTheirKeyOrder()
    {
        using var config = Transcripts.Empty();
        Write(config, Absent, Message("user", "33333333-3333-4333-8333-333333333333", null, Absent));

        var given = await Editor(config).ForkAsync(Absent, null, "My Fork", Transcripts.Cwd, CancellationToken.None);
        var derived = await Editor(config).ForkAsync(Absent, null, null, Transcripts.Cwd, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                $$"""{"type":"custom-title","customTitle":"My Fork","sessionId":"{{given.Value}}"}""",
                $$"""{"type":"agent-name","agentName":"My Fork","sessionId":"{{given.Value}}"}""",
            },
            RawLines(config, given.Value)[^2..]);
        CollectionAssert.AreEqual(
            new[]
            {
                $$"""{"type":"custom-title","customTitle":"user 33333333-3333-4333-8333-333333333333 (Branch)","sessionId":"{{derived.Value}}"}""",
                $$"""{"type":"agent-name","agentName":"user 33333333-3333-4333-8333-333333333333 (Branch)","sessionId":"{{derived.Value}}"}""",
            },
            RawLines(config, derived.Value)[^2..]);
    }

    // The recording that founds the two lines: the renamed transcript, where the binary wrote a
    // custom-title line and an agent-name line of its own after the prompt "/rename kept title". The
    // fork's lines for that title and that session are the recorded lines, byte for byte.
    [TestMethod]
    public void TheTitleAndAgentNameLinesAreTheOnesTheBinaryWroteWhenItRenamedASession()
    {
        const string renamed = "474e0e86-4835-4b31-9f46-487185450a42";
        var recorded = Transcripts.LinesOf(renamed);
        var entries = Transcripts.EntriesOf(renamed);

        Assert.AreEqual(
            recorded[entries.IndexOf(entries.Single(entry => entry.Type == "custom-title"))],
            new SessionEntry(SessionFork.TitleLine("kept title", renamed)).ToCompactJson());
        Assert.AreEqual(
            recorded[entries.IndexOf(entries.Single(entry => entry.Type == "agent-name"))],
            new SessionEntry(SessionFork.AgentNameLine("kept title", renamed)).ToCompactJson());
    }

    // A session with nothing forkable in it is refused, not forked empty. The sidechain lines are
    // built here: the binary writes them only into a subagent's own transcript.
    [TestMethod]
    public void ASessionOfNothingButBuiltSidechainEntriesHasNothingToCopy()
    {
        var sidechain = Message("user", "11111111-1111-4111-8111-111111111111", null, Absent).Json;
        sidechain["isSidechain"] = true;

        var sliced = SessionFork.Slice(Absent, [new SessionEntry(sidechain)], null);

        Assert.IsInstanceOfType<ForkError>(sliced.Error);
        Assert.AreEqual($"Session {Absent} holds no conversation to copy into a fork.", sliced.Error.Message);
    }

    private static string Text<T>(Result<T, Error> result) => result.IsError ? result.Error.Message : "";

    private static string Title(ImmutableArray<SessionEntry> lines) =>
        lines.Single(line => line.Type == "custom-title").Json["customTitle"]!.GetValue<string>();

    private static string Base(string prompt)
    {
        var entry = Message("user", "11111111-1111-4111-8111-111111111111", null, Absent).Json;
        entry["message"] = new JsonObject { ["role"] = "user", ["content"] = prompt };
        return SessionFork.BaseTitle([new SessionEntry(entry)]);
    }

    private static SessionEditor Editor(TemporaryConfig config) =>
        new(new TranscriptDirectory(config.Layout), new TranscriptDirectory(new UntouchableLayout()));

    private static ImmutableArray<string> SourceUuids(IEnumerable<SessionEntry> entries) =>
        [.. entries
            .Where(entry => entry.Uuid is not null && ConversationTypes.Contains(entry.Type))
            .Select(entry => entry.Uuid!)];

    private static void Write(TemporaryConfig config, string session, params SessionEntry[] entries)
    {
        Directory.CreateDirectory(config.ProjectDirectory);
        File.WriteAllText(
            Path.Combine(config.ProjectDirectory, session + ".jsonl"),
            string.Concat(entries.Select(entry => entry.ToCompactJson() + "\n")));
    }

    private static string[] RawLines(TemporaryConfig config, string forked) =>
        [.. File.ReadAllText(Path.Combine(config.ProjectDirectory, forked + ".jsonl"))
            .Split('\n')
            .Where(line => line.Length != 0)];

    private static ImmutableArray<SessionEntry> Lines(TemporaryConfig config, string forked) =>
        [.. RawLines(config, forked).Select(line => SessionEntry.Parse(line).Value)];

    private static async Task<string> Fork(TemporaryConfig config, string session)
    {
        var forked = await Editor(config).ForkAsync(
            session, null, null, Transcripts.Cwd, CancellationToken.None);

        Assert.IsTrue(forked.IsOk, Text(forked));
        return forked.Value;
    }

    private static SessionEntry Message(string type, string uuid, string? parent, string session) =>
        new(new JsonObject
        {
            ["type"] = type,
            ["uuid"] = uuid,
            ["parentUuid"] = parent,
            ["sessionId"] = session,
            ["timestamp"] = "2024-01-01T00:00:00.000Z",
            ["message"] = new JsonObject { ["role"] = type, ["content"] = type + " " + uuid },
        });
}
