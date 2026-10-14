using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using Claude.Agent.Tests.Replay;
using Claude.Agent.Tests.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace Claude.Agent.Tests.Wire;

// A schema taken from a type, beside one handed in as JSON. The platform's exporter writes it, and
// the library writes into it, node by node, what the API needs and the type says that the exporter
// leaves out: the root an object, never nullable; the descriptions; the maximum lengths; no member
// the type does not name. What the exporter writes itself — an enum's names, the required members —
// is held here as it stands. A caller's exporter options apply over the library's work. The wire is
// unchanged: the schema is argv's alone. `structured` was recorded with PongAnswer's schema byte for
// byte; `schema-type` with WordReport's (Wire/CommandLineFixtureTests.cs), and `schema-nullable`
// with the same under a nullable root, which the API refused (Sessions/StructuredOutputTests.cs).
[TestClass]
public sealed class OutputSchemaTests
{
    // The schema the `structured` fixture was recorded with, hand-written as its argv carries it.
    internal const string PongSchema =
        """{"type": "object", "properties": {"word": {"type": "string"}, "letters": {"type": "integer"}}, "required": ["word", "letters"], "additionalProperties": false}""";

    [TestMethod]
    public void ASchemaTakenFromARecordIsTheOneTheStructuredRecordingAskedWith()
    {
        var options = new ClaudeAgentSessionOptions();
        options.SetOutputSchema<PongAnswer>();

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--json-schema", PongSchema),
            Build(options));
    }

    // The exporter writes a class's root ["object", "null"], since a reference may be null, and a
    // Nullable<T>'s the same; the API refuses both (schema-nullable). The library writes "object",
    // for a class, a struct and a Nullable<T> of one alike. A dictionary is an object too, its
    // values' schema kept as its additionalProperties.
    [TestMethod]
    public void TheRootIsAnObjectAndNeverNullableForAClassAStructAndANullableStruct()
    {
        Assert.AreEqual("""["object","null"]""", JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(PongAnswer))["type"]!.ToJsonString());
        Assert.AreEqual("""["object","null"]""", JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(PongValue?))["type"]!.ToJsonString());

        Assert.AreEqual(PongSchema, WireSchemaOf<PongAnswer>());
        Assert.AreEqual(PongSchema, WireSchemaOf<PongValue>());
        Assert.AreEqual(PongSchema, WireSchemaOf<PongValue?>());
        Assert.AreEqual("""{"type":"object","additionalProperties":{"type":"integer"}}""", SchemaOf<Dictionary<string, int>>());
    }

    // What the API refuses at the root is refused here, before any process starts: a primitive, a
    // string, a collection and a value of any shape. The schema set before stays.
    [TestMethod]
    public void ARootThatIsNotAnObjectIsAnArgumentExceptionAndTheSchemaSetBeforeStays()
    {
        var options = new ClaudeAgentSessionOptions { OutputSchema = Recorded.Json(PongSchema) };

        var refused = Assert.ThrowsExactly<ArgumentException>(() => options.SetOutputSchema<int>());
        Assert.AreEqual(
            """The schema of System.Int32 has {"type":"integer"} at its root; structured output takes a schema whose type is "object".""",
            refused.Message);
        Assert.IsNull(refused.ParamName);

        Assert.ThrowsExactly<ArgumentException>(() => options.SetOutputSchema<string>());
        Assert.ThrowsExactly<ArgumentException>(() => options.SetOutputSchema<List<PongAnswer>>());
        Assert.ThrowsExactly<ArgumentException>(() => options.SetOutputSchema<PongAnswer[]>());
        Assert.ThrowsExactly<ArgumentException>(() => options.SetOutputSchema<JsonElement>());
        var generated = Assert.ThrowsExactly<ArgumentException>(() => options.SetOutputSchema(SchemaTestContext.Default.ListPongAnswer));
        Assert.AreEqual("jsonTypeInfo", generated.ParamName);

        Assert.AreEqual(PongSchema, WireJson.Serialize(options.OutputSchema!.Value));
    }

    // A polymorphic type's schema is the union of its cases, an object with anyOf at its root, for a
    // base that is a case of its own and for an abstract one; the API does not accept anyOf, oneOf or
    // allOf at the root, the binary's own note on an MCP tool's schema says, and the binary hands
    // --json-schema to it as it stands. So a polymorphic root is refused before any process starts,
    // through either overload, and the schema set before stays.
    [TestMethod]
    public void APolymorphicRootIsAnArgumentExceptionThroughEitherOverload()
    {
        var options = new ClaudeAgentSessionOptions { OutputSchema = Recorded.Json(PongSchema) };

        var reflected = Assert.ThrowsExactly<ArgumentException>(() => options.SetOutputSchema<ShapePart>());
        Assert.AreEqual(
            """The schema of Claude.Agent.Tests.Wire.ShapePart has "anyOf" at its root; the API does not accept anyOf, oneOf or allOf at the root of a structured output schema.""",
            reflected.Message);
        Assert.IsNull(reflected.ParamName);

        var generated = Assert.ThrowsExactly<ArgumentException>(() => options.SetOutputSchema(SchemaTestContext.Default.ShapePart));
        Assert.AreEqual("jsonTypeInfo", generated.ParamName);
        Assert.StartsWith(reflected.Message, generated.Message);

        var abstracted = Assert.ThrowsExactly<ArgumentException>(() => options.SetOutputSchema<AbstractShapePart>());
        Assert.AreEqual(
            """The schema of Claude.Agent.Tests.Wire.AbstractShapePart has "anyOf" at its root; the API does not accept anyOf, oneOf or allOf at the root of a structured output schema.""",
            abstracted.Message);

        Assert.AreEqual(PongSchema, WireJson.Serialize(options.OutputSchema!.Value));
    }

    // A schema handed in as it stands is held to the rule a schema taken from a type is, in the same
    // words but for what names it: a nullable root, as the schema-nullable recording's, a root of
    // another type or of none, a root that is not an object at all, and a union at the root are each
    // an ArgumentException naming OutputSchema before any process starts — on a call as it is made,
    // and on the defaults when the client is made. An object root is taken, and JSON null is no
    // schema.
    [TestMethod]
    public async Task AHandedInSchemaWhoseRootIsNotAnObjectIsRefusedAsATypesIs()
    {
        (string Schema, string Message)[] refused =
        [
            ("""{"type": ["object", "null"], "properties": {}}""", NotAnObject("""{"type":["object","null"],"properties":{}}""")),
            ("""{"type": "integer"}""", NotAnObject("""{"type":"integer"}""")),
            ("""{"properties": {"word": {"type": "string"}}}""", NotAnObject("""{"properties":{"word":{"type":"string"}}}""")),
            ("""[{"type": "object"}]""", NotAnObject("""[{"type":"object"}]""")),
            ("true", NotAnObject("true")),
            ("""{"type": "object", "anyOf": [{"required": ["word"]}]}""", Union("anyOf")),
            ("""{"type": "object", "oneOf": [{"required": ["word"]}]}""", Union("oneOf")),
            ("""{"type": "object", "allOf": [{"required": ["word"]}]}""", Union("allOf")),
        ];

        foreach (var (schema, message) in refused)
        {
            var call = new ClaudeAgentSessionOptions { OutputSchema = Recorded.Json(schema) };
            var validated = Assert.ThrowsExactly<ArgumentException>(call.Validate, schema);
            Assert.AreEqual(nameof(ClaudeAgentSessionOptions.OutputSchema), validated.ParamName, schema);
            Assert.AreEqual($"{message} (Parameter 'OutputSchema')", validated.Message, schema);

            var transports = new FixtureTransportFactory(new FrameCodec());
            var client = new ClaudeAgentClient(new ClaudeAgentClientOptions(), null, transports, TestHostEnvironments.Unmade);
            var queried = Assert.ThrowsExactly<ArgumentException>(() => client.QueryAsync("Report on the word: pong", call), schema);
            var started = await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.StartSessionAsync(call), schema);
            Assert.AreEqual(validated.Message, queried.Message, schema);
            Assert.AreEqual(validated.Message, started.Message, schema);
            Assert.IsEmpty(transports.Requests, schema);

            var defaults = new ClaudeAgentClientOptions();
            defaults.Defaults.OutputSchema = Recorded.Json(schema);
            var made = Assert.ThrowsExactly<ArgumentException>(
                () => new ClaudeAgentClient(defaults, null, new FixtureTransportFactory(new FrameCodec()), TestHostEnvironments.Unmade),
                schema);
            Assert.AreEqual(validated.Message, made.Message, schema);
        }

        new ClaudeAgentSessionOptions { OutputSchema = Recorded.Json(PongSchema) }.Validate();
        new ClaudeAgentSessionOptions { OutputSchema = Recorded.Json("""{"type": "object"}""") }.Validate();
        new ClaudeAgentSessionOptions { OutputSchema = Recorded.Json("null") }.Validate();
    }

    // The root is judged on the element as it is held, and nothing parses it again: a schema read with
    // comments or trailing commas allowed, or nested past the default reader depth of 64, is judged by
    // its root like any other. A root that repeats a key is read as the binary's JSON.parse reads it:
    // by its last "type", and by any combinator it names at all. Whatever the element holds, a refusal
    // is the rule's ArgumentException naming OutputSchema — on a call, on the defaults when the client
    // is made, and under a container the OptionsValidationException that carries its message — and
    // what the rule takes, each of those takes.
    [TestMethod]
    public void AHandedInSchemaIsJudgedByItsRootHoweverItWasRead()
    {
        static string Nested(string open, string inner, string close) =>
            string.Concat(Enumerable.Repeat(open, 70)) + inner + string.Concat(Enumerable.Repeat(close, 70));
        string[] taken =
        [
            """{"type": /* the root */ "object"}""",
            """{"type": "object", "properties": {"word": {"type": "string",},},}""",
            """{"type": "object", "properties": """ + Nested("""{"a": """, "{}", "}") + "}",
            """{"type": "string", "type": "object"}""",
            """{"type": "object", "required": [], "required": ["word"]}""",
        ];
        (string Schema, string Message)[] refused =
        [
            ("""{"type": "object", "type": "string"}""", NotAnObject("""{"type":"object","type":"string"}""")),
            ("""/* a list */ [{"type": "object",},]""", NotAnObject("""[{"type":"object"}]""")),
            (Nested("[", "{}", "]"), NotAnObject(Nested("[", "{}", "]"))),
            ("""{"anyOf": [], "type": "object", "anyOf": []}""", Union("anyOf")),
            ("""{"type": "object", "oneOf": [], /* again */ "oneOf": [],}""", Union("oneOf")),
        ];

        foreach (var schema in taken)
        {
            var call = new ClaudeAgentSessionOptions { OutputSchema = Leniently(schema) };
            call.Validate();

            var defaults = new ClaudeAgentClientOptions();
            defaults.Defaults.OutputSchema = Leniently(schema);
            _ = new ClaudeAgentClient(defaults, null, new FixtureTransportFactory(new FrameCodec()), TestHostEnvironments.Unmade);
            _ = Contained(schema).Value;
        }

        foreach (var (schema, message) in refused)
        {
            var call = new ClaudeAgentSessionOptions { OutputSchema = Leniently(schema) };
            var validated = Assert.ThrowsExactly<ArgumentException>(call.Validate, schema);
            Assert.AreEqual(nameof(ClaudeAgentSessionOptions.OutputSchema), validated.ParamName, schema);
            Assert.AreEqual($"{message} (Parameter 'OutputSchema')", validated.Message, schema);

            var defaults = new ClaudeAgentClientOptions();
            defaults.Defaults.OutputSchema = Leniently(schema);
            var made = Assert.ThrowsExactly<ArgumentException>(
                () => new ClaudeAgentClient(defaults, null, new FixtureTransportFactory(new FrameCodec()), TestHostEnvironments.Unmade),
                schema);
            Assert.AreEqual(validated.Message, made.Message, schema);

            var contained = Assert.ThrowsExactly<OptionsValidationException>(() => Contained(schema).Value, schema);
            StringAssert.Contains(contained.Message, validated.Message, schema);
        }

        static JsonElement Leniently(string schema)
        {
            using var document = JsonDocument.Parse(
                schema,
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, MaxDepth = 1000 });
            return document.RootElement.Clone();
        }

        static IOptions<ClaudeAgentClientOptions> Contained(string schema)
        {
            var services = new ServiceCollection();
            services.AddSingleton(TestHostEnvironments.Unmade);
            services.AddClaudeAgentClient(options => options.Defaults.OutputSchema = Leniently(schema));
            return services.BuildServiceProvider().GetRequiredService<IOptions<ClaudeAgentClientOptions>>();
        }
    }

    private static string NotAnObject(string root) =>
        $"The output schema has {root} at its root; structured output takes a schema whose type is \"object\".";

    private static string Union(string combinator) =>
        $"The output schema has \"{combinator}\" at its root; the API does not accept anyOf, oneOf or allOf at the root of a structured output schema.";

    // A reference back to the root is "$ref": "#", which the exporter writes against the root it
    // wrote, one that admits null for a class; the contract reads a null element or value there. The
    // root made an object, each such reference is the root or null, through a list's items and a
    // dictionary's values alike, and an answer with a null in either reads back. Where the root never
    // admitted null the references stay as the exporter wrote them, and a reference to any other node
    // is left alone (Chain, below).
    [TestMethod]
    public void AReferenceBackToANullableRootStillAdmitsNull()
    {
        Assert.AreEqual(
            """{"type":"object","properties":{"Children":{"type":"array","items":{"anyOf":[{"$ref":"#"},{"type":"null"}]}},"Named":{"type":["object","null"],"additionalProperties":{"anyOf":[{"$ref":"#"},{"type":"null"}]}}},"required":["Children"],"additionalProperties":false}""",
            SchemaOf<TreeAnswer>());
        Assert.AreEqual(
            """{"type":"object","properties":{"Children":{"type":"array","items":{"$ref":"#"}},"Named":{"type":["object","null"],"additionalProperties":{"$ref":"#"}}},"required":["Children"],"additionalProperties":false}""",
            SchemaOf<TreeAnswer>(exporterOptions: new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true }));

        var trace = ClaudeAgentModelFactory.AgentTrace(
            [ClaudeAgentModelFactory.ResultMessage(raw: Recorded.Json("""{"structured_output": {"Children": [null, {"Children": []}], "Named": {"leaf": null}}}"""))]);
        var answer = trace.GetStructuredOutput<TreeAnswer>()!;

        Assert.IsNull(answer.Children[0]);
        Assert.IsEmpty(answer.Children[1]!.Children);
        Assert.IsNull(answer.Named!["leaf"]);
    }

    // The type's description is its schema's; a member's own, on the property or on the constructor
    // parameter that sets it, is the member's, and a member without one takes its type's, as the
    // items of a list do. A member whose schema is any value becomes the object that says so.
    [TestMethod]
    public void ADescriptionIsTheMembersOwnOrItsTypes()
    {
        Assert.AreEqual(
            """{"description":"An answer with descriptions.","type":"object","properties":{"Word":{"description":"The word.","type":"string"},"Part":{"description":"A part.","type":"object","properties":{"Size":{"type":"integer"}},"additionalProperties":false},"Other":{"description":"Its own.","type":"object","properties":{"Size":{"type":"integer"}},"additionalProperties":false},"Parts":{"type":"array","items":{"description":"A part.","type":["object","null"],"properties":{"Size":{"type":"integer"}},"additionalProperties":false}},"Shade":{"description":"A shade.","type":"integer"},"Anything":{"description":"Anything at all."}},"required":["Word","Part","Other","Parts"],"additionalProperties":false}""",
            SchemaOf<DescribedAnswer>());
        Assert.AreEqual(
            """{"type":"object","properties":{"Word":{"description":"From the parameter.","type":"string"},"Other":{"description":"From the property.","type":"string"}},"required":["Word","Other"],"additionalProperties":false}""",
            SchemaOf<DescribedRecord>());
    }

    // [MaxLength], [StringLength] and [Length] are a string's maxLength and an array's maxItems, the
    // least of them when a member has several, on the property or the constructor parameter.
    // [MaxLength] with no length sets none, and a member that is neither a string nor an array
    // takes none.
    [TestMethod]
    public void TheMaximumLengthsAreAStringsMaxLengthAndAnArraysMaxItems()
    {
        Assert.AreEqual(
            """{"type":"object","properties":{"Word":{"type":"string","maxLength":8},"Remark":{"type":["string","null"],"maxLength":80},"Tags":{"type":"array","items":{"type":["string","null"]},"maxItems":3},"Scores":{"type":"array","items":{"type":"integer"},"maxItems":4},"Both":{"type":"string","maxLength":6},"Unbounded":{"type":"string"},"Count":{"type":"integer"}},"required":["Word","Tags","Scores","Both","Unbounded","Count"],"additionalProperties":false}""",
            SchemaOf<BoundedAnswer>());
        Assert.AreEqual(
            """{"type":"object","properties":{"Word":{"type":"string","maxLength":5},"Numbers":{"type":["array","null"],"items":{"type":"integer"},"maxItems":2}},"required":["Word","Numbers"],"additionalProperties":false}""",
            SchemaOf<BoundedRecord>());
    }

    // The contract writes a byte array as a base64 string, and its [MaxLength], [Length] and
    // [StringLength] count bytes, as DataAnnotations counts them: the string's maxLength is the
    // base64 length of that many bytes, four characters for each three and for a last one or two,
    // for byte[], ReadOnlyMemory<byte> and Memory<byte> alike. Four bytes, which [MaxLength(4)]
    // allows, are eight characters.
    [TestMethod]
    public void OnAByteArrayTheMaximumLengthsCountBytesAndTheMaxLengthIsTheirBase64Length()
    {
        Assert.AreEqual(
            """{"type":"object","properties":{"Bytes":{"type":"string","maxLength":8},"Memory":{"type":"string","maxLength":4},"Writable":{"type":"string","maxLength":8},"Maybe":{"type":["string","null"],"maxLength":8}},"required":["Bytes"],"additionalProperties":false}""",
            SchemaOf<BytesAnswer>());

        Assert.IsTrue(new MaxLengthAttribute(4).IsValid(new byte[4]));
        Assert.AreEqual("\"AAECAw==\"", JsonSerializer.Serialize(new byte[] { 0, 1, 2, 3 }));
    }

    // [ItemMaxLength] on a list member is each item's maxLength where the items are strings, beside the
    // list's own maxItems; nullable items as well, on a record's parameter as on a property, and on a
    // list of byte arrays the base64 length of that many bytes. Where the items are not strings, or
    // the member is no list, it writes nothing. A length below zero is no length.
    [TestMethod]
    public void AnItemMaxLengthIsEachStringItemsMaxLengthOnAList()
    {
        Assert.AreEqual(
            """{"type":"object","properties":{"Tags":{"type":"array","items":{"type":["string","null"],"maxLength":40},"maxItems":5},"Notes":{"type":["array","null"],"items":{"type":["string","null"],"maxLength":12}},"Blobs":{"type":"array","items":{"type":["string","null"],"maxLength":8}},"Scores":{"type":"array","items":{"type":"integer"}},"Word":{"type":"string"}},"required":["Tags","Blobs","Scores","Word"],"additionalProperties":false}""",
            SchemaOf<ItemBoundedAnswer>());
        Assert.AreEqual(
            """{"type":"object","properties":{"Words":{"type":"array","items":{"type":["string","null"],"maxLength":3}}},"required":["Words"],"additionalProperties":false}""",
            SchemaOf<ItemBoundedRecord>());

        Assert.AreEqual(40, new ItemMaxLengthAttribute(40).Length);
        Assert.AreEqual("length", Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ItemMaxLengthAttribute(-1)).ParamName);
    }

    // An attribute on the member an override overrides is the override's, as DataAnnotations reads
    // it: the runtime's own lookup on a property does not reach the base declaration. Through the
    // reflecting overload and the source-generated one alike.
    [TestMethod]
    public void TheAttributesOfAnOverriddenMemberAreTheOverrides()
    {
        const string Expected =
            """{"type":"object","properties":{"Name":{"description":"The name.","type":"string","maxLength":3}},"additionalProperties":false}""";
        var generated = new ClaudeAgentSessionOptions();
        generated.SetOutputSchema(SchemaTestContext.Default.OverridingAnswer);

        Assert.AreEqual(Expected, SchemaOf<OverridingAnswer>());
        Assert.AreEqual(Expected, generated.OutputSchema!.Value.GetRawText());
    }

    // The exporter writes an enum's names where the contract writes the enum as strings: the
    // converter on the enum, on the member, or in the options; otherwise its numbers.
    [TestMethod]
    public void AnEnumIsItsNamesWhereTheContractWritesItAsStrings()
    {
        Assert.AreEqual(
            """{"type":"object","properties":{"ByNumber":{"type":"integer"},"ByMember":{"enum":["Light","Dark"]},"ByType":{"enum":["Warm","Cool"]},"MaybeByType":{"enum":["Warm","Cool",null]}},"additionalProperties":false}""",
            SchemaOf<EnumAnswer>());
        Assert.AreEqual(
            """{"type":"object","properties":{"ByNumber":{"enum":["Light","Dark"]},"ByMember":{"enum":["Light","Dark"]},"ByType":{"enum":["Warm","Cool"]},"MaybeByType":{"enum":["Warm","Cool",null]}},"additionalProperties":false}""",
            SchemaOf<EnumAnswer>(new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }));
    }

    // Every object the contract reads member by member allows no other member, at every depth and
    // in each case of a polymorphic one; a dictionary's additionalProperties is its values, and a
    // type that keeps extension data, or a reference back up the tree, is left open.
    [TestMethod]
    public void EveryObjectWhoseMembersTheContractFixesAllowsNoOther()
    {
        Assert.AreEqual(
            """{"type":"object","properties":{"Part":{"type":"object","properties":{"Size":{"type":"integer"}},"additionalProperties":false},"MaybePart":{"type":["object","null"],"properties":{"Size":{"type":"integer"}},"additionalProperties":false},"Parts":{"type":"array","items":{"type":["object","null"],"properties":{"Size":{"type":"integer"}},"additionalProperties":false}},"Counts":{"type":"object","additionalProperties":{"type":"integer"}},"Open":{"type":"object","properties":{"Size":{"type":"integer"}}},"Shape":{"type":["object","null"],"anyOf":[{"properties":{"$type":{"const":"circle"},"Radius":{"type":"integer"},"Id":{"type":"integer"}},"required":["$type"],"additionalProperties":false},{"properties":{"Id":{"type":"integer"}},"additionalProperties":false}]},"Chain":{"type":["object","null"],"properties":{"Next":{"type":["object","null"],"properties":{"Next":{"$ref":"#/properties/Chain/properties/Next"},"Value":{"type":"integer"}},"additionalProperties":false},"Value":{"type":"integer"}},"additionalProperties":false}},"required":["Part","Parts","Counts","Open"],"additionalProperties":false}""",
            SchemaOf<ClosedAnswer>());
    }

    // The exporter writes required: the required members, those marked [JsonRequired], and the
    // constructor parameters that have no default, whatever the options say of the last; the
    // library adds none.
    [TestMethod]
    public void TheRequiredMembersAreTheOnesTheContractRequires()
    {
        const string Expected =
            """{"type":"object","properties":{"ByParameter":{"type":"string"},"WithDefault":{"type":"integer","default":1},"ByKeyword":{"type":"string"},"ByAttribute":{"type":"string"},"Optional":{"type":["string","null"]}},"required":["ByParameter","ByKeyword","ByAttribute"],"additionalProperties":false}""";

        Assert.AreEqual(Expected, SchemaOf<RequiredAnswer>());
        Assert.AreEqual(Expected, SchemaOf<RequiredAnswer>(new JsonSerializerOptions { RespectRequiredConstructorParameters = true }));
    }

    // A caller's exporter settings apply: the items of a list of strings, whose nullability no
    // annotation reaches, are non-nullable when the caller says so.
    [TestMethod]
    public void TheCallersExporterSettingsApply()
    {
        Assert.AreEqual(
            """{"type":"object","properties":{"Words":{"type":"array","items":{"type":["string","null"]}}},"required":["Words"],"additionalProperties":false}""",
            SchemaOf<ListAnswer>());
        Assert.AreEqual(
            """{"type":"object","properties":{"Words":{"type":"array","items":{"type":"string"}}},"required":["Words"],"additionalProperties":false}""",
            SchemaOf<ListAnswer>(exporterOptions: new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true }));
    }

    // A caller's transform runs over each node after the library's work, sees it, and has the last
    // word, through the reflecting overload and the source-generated one alike.
    [TestMethod]
    public void TheCallersTransformRunsOverTheLibrarysWorkAndHasTheLastWord()
    {
        var seen = new List<string>();
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TransformSchemaNode = (context, node) =>
            {
                if (context.Path.IsEmpty && node is JsonObject root)
                {
                    seen.Add(root.ToJsonString());
                    root.Remove("additionalProperties");
                    root["title"] = context.TypeInfo.Type.Name;
                }

                return node;
            },
        };
        const string Expected =
            """{"type":"object","properties":{"word":{"type":"string"},"letters":{"type":"integer"}},"required":["word","letters"],"title":"PongAnswer"}""";

        var reflected = new ClaudeAgentSessionOptions();
        reflected.SetOutputSchema<PongAnswer>(exporterOptions: exporterOptions);
        var generated = new ClaudeAgentSessionOptions();
        generated.SetOutputSchema(StructuredOutputContext.Default.PongAnswer, exporterOptions);

        Assert.AreEqual(Expected, reflected.OutputSchema!.Value.GetRawText());
        Assert.AreEqual(Expected, generated.OutputSchema!.Value.GetRawText());
        CollectionAssert.AreEqual(
            new[] { """{"type":"object","properties":{"word":{"type":"string"},"letters":{"type":"integer"}},"required":["word","letters"],"additionalProperties":false}""" },
            seen.Distinct().ToArray());
    }

    // What the caller's transform makes the root is held to the same rule: a root that is not an
    // object is refused.
    [TestMethod]
    public void ACallersTransformThatMakesTheRootNullableIsRefused()
    {
        var options = new ClaudeAgentSessionOptions();
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TransformSchemaNode = (context, node) =>
            {
                if (context.Path.IsEmpty && node is JsonObject root)
                {
                    root["type"] = new JsonArray("object", "null");
                }

                return node;
            },
        };

        Assert.ThrowsExactly<ArgumentException>(() => options.SetOutputSchema<PongAnswer>(exporterOptions: exporterOptions));
        Assert.IsNull(options.OutputSchema);
    }

    // A caller's transform that puts oneOf or allOf at the root is refused as a polymorphic type's
    // anyOf is.
    [TestMethod]
    public void ACallersTransformThatPutsAUnionAtTheRootIsRefused()
    {
        foreach (var combinator in new[] { "oneOf", "allOf" })
        {
            var options = new ClaudeAgentSessionOptions();
            var exporterOptions = new JsonSchemaExporterOptions
            {
                TransformSchemaNode = (context, node) =>
                {
                    if (context.Path.IsEmpty && node is JsonObject root)
                    {
                        root[combinator] = new JsonArray(new JsonObject { ["required"] = new JsonArray("word") });
                    }

                    return node;
                },
            };

            var refused = Assert.ThrowsExactly<ArgumentException>(() => options.SetOutputSchema<PongAnswer>(exporterOptions: exporterOptions));
            Assert.StartsWith($"""The schema of Claude.Agent.Tests.Sessions.PongAnswer has "{combinator}" at its root;""", refused.Message);
            Assert.IsNull(options.OutputSchema);
        }
    }

    [TestMethod]
    public void ASchemaTakenThroughASourceGeneratedContractIsTheOneTakenByReflection()
    {
        var generated = new ClaudeAgentSessionOptions();
        generated.SetOutputSchema(StructuredOutputContext.Default.PongAnswer);
        var reflected = new ClaudeAgentSessionOptions();
        reflected.SetOutputSchema<PongAnswer>();

        Assert.AreEqual(reflected.OutputSchema!.Value.GetRawText(), generated.OutputSchema!.Value.GetRawText());
        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--json-schema", PongSchema),
            Build(generated));
    }

    // The options the schema is taken with are the contract it is taken off: a web policy names the
    // members its way and widens the integer, because those options would read one written as a
    // string too.
    [TestMethod]
    public void ASchemaTakenFromATypeFollowsTheSerializerOptionsHandedIn()
    {
        var options = new ClaudeAgentSessionOptions();
        options.SetOutputSchema<PlainAnswer>(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.AreEqual(
            """{"type":"object","properties":{"word":{"type":"string"},"letters":{"type":["string","integer"],"pattern":"^-?(?:0|[1-9]\\d*)$"}},"required":["word","letters"],"additionalProperties":false}""",
            options.OutputSchema!.Value.GetRawText());
    }

    // The schema a type was taken into is the options' own, as one handed in is: it is there after
    // the call is layered over the defaults, and a later one replaces it.
    [TestMethod]
    public void ASchemaTakenFromATypeIsTheOptionsOwnAndTheLastOneSetWins()
    {
        const string AnyObject = """{"type": "object"}""";
        var options = new ClaudeAgentSessionOptions();
        options.SetOutputSchema<PongAnswer>();

        Assert.AreEqual(PongSchema, WireJson.Serialize(options.Clone().OutputSchema!.Value));

        options.OutputSchema = Recorded.Json(AnyObject);
        Assert.AreEqual(AnyObject, WireJson.Serialize(options.OutputSchema!.Value));
        options.SetOutputSchema<PongAnswer>();
        Assert.AreEqual(PongSchema, WireJson.Serialize(options.OutputSchema!.Value));
    }

    [TestMethod]
    public void TakingASchemaThroughNoContractAtAllIsRefused()
    {
        var options = new ClaudeAgentSessionOptions();

        Assert.ThrowsExactly<ArgumentNullException>(() => options.SetOutputSchema<PongAnswer>(jsonTypeInfo: null!));
        Assert.IsNull(options.OutputSchema);
    }

    // A schema the platform cannot write throws what the platform throws, where it stands, and the
    // options keep no schema.
    [TestMethod]
    public void ASchemaThePlatformCannotWriteThrowsWhatThePlatformThrows()
    {
        var options = new ClaudeAgentSessionOptions();

        Assert.ThrowsExactly<InvalidOperationException>(() => options.SetOutputSchema<CollidingAnswer>());
        Assert.ThrowsExactly<NotSupportedException>(() => options.SetOutputSchema<PongAnswer>(new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve }));
        Assert.IsNull(options.OutputSchema);
    }

    // A schema is taken as deep as the contract lets the platform write it, past the 64 levels a
    // JsonDocument reads by default: 65 under the default MaxDepth, a Nest more past which the platform
    // throws its own refusal, and 401 where MaxDepth is raised. Each Nest is two levels of the
    // schema, its properties and its member.
    [TestMethod]
    public void ASchemaIsTakenAsDeepAsTheContractLetsThePlatformWriteIt()
    {
        var deepest = SetOutputSchema(Nested(32), null);
        var raised = SetOutputSchema(Nested(200), new JsonSerializerOptions { MaxDepth = 500 });

        Assert.AreEqual(65, Depth(deepest.OutputSchema!.Value));
        Assert.AreEqual(401, Depth(raised.OutputSchema!.Value));
        Assert.AreEqual(JsonValueKind.Object, raised.OutputSchema!.Value.ValueKind);
        Assert.IsInstanceOfType<InvalidOperationException>(
            Assert.ThrowsExactly<System.Reflection.TargetInvocationException>(() => SetOutputSchema(Nested(33), null)).InnerException);
    }

    private static Type Nested(int levels)
    {
        var type = typeof(int);
        for (var i = 0; i < levels; i++)
        {
            type = typeof(Nest<>).MakeGenericType(type);
        }

        return type;
    }

    private static ClaudeAgentSessionOptions SetOutputSchema(Type type, JsonSerializerOptions? serializerOptions)
    {
        var options = new ClaudeAgentSessionOptions();
        typeof(ClaudeAgentSessionOptions).GetMethods()
            .Single(m => m.Name == nameof(ClaudeAgentSessionOptions.SetOutputSchema) && m.GetParameters()[0].ParameterType == typeof(JsonSerializerOptions))
            .MakeGenericMethod(type)
            .Invoke(options, [serializerOptions, null]);
        return options;
    }

    private static int Depth(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => 1 + element.EnumerateObject().Select(p => Depth(p.Value)).DefaultIfEmpty(0).Max(),
        JsonValueKind.Array => 1 + element.EnumerateArray().Select(Depth).DefaultIfEmpty(0).Max(),
        _ => 0,
    };

    private static string SchemaOf<T>(JsonSerializerOptions? serializerOptions = null, JsonSchemaExporterOptions? exporterOptions = null)
    {
        var options = new ClaudeAgentSessionOptions();
        options.SetOutputSchema<T>(serializerOptions, exporterOptions);
        return options.OutputSchema!.Value.GetRawText();
    }

    private static string WireSchemaOf<T>()
    {
        var options = new ClaudeAgentSessionOptions();
        options.SetOutputSchema<T>();
        return WireJson.Serialize(options.OutputSchema!.Value);
    }

    private static string[] Expected(params string[] flags) =>
        ["--output-format", "stream-json", "--verbose", .. flags, "--input-format", "stream-json"];

    private static string[] Build(ClaudeAgentSessionOptions options) =>
        [.. CommandLine.Build(
            ClaudeAgentSessionOptions.Layer(options, null),
            new TransportNeeds(false),
            _ => null)];
}

/// <summary>PongAnswer's members as a struct.</summary>
internal readonly struct PongValue
{
    [JsonPropertyName("word")]
    public required string Word { get; init; }

    [JsonPropertyName("letters")]
    public required int Letters { get; init; }
}

[Description("An answer with descriptions.")]
internal sealed class DescribedAnswer
{
    [Description("The word.")]
    public required string Word { get; init; }

    public required DescribedPart Part { get; init; }

    [Description("Its own.")]
    public required DescribedPart Other { get; init; }

    public required IReadOnlyList<DescribedPart> Parts { get; init; }

    public Shade Shade { get; init; }

    [Description("Anything at all.")]
    public JsonElement Anything { get; init; }
}

[Description("A part.")]
internal sealed class DescribedPart
{
    public int Size { get; init; }
}

[Description("A shade.")]
internal enum Shade
{
    Light,
    Dark,
}

internal sealed record DescribedRecord(
    [Description("From the parameter.")] string Word,
    [property: Description("From the property.")] string Other);

internal sealed class BoundedAnswer
{
    [MaxLength(8)]
    public required string Word { get; init; }

    [StringLength(80)]
    public string? Remark { get; init; }

    [Length(1, 3)]
    public required IReadOnlyList<string> Tags { get; init; }

    [MaxLength(4)]
    public required int[] Scores { get; init; }

    [MaxLength(10)]
    [StringLength(6)]
    public required string Both { get; init; }

    [MaxLength]
    public required string Unbounded { get; init; }

    [MaxLength(2)]
    public required int Count { get; init; }
}

internal sealed record BoundedRecord(
    [MaxLength(5)] string Word,
    [property: Length(0, 2)] IReadOnlyList<int>? Numbers);

internal sealed class ItemBoundedAnswer
{
    [MaxLength(5)]
    [ItemMaxLength(40)]
    public required IReadOnlyList<string> Tags { get; init; }

    [ItemMaxLength(12)]
    public List<string?>? Notes { get; init; }

    [ItemMaxLength(4)]
    public required byte[][] Blobs { get; init; }

    [ItemMaxLength(4)]
    public required int[] Scores { get; init; }

    [ItemMaxLength(4)]
    public required string Word { get; init; }
}

internal sealed record ItemBoundedRecord([ItemMaxLength(3)] IReadOnlyList<string> Words);

internal sealed class BytesAnswer
{
    [MaxLength(4)]
    public required byte[] Bytes { get; init; }

    [Length(1, 3)]
    public ReadOnlyMemory<byte> Memory { get; init; }

    [StringLength(6)]
    public Memory<byte> Writable { get; init; }

    [MaxLength(5)]
    public ReadOnlyMemory<byte>? Maybe { get; init; }
}

internal class BaseAnswer
{
    [Description("The name.")]
    [MaxLength(3)]
    public virtual string Name { get; init; } = string.Empty;
}

internal sealed class OverridingAnswer : BaseAnswer
{
    public override string Name { get; init; } = string.Empty;
}

internal enum Brightness
{
    Light,
    Dark,
}

[JsonConverter(typeof(JsonStringEnumConverter<Tone>))]
internal enum Tone
{
    Warm,
    Cool,
}

internal sealed class EnumAnswer
{
    public Brightness ByNumber { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter<Brightness>))]
    public Brightness ByMember { get; init; }

    public Tone ByType { get; init; }

    public Tone? MaybeByType { get; init; }
}

internal sealed class ClosedAnswer
{
    public required ClosedPart Part { get; init; }

    public ClosedPart? MaybePart { get; init; }

    public required IReadOnlyList<ClosedPart> Parts { get; init; }

    public required Dictionary<string, int> Counts { get; init; }

    public required OpenPart Open { get; init; }

    public ShapePart? Shape { get; init; }

    public ChainPart? Chain { get; init; }
}

internal sealed class ChainPart
{
    public ChainPart? Next { get; init; }

    public int Value { get; init; }
}

internal sealed class ClosedPart
{
    public int Size { get; init; }
}

internal sealed class OpenPart
{
    public int Size { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Rest { get; init; }
}

[JsonDerivedType(typeof(CirclePart), "circle")]
internal class ShapePart
{
    public int Id { get; init; }
}

internal sealed class CirclePart : ShapePart
{
    public int Radius { get; init; }
}

[JsonDerivedType(typeof(SquarePart), "square")]
internal abstract class AbstractShapePart
{
    public int Id { get; init; }
}

internal sealed class SquarePart : AbstractShapePart
{
    public int Side { get; init; }
}

internal sealed class TreeAnswer
{
    public required IReadOnlyList<TreeAnswer?> Children { get; init; }

    public Dictionary<string, TreeAnswer?>? Named { get; init; }
}

internal sealed record RequiredAnswer(string ByParameter, int WithDefault = 1)
{
    public required string ByKeyword { get; init; }

    [JsonRequired]
    public string ByAttribute { get; init; } = string.Empty;

    public string? Optional { get; init; }
}

internal sealed class ListAnswer
{
    public required IReadOnlyList<string> Words { get; init; }
}

[JsonSerializable(typeof(List<PongAnswer>))]
[JsonSerializable(typeof(ShapePart))]
[JsonSerializable(typeof(OverridingAnswer))]
internal sealed partial class SchemaTestContext : JsonSerializerContext;

/// <summary>One level of nesting, a member of the type within.</summary>
internal sealed record Nest<T>(T Inner);
