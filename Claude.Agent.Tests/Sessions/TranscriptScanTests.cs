namespace Claude.Agent.Tests.Sessions;

[TestClass]
public sealed class TranscriptScanTests
{
    // Every line is parsed whole; a blank line and one that is no JSON object are skipped.
    [TestMethod]
    public void EveryLineIsParsedAndABlankOrCorruptOneIsSkipped()
    {
        const string text = """
            {"type":"user","k":"a\"b"}

            not json
            [1,2]
            {"type":"tag","tag":"t"}
            """;

        var entries = TranscriptScan.Entries(text);

        CollectionAssert.AreEqual(new[] { "user", "tag" }, entries.Select(entry => entry.Type).ToArray());
        Assert.AreEqual("a\"b", entries[0].Field("k"));
    }

    // A field is a key of the line itself: the same key inside a tool's input on an earlier line is
    // not it.
    [TestMethod]
    public void AKeyInsideAToolInputIsNotTheLinesField()
    {
        var head = TranscriptScan.Entries("""
            {"type":"assistant","message":{"content":[{"type":"tool_use","input":{"cwd":"/elsewhere","gitBranch":"other","timestamp":"2020-01-01T00:00:00.000Z"}}]}}
            {"type":"user","cwd":"/here","gitBranch":"main","timestamp":"2024-01-01T00:00:00.000Z","message":{"role":"user","content":"hi"}}
            """);

        var info = TranscriptScan.Info("s", head, head, DateTimeOffset.UnixEpoch, 1, null);

        Assert.AreEqual("/here", info?.WorkingDirectory);
        Assert.AreEqual("main", info?.GitBranch);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeMilliseconds(1704067200000L), info?.CreatedAt);
    }

    [TestMethod]
    public void TheFirstPromptComesOffAStringContent()
    {
        const string head = """{"type":"user","message":{"role":"user","content":"Say: one"}}""";

        Assert.AreEqual("Say: one", FirstPrompt(head));
    }

    [TestMethod]
    public void TheFirstPromptComesOffTextBlocksInOrder()
    {
        const string head = """
            {"type":"user","message":{"role":"user","content":[{"type":"image"},{"type":"text","text":"block"}]}}
            """;

        Assert.AreEqual("block", FirstPrompt(head));
    }

    [TestMethod]
    public void TheFirstPromptSkipsMetaToolResultAndCompactSummaryLines()
    {
        const string head = """
            {"type":"user","isMeta":true,"message":{"role":"user","content":"meta"}}
            {"type":"user","message":{"role":"user","content":[{"type":"tool_result","content":"r"}]}}
            {"type":"user","isCompactSummary":true,"message":{"role":"user","content":"compacted"}}
            {"type":"assistant","message":{"role":"assistant","content":"not a prompt"}}
            {"type":"user","message":{"role":"user","content":"real"}}
            """;

        Assert.AreEqual("real", FirstPrompt(head));
    }

    [TestMethod]
    public void TheFirstPromptTruncatesPast200CharactersAndEndsWithAnEllipsis()
    {
        var head = """{"type":"user","message":{"role":"user","content":"XXX"}}"""
            .Replace("XXX", new string('x', 250), StringComparison.Ordinal);

        var prompt = FirstPrompt(head);

        Assert.AreEqual(201, prompt.Length);
        Assert.AreEqual('…', prompt[^1]);
        Assert.AreEqual(new string('x', 200), prompt[..200]);
    }

    [TestMethod]
    public void ASlashCommandIsOnlyTheFallbackAndALaterRealPromptWins()
    {
        const string command = """{"type":"user","message":{"role":"user","content":"<command-name>/help</command-name>"}}""";

        Assert.AreEqual("/help", FirstPrompt(command));
        Assert.AreEqual("real", FirstPrompt(
            command + "\n" + """{"type":"user","message":{"role":"user","content":"real"}}"""));
    }

    // A shell command typed at the prompt is the prompt: "! " and the command, trimmed, however long.
    [TestMethod]
    public void ABashInputIsThePromptAsTheCommandItRanUncut()
    {
        var command = new string('y', 300);

        Assert.AreEqual("! ls -la", FirstPrompt(Head("<bash-input>  ls -la\\n</bash-input>")));
        Assert.AreEqual("! " + command, FirstPrompt(Head("<bash-input>" + command + "</bash-input>")));
        Assert.AreEqual("! ls", FirstPrompt(Head("<command-name>/x</command-name>") + "\n" + Head("<bash-input>ls</bash-input>")));
    }

    // A pasted block is read as what it holds: the tags and up to two line feeds either side of them
    // go, and a block whose id is not four lowercase hex digits, or that never closes, is left alone.
    [TestMethod]
    public void APastedBlockIsReadAsWhatItHolds()
    {
        Assert.AreEqual("the body", TranscriptScan.ExpandPastedContent("<pasted_content id=\"0a1f\">\nthe body\n</pasted_content id=\"0a1f\">"));
        Assert.AreEqual("beforeinsideafter", TranscriptScan.ExpandPastedContent("before\n\n<pasted_content id=\"beef\">\ninside\n</pasted_content id=\"beef\">\n\nafter"));
        Assert.AreEqual("a\nbody\nb", TranscriptScan.ExpandPastedContent("a\n\n\n<pasted_content id=\"beef\">\nbody\n</pasted_content id=\"beef\">\n\n\nb"));
        Assert.AreEqual(string.Empty, TranscriptScan.ExpandPastedContent("<pasted_content id=\"beef\">\n</pasted_content id=\"beef\">"));

        const string upper = "<pasted_content id=\"BEEF\">\nx\n</pasted_content id=\"BEEF\">";
        const string open = "<pasted_content id=\"beef\">\nx";
        Assert.AreEqual(upper, TranscriptScan.ExpandPastedContent(upper));
        Assert.AreEqual(open, TranscriptScan.ExpandPastedContent(open));

        Assert.AreEqual("pasted words", FirstPrompt(Head("<pasted_content id=\\\"0a1f\\\">\\npasted words\\n</pasted_content id=\\\"0a1f\\\">")));
    }

    // The rule names no tag: any text that opens on a lowercase tag name followed by whitespace or
    // '>' is passed over, whatever follows it, and so is the interrupt line. A tag name that opens
    // uppercase or on a digit, one followed by anything else, and a tag after text are kept.
    [TestMethod]
    public void ATextOpeningOnALowercaseTagOrTheInterruptLineIsPassedOver()
    {
        Assert.IsTrue(TranscriptScan.IsPassedOver("<anything-at-all>"));
        Assert.IsTrue(TranscriptScan.IsPassedOver("<a_b-c9 attribute=\"x\">body</a_b-c9> and text after it"));
        Assert.IsTrue(TranscriptScan.IsPassedOver("<bash-input>ls</bash-input>"));
        Assert.IsTrue(TranscriptScan.IsPassedOver("[Request interrupted by user]"));
        Assert.IsTrue(TranscriptScan.IsPassedOver("[Request interrupted by user for tool use] and more"));

        Assert.IsFalse(TranscriptScan.IsPassedOver("<Tag>"));
        Assert.IsFalse(TranscriptScan.IsPassedOver("<3 bees"));
        Assert.IsFalse(TranscriptScan.IsPassedOver("<br/>"));
        Assert.IsFalse(TranscriptScan.IsPassedOver("<tag"));
        Assert.IsFalse(TranscriptScan.IsPassedOver("text <tag>"));
        Assert.IsFalse(TranscriptScan.IsPassedOver("[Request interrupted by user"));
    }

    // The trim is the binary's: it takes U+FEFF and leaves U+0085, where string.Trim does the other.
    [TestMethod]
    public void TheFirstPromptIsTrimmedOfTheWhitespaceTheBinaryTrims()
    {
        Assert.AreEqual("hi", FirstPrompt(Head("\\ufeff hi\\u2028")));
        Assert.AreEqual("\u0085", FirstPrompt(Head("\\u0085")));
    }

    [TestMethod]
    public void TheFirstNonEmptyCommandNameIsTheFallback()
    {
        var head = Head("<command-name></command-name>") + "\n" + Head("<command-name>/compact</command-name>");

        Assert.AreEqual("/compact", FirstPrompt(head));
    }

    // Every user text in the kept transcripts that the rule passes over: the /clear, /compact and
    // /rename command lines, which leave their names as the fallback, two interrupt lines, and the one
    // task-notification the binary wrote as a user prompt. The caveat before each command line is
    // isMeta and never read.
    [TestMethod]
    public void TheRecordedTextsPassedOverAreThreeCommandsTwoInterruptsAndATaskNotification()
    {
        var passed = Transcripts.SessionIds()
            .SelectMany(session => Transcripts.EntriesOf(session))
            .Where(entry => entry.Type == "user" && !entry.Flag("isMeta") && !entry.Flag("isCompactSummary") && !entry.CarriesToolResult())
            .SelectMany(entry => entry.TextBlocks())
            .Select(text => UnicodeText.Trim(text.Replace('\n', ' ')))
            .Where(TranscriptScan.IsPassedOver)
            .ToArray();
        var fallbacks = Transcripts.SessionIds()
            .SelectMany(session => Transcripts.EntriesOf(session))
            .Select(entry =>
            {
                string? fallback = null;
                return TranscriptScan.Prompt(entry, ref fallback) is null ? fallback : null;
            })
            .Where(fallback => fallback is not null)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.AreEqual(6, passed.Length, string.Join(" | ", passed));
        Assert.AreEqual(3, passed.Count(text => text.StartsWith("<command-name>", StringComparison.Ordinal)));
        Assert.AreEqual(2, passed.Count(text => text == "[Request interrupted by user for tool use]"));
        Assert.AreEqual(1, passed.Count(text => text.StartsWith("<task-notification> <task-id>", StringComparison.Ordinal)));
        CollectionAssert.AreEqual(new[] { "/clear", "/compact", "/rename" }, fallbacks);
    }

    // The task-notification comes after its session's first prompt, so no recorded session's first
    // prompt or summary moves with it. Alone it is no prompt: a transcript of it alone has no summary
    // at all.
    [TestMethod]
    public void TheRecordedTaskNotificationIsNoPrompt()
    {
        const string session = "24ab0d44-a6b5-40c0-98fc-cc43b8ff3c9f";
        var notification = Transcripts.EntriesOf(session)
            .Single(entry => entry.TextBlocks() is [var text] && text.StartsWith("<task-notification>", StringComparison.Ordinal));
        string? fallback = null;

        Assert.IsNull(TranscriptScan.Prompt(notification, ref fallback));
        Assert.IsNull(fallback);
        Assert.IsNull(TranscriptScan.Info(session, [notification], [notification], DateTimeOffset.UnixEpoch, 1, null));
        StringAssert.StartsWith(Info(session).FirstPrompt, "Use the Agent tool with run_in_background set to true");
    }

    [TestMethod]
    public void AnEmptyOrAssistantOnlyHeadYieldsNoPrompt()
    {
        Assert.AreEqual("", FirstPrompt(""));
        Assert.AreEqual("", FirstPrompt("""{"type":"assistant","message":{"content":"hi"}}"""));
    }

    [TestMethod]
    public void TheTagIsReadOnlyOffATagLine()
    {
        const string tail = """
            {"type":"user","tag":"not-a-tag","message":{"content":[{"type":"tool_use","input":{"tag":"myapp:v2"}}]}}
            {"type":"tag","tag":"release","sessionId":"s"}
            """;

        Assert.AreEqual("release", TranscriptScan.Tag(TranscriptScan.Entries(tail)));
        Assert.IsNull(TranscriptScan.Tag(TranscriptScan.Entries("""{"type":"user","tag":"x","message":{"content":[{"input":{"tag":"myapp:v2"}}]}}""")));
    }

    [TestMethod]
    public void TheLastTagLineWinsAndAnEmptyTagClearsIt()
    {
        const string tail = """
            {"type":"tag","tag":"first","sessionId":"s"}
            {"type":"tag","tag":"second","sessionId":"s"}
            """;

        Assert.AreEqual("second", TranscriptScan.Tag(TranscriptScan.Entries(tail)));
        Assert.IsNull(TranscriptScan.Tag(TranscriptScan.Entries(tail + "\n" + """{"type":"tag","tag":"","sessionId":"s"}""")));
    }

    [TestMethod]
    public void CreatedAtIsTheFirstTimestampInTheHeadWhereverItSits()
    {
        const string head = """
            {"type":"permission-mode","mode":"normal"}
            {"type":"queue-operation","timestamp":"2026-01-15T10:30:00.000Z"}
            {"type":"user","timestamp":"2026-01-15T11:30:00.000Z"}
            """;

        Assert.AreEqual(1768473000000L, TranscriptScan.CreatedAt(TranscriptScan.Entries(head)));
    }

    [TestMethod]
    public void TheTrailingZAndTheExplicitOffsetParseToTheSameInstant()
    {
        Assert.AreEqual(1704067200000L, TranscriptScan.EpochMs("2024-01-01T00:00:00.000Z"));
        Assert.AreEqual(1704067200000L, TranscriptScan.EpochMs("2024-01-01T00:00:00.000+00:00"));
        Assert.AreEqual(1704067200000L, TranscriptScan.EpochMs("2024-01-01T02:00:00+02:00"));
    }

    // The binary writes one form, a date and a time with its offset. A time with no offset names no
    // instant, and a date alone, a space for the T and a short date are not that form: all are null.
    [TestMethod]
    public void ATimestampNotInTheFormTheBinaryWritesIsNull()
    {
        Assert.IsNull(TranscriptScan.EpochMs("2024-01-01T00:00:00.000"));
        Assert.IsNull(TranscriptScan.EpochMs("2024-01-02"));
        Assert.IsNull(TranscriptScan.EpochMs("2024-01-02 03:04:05Z"));
        Assert.IsNull(TranscriptScan.EpochMs("1/2/2024"));
        Assert.IsNull(TranscriptScan.EpochMs("Jan 2, 2024"));
        Assert.IsNull(TranscriptScan.EpochMs("not-a-valid-iso-date"));
        Assert.IsNull(TranscriptScan.EpochMs(null));
        Assert.IsNull(TranscriptScan.CreatedAt(TranscriptScan.Entries("""{"type":"x"}""")));
    }

    // The limit counts UTF-16 units, as the binary's length does. A cut through a surrogate pair
    // drops the high half the cut leaves last; 150 astral characters are 300 units, over the limit.
    [TestMethod]
    public void TruncateCountsUtf16UnitsAndDropsTheHighSurrogateACutLeavesLast()
    {
        var throughAPair = new string('x', 199) + "\U0001F600" + "tail";
        var astral = string.Concat(Enumerable.Repeat("\U0001F600", 150));
        var atLimit = new string('x', 200);

        Assert.AreEqual(new string('x', 199) + "…", TranscriptScan.Truncate(throughAPair));
        Assert.AreEqual(string.Concat(Enumerable.Repeat("\U0001F600", 100)) + "…", TranscriptScan.Truncate(astral));
        Assert.AreEqual(atLimit, TranscriptScan.Truncate(atLimit));
    }

    // The cut is trimmed as JavaScript trims: U+FEFF goes, and U+001C and U+0085, which it does not
    // count as whitespace, stay before the ellipsis.
    [TestMethod]
    public void TruncateTrimsTheCutAsJavaScriptTrims()
    {
        Assert.AreEqual(new string('a', 199) + "\u001c…", TranscriptScan.Truncate(new string('a', 199) + "\u001c" + new string('b', 10)));
        Assert.AreEqual(new string('a', 199) + "\u0085…", TranscriptScan.Truncate(new string('a', 199) + "\u0085" + new string('b', 10)));
        Assert.AreEqual(new string('a', 199) + "…", TranscriptScan.Truncate(new string('a', 199) + "\uFEFF" + new string('b', 10)));
    }

    // This library's step, not the binary's: a string it hands out holds no half of a pair, so a
    // low surrogate the cut text carries alone becomes U+FFFD. The binary's cut keeps it.
    [TestMethod]
    public void TruncateHandsOutNoLoneSurrogateWhereTheBinarysCutKeepsOne() =>
        Assert.AreEqual("\uFFFD" + new string('a', 199) + "…", TranscriptScan.Truncate("\uDE00" + new string('a', 210)));

    // Any head line that says it is a sidechain makes the transcript no session of the listing's.
    [TestMethod]
    public void ASessionWithASidechainLineInItsHeadIsNoRow()
    {
        var head = TranscriptScan.Entries("""
            {"type":"user","message":{"content":"x"}}
            {"isSidechain":true,"type":"user","message":{"content":"y"}}
            """);

        Assert.IsNull(TranscriptScan.Info("s", head, head, DateTimeOffset.UnixEpoch, 1, null));
    }

    [TestMethod]
    public void AMetadataOnlySessionWithNoSummaryAtAllIsNoRow()
    {
        var head = TranscriptScan.Entries("""{"type":"mode","mode":"normal","sessionId":"s"}""");

        Assert.IsNull(TranscriptScan.Info("s", head, head, DateTimeOffset.UnixEpoch, 1, null));
    }

    [TestMethod]
    public void TheCwdFallsBackToTheProjectPath()
    {
        var head = TranscriptScan.Entries("""{"type":"user","message":{"role":"user","content":"hi"}}""");

        var info = TranscriptScan.Info("s", head, head, DateTimeOffset.FromUnixTimeMilliseconds(7), 9, "/projects/here");

        Assert.IsNotNull(info);
        Assert.AreEqual("/projects/here", info.WorkingDirectory);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeMilliseconds(7), info.LastModified);
        Assert.AreEqual(9, info.SizeBytes);
        Assert.AreEqual("hi", info.Summary);
    }

    [TestMethod]
    public void ACustomTitleInTheHeadBeatsAnAiTitleInTheTail()
    {
        var head = TranscriptScan.Entries("""{"type":"custom-title","customTitle":"mine","sessionId":"s"}""");
        var tail = TranscriptScan.Entries("""{"type":"ai-title","aiTitle":"theirs","sessionId":"s"}""");

        var info = TranscriptScan.Info("s", head, tail, DateTimeOffset.UnixEpoch, 1, null);

        Assert.AreEqual("mine", info?.CustomTitle);
        Assert.AreEqual("mine", info?.Summary);
    }

    // Each title, the last prompt and the summary are read off the line the binary writes them on:
    // the same key on another kind of line is not one.
    [TestMethod]
    public void ATitleOrSummaryKeyOnAnotherKindOfLineIsNotRead()
    {
        var head = TranscriptScan.Entries("""
            {"type":"user","customTitle":"a","aiTitle":"b","lastPrompt":"c","summary":"d","message":{"role":"user","content":"first"}}
            """);

        var info = TranscriptScan.Info("s", head, head, DateTimeOffset.UnixEpoch, 1, null);

        Assert.IsNull(info?.CustomTitle);
        Assert.AreEqual("first", info?.Summary);
    }

    [TestMethod]
    public void ALastPromptInTheTailBeatsTheFirstPrompt()
    {
        var head = TranscriptScan.Entries("""{"type":"user","message":{"role":"user","content":"first"}}""");
        var tail = TranscriptScan.Entries("""{"type":"last-prompt","lastPrompt":"latest","sessionId":"s"}""");

        var info = TranscriptScan.Info("s", head, tail, DateTimeOffset.UnixEpoch, 1, null);

        Assert.AreEqual("latest", info?.Summary);
        Assert.AreEqual("first", info?.FirstPrompt);
    }

    [TestMethod]
    public void ATailGitBranchBeatsTheHeadOne()
    {
        var head = TranscriptScan.Entries("""{"type":"user","gitBranch":"old","message":{"role":"user","content":"hi"}}""");
        var tail = TranscriptScan.Entries("""
            {"type":"user","gitBranch":"new","message":{"role":"user","content":"hi"}}
            {"type":"assistant","gitBranch":"newest","message":{"role":"assistant","content":"hi"}}
            """);

        Assert.AreEqual("newest", TranscriptScan.Info("s", head, tail, DateTimeOffset.UnixEpoch, 1, null)?.GitBranch);
    }

    [TestMethod]
    public void EveryRecordedSessionReportsTheAiTitleTheBinaryWrote()
    {
        var expected = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["0851c866-1c1b-4661-b60b-b734c45296dd"] = "Pong",
            ["0f467c9c-7240-4183-8657-66c5dc288e2a"] = "Read notes.txt first line",
            ["17a5a07e-50c8-46aa-a329-da0fa774c768"] = "File operations and subagent test",
            ["24ab0d44-a6b5-40c0-98fc-cc43b8ff3c9f"] = "Background subagent launch test",
            ["258cdee4-0fd6-4b5e-a1fe-22601197b9a2"] = "Pong",
            ["3c542678-a922-4eab-82c3-a5bb3440454b"] = "Word pong letter count",
            ["40bf1e72-e8c2-4684-acd0-1b40fd59f3f1"] = null,
            ["41603023-fa17-4d91-a02b-10c70e1bca09"] = "Read notes.txt first line",
            ["474e0e86-4835-4b31-9f46-487185450a42"] = "kept title",
            ["4ca46a1b-0db6-4e0b-8cdb-0c98b59472c8"] = null,
            ["599be1d3-f7a1-4a55-bdd1-7bc714707377"] = "hello.txt file creation",
            ["5a264882-daef-4898-ba93-b67e298c4705"] = "Add 2 and 3",
            ["8a1c5516-dd2e-4951-878f-f9eeb6ed54ad"] = "Create denied.txt file",
            ["91afa0ce-07f3-4941-b411-d39b8f7fbaf3"] = "Create denied.txt file",
            ["a4ee9042-2e6c-4f32-8237-94b1609b06af"] = "Read notes.txt first line",
            ["a5258a12-f5f1-4f16-b9ff-a6048b59abb3"] = "Oulu founding year",
            ["b0bf1d40-2f2c-4f0c-a43d-6dfc8e992c24"] = "Background sleep process",
            ["b190f94e-916b-402a-9016-91266e02c461"] = null,
            ["d10f4163-5fab-46d6-948f-8fc4e19b3315"] = "Pong",
            ["d80ad26e-cac0-4fba-87bf-e2d2a7882a30"] = "Echo pong test",
            ["dd69d1c6-e13f-4d37-8e72-8684030b744f"] = "Agent tool with subagent reader",
            ["e67351d1-f522-421e-a7f3-45bfeef09aa5"] = "Pong",
            ["ffeb75c3-4ba5-4849-9688-85e6193729ca"] = "rewound.txt file creation",
        };

        CollectionAssert.AreEqual(expected.Keys.Order(StringComparer.Ordinal).ToArray(), Transcripts.SessionIds().ToArray());

        foreach (var (session, title) in expected)
        {
            Assert.AreEqual(title, Info(session).CustomTitle, session);
        }
    }

    [TestMethod]
    public void EveryRecordedSessionReportsTheFirstPromptTheUserSent()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["0851c866-1c1b-4661-b60b-b734c45296dd"] = "Reply with exactly one word: pong",
            ["0f467c9c-7240-4183-8657-66c5dc288e2a"] = "Read notes.txt in the working directory and reply with its first line only.",
            ["258cdee4-0fd6-4b5e-a1fe-22601197b9a2"] = "Reply with exactly one word: pong",
            ["3c542678-a922-4eab-82c3-a5bb3440454b"] = "Think briefly, then answer: the word is pong; how many letters does it have?",
            ["40bf1e72-e8c2-4684-acd0-1b40fd59f3f1"] = "Say: one",
            ["41603023-fa17-4d91-a02b-10c70e1bca09"] = "Read notes.txt in the working directory and reply with its first line only.",
            ["474e0e86-4835-4b31-9f46-487185450a42"] = "Reply with exactly one word: pong",
            ["4ca46a1b-0db6-4e0b-8cdb-0c98b59472c8"] = "Say: two",
            ["599be1d3-f7a1-4a55-bdd1-7bc714707377"] = "Create a file named hello.txt in the working directory containing exactly: hello",
            ["5a264882-daef-4898-ba93-b67e298c4705"] = "Use the add tool to add 2 and 3, then reply with just the number.",
            ["8a1c5516-dd2e-4951-878f-f9eeb6ed54ad"] = "Create a file named denied.txt in the working directory containing exactly: no. If you cannot, reply with exactly the word: denied",
            ["91afa0ce-07f3-4941-b411-d39b8f7fbaf3"] = "Create a file named denied.txt in the working directory containing exactly: no. If you cannot, reply with exactly the word: denied",
            ["a4ee9042-2e6c-4f32-8237-94b1609b06af"] = "Read notes.txt in the working directory and reply with its first line only.",
            ["a5258a12-f5f1-4f16-b9ff-a6048b59abb3"] = "Search the web for: what year was the Finnish city of Oulu founded. Reply with the year only.",
            ["b0bf1d40-2f2c-4f0c-a43d-6dfc8e992c24"] = "Use the Bash tool with run_in_background set to true to run: sleep 300. Then reply with exactly the word: started",
            ["b190f94e-916b-402a-9016-91266e02c461"] = "Say: one",
            ["d10f4163-5fab-46d6-948f-8fc4e19b3315"] = "Reply with exactly one word: pong",
            ["d80ad26e-cac0-4fba-87bf-e2d2a7882a30"] = "Use the echo tool with the text pong, then reply with exactly what it returned.",
            ["dd69d1c6-e13f-4d37-8e72-8684030b744f"] = "Use the Agent tool with subagent_type reader, run_in_background false, and the prompt: go. Then reply with exactly the subagent's answer.",
            ["e67351d1-f522-421e-a7f3-45bfeef09aa5"] = "Reply with exactly one word: pong",
            ["ffeb75c3-4ba5-4849-9688-85e6193729ca"] = "Create a file named rewound.txt in the working directory containing exactly: before",
        };

        foreach (var (session, prompt) in expected)
        {
            Assert.AreEqual(prompt, Info(session).FirstPrompt, session);
        }
    }

    [TestMethod]
    public void TheTwoLongestRecordedPromptsAreTruncatedToTheLimit()
    {
        foreach (var session in new[] { "17a5a07e-50c8-46aa-a329-da0fa774c768", "24ab0d44-a6b5-40c0-98fc-cc43b8ff3c9f" })
        {
            var prompt = Info(session).FirstPrompt;

            Assert.IsNotNull(prompt);
            // 200 characters, the trailing whitespace of that slice trimmed, then the ellipsis.
            Assert.IsTrue(prompt.Length is > 190 and <= 201, session);
            Assert.AreEqual('…', prompt[^1], session);
            Assert.IsFalse(char.IsWhiteSpace(prompt[^2]), session);
        }
    }

    [TestMethod]
    public void TheOneSessionWithNeitherTitleNorLastPromptFallsBackToItsFirstPrompt()
    {
        var info = Info("40bf1e72-e8c2-4684-acd0-1b40fd59f3f1");

        Assert.IsNull(info.CustomTitle);
        Assert.AreEqual("Say: one", info.FirstPrompt);
        Assert.AreEqual("Say: one", info.Summary);
    }

    [TestMethod]
    public void TheResetSessionTakesTheRealPromptAfterTheSlashCommandNotTheFallback()
    {
        var info = Info("4ca46a1b-0db6-4e0b-8cdb-0c98b59472c8");

        Assert.AreEqual("Say: two", info.FirstPrompt);
        Assert.AreEqual("Say: two", info.Summary);
    }

    // One recorded session was tagged through the session client and then resumed: the session
    // client's tag line, and the one the binary wrote after the resumed turn as it appended the
    // session's metadata again. No other carries a tag.
    [TestMethod]
    public void EveryRecordedSessionReportsTheCwdBranchTagAndCreationTimeTheBinaryWrote()
    {
        foreach (var session in Transcripts.SessionIds())
        {
            var info = Info(session);

            Assert.AreEqual("/tmp/claude-agent-fixtures/cwd", info.WorkingDirectory, session);
            Assert.AreEqual("HEAD", info.GitBranch, session);
            Assert.AreEqual(session == "0851c866-1c1b-4661-b60b-b734c45296dd" ? "kept" : null, info.Tag, session);
            Assert.IsTrue(info.CreatedAt > DateTimeOffset.UnixEpoch.AddYears(30), session);
            Assert.IsTrue(info.CreatedAt <= info.LastModified, session);
        }
    }

    // The tag line the listing reads is one the binary writes: the session client wrote the first tag
    // line, between the two runs, and the binary wrote the second after the resumed turn's Read,
    // among the metadata it appends again.
    [TestMethod]
    public void TheTaggedTranscriptCarriesATagLineTheBinaryWroteAfterTheResumedTurn()
    {
        var entries = Transcripts.EntriesOf("0851c866-1c1b-4661-b60b-b734c45296dd");
        var tags = Enumerable.Range(0, entries.Length).Where(index => entries[index].Type == "tag").ToArray();
        var read = entries.IndexOf(entries.First(entry => entry.CarriesToolResult()));

        Assert.AreEqual(2, tags.Length);
        Assert.IsTrue(tags[0] < read && read < tags[1], $"tags at {tags[0]} and {tags[1]}, the read at {read}");
        Assert.AreEqual("ai-title", entries[tags[1] - 1].Type);
    }

    // A user line whose string content is the JSON string body given, escapes and all.
    private static string Head(string content) =>
        "{\"type\":\"user\",\"message\":{\"role\":\"user\",\"content\":\"" + content + "\"}}";

    private static string FirstPrompt(string head) => TranscriptScan.FirstPrompt(TranscriptScan.Entries(head));

    private static SessionInfo Info(string session)
    {
        var entries = Transcripts.EntriesOf(session);
        var mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(Transcripts.PathOf(session)), TimeSpan.Zero);

        var info = TranscriptScan.Info(
            session,
            entries,
            entries,
            mtime,
            new FileInfo(Transcripts.PathOf(session)).Length,
            Transcripts.Cwd);

        Assert.IsNotNull(info, session);
        return info;
    }
}
