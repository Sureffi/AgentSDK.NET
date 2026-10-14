using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Claude.Agent.Recorder;
using Claude.Agent.Tests.Replay;
using Claude.Agent.Tests.Wire;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace Claude.Agent.Tests.Sessions;

// A call's evidence is a type, as its answer is. The `ask` recording is haiku asked over
// WordEvidence and answering as WordReport through AskAsync: its replay writes the prompt byte for
// byte from the evidence and rebuilds the argv from the ask's options, and the trace names both
// types. The rest is what an ask writes, what it refuses at the call, and which runs name a type.
[TestClass]
public sealed class AskTests
{
    private const string RecordedCwd = "/tmp/claude-agent-fixtures/cwd";

    [TestMethod]
    public async Task TheAskFixtureIsWrittenFromItsEvidenceAndItsAnswerReadsBackAsTheAnswerType()
    {
        await using var server = FixtureServer.Start("ask");
        using var host = TestHost.Start(server, AsTheAskFixture);

        var trace = await host.Client.AskAsync<WordEvidence, WordReport>(WordEvidence.AskTask, WordEvidence.Recorded).WaitForCompletionAsync();

        await AssertWroteTheRecordedStdin(server);
        CollectionAssert.AreEqual(Fixture.Load("ask").Argv.ToArray(), host.Transports.Last!.Argv.ToArray(), "ask.argv.json");
        var answer = trace.GetStructuredOutput<WordReport>();
        Assert.IsNotNull(answer);
        Assert.AreEqual("pöllö", answer.Word);
        Assert.AreEqual(5, answer.Letters);
        Assert.AreEqual(PartOfSpeech.Noun, answer.PartOfSpeech);
        Assert.AreEqual(typeof(WordEvidence), trace.EvidenceType);
        Assert.AreEqual(typeof(WordReport), trace.AnswerType);
    }

    // The contracts a source generator wrote say what reflection says: the same prompt, the same argv.
    [TestMethod]
    public async Task TheAskThroughSourceGeneratedContractsWritesTheSameRecordedPromptAndArgv()
    {
        await using var server = FixtureServer.Start("ask");
        using var host = TestHost.Start(server, AsTheAskFixture);

        var trace = await host.Client.AskAsync(
            WordEvidence.AskTask,
            WordEvidence.Recorded,
            AskContext.Default.WordEvidence,
            WordReportContext.Default.WordReport).WaitForCompletionAsync();

        await AssertWroteTheRecordedStdin(server);
        CollectionAssert.AreEqual(Fixture.Load("ask").Argv.ToArray(), host.Transports.Last!.Argv.ToArray(), "ask.argv.json");
        Assert.AreEqual("pöllö", trace.GetStructuredOutput(WordReportContext.Default.WordReport)?.Word);
        Assert.AreEqual(typeof(WordEvidence), trace.EvidenceType);
        Assert.AreEqual(typeof(WordReport), trace.AnswerType);
    }

    // The task as given, then the schema and the evidence off the serializer options handed in, a
    // blank line apart, each in its tag, indented two spaces with line feeds; the descriptions in
    // the schema, and the text as the evidence holds it, escaped only where JSON needs it.
    [TestMethod]
    public void ThePromptIsTheTaskThenTheEvidencesSchemaThenTheEvidenceEachInItsTag()
    {
        var contract = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        contract.MakeReadOnly(populateMissingResolver: true);
        var evidence = new SiteEvidence("Sähkö & Putki <Oy>", ["Asentaa \"kaikki\" johdot.\nMyös yöllä."]);

        var prompt = EvidencePrompt.Write("Judge the site.", evidence, (System.Text.Json.Serialization.Metadata.JsonTypeInfo<SiteEvidence>)contract.GetTypeInfo(typeof(SiteEvidence)), null);

        Assert.AreEqual(
            """
            Judge the site.

            <evidence_schema>
            {
              "description": "A company's site, as it was read.",
              "type": "object",
              "properties": {
                "company_name": {
                  "description": "The company's name in the register.",
                  "type": "string"
                },
                "page_texts": {
                  "description": "The text of each page read.",
                  "type": "array",
                  "items": {
                    "type": [
                      "string",
                      "null"
                    ]
                  }
                }
              },
              "required": [
                "company_name",
                "page_texts"
              ],
              "additionalProperties": false
            }
            </evidence_schema>

            <evidence>
            {
              "company_name": "Sähkö & Putki <Oy>",
              "page_texts": [
                "Asentaa \"kaikki\" johdot.\nMyös yöllä."
              ]
            }
            </evidence>
            """,
            prompt);
    }

    // What JSON does not need escaped is not: a no-break space, the line separator, a character
    // outside the Basic Multilingual Plane, a member name outside ASCII. A backslash before a u in
    // the text stays the escaped backslash it is, and a control character stays escaped.
    [TestMethod]
    public void NothingJsonDoesNotNeedEscapedIsEscaped()
    {
        var contract = JsonSerializerOptions.Default;
        var evidence = new PriceEvidence("1\u00a0234,50\u00a0€ 😀 \u2028 \\u00e4 \"x\" \u0007");

        var prompt = EvidencePrompt.Write("Read it.", evidence, (System.Text.Json.Serialization.Metadata.JsonTypeInfo<PriceEvidence>)contract.GetTypeInfo(typeof(PriceEvidence)), null);

        Assert.AreEqual("{\n  \"hinta€\": \"1\u00a0234,50\u00a0€ 😀 \u2028 \\\\u00e4 \\\"x\\\" \\u0007\"\n}", Tagged(prompt, "evidence"));
        StringAssert.Contains(Tagged(prompt, "evidence_schema"), "\"hinta€\": {", StringComparison.Ordinal);
    }

    // A transform that makes the evidence schema's root null is refused, naming the options that
    // carried it.
    [TestMethod]
    public void ATransformThatMakesTheEvidenceSchemasRootNullIsRefused()
    {
        var contract = JsonSerializerOptions.Default;
        var nulling = new System.Text.Json.Schema.JsonSchemaExporterOptions { TransformSchemaNode = (context, node) => context.Path.IsEmpty ? null! : node };

        var refusal = Assert.ThrowsExactly<ArgumentException>(() => EvidencePrompt.Write("Read it.", new PriceEvidence("1"), (System.Text.Json.Serialization.Metadata.JsonTypeInfo<PriceEvidence>)contract.GetTypeInfo(typeof(PriceEvidence)), nulling));

        Assert.AreEqual("exporterOptions", refusal.ParamName);
    }

    // The serializer options and the exporter options handed to AskAsync are the ones the prompt
    // and the answer's schema are written through: the prompt the ask writes is the one written off
    // that contract, in snake case and with list items never null, and the argv's schema is
    // SetOutputSchema's through the same two. The ask fixture is played under a stdin it was not
    // recorded with, which only the prompt's line differs from.
    [TestMethod]
    public async Task TheAsksSerializerAndExporterOptionsWriteThePromptAndTheAnswersSchema()
    {
        var contract = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        var exporter = new System.Text.Json.Schema.JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true };
        await using var server = FixtureServer.Start("ask", stdinDiffers: true);
        using var host = TestHost.Start(server, AsTheAskFixture);

        await host.Client.AskAsync<WordEvidence, WordReport>(WordEvidence.AskTask, WordEvidence.Recorded, null, contract, exporter).WaitForCompletionAsync();

        var prompt = WrittenPrompt(server);
        Assert.AreEqual(EvidencePrompt.Write(WordEvidence.AskTask, WordEvidence.Recorded, (System.Text.Json.Serialization.Metadata.JsonTypeInfo<WordEvidence>)contract.GetTypeInfo(typeof(WordEvidence)), exporter), prompt);
        StringAssert.Contains(Tagged(prompt, "evidence"), "\"seen_in\": [", StringComparison.Ordinal);
        Assert.AreEqual("\"string\"", JsonNode.Parse(Tagged(prompt, "evidence_schema"))!["properties"]!["seen_in"]!["items"]!["type"]!.ToJsonString());
        var expected = new ClaudeAgentSessionOptions();
        AsTheAskFixture(expected);
        expected.SetOutputSchema<WordReport>(contract, exporter);
        CollectionAssert.AreEqual(CommandLine.Build(expected, new TransportNeeds(false), _ => null).ToArray(), host.Transports.Last!.Argv.ToArray());
    }

    // The contracts a source generator wrote are the ones the twin writes through, a naming policy
    // of their own included.
    [TestMethod]
    public async Task TheSourceGeneratedContractsHandedInWriteThePromptAndTheAnswersSchema()
    {
        await using var server = FixtureServer.Start("ask", stdinDiffers: true);
        using var host = TestHost.Start(server, AsTheAskFixture);

        await host.Client.AskAsync(WordEvidence.AskTask, WordEvidence.Recorded, SnakeAskContext.Default.WordEvidence, SnakeAskContext.Default.WordReport).WaitForCompletionAsync();

        var prompt = WrittenPrompt(server);
        Assert.AreEqual(EvidencePrompt.Write(WordEvidence.AskTask, WordEvidence.Recorded, SnakeAskContext.Default.WordEvidence, null), prompt);
        StringAssert.Contains(Tagged(prompt, "evidence"), "\"seen_in\": [", StringComparison.Ordinal);
        var expected = new ClaudeAgentSessionOptions();
        AsTheAskFixture(expected);
        expected.SetOutputSchema(SnakeAskContext.Default.WordReport);
        var argv = host.Transports.Last!.Argv;
        CollectionAssert.AreEqual(CommandLine.Build(expected, new TransportNeeds(false), _ => null).ToArray(), argv.ToArray());
        StringAssert.Contains(argv[argv.IndexOf("--json-schema") + 1], "\"part_of_speech\"", StringComparison.Ordinal);
    }

    // The evidence's root is any type the contract writes: a list is evidence, as an object is.
    [TestMethod]
    public void AListIsEvidenceAndItsSchemaIsAnArrays()
    {
        var contract = JsonSerializerOptions.Default;
        IReadOnlyList<int> evidence = [1, 2];

        var prompt = EvidencePrompt.Write("Sum them.", evidence, (System.Text.Json.Serialization.Metadata.JsonTypeInfo<IReadOnlyList<int>>)contract.GetTypeInfo(typeof(IReadOnlyList<int>)), null);

        var schema = Tagged(prompt, "evidence_schema");
        Assert.IsTrue(JsonNode.Parse(schema)!["type"]!.ToJsonString().Contains("\"array\"", StringComparison.Ordinal), schema);
        Assert.AreEqual("[\n  1,\n  2\n]", Tagged(prompt, "evidence"));
    }

    // The exporter's options reach the evidence's schema as they reach the answer's.
    [TestMethod]
    public void TheExportersOptionsShapeTheEvidencesSchemaToo()
    {
        var contract = JsonSerializerOptions.Default;
        var typeInfo = (System.Text.Json.Serialization.Metadata.JsonTypeInfo<SiteEvidence>)contract.GetTypeInfo(typeof(SiteEvidence));

        var prompt = EvidencePrompt.Write("Judge the site.", new SiteEvidence("a", []), typeInfo, new() { TreatNullObliviousAsNonNullable = true });

        var items = JsonNode.Parse(Tagged(prompt, "evidence_schema"))!["properties"]!["PageTexts"]!["items"]!;
        Assert.AreEqual("\"string\"", items["type"]!.ToJsonString());
    }

    // The ask's answer is its answer type: a schema already on the call, or on the client's defaults,
    // is refused at the call, before any process starts. JSON null is no schema, and is not refused.
    [TestMethod]
    public void AnAskOnOptionsThatAlreadyCarryASchemaIsRefusedAtTheCall()
    {
        using var bare = TestHost.Start([]);
        var call = new ClaudeAgentSessionOptions { OutputSchema = Recorded.Json(OutputSchemaTests.PongSchema) };

        var onTheCall = Assert.ThrowsExactly<ArgumentException>(() => bare.Client.AskAsync<WordEvidence, WordReport>(WordEvidence.AskTask, WordEvidence.Recorded, call));
        Assert.AreEqual("sessionOptions", onTheCall.ParamName);

        using var typed = TestHost.Start([], options => options.SetOutputSchema<WordReport>());
        var onTheDefaults = Assert.ThrowsExactly<ArgumentException>(() => typed.Client.AskAsync<WordEvidence, WordReport>(WordEvidence.AskTask, WordEvidence.Recorded));
        Assert.AreEqual("sessionOptions", onTheDefaults.ParamName);

        Assert.IsEmpty(bare.Transports.Requests);
        Assert.IsEmpty(typed.Transports.Requests);
    }

    [TestMethod]
    public async Task AnAskOverJsonNullAsItsSchemaIsNotRefused()
    {
        await using var server = FixtureServer.Start("ask");
        using var host = TestHost.Start(server, AsTheAskFixture);
        var call = new ClaudeAgentSessionOptions { OutputSchema = Recorded.Json("null") };

        var trace = await host.Client.AskAsync<WordEvidence, WordReport>(WordEvidence.AskTask, WordEvidence.Recorded, call).WaitForCompletionAsync();

        await AssertWroteTheRecordedStdin(server);
        CollectionAssert.AreEqual(Fixture.Load("ask").Argv.ToArray(), host.Transports.Last!.Argv.ToArray(), "ask.argv.json");
        Assert.AreEqual(typeof(WordReport), trace.AnswerType);
    }

    [TestMethod]
    public void NoEvidenceNoTaskAndNoContractAreRefusedAtTheCall()
    {
        using var host = TestHost.Start([]);

        Assert.ThrowsExactly<ArgumentNullException>(() => host.Client.AskAsync<WordEvidence, WordReport>(WordEvidence.AskTask, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => host.Client.AskAsync<WordEvidence, WordReport>(null!, WordEvidence.Recorded));
        Assert.ThrowsExactly<ArgumentException>(() => host.Client.AskAsync<WordEvidence, WordReport>(" \n", WordEvidence.Recorded));
        Assert.ThrowsExactly<ArgumentNullException>(() => host.Client.AskAsync(WordEvidence.AskTask, WordEvidence.Recorded, null!, WordReportContext.Default.WordReport));
        Assert.ThrowsExactly<ArgumentNullException>(() => host.Client.AskAsync(WordEvidence.AskTask, WordEvidence.Recorded, AskContext.Default.WordEvidence, (System.Text.Json.Serialization.Metadata.JsonTypeInfo<WordReport>)null!));
        Assert.IsEmpty(host.Transports.Requests);
    }

    // What System.Text.Json throws, taking the evidence's schema or writing it, comes out as it threw
    // it, and no process starts: a contract it refuses, a member it does not write.
    [TestMethod]
    public void WhatSystemTextJsonThrowsOverTheEvidenceComesOutAsItThrewIt()
    {
        using var host = TestHost.Start([]);

        Assert.ThrowsExactly<InvalidOperationException>(() => host.Client.AskAsync<CollidingAnswer, WordReport>(WordEvidence.AskTask, new CollidingAnswer()));
        Assert.ThrowsExactly<NotSupportedException>(() => host.Client.AskAsync<UnreadableAnswer, WordReport>(WordEvidence.AskTask, new UnreadableAnswer(IntPtr.Zero)));
        Assert.IsEmpty(host.Transports.Requests);
    }

    // A run asked no schema names no type; a schema taken from a type names that type and no
    // evidence; a schema handed in names none.
    [TestMethod]
    public async Task AQueryNamesTheAnswerTypeOnlyWhenItsSchemaWasTakenFromOne()
    {
        await using (var server = FixtureServer.Start("trivial"))
        {
            using var host = TestHost.Start(server);
            var trace = await host.Client.QueryAsync("Reply with exactly one word: pong").WaitForCompletionAsync();
            Assert.IsNull(trace.EvidenceType);
            Assert.IsNull(trace.AnswerType);
        }

        await using (var server = FixtureServer.Start("schema-type"))
        {
            using var host = TestHost.Start(server, options =>
            {
                AsTheAskFixture(options);
                options.SetOutputSchema<WordReport>();
            });
            var trace = await host.Client.QueryAsync("Report on the word: pong").WaitForCompletionAsync();
            Assert.IsNull(trace.EvidenceType);
            Assert.AreEqual(typeof(WordReport), trace.AnswerType);
        }

        await using (var server = FixtureServer.Start("schema-type"))
        {
            using var host = TestHost.Start(server, options =>
            {
                AsTheAskFixture(options);
                options.SetOutputSchema<WordReport>();
                options.OutputSchema = options.OutputSchema;
            });
            var trace = await host.Client.QueryAsync("Report on the word: pong").WaitForCompletionAsync();
            Assert.IsNull(trace.AnswerType, "the same schema handed in names no type");
        }
    }

    // The type follows the schema through the layering: the call's schema wins and brings its type,
    // or none; the defaults' brings theirs when the call sets none; a copy keeps it.
    [TestMethod]
    public void TheAnswerTypeFollowsTheSchemaThatWins()
    {
        var typed = new ClaudeAgentSessionOptions();
        typed.SetOutputSchema<WordReport>();
        var handedIn = new ClaudeAgentSessionOptions { OutputSchema = typed.OutputSchema };
        var fromAContract = new ClaudeAgentSessionOptions();
        fromAContract.SetOutputSchema(WordReportContext.Default.WordReportValue);

        Assert.AreEqual(typeof(WordReport), ClaudeAgentSessionOptions.Layer(typed, null).AnswerType);
        Assert.IsNull(ClaudeAgentSessionOptions.Layer(typed, handedIn).AnswerType);
        Assert.AreEqual(typeof(WordReport), ClaudeAgentSessionOptions.Layer(handedIn, typed).AnswerType);
        Assert.AreEqual(typeof(WordReportValue), ClaudeAgentSessionOptions.Layer(typed, fromAContract).AnswerType);
        Assert.AreEqual(typeof(WordReport), typed.Clone().AnswerType);
        Assert.IsNull(handedIn.AnswerType);
    }

    [TestMethod]
    public void TheFactoryBuildsATraceNamingTheTypesItIsGiven()
    {
        var result = ClaudeAgentModelFactory.ResultMessage();

        var named = ClaudeAgentModelFactory.AgentTrace([result], typeof(WordEvidence), typeof(WordReport));
        var unnamed = ClaudeAgentModelFactory.AgentTrace([result]);

        Assert.AreEqual(typeof(WordEvidence), named.EvidenceType);
        Assert.AreEqual(typeof(WordReport), named.AnswerType);
        Assert.IsNull(unnamed.EvidenceType);
        Assert.IsNull(unnamed.AnswerType);
    }

    // What the recorder asked the ask fixture over the client's defaults.
    private static void AsTheAskFixture(ClaudeAgentSessionOptions options)
    {
        options.WorkingDirectory = RecordedCwd;
        options.SettingSources = [];
        options.Model = "haiku";
        options.MaxTurns = 2;
        options.Effort = Effort.Low;
    }

    private static async Task AssertWroteTheRecordedStdin(FixtureServer server)
    {
        var recorded = Fixture.Load("ask").Stdin.Anonymised();
        await server.WaitForWrites(recorded.Length, TimeSpan.FromSeconds(10));
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        CollectionAssert.AreEqual(recorded, server.Written.Anonymised());
    }

    // The prompt the client wrote: the text of its one user line.
    private static string WrittenPrompt(FixtureServer server)
    {
        var line = server.Written.Single(written => Fixture.TypeOf(written) == "user");
        using var document = JsonDocument.Parse(line);
        return document.RootElement.GetProperty("message").GetProperty("content").GetString()!;
    }

    // What stands between <tag> and </tag>, the line feeds that frame it left out.
    private static string Tagged(string prompt, string tag)
    {
        var start = prompt.IndexOf($"<{tag}>\n", StringComparison.Ordinal) + tag.Length + 3;
        return prompt[start..prompt.IndexOf($"\n</{tag}>", start, StringComparison.Ordinal)];
    }
}

[Description("A company's site, as it was read.")]
internal sealed record SiteEvidence(
    [Description("The company's name in the register.")] string CompanyName,
    [Description("The text of each page read.")] IReadOnlyList<string> PageTexts);

internal sealed record PriceEvidence([property: JsonPropertyName("hinta€")] string Price);

[JsonSerializable(typeof(WordEvidence))]
internal sealed partial class AskContext : JsonSerializerContext;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(WordEvidence))]
[JsonSerializable(typeof(WordReport))]
internal sealed partial class SnakeAskContext : JsonSerializerContext;
