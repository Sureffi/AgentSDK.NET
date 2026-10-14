using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace Claude.Agent.Tests.Sessions;

[TestClass]
public sealed class TranscriptMessagesTests
{
    private static readonly SessionQueryOptions All = new();

    [TestMethod]
    public async Task EveryRecordedSessionReadsBackTheChainTheBinaryWrote()
    {
        // Each session's visible chain, root to leaf: u for a user turn, a for an assistant one.
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["0851c866-1c1b-4661-b60b-b734c45296dd"] = "uaauaauaa",
            ["0f467c9c-7240-4183-8657-66c5dc288e2a"] = "uaa",
            ["17a5a07e-50c8-46aa-a329-da0fa774c768"] = "uauauaua",
            ["24ab0d44-a6b5-40c0-98fc-cc43b8ff3c9f"] = "uauaua",
            ["258cdee4-0fd6-4b5e-a1fe-22601197b9a2"] = "uaua",
            ["3c542678-a922-4eab-82c3-a5bb3440454b"] = "uau",
            ["40bf1e72-e8c2-4684-acd0-1b40fd59f3f1"] = "ua",
            ["41603023-fa17-4d91-a02b-10c70e1bca09"] = "uauuuauu",
            ["474e0e86-4835-4b31-9f46-487185450a42"] = "uaau",
            ["4ca46a1b-0db6-4e0b-8cdb-0c98b59472c8"] = "uua",
            ["599be1d3-f7a1-4a55-bdd1-7bc714707377"] = "uaua",
            ["5a264882-daef-4898-ba93-b67e298c4705"] = "uaua",
            ["8a1c5516-dd2e-4951-878f-f9eeb6ed54ad"] = "uaa",
            ["91afa0ce-07f3-4941-b411-d39b8f7fbaf3"] = "uaua",
            ["a4ee9042-2e6c-4f32-8237-94b1609b06af"] = "uaua",
            ["a5258a12-f5f1-4f16-b9ff-a6048b59abb3"] = "uaua",
            ["b0bf1d40-2f2c-4f0c-a43d-6dfc8e992c24"] = "uauauaua",
            ["b190f94e-916b-402a-9016-91266e02c461"] = "uauua",
            ["d10f4163-5fab-46d6-948f-8fc4e19b3315"] = "uaua",
            ["d80ad26e-cac0-4fba-87bf-e2d2a7882a30"] = "uaua",
            ["dd69d1c6-e13f-4d37-8e72-8684030b744f"] = "uaauaa",
            ["e67351d1-f522-421e-a7f3-45bfeef09aa5"] = "ua",
            ["ffeb75c3-4ba5-4849-9688-85e6193729ca"] = "uaua",
        };

        // Every recorded session is in the table, so a recording added without its chain fails here.
        CollectionAssert.AreEquivalent(Transcripts.SessionIds().ToArray(), expected.Keys.ToArray());
        Assert.AreEqual(23, expected.Count);

        var reader = Built.Reader(Transcripts.Layout());
        foreach (var (session, roles) in expected)
        {
            var read = await reader.GetMessagesAsync(session, Query.All(Transcripts.Cwd), CancellationToken.None);

            Assert.AreEqual(
                roles,
                string.Concat(read.Value.Select(message => message.Role == "user" ? "u" : "a")),
                session);
        }
    }

    [TestMethod]
    public async Task ARecordedMessageCarriesItsUuidSessionAndTheBlocksOfItsTurnAndNoParentIds()
    {
        var read = await Built.Reader(Transcripts.Layout()).GetMessagesAsync("e67351d1-f522-421e-a7f3-45bfeef09aa5", Query.All(Transcripts.Cwd), CancellationToken.None);

        var first = read.Value[0];
        Assert.AreEqual("fc274089-54a9-48fc-9ca4-47fcf441be83", first.Uuid);
        Assert.AreEqual("e67351d1-f522-421e-a7f3-45bfeef09aa5", first.SessionId);
        Assert.AreEqual("Reply with exactly one word: pong", first.Text);
        Assert.IsInstanceOfType<TextBlock>(first.Content.Single());
        Assert.IsNull(first.ParentToolUseId);
        Assert.IsNull(first.ParentAgentId);
    }

    // A person's turn carries content alone; the model's carries the API's own message, the blocks
    // of the turn among it.
    [TestMethod]
    public async Task ARecordedModelTurnCarriesTheApiMessageWholeAndAPersonsTurnCarriesNone()
    {
        var read = await Built.Reader(Transcripts.Layout()).GetMessagesAsync("e67351d1-f522-421e-a7f3-45bfeef09aa5", Query.All(Transcripts.Cwd), CancellationToken.None);

        Assert.IsNull(read.Value[0].Message);

        var answer = read.Value[^1];
        Assert.AreEqual(SessionMessageRole.Assistant, answer.Role);
        Assert.IsNotNull(answer.Message);
        Assert.AreEqual("msg_011CeveqmskgXDHsB2uDh9yd", answer.Message.Id);
        Assert.AreEqual("claude-opus-5", answer.Message.Model);
        Assert.AreEqual(StopReason.EndTurn, answer.Message.StopReason);
        Assert.AreEqual(4, answer.Message.Usage!.OutputTokens);
        Assert.AreEqual("pong", answer.Text);
        CollectionAssert.AreEqual(answer.Content.ToArray(), answer.Message.Content.ToArray());
    }

    [TestMethod]
    public async Task ARecordedChainPagesByLimitAndOffset()
    {
        var reader = Built.Reader(Transcripts.Layout());
        var session = "258cdee4-0fd6-4b5e-a1fe-22601197b9a2";

        var all = await reader.GetMessagesAsync(session, Query.All(Transcripts.Cwd), CancellationToken.None);
        var page = await reader.GetMessagesAsync(session, Query.Page(2, 1, Transcripts.Cwd), CancellationToken.None);

        Assert.AreEqual(4, all.Value.Length);
        CollectionAssert.AreEqual(
            all.Value.Skip(1).Take(2).Select(message => message.Uuid).ToArray(),
            page.Value.Select(message => message.Uuid).ToArray());
    }

    [TestMethod]
    public async Task AnIdThatIsNotAUuidIsEmptyBeforeAnythingIsRead()
    {
        var reader = Built.Reader(new UntouchableLayout());

        var read = await reader.GetMessagesAsync("nope", Query.All(), CancellationToken.None);

        Assert.IsTrue(read.IsOk);
        Assert.IsTrue(read.Value.IsEmpty);
    }

    [TestMethod]
    public async Task AMissingSessionIsEmpty()
    {
        using var config = Transcripts.Empty();

        var read = await Built.Reader(config).GetMessagesAsync("00000000-0000-0000-0000-000000000000", Query.All(), CancellationToken.None);

        Assert.IsTrue(read.Value.IsEmpty);
    }

    [TestMethod]
    public async Task AConfigDirectoryThatIsNotThereIsEmpty()
    {
        var layout = Transcripts.Layout(Path.Combine(Path.GetTempPath(), "claude-agent-tests-" + Guid.NewGuid().ToString("n")));

        var read = await Built.Reader(layout).GetMessagesAsync("00000000-0000-0000-0000-000000000000", Query.All(), CancellationToken.None);

        Assert.IsTrue(read.IsOk);
        Assert.IsTrue(read.Value.IsEmpty);
    }

    [TestMethod]
    public async Task AFourEntryChainComesBackRootToLeaf()
    {
        var read = await Read(
            User("a", null, "first"),
            Assistant("b", "a", "second"),
            User("c", "b", "third"),
            Assistant("d", "c", "fourth"));

        CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, read.Select(message => message.Uuid).ToArray());
        CollectionAssert.AreEqual(
            new[] { "first", "second", "third", "fourth" },
            read.Select(message => message.Text).ToArray());
    }

    [TestMethod]
    public async Task AMetaEntryMidChainIsWalkedThroughAndLeftOut()
    {
        var read = await Read(
            User("a", null, "first"),
            Meta(User("b", "a", "caveat")),
            Assistant("c", "b", "answer"));

        CollectionAssert.AreEqual(new[] { "a", "c" }, read.Select(message => message.Uuid).ToArray());
    }

    // isMeta is read by truthiness, not as a bool: a number hides the line when
    // it is not zero and shows it when it is.
    [TestMethod]
    public async Task IsMetaAsANonZeroNumberHidesTheLineAndZeroDoesNot()
    {
        var one = User("b", "a", "hidden");
        one["isMeta"] = 1;
        var zero = User("c", "b", "shown");
        zero["isMeta"] = 0;

        var read = await Read(User("a", null, "first"), one, zero);

        CollectionAssert.AreEqual(new[] { "a", "c" }, read.Select(message => message.Uuid).ToArray());
    }

    [TestMethod]
    public async Task ACompactSummaryIsKept()
    {
        var compact = User("b", "a", "the summary");
        compact["isCompactSummary"] = true;

        var read = await Read(User("a", null, "first"), compact, Assistant("c", "b", "answer"));

        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, read.Select(message => message.Uuid).ToArray());
    }

    [TestMethod]
    public async Task ThePagingContractHolds()
    {
        var entries = new[]
        {
            User("a", null, "1"),
            Assistant("b", "a", "2"),
            User("c", "b", "3"),
            Assistant("d", "c", "4"),
        };

        CollectionAssert.AreEqual(new[] { "a", "b" }, Ids(await Read(Query.Page(2), entries)));
        CollectionAssert.AreEqual(new[] { "b", "c" }, Ids(await Read(Query.Page(2, 1), entries)));
        CollectionAssert.AreEqual(new[] { "c", "d" }, Ids(await Read(Query.Page(null, 2), entries)));
        CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, Ids(await Read(Query.Page(0), entries)));
        CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, Ids(await Read(Query.Page(-1), entries)));
        Assert.AreEqual(0, (await Read(Query.Page(null, 99), entries)).Length);
    }

    [TestMethod]
    public async Task ASidechainLeafLosesToAMainLeaf()
    {
        var side = Assistant("s", "a", "sidechain answer");
        side["isSidechain"] = true;

        var read = await Read(
            User("a", null, "first"),
            Assistant("b", "a", "main answer"),
            side);

        CollectionAssert.AreEqual(new[] { "a", "b" }, read.Select(message => message.Uuid).ToArray());
    }

    [TestMethod]
    public async Task BetweenTwoMainLeavesTheLaterFilePositionWins()
    {
        var read = await Read(
            User("a", null, "first"),
            Assistant("b", "a", "earlier leaf"),
            Assistant("c", "a", "later leaf"));

        CollectionAssert.AreEqual(new[] { "a", "c" }, read.Select(message => message.Uuid).ToArray());
    }

    [TestMethod]
    public async Task CorruptAndBlankLinesAreSkipped()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var session = Guid.NewGuid().ToString();
        Built.Raw(
            Built.ProjectDirectory(config, project.Path),
            session,
            Built.Line(User("a", null, "first")) + "\n\nnot json at all\n" + Built.Line(Assistant("b", "a", "answer")) + "\n");

        var read = await Built.Reader(config).GetMessagesAsync(session, Query.All(project.Path), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "a", "b" }, read.Value.Select(message => message.Uuid).ToArray());
    }

    // JSON.parse takes a line carrying half a surrogate pair escaped without its other half, as
    // JSON.stringify writes a string cut through a pair, and a line nested past System.Text.Json's
    // default of 64, in the line itself or in its message. None is passed over, so the turns before it
    // stay in the chain; the half reads as U+FFFD.
    [TestMethod]
    public async Task ALineCarryingHalfOfASurrogatePairOrNestedPastSixtyFourKeepsTheTurnsBeforeIt()
    {
        var halved = Assistant("b", "a", "cut \ud83d here");
        StringAssert.Contains(Built.Line(halved), "cut \\ud83d here");
        var nestedResult = Assistant("b", "a", "answer");
        nestedResult["toolUseResult"] = Nested(70);
        var nestedInput = Entry("assistant", "b", "a");
        nestedInput["message"] = new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = new JsonArray(new JsonObject
            {
                ["type"] = "tool_use",
                ["id"] = "toolu_01",
                ["name"] = "mcp__deep__echo",
                ["input"] = Nested(70),
            }),
        };

        foreach (var line in new[] { halved, nestedResult, nestedInput })
        {
            var read = await Read(User("a", null, "first"), line, User("c", "b", "third"), Assistant("d", "c", "fourth"));

            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, Ids(read), line.ToJsonString());
        }

        var text = await Read(User("a", null, "first"), halved);
        Assert.AreEqual("cut \uFFFD here", text[1].Text);
        var tool = await Read(User("a", null, "first"), nestedInput);
        Assert.AreEqual("toolu_01", ((ToolUseBlock)tool[1].Content.Single()).Id);
    }

    [TestMethod]
    public async Task EveryProjectIsSearchedWhenNoDirectoryIsGiven()
    {
        using var config = Transcripts.Empty();
        var session = Guid.NewGuid().ToString();
        Built.ProjectDirectory(config, "/some/empty/project");
        Built.Transcript(
            Built.ProjectDirectory(config, "/some/other/project"),
            session,
            null,
            User("a", null, "first"),
            Assistant("b", "a", "answer"));

        var read = await Built.Reader(config).GetMessagesAsync(session, Query.All(), CancellationToken.None);

        Assert.AreEqual(2, read.Value.Length);
    }

    // The walk back from the last entry stops at the first entry it meets again.
    [TestMethod]
    public async Task AParentUuidCycleEndsTheWalkWhereItComesBackAround()
    {
        var read = await Read(User("a", "b", "one"), Assistant("b", "a", "two"));

        CollectionAssert.AreEqual(new[] { "a", "b" }, Ids(read));
    }

    [TestMethod]
    public async Task ALastPromptLineNamesTheLeafWhenTheLastEntryIsOnAnotherBranch()
    {
        var read = await Read(
            User("a", null, "first"),
            Assistant("b", "a", "named leaf"),
            Assistant("c", "a", "later branch"),
            LastPrompt("b"));

        CollectionAssert.AreEqual(new[] { "a", "b" }, Ids(read));
    }

    [TestMethod]
    public async Task ALastEntryThatDescendsFromTheNamedLeafIsWhereTheWalkStarts()
    {
        var read = await Read(
            User("a", null, "first"),
            Assistant("b", "a", "named leaf"),
            LastPrompt("b"),
            User("c", "b", "second"),
            Assistant("d", "c", "answer"));

        CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, Ids(read));
    }

    // A leaf named before a compaction boundary is not taken; the walk starts at the last entry, a
    // system one here, and goes back to the nearest turn.
    [TestMethod]
    public async Task ACompactionBoundaryDropsTheLeafALastPromptLineNamedBeforeIt()
    {
        var boundary = Entry("system", "e", "c");
        boundary["subtype"] = "compact_boundary";

        var read = await Read(
            User("a", null, "first"),
            Assistant("b", "a", "named leaf"),
            Assistant("c", "a", "later branch"),
            LastPrompt("b"),
            boundary);

        CollectionAssert.AreEqual(new[] { "a", "c" }, Ids(read));
    }

    [TestMethod]
    public async Task AZeroByteTranscriptIsEmpty()
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var session = Guid.NewGuid().ToString();
        Built.Raw(Built.ProjectDirectory(config, project.Path), session, string.Empty);

        var read = await Built.Reader(config).GetMessagesAsync(session, Query.All(project.Path), CancellationToken.None);

        Assert.IsTrue(read.Value.IsEmpty);
    }

    [TestMethod]
    public async Task ASummaryLineBetweenTwoChainedEntriesNeitherBreaksTheChainNorAppears()
    {
        var read = await Read(
            User("a", null, "first"),
            new JsonObject { ["type"] = "summary", ["summary"] = "a summary", ["leafUuid"] = "b" },
            Assistant("b", "a", "answer"));

        CollectionAssert.AreEqual(new[] { "a", "b" }, read.Select(message => message.Uuid).ToArray());
    }

    [TestMethod]
    public async Task ALoneRootlessUserEntryIsItsOwnChain()
    {
        var read = await Read(User("a", null, "alone"));

        CollectionAssert.AreEqual(new[] { "a" }, read.Select(message => message.Uuid).ToArray());
    }

    [TestMethod]
    public async Task AMessageWithoutASessionIdCarriesTheSessionItWasReadUnder()
    {
        // An empty session id on the line does not count: the id the read was
        // asked for stands in.
        var entry = User("a", null, "first");
        entry.Remove("sessionId");

        var read = await Read(entry);

        Assert.AreEqual(1, read.Length);
        Assert.IsTrue(global::Claude.Agent.Ids.IsUuid(read[0].SessionId));
    }

    private static string[] Ids(ImmutableArray<SessionMessage> messages) =>
        [.. messages.Select(message => message.Uuid)];

    private static async Task<ImmutableArray<SessionMessage>> Read(params JsonObject[] entries) =>
        await Read(All, entries);

    private static async Task<ImmutableArray<SessionMessage>> Read(SessionQueryOptions page, params JsonObject[] entries)
    {
        using var config = Transcripts.Empty();
        using var project = new TemporaryProject();
        var session = Guid.NewGuid().ToString();
        Built.Transcript(Built.ProjectDirectory(config, project.Path), session, null, entries);

        var read = await Built.Reader(config).GetMessagesAsync(
            session,
            new SessionQueryOptions { WorkingDirectory = project.Path, Limit = page.Limit, Offset = page.Offset },
            CancellationToken.None);

        Assert.IsTrue(read.IsOk);
        return read.Value;
    }

    private static JsonObject Entry(string type, string uuid, string? parent) => new()
    {
        ["type"] = type,
        ["uuid"] = uuid,
        ["parentUuid"] = parent,
        ["sessionId"] = "3fb2ef6c-0b90-4a9c-bd9a-1a0a3c4e5d6f",
    };

    private static JsonObject LastPrompt(string leaf) => new()
    {
        ["type"] = "last-prompt",
        ["lastPrompt"] = "a prompt",
        ["leafUuid"] = leaf,
        ["sessionId"] = "3fb2ef6c-0b90-4a9c-bd9a-1a0a3c4e5d6f",
    };

    private static JsonObject User(string uuid, string? parent, string text) => Message("user", uuid, parent, text);

    private static JsonObject Assistant(string uuid, string? parent, string text) =>
        Message("assistant", uuid, parent, text);

    // `depth` objects, each but the last holding the next.
    private static JsonObject Nested(int depth)
    {
        var nested = new JsonObject();
        for (var level = 1; level < depth; level++)
        {
            nested = new JsonObject { ["n"] = nested };
        }

        return nested;
    }

    private static JsonObject Meta(JsonObject entry)
    {
        entry["isMeta"] = true;
        return entry;
    }

    private static JsonObject Message(string type, string uuid, string? parent, string text)
    {
        var entry = Entry(type, uuid, parent);
        entry["message"] = new JsonObject { ["role"] = type, ["content"] = text };
        return entry;
    }
}
