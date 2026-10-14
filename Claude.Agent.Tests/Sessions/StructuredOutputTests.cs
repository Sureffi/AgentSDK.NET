using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Claude.Agent.Recorder;
using Claude.Agent.Tests.Replay;
using Claude.Agent.Tests.Wire;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace Claude.Agent.Tests.Sessions;

// The answer a schema asked for, read off the trace as a type. The `structured` and `schema-type`
// recordings carry one: the binary calls the StructuredOutput tool and puts the same object on the
// result. Reading it is the platform's, so what is held here is which answer is read, that it reads
// back as the type its schema was taken from, when there is none, and that an answer that does not
// fit is not none. `schema-nullable` is what the binary does with a schema whose root may be null.
[TestClass]
public sealed class StructuredOutputTests
{
    private const string StructuredPrompt =
        "Think briefly, then answer: the word is pong; how many letters does it have?";

    private const string TrivialPrompt = "Reply with exactly one word: pong";

    private const string SchemaPrompt = "Report on the word: pong";

    [TestMethod]
    public async Task TheRecordedAnswerIsReadAsTheRecordTypeItsSchemaAskedFor()
    {
        var trace = await StructuredTrace();

        var answer = trace.GetStructuredOutput<PongAnswer>();

        Assert.IsNotNull(answer);
        Assert.AreEqual("pong", answer.Word);
        Assert.AreEqual(4, answer.Letters);
    }

    [TestMethod]
    public async Task TheRecordedAnswerIsReadWholeAsAJsonElement()
    {
        var trace = await StructuredTrace();

        var answer = trace.GetStructuredOutput<JsonElement>();

        Assert.AreEqual(JsonValueKind.Object, answer.ValueKind);
        Assert.AreEqual("""{"word":"pong","letters":4}""", answer.GetRawText());
    }

    [TestMethod]
    public async Task TheRecordedAnswerIsReadThroughASourceGeneratedContractToo()
    {
        var trace = await StructuredTrace();

        var answer = trace.GetStructuredOutput(StructuredOutputContext.Default.PongAnswer);

        Assert.IsNotNull(answer);
        Assert.AreEqual("pong", answer.Word);
        Assert.AreEqual(4, answer.Letters);
    }

    // The options handed in are the ones that read: a naming policy alone finds the same answer in a
    // type that spells its members no way in particular.
    [TestMethod]
    public async Task TheAnswerIsReadThroughTheSerializerOptionsHandedIn()
    {
        var trace = await StructuredTrace();

        var answer = trace.GetStructuredOutput<PlainAnswer>(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.IsNotNull(answer);
        Assert.AreEqual("pong", answer.Word);
        Assert.AreEqual(4, answer.Letters);
        Assert.IsNull(trace.GetStructuredOutput<PlainAnswer>()!.Word, "the default options match by case and find nothing");
    }

    [TestMethod]
    public async Task ARunThatAskedForNoSchemaHasNoStructuredOutput()
    {
        await using var server = FixtureServer.Start("trivial");
        using var host = TestHost.Start(server);

        var trace = await host.Client.QueryAsync(TrivialPrompt).WaitForCompletionAsync();

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");

        Assert.IsNull(trace.GetStructuredOutput<PongAnswer>());
        Assert.AreEqual(JsonValueKind.Undefined, trace.GetStructuredOutput<JsonElement>().ValueKind);
        Assert.IsNull(trace.GetStructuredOutput(StructuredOutputContext.Default.PongAnswer));
    }

    // Three ways a result carries none, each the default and none of them a throw: the frame said
    // nothing, the frame said null, and there was no frame at all.
    [TestMethod]
    public void AResultWithNoAnswerInItsFrameAndOneWithNoFrameBothCarryNone()
    {
        (string Case, JsonElement? Raw)[] carrying =
        [
            ("the frame has no such key", Recorded.Json("""{"subtype": "success"}""")),
            ("the frame has it as null", Recorded.Json("""{"structured_output": null}""")),
            ("there is no frame", null),
        ];

        foreach (var (name, raw) in carrying)
        {
            var trace = Trace(raw);

            Assert.IsNull(trace.GetStructuredOutput<PongAnswer>(), $"{name}: it was read as an answer");
            Assert.AreEqual(JsonValueKind.Undefined, trace.GetStructuredOutput<JsonElement>().ValueKind, name);
        }
    }

    // An answer that is there and does not fit is not none: the platform's JsonException says where
    // it stopped, and the same trace still hands the answer out whole.
    [TestMethod]
    public async Task AnAnswerThatDoesNotFitTheTypeThrowsAndIsNotReadAsNone()
    {
        var trace = await StructuredTrace();

        var failure = Assert.ThrowsExactly<JsonException>(() => trace.GetStructuredOutput<MisfitAnswer>());

        Assert.AreEqual("$.letters", failure.Path);
        Assert.AreEqual(JsonValueKind.Object, trace.GetStructuredOutput<JsonElement>().ValueKind);
    }

    [TestMethod]
    public void ReadingThroughNoContractAtAllIsRefused()
    {
        var trace = Trace(Recorded.Json("""{"structured_output": {"word": "pong", "letters": 4}}"""));

        Assert.ThrowsExactly<ArgumentNullException>(() => trace.GetStructuredOutput<PongAnswer>(jsonTypeInfo: null!));
    }

    // What the platform throws comes out as it threw it: a contract it refuses and a type it does not
    // read, as well as an answer that does not fit, and nothing of this library's wraps them.
    [TestMethod]
    public void AContractThePlatformRefusesThrowsWhatThePlatformThrows()
    {
        var trace = Trace(Recorded.Json("""{"structured_output": {"word": "pong", "letters": 4}}"""));

        Assert.ThrowsExactly<InvalidOperationException>(() => trace.GetStructuredOutput<CollidingAnswer>());
        Assert.ThrowsExactly<NotSupportedException>(() => trace.GetStructuredOutput<UnreadableAnswer>());
    }

    // The schema taken from the class is the one the binary answered on, and the answer reads back as
    // the class through the contract the schema was taken off: by reflection and source-generated.
    [TestMethod]
    public async Task TheAnswerToASchemaTakenFromAClassReadsBackAsThatClass()
    {
        var trace = await SchemaTypeTrace();

        foreach (var answer in new[] { trace.GetStructuredOutput<WordReport>(), trace.GetStructuredOutput(WordReportContext.Default.WordReport) })
        {
            Assert.IsNotNull(answer);
            Assert.AreEqual("pong", answer.Word);
            Assert.AreEqual(4, answer.Letters);
            CollectionAssert.AreEqual(new[] { "p", "o", "n", "g" }, answer.Spelling.ToArray());
            Assert.AreEqual(PartOfSpeech.Noun, answer.PartOfSpeech);
            Assert.AreEqual(1, answer.Counts.Vowels);
            Assert.AreEqual(3, answer.Counts.Consonants);
            Assert.AreEqual("Classic arcade game or the sound a paddle makes hitting a ball", answer.Remark);
        }
    }

    // A struct with the class's members is taken into the same schema, its root an object as it is
    // for its Nullable<T>, so the recording answers it as well, and the answer reads back as the
    // struct.
    [TestMethod]
    public async Task TheAnswerToASchemaTakenFromAStructReadsBackAsThatStruct()
    {
        var byClass = new ClaudeAgentSessionOptions();
        byClass.SetOutputSchema<WordReport>();
        var byStruct = new ClaudeAgentSessionOptions();
        byStruct.SetOutputSchema<WordReportValue>();
        var byNullableStruct = new ClaudeAgentSessionOptions();
        byNullableStruct.SetOutputSchema<WordReportValue?>();
        var generated = new ClaudeAgentSessionOptions();
        generated.SetOutputSchema(WordReportContext.Default.WordReportValue);

        foreach (var options in new[] { byStruct, byNullableStruct, generated })
        {
            Assert.AreEqual(byClass.OutputSchema!.Value.GetRawText(), options.OutputSchema!.Value.GetRawText());
        }

        var trace = await SchemaTypeTrace();

        foreach (var answer in new[] { trace.GetStructuredOutput<WordReportValue>(), trace.GetStructuredOutput(WordReportContext.Default.WordReportValue) })
        {
            Assert.AreEqual("pong", answer.Word);
            Assert.AreEqual(4, answer.Letters);
            CollectionAssert.AreEqual(new[] { "p", "o", "n", "g" }, answer.Spelling.ToArray());
            Assert.AreEqual(PartOfSpeech.Noun, answer.PartOfSpeech);
            Assert.AreEqual(3, answer.Counts.Consonants);
        }

        Assert.AreEqual("pong", trace.GetStructuredOutput<WordReportValue?>()!.Value.Word);
    }

    // A root of ["object", "null"] went out and the API refused the request: the binary wrote the
    // refusal as a synthetic assistant message and ended the turn with an error result, and there was
    // no answer to read. This is why a schema taken from a type has "object" at its root, and why the
    // recorded schema handed in raw is refused now, on the defaults when the client is made and on a
    // call as it is made, before any process starts. The recording is replayed on the options it was
    // recorded with but for the schema, which is WordReport's as the type gives it, the recorded one
    // but for the root's type: the schema goes out on argv alone, and a replay sends no argv, so what
    // the client writes is the recorded stdin line for line. Wire/CommandLineFixtureTests.cs rebuilds
    // the recorded argv.
    [TestMethod]
    public async Task TheBinaryEndsATurnWhoseSchemaHasANullableRootWithTheApisRefusal()
    {
        const string Refusal = "API Error: 400 tools.9.custom.input_schema.type: Input should be 'object'";
        var recorded = new ClaudeAgentClientOptions();
        NullableRootSchema(recorded.Defaults);
        var refusedOnTheDefaults = Assert.ThrowsExactly<ArgumentException>(
            () => new ClaudeAgentClient(recorded, null, new FixtureTransportFactory(new FrameCodec()), TestHostEnvironments.Unmade));
        Assert.AreEqual(nameof(ClaudeAgentSessionOptions.OutputSchema), refusedOnTheDefaults.ParamName);

        await using var server = FixtureServer.Start("schema-nullable");
        using var host = TestHost.Start(server, options: TypeSchema);

        var asked = new ClaudeAgentSessionOptions();
        NullableRootSchema(asked);
        var refusedOnTheCall = Assert.ThrowsExactly<ArgumentException>(() => host.Client.QueryAsync(SchemaPrompt, asked));
        Assert.AreEqual(nameof(ClaudeAgentSessionOptions.OutputSchema), refusedOnTheCall.ParamName);
        Assert.IsEmpty(host.Transports.Requests, "a refused call started a session");

        var trace = await host.Client.QueryAsync(SchemaPrompt).WaitForCompletionAsync();

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        var refusal = trace.Messages.OfType<AssistantMessage>().Single();
        Assert.AreEqual(Refusal, refusal.Text);
        Assert.AreEqual("<synthetic>", refusal.Model);
        Assert.AreEqual(AssistantErrorCode.Unknown, refusal.Error);
        Assert.IsTrue(trace.Result.IsError);
        Assert.AreEqual(ResultMessageSubtype.Success, trace.Result.Subtype);
        Assert.AreEqual(400, trace.Result.ApiErrorStatus);
        Assert.AreEqual(TerminalReason.ApiError, trace.Result.TerminalReason);
        Assert.AreEqual(Refusal, trace.Result.Result);
        Assert.IsNull(trace.GetStructuredOutput<WordReport>());
    }

    // A trace built as a caller's test builds one: the result alone, carrying the frame it names.
    private static AgentTrace Trace(JsonElement? raw) =>
        ClaudeAgentModelFactory.AgentTrace([ClaudeAgentModelFactory.ResultMessage(sessionId: "s-1", raw: raw)]);

    private static async Task<AgentTrace> StructuredTrace()
    {
        await using var server = FixtureServer.Start("structured");
        using var host = TestHost.Start(server, options: Structured);

        return await host.Client.QueryAsync(StructuredPrompt).WaitForCompletionAsync();
    }

    private static async Task<AgentTrace> SchemaTypeTrace()
    {
        await using var server = FixtureServer.Start("schema-type");
        using var host = TestHost.Start(server, options: TypeSchema);

        var trace = await host.Client.QueryAsync(SchemaPrompt).WaitForCompletionAsync();

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        return trace;
    }

    private static void TypeSchema(ClaudeAgentSessionOptions options)
    {
        options.WorkingDirectory = "/tmp/claude-agent-fixtures/cwd";
        options.SettingSources = [];
        options.Model = "haiku";
        options.MaxTurns = 2;
        options.Effort = Effort.Low;
        options.SetOutputSchema<WordReport>();
    }

    private static void NullableRootSchema(ClaudeAgentSessionOptions options)
    {
        TypeSchema(options);
        var schema = JsonNode.Parse(options.OutputSchema!.Value.GetRawText())!.AsObject();
        schema["type"] = new JsonArray("object", "null");
        options.OutputSchema = Recorded.Json(schema.ToJsonString());
    }

    private static void Structured(ClaudeAgentSessionOptions options)
    {
        options.WorkingDirectory = "/tmp/claude-agent-fixtures/cwd";
        options.SettingSources = [];
        options.Model = "haiku";
        options.MaxTurns = 1;
        options.Effort = Effort.Medium;
        options.Thinking = ThinkingOptions.WithBudget(1024);
        options.OutputSchema = Recorded.Json(OutputSchemaTests.PongSchema);
    }
}

/// <summary>The shape the `structured` recording's schema asks for, spelled as the binary writes it.</summary>
internal sealed record PongAnswer(
    [property: JsonPropertyName("word")] string Word,
    [property: JsonPropertyName("letters")] int Letters);

/// <summary>The same shape, spelling its members no way in particular: read by a naming policy.</summary>
internal sealed record PlainAnswer(string Word, int Letters);

/// <summary>The same members under the same names, one of them the wrong type.</summary>
internal sealed record MisfitAnswer(
    [property: JsonPropertyName("word")] string Word,
    [property: JsonPropertyName("letters")] IReadOnlyList<string> Letters);

[JsonSerializable(typeof(PongAnswer))]
internal sealed partial class StructuredOutputContext : JsonSerializerContext;

/// <summary><see cref="WordReport"/> as a struct: the same members under the same attributes.</summary>
[Description("A report on one word.")]
internal readonly struct WordReportValue
{
    [Description("The word, as it was given.")]
    [MaxLength(16)]
    public required string Word { get; init; }

    [Description("How many letters the word has.")]
    public required int Letters { get; init; }

    [Description("The word's letters in order, one letter a string.")]
    [Length(1, 16)]
    public required IReadOnlyList<string> Spelling { get; init; }

    [Description("The part of speech the word is most often.")]
    public required PartOfSpeech PartOfSpeech { get; init; }

    public required LetterCounts Counts { get; init; }

    [StringLength(80)]
    public string? Remark { get; init; }
}

[JsonSerializable(typeof(WordReport))]
[JsonSerializable(typeof(WordReportValue))]
internal sealed partial class WordReportContext : JsonSerializerContext;

/// <summary>Two members under one wire name: a contract the platform refuses.</summary>
internal sealed class CollidingAnswer
{
    [JsonPropertyName("word")]
    public string? Word { get; init; }

    [JsonPropertyName("word")]
    public string? Spelling { get; init; }
}

/// <summary>A member of a type the platform does not read or write at all.</summary>
internal sealed record UnreadableAnswer(
    [property: JsonPropertyName("word")] IntPtr Word);
