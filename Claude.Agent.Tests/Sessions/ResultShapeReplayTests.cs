using System.Text.Json;
using Claude.Agent.Tests.Replay;
using Claude.Agent.Tests.Wire;

namespace Claude.Agent.Tests.Sessions;

[TestClass]
public sealed class ResultShapeReplayTests
{
    private const string DeferPrompt = "Read notes.txt in the working directory and reply with its first line only.";

    private const string StructuredPrompt =
        "Think briefly, then answer: the word is pong; how many letters does it have?";

    private const string WebSearchPrompt =
        "Search the web for: what year was the Finnish city of Oulu founded. Reply with the year only.";

    private const string ThinkingPrompt =
        "A train leaves at 14:35 and arrives at 17:10. How many minutes is the trip? Think it through, then reply with just the number.";

    private const string NotesPath = "/tmp/claude-agent-fixtures/cwd/notes.txt";
    private const string DeferredToolUseId = "toolu_01MFnMzfrwnNkHGzqjid9LNp";
    private const string StructuredToolUseId = "toolu_01Kp4fL5m5AfvNMESBA27mCo";
    private const string StructuredText = "The word \"pong\" has **4 letters**: p-o-n-g.";
    private const string WebSearchToolUseId = "toolu_01J6FV6tf3GiPK4NwMZH4zds";
    private const string SearchQuery = "Finnish city Oulu founded year";
    private const string SearchFinding = "The Finnish city of Oulu was founded on April 8, 1605 by King Charles IX of Sweden.";

    private const string Schema =
        """{"type": "object", "properties": {"word": {"type": "string"}, "letters": {"type": "integer"}}, "required": ["word", "letters"], "additionalProperties": false}""";

    private const string WebSearchAnswer = """
        1605

        Sources:
        - [History of Oulu | City of Oulu](https://www.ouka.fi/en/history-oulu)
        - [Oulu | Wikipedia](https://en.wikipedia.org/wiki/Oulu)
        """;

    private const string ThinkingAnswer = """
        From 14:35 to 17:10:
        - 14:35 to 17:35 = 3 hours = 180 minutes
        - But we arrive at 17:10, which is 25 minutes before 17:35
        - So: 180 - 25 = 155 minutes

        155
        """;

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task ADeferringPreToolUseHookWritesTheDeferDecisionAndTheBinaryStopsTheTurnThere()
    {
        await using var server = FixtureServer.Start("defer");
        using var host = TestHost.Start(server, options: Deferring);

        var seen = await ReplayAsync(host, DeferPrompt);

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);

        await server.WaitForWrites(3, Patience);
        CollectionAssert.AreEqual(
            Fixture.Load("defer").Stdin.Anonymised(),
            server.Written.Anonymised());
    }

    [TestMethod]
    public async Task TheDeferredReadArrivesAsAThinkingBlockAndThenAToolUseOnTheSameApiMessage()
    {
        await using var server = FixtureServer.Start("defer");
        using var host = TestHost.Start(server, options: Deferring);

        var assistants = (await ReplayAsync(host, DeferPrompt)).OfType<AssistantMessage>().ToArray();

        Assert.AreEqual(2, assistants.Length);

        var thinking = (ThinkingBlock)assistants[0].Content.Single();
        Assert.AreEqual(string.Empty, thinking.Thinking);
        Assert.IsTrue(
            thinking.Signature.StartsWith("Es8DCrIBCBIYAipA", StringComparison.Ordinal),
            thinking.Signature);

        var toolUse = (ToolUseBlock)assistants[1].Content.Single();
        Assert.AreEqual(DeferredToolUseId, toolUse.Id);
        Assert.AreEqual("Read", toolUse.Name);
        Assert.AreEqual(NotesPath, toolUse.RawInput.GetProperty("file_path").GetString());
        Assert.AreEqual(assistants[0].ApiMessageId, assistants[1].ApiMessageId);
    }

    [TestMethod]
    public async Task TheResultOfADeferredReadNamesTheToolUseTheBinaryHeldBack()
    {
        await using var server = FixtureServer.Start("defer");
        using var host = TestHost.Start(server, options: Deferring);

        var result = (await ReplayAsync(host, DeferPrompt)).OfType<ResultMessage>().Single();

        Assert.AreEqual("tool_deferred", result.StopReason);
        Assert.AreEqual("tool_deferred", result.TerminalReason);
        Assert.IsNotNull(result.DeferredToolUse);
        Assert.AreEqual(DeferredToolUseId, result.DeferredToolUse.Id);
        Assert.AreEqual("Read", result.DeferredToolUse.Name);
        Assert.AreEqual(NotesPath, result.DeferredToolUse.RawInput.GetProperty("file_path").GetString());
    }

    // Nothing answers the deferred read: the run closes it unanswered just before the result.
    [TestMethod]
    public async Task TheDeferredReadStartsAfterItsAssistantMessageAndClosesUnansweredJustBeforeTheResult()
    {
        await using var server = FixtureServer.Start("defer");
        using var host = TestHost.Start(server, options: Deferring);

        var seen = await ReplayAsync(host, DeferPrompt);

        var snapshots = seen.OfType<ToolCallMessage>().ToArray();
        CollectionAssert.AreEqual(
            new[] { ToolCallStatus.Started, ToolCallStatus.Unanswered },
            snapshots.Select(snapshot => snapshot.ToolCall.Status).ToArray());
        Assert.IsTrue(snapshots.All(snapshot => snapshot.ToolCall.Id == DeferredToolUseId && snapshot.ToolCall.Name == "Read"));

        var started = seen.IndexOf(snapshots[0]);
        Assert.IsInstanceOfType<AssistantMessage>(seen[started - 1]);
        Assert.AreSame(snapshots[1], seen[^2]);
        Assert.IsInstanceOfType<ResultMessage>(seen[^1]);

        var unanswered = snapshots[1].ToolCall;
        Assert.AreEqual(NotesPath, unanswered.RawInput.GetProperty("file_path").GetString());
        Assert.AreEqual(0, unanswered.Content.Count);
        Assert.IsNull(unanswered.RawOutput);
        Assert.IsNull(unanswered.Error);
        Assert.IsNull(snapshots[1].Raw);
    }

    [TestMethod]
    public async Task AJsonSchemaOutputFormatPutsTheSchemaOnTheArgvInTheWiresJsonSpacing()
    {
        await using var server = FixtureServer.Start("structured");
        using var host = TestHost.Start(server, options: Structured);

        await ReplayAsync(host, StructuredPrompt);

        AssertArgv(host, "structured");
    }

    [TestMethod]
    public async Task TheStructuredRunWritesAThinkingBlockATextAndThenAToolUseOfTheStructuredOutputToolOnTheSameApiMessage()
    {
        await using var server = FixtureServer.Start("structured");
        using var host = TestHost.Start(server, options: Structured);

        var seen = await ReplayAsync(host, StructuredPrompt);

        var assistants = seen.OfType<AssistantMessage>().ToArray();
        CollectionAssert.AreEqual(
            new[] { typeof(ThinkingBlock), typeof(TextBlock), typeof(ToolUseBlock) },
            assistants.Select(message => message.Content.Single().GetType()).ToArray());
        Assert.IsTrue(assistants.All(message => message.ApiMessageId == assistants[0].ApiMessageId));
        Assert.AreEqual(string.Empty, ((ThinkingBlock)assistants[0].Content.Single()).Thinking);
        Assert.AreEqual(StructuredText, assistants[1].Text);

        var toolUse = (ToolUseBlock)assistants[2].Content.Single();
        Assert.AreEqual(StructuredToolUseId, toolUse.Id);
        Assert.AreEqual("StructuredOutput", toolUse.Name);
        Assert.AreEqual("pong", toolUse.RawInput.GetProperty("word").GetString());
        Assert.AreEqual(4, toolUse.RawInput.GetProperty("letters").GetInt32());

        var toolResult = (ToolResultBlock)seen.OfType<UserMessage>().Single().Content.Single();
        Assert.AreEqual(StructuredToolUseId, toolResult.ToolUseId);
        Assert.AreEqual("Structured output provided successfully", toolResult.Content.Text());

        await server.WaitForWrites(2, Patience);
        CollectionAssert.AreEqual(
            Fixture.Load("structured").Stdin.Anonymised(),
            server.Written.Anonymised());
    }

    // What the structured result carries beside its text — the answer itself — is read through the
    // trace: Sessions/StructuredOutputTests.cs.
    [TestMethod]
    public async Task TheStructuredResultsTextIsTheAnswerAsCompactJson()
    {
        await using var server = FixtureServer.Start("structured");
        using var host = TestHost.Start(server, options: Structured);

        var result = (await ReplayAsync(host, StructuredPrompt)).OfType<ResultMessage>().Single();

        Assert.AreEqual("""{"word":"pong","letters":4}""", result.Result);
    }

    [TestMethod]
    public async Task WebSearchArrivesAsAClientToolUseAndNeverAsAServerToolUseBlock()
    {
        await using var server = FixtureServer.Start("web-search");
        using var host = TestHost.Start(server, options: Searching);

        var seen = await ReplayAsync(host, WebSearchPrompt);
        var assistants = seen.OfType<AssistantMessage>().ToArray();

        var toolUse = assistants.SelectMany(message => message.Content).OfType<ToolUseBlock>().Single();
        Assert.AreEqual(WebSearchToolUseId, toolUse.Id);
        Assert.AreEqual("WebSearch", toolUse.Name);
        Assert.AreEqual(SearchQuery, toolUse.RawInput.GetProperty("query").GetString());
        Assert.IsFalse(assistants.SelectMany(message => message.Content).Any(block => block is ServerToolUseBlock));

        await server.WaitForWrites(2, Patience);
        CollectionAssert.AreEqual(
            Fixture.Load("web-search").Stdin.Anonymised(),
            server.Written.Anonymised());
    }

    [TestMethod]
    public async Task TheWebSearchResultComesBackOnAUserFrameAndTheSecondApiMessageIsTheAnswer()
    {
        await using var server = FixtureServer.Start("web-search");
        using var host = TestHost.Start(server, options: Searching);

        var seen = await ReplayAsync(host, WebSearchPrompt);

        var toolResult = (ToolResultBlock)seen.OfType<UserMessage>().Single().Content.Single();
        Assert.AreEqual(WebSearchToolUseId, toolResult.ToolUseId);
        var found = toolResult.Content.Text();
        Assert.IsTrue(found.Contains(SearchFinding, StringComparison.Ordinal), found);

        var assistants = seen.OfType<AssistantMessage>().ToArray();
        var apiMessages = assistants.Select(message => message.ApiMessageId).Distinct().ToArray();
        Assert.AreEqual(2, apiMessages.Length);

        var answer = assistants[^1];
        Assert.AreEqual(apiMessages[1], answer.ApiMessageId);
        Assert.IsInstanceOfType<TextBlock>(answer.Content.Single());
        Assert.AreEqual(WebSearchAnswer, answer.Text);
        Assert.AreEqual("end_turn", seen.OfType<ResultMessage>().Single().StopReason);
    }

    // A thinking budget does not make haiku write its thinking: the block carries no text, only its signature.
    [TestMethod]
    public async Task AThinkingBudgetBringsAThinkingBlockWithEmptyTextAndTheRecordedSignatureAheadOfTheAnswer()
    {
        await using var server = FixtureServer.Start("thinking");
        using var host = TestHost.Start(server, options: Thinking);

        var seen = await ReplayAsync(host, ThinkingPrompt);
        var assistants = seen.OfType<AssistantMessage>().ToArray();

        Assert.AreEqual(2, assistants.Length);
        Assert.AreEqual(assistants[0].ApiMessageId, assistants[1].ApiMessageId);

        var thinking = (ThinkingBlock)assistants[0].Content.Single();
        Assert.AreEqual(string.Empty, thinking.Thinking);
        Assert.AreEqual(RecordedSignature("thinking"), thinking.Signature);

        Assert.IsInstanceOfType<TextBlock>(assistants[1].Content.Single());
        Assert.AreEqual(ThinkingAnswer, assistants[1].Text);
        Assert.AreEqual(ThinkingAnswer, seen.OfType<ResultMessage>().Single().Result);

        await server.WaitForWrites(2, Patience);
        CollectionAssert.AreEqual(
            Fixture.Load("thinking").Stdin.Anonymised(),
            server.Written.Anonymised());
    }

    private static void Recording(ClaudeAgentSessionOptions options)
    {
        options.WorkingDirectory = "/tmp/claude-agent-fixtures/cwd";
        options.SettingSources = [];
        options.Model = "haiku";
    }

    private static void Deferring(ClaudeAgentSessionOptions options)
    {
        Recording(options);
        options.Tools = ["Read"];
        options.AllowedTools.Add("Read");
        options.MaxTurns = 3;
        options.Effort = Effort.Low;
        options.AddHook(
            HookEvent.PreToolUse,
            "Read",
            (_, _) => Task.FromResult(
                new HookOutput { Decision = PermissionDecision.Defer }));
    }

    private static void Structured(ClaudeAgentSessionOptions options)
    {
        Recording(options);
        options.MaxTurns = 1;
        options.Effort = Effort.Medium;
        options.Thinking = ThinkingOptions.WithBudget(1024);
        options.OutputSchema = Recorded.Json(Schema);
    }

    private static void Searching(ClaudeAgentSessionOptions options)
    {
        Recording(options);
        options.Tools = ["WebSearch"];
        options.AllowedTools.Add("WebSearch");
        options.MaxTurns = 2;
        options.Effort = Effort.Low;
    }

    private static void Thinking(ClaudeAgentSessionOptions options)
    {
        Recording(options);
        options.MaxTurns = 1;
        options.Thinking = ThinkingOptions.WithBudget(2048);
    }

    private static async Task<List<AgentMessage>> ReplayAsync(TestHost host, string prompt)
    {
        var seen = new List<AgentMessage>();
        await foreach (var message in host.Client.QueryAsync(prompt))
        {
            seen.Add(message);
        }

        return seen;
    }

    // The signature on the fixture's first assistant frame, as the binary wrote it.
    private static string? RecordedSignature(string fixture)
    {
        using var document = JsonDocument.Parse(Fixture.Line(fixture, "assistant"));
        return document.RootElement.GetProperty("message").GetProperty("content")[0].GetProperty("signature").GetString();
    }

    private static void AssertArgv(TestHost host, string fixture)
    {
        var asked = host.Transports.Requests[0];
        CollectionAssert.AreEqual(Fixture.Load(fixture).Argv.ToArray(), asked.Argv.ToArray(), $"{fixture}.argv.json");
    }
}
