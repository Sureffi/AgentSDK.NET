using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Architecture;

// The models, found by reflection: every exported class that is not static and not on the short
// list below. The library builds them and hands them out, so a caller's test builds one through
// the model factory. The round-trip tool type goes in as well as out: it has a builder too, and a
// public constructor besides. No model is a bag whose kind says which members are set, which the
// recordings show, but the three the page names: the bag check below.
[TestClass]
public sealed partial class ShapeTests
{
    private static readonly Assembly Library = typeof(ClaudeAgentClientOptions).Assembly;

    // The exported classes that are not models, each with its reason.
    private static readonly Dictionary<Type, string> NotModels = new()
    {
        [typeof(ClaudeAgentClient)] = "a client: mockable, with a protected constructor for a caller's test double",
        [typeof(ClaudeSessionClient)] = "a client",
        [typeof(ClaudeAgentSession)] = "a client",
        [typeof(ClaudeAgentRun)] = "a client: the run a call hands back",
        [typeof(ClaudeAgentException)] = "the exception: a caller's test double throws one",
        [typeof(ClaudeAgentClientOptions)] = "a caller-built input",
        [typeof(ClaudeAgentSessionOptions)] = "a caller-built input",
        [typeof(SessionQueryOptions)] = "a caller-built input",
        [typeof(SandboxOptions)] = "a caller-built input",
        [typeof(ThinkingOptions)] = "a caller-built input",
        [typeof(AgentDefinition)] = "a caller-built input",
        [typeof(AgentSystemPrompt)] = "a caller-built input",
        [typeof(McpServerConfiguration)] = "a caller-built input",
        [typeof(McpStdioServerConfiguration)] = "a caller-built input: one MCP server as a caller configures it",
        [typeof(McpSseServerConfiguration)] = "a caller-built input: one MCP server as a caller configures it",
        [typeof(McpHttpServerConfiguration)] = "a caller-built input: one MCP server as a caller configures it",
        [typeof(McpSdkServerConfiguration)] = "a caller-built input: one MCP server as a caller configures it",
        [typeof(McpServerToolPolicy)] = "a caller-built input: what a caller asks of one tool of an MCP server",
        [typeof(McpToolAttribute)] = "a caller-built input: the attribute on a caller's tool method",
        [typeof(ItemMaxLengthAttribute)] = "a caller-built input: the attribute on a member of a caller's output type",
        [typeof(HookOutput)] = "a caller-built input: a hook's answer",
        [typeof(ToolPermissionResult)] = "a caller-built input: the permission callback's answer",
        [typeof(ToolResult)] = "a caller-built input: a caller's tool's answer",
    };

    // Keys a recorded init frame or initialize reply carries that no model reads, each with its gap,
    // by the path the check below reports. They are held apart from that check and nothing else, and
    // a test holds that each is still recorded and still unread, so an entry goes when its key is
    // typed. An entry is a gap reported, not a verdict.
    private static readonly Dictionary<string, string> UnreadKeys = new(StringComparer.Ordinal)
    {
        ["init plugins[].source"] = "the binary writes where each plugin it loaded came from beside its name and path, \"agents-md@builtin\" (trivial.stdout.jsonl:2) on 2.1.276 and 2.1.283, and PluginInfo reads name, path and version",
    };

    // The bag clause's three exceptions, by name and nothing else, each one type a caller reads whole
    // with one member the binary sends or does not: the result, its subtype the ending; the stream
    // event, its time to first token; the context category, whether it is deferred. Each with the kind
    // the check finds selecting on it.
    private static readonly Dictionary<Type, string> BagExceptions = new()
    {
        [typeof(ResultMessage)] = nameof(ResultMessage.Subtype),
        [typeof(StreamEventMessage)] = nameof(StreamEventMessage.EventType),
        [typeof(ContextUsageCategory)] = nameof(ContextUsageCategory.Kind),
    };

    private static readonly Type[] Models = [.. ModelsOf(Library.GetExportedTypes(), NotModels.Keys)];

    private static readonly HashSet<Type> RoundTrip = RoundTripTypes(Library, typeof(ToolInput), typeof(ToolOutput));

    [TestMethod]
    public void EveryTypeLeftOutOfTheModelsIsAnExportedClassWithAReason()
    {
        foreach (var (type, reason) in NotModels)
        {
            Assert.IsTrue(type.IsPublic && type.IsClass && !(type.IsAbstract && type.IsSealed), $"{type.Name} is not an exported class");
            Assert.IsFalse(string.IsNullOrWhiteSpace(reason), $"{type.Name} is left out for no reason");
        }

        Assert.IsTrue(Models.Length > 30, $"{Models.Length} models found: the scan has lost them");
        CollectionAssert.IsSubsetOf(
            new[] { typeof(AgentMessage), typeof(AgentTrace), typeof(ToolCall), typeof(ContentBlock), typeof(ToolInput), typeof(ToolOutput), typeof(Usage), typeof(ModelUsage), typeof(HookInput) },
            Models);
    }

    [TestMethod]
    public void NoModelHasAPublicConstructorButTheRoundTripToolTypes()
    {
        foreach (var type in Models.Where(type => !RoundTrip.Contains(type)))
        {
            Assert.AreEqual(
                0,
                type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length,
                $"{type.Name} has a public constructor");
        }
    }

    [TestMethod]
    public void NoModelHasAPublicSetterTheRoundTripToolTypesIncluded()
    {
        foreach (var type in Models.Concat(RoundTrip))
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.IsNull(property.GetSetMethod(nonPublic: false), $"{type.Name}.{property.Name} has a public setter");
            }
        }
    }

    // A caller derives no model of their own: an abstract model's constructors are the library's.
    [TestMethod]
    public void NoCallerCanDeriveFromAnAbstractModelToolInputAndToolOutputIncluded()
    {
        var abstractModels = Models.Where(type => type.IsAbstract).ToArray();
        CollectionAssert.IsSubsetOf(new[] { typeof(AgentMessage), typeof(ContentBlock), typeof(ToolInput), typeof(ToolOutput) }, abstractModels);
        foreach (var type in abstractModels)
        {
            foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.IsFalse(
                    constructor.IsPublic || constructor.IsFamily || constructor.IsFamilyOrAssembly,
                    $"{type.Name} has a constructor a caller's class can call");
            }
        }

        foreach (var toolBase in new[] { typeof(ToolInput), typeof(ToolOutput) })
        {
            Assert.AreEqual(
                0,
                toolBase.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length,
                $"{toolBase.Name} has members of its own");
        }
    }

    // A tool's input and output go in as well as out: each concrete type deriving from ToolInput or
    // ToolOutput, and each class of the library its properties reach, is built by a caller through
    // one public constructor over its members.
    [TestMethod]
    public void EveryRoundTripToolTypeHasExactlyOnePublicConstructor()
    {
        foreach (var type in RoundTrip)
        {
            Assert.AreEqual(
                1,
                type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length,
                $"{type.Name} does not have exactly one public constructor");
        }
    }

    // The walk the round-trip tests stand on, held on a shape of this file's own, since the
    // library has no concrete tool type yet: it finds the concrete types deriving from the base and
    // every class of theirs a property reaches, through nullables, lists and dictionaries, two
    // classes down (SampleSpan, reached through SampleEdit), and the concrete classes deriving from
    // an abstract class a property carries (SampleFlag, reached only as a SampleNoteBase); and
    // nothing else.
    [TestMethod]
    public void TheRoundTripWalkFindsTheConcreteToolTypesAndEveryClassTheirPropertiesReach()
    {
        var found = RoundTripTypes(typeof(ShapeTests).Assembly, typeof(SampleToolBase));

        CollectionAssert.AreEquivalent(
            new[] { typeof(SampleToolInput), typeof(SampleEdit), typeof(SampleSpan), typeof(SampleRange), typeof(SampleNote), typeof(SampleFlag) },
            found.ToArray());
    }

    // Usage round-trips through System.Text.Json off its own contract, and so does every class it
    // holds: each is read back through its internal constructor, marked [JsonConstructor]. No other
    // exported type is promised that yet, and none carries the mark. The round trip itself is held in
    // Models/UsageRoundTripTests.cs.
    [TestMethod]
    public void UsageAndWhatItHoldsAreReadBackThroughAMarkedInternalConstructorAndNothingElseIsMarked()
    {
        var usage = Reached(Library, [typeof(Usage)]);
        CollectionAssert.AreEquivalent(
            new[]
            {
                typeof(Usage), typeof(CacheCreationTokens), typeof(OutputTokensDetails), typeof(ServerToolUsage),
                typeof(MessageUsageIteration), typeof(CompactionUsageIteration), typeof(AdvisorMessageUsageIteration),
                typeof(FallbackMessageUsageIteration), typeof(UnknownUsageIteration), typeof(FallbackCredit),
                typeof(RedeemedFallbackCreditStatus), typeof(NotAppliedFallbackCreditStatus), typeof(UnknownFallbackCreditStatus),
            },
            usage.ToArray());

        foreach (var type in Library.GetExportedTypes())
        {
            var marked = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(constructor => constructor.IsDefined(typeof(System.Text.Json.Serialization.JsonConstructorAttribute)))
                .ToArray();
            if (usage.Contains(type))
            {
                Assert.AreEqual(1, marked.Length, $"{type.Name} is held by Usage and has no marked constructor");
                Assert.IsTrue(marked[0].IsAssembly, $"{type.Name}'s marked constructor is not internal");
            }
            else
            {
                Assert.AreEqual(0, marked.Length, $"{type.Name} is marked for the round trip and only Usage is promised it");
            }
        }
    }

    [TestMethod]
    public void EveryConcreteModelHasABuilderOnTheModelFactoryTheRoundTripToolTypesIncluded()
    {
        foreach (var type in Models)
        {
            Assert.IsNull(MissingBuilder(type), MissingBuilder(type));
        }
    }

    // The bag clause, read off the recordings: every model a recorded stdout hands out, gathered by
    // its type. A kind member selects a member when two values it names are recorded on one type,
    // the member set on every instance of one and on none of the other; a type split by its kind
    // carries one value of it and so selects nothing. The three exceptions are the clause's, and the
    // check sees each of them, so each stands on a selection the recordings make: the result's
    // subtype selects what the binary writes on a success and on an error, the stream event's kind
    // its time to first token, the context category's kind whether it is deferred.
    [TestMethod]
    public void NoModelIsABagWhoseKindSaysWhichMembersAreSetButTheResultTheStreamEventAndTheContextCategory()
    {
        var found = Selections(RecordedModels.Value).ToArray();
        var bags = found
            .Where(selection => !BagExceptions.ContainsKey(selection.Model))
            .Select(selection => selection.ToString())
            .ToArray();

        Assert.AreEqual(0, bags.Length, string.Join("\n", bags));
        CollectionAssert.AreEquivalent(
            new[] { typeof(ResultMessage), typeof(StreamEventMessage), typeof(ContextUsageCategory) },
            BagExceptions.Keys.ToArray());
        foreach (var (type, kind) in BagExceptions)
        {
            Assert.IsTrue(
                found.Any(selection => selection.Model == type && selection.Kind.Name == kind),
                $"{type.Name}.{kind} selects no member in the recordings: the exception stands on nothing");
        }

        CollectionAssert.AreEquivalent(
            new[]
            {
                $"{nameof(StreamEventMessage)}.{nameof(StreamEventMessage.EventType)} selects {nameof(StreamEventMessage.TimeToFirstToken)}",
                $"{nameof(ContextUsageCategory)}.{nameof(ContextUsageCategory.Kind)} selects {nameof(ContextUsageCategory.IsDeferred)}",
            },
            found
                .Where(selection => selection.Model != typeof(ResultMessage) && BagExceptions.ContainsKey(selection.Model))
                .Select(selection => $"{selection.Model.Name}.{selection.Kind.Name} selects {selection.Member.Name}")
                .Distinct()
                .ToArray(),
            "the stream event and the context category are each one member the binary sends or does not");
        CollectionAssert.IsSubsetOf(
            new[]
            {
                typeof(LocalAgentTaskStartedMessage), typeof(LocalBashTaskStartedMessage), typeof(WriteCreateToolOutput), typeof(WriteUpdateToolOutput),
                typeof(ShellStopHookBackgroundTask), typeof(SubagentStopHookBackgroundTask), typeof(ResultMessage), typeof(StreamEventMessage), typeof(ContextUsageCategory),
            },
            RecordedModels.Value.Select(model => model.GetType()).Distinct().ToArray(),
            "the recordings no longer hand out a model the bag check reads");
    }

    // The check itself, held on shapes of this file's own: a kind that sets a member under one of its
    // values and never under another is flagged; a kind whose values each carry a member some of the
    // time is not; an absent kind names no value; a closed set that is not a kind, a status, selects
    // nothing however its members fall.
    [TestMethod]
    public void TheBagCheckFlagsAKindThatSelectsAMemberAndNoKindThatSelectsNone()
    {
        object[] instances =
        [
            new SampleBag(TaskType.LocalAgent, "read the notes", true),
            new SampleBag(TaskType.LocalAgent, "write the notes", null),
            new SampleBag(TaskType.LocalBash, null, true),
            new SampleBag(TaskType.LocalBash, null, false),
            new SampleKindSelectingNothing(TaskType.LocalAgent, "one"),
            new SampleKindSelectingNothing(TaskType.LocalAgent, null),
            new SampleKindSelectingNothing(TaskType.LocalBash, "two"),
            new SampleKindSelectingNothing(default, null),
            new SampleAbsentKind(TaskType.LocalBash, "sleep 5"),
            new SampleAbsentKind(null, null),
            new SampleStatus(BackgroundTaskStatus.Running, "sleep 5"),
            new SampleStatus(BackgroundTaskStatus.Completed, null),
        ];

        CollectionAssert.AreEquivalent(
            new[] { "SampleBag.TaskType selects Prompt: set under local_agent, never under local_bash" },
            Selections(instances).Select(selection => selection.ToString()).ToArray());
    }

    // A model types the whole of what the binary sends for it, read off the recordings for the two
    // frames a session opens on: every key of every recorded init frame, and of every initialize
    // reply's body, at every depth, is taken out of its line — out of every entry of a list at once —
    // and the line is read again. A key is read when taking it out changes what the line hands out, on
    // any one recording; one whose absence changes nothing is read by no model, and fails.
    [TestMethod]
    public void EveryKeyARecordedInitFrameOrInitializeReplyCarriesIsReadByTheModelItIsHandedOutAs()
    {
        var unread = RecordedKeys.Value
            .Where(key => !key.Value && !UnreadKeys.ContainsKey(key.Key))
            .Select(key => key.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.AreEqual(0, unread.Length, $"read by no model: {string.Join("; ", unread)}");
        Assert.IsTrue(RecordedKeys.Value.Count >= 74, $"{RecordedKeys.Value.Count} keys found, fewer than the 74 the recordings carry today: the walk has lost a frame or stopped descending");
        CollectionAssert.IsSubsetOf(
            new[] { "init scratchpad_path", "init per_turn_effort_active", "init view_mode", "init memory_paths.auto", "initialize commands[].builtin", "initialize account.email" },
            RecordedKeys.Value.Keys.ToArray(),
            "the recordings no longer carry a key the check reads");
    }

    // Each key held apart as unread is still recorded, and still read by no model.
    [TestMethod]
    public void EveryKeyHeldApartAsUnreadIsStillRecordedAndStillUnread()
    {
        foreach (var (key, gap) in UnreadKeys)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(gap), $"{key} is held apart for no gap");
            Assert.IsTrue(RecordedKeys.Value.TryGetValue(key, out var read), $"{key} is held apart as unread and no recording carries it: its entry goes");
            Assert.IsFalse(read, $"{key} is held apart as unread and a model reads it now: its entry goes");
        }
    }

    // The check itself, held on lines of this file's own: a key the library reads is read, at the top,
    // inside an object and inside a list's entries, and a key it has no member for is not, at any of
    // the three; the envelope's own keys are read, since taking one out changes what the line is.
    [TestMethod]
    public void TheUnreadKeyCheckFlagsAKeyNoModelReadsAndNoKeyAModelReads()
    {
        var init = Keys(
            """{"type":"system","subtype":"init","session_id":"s-1","uuid":"u-1","view_mode":"default","moon_phase":"waxing","plugins":[{"name":"mill","path":"/p","tint":"blue"}]}""",
            new Dictionary<string, string>());
        CollectionAssert.AreEquivalent(
            new[] { "init moon_phase", "init plugins[].tint" },
            init.Where(key => !key.Value).Select(key => key.Key).ToArray());
        CollectionAssert.IsSubsetOf(
            new[] { "init type", "init subtype", "init session_id", "init uuid", "init view_mode", "init plugins", "init plugins[].name", "init plugins[].path" },
            init.Where(key => key.Value).Select(key => key.Key).ToArray());

        var reply = Keys(
            """{"type":"control_response","response":{"subtype":"success","request_id":"req_1","response":{"pid":7,"tide":"high","account":{"email":"user@example.com","shade":"dark"},"commands":[{"name":"clear","description":"clears","argumentHint":"","builtin":true,"hue":"red"}]}}}""",
            new Dictionary<string, string> { ["req_1"] = "initialize" });
        CollectionAssert.AreEquivalent(
            new[] { "initialize tide", "initialize account.shade", "initialize commands[].hue" },
            reply.Where(key => !key.Value).Select(key => key.Key).ToArray());
        CollectionAssert.IsSubsetOf(
            new[] { "initialize pid", "initialize account", "initialize account.email", "initialize commands", "initialize commands[].builtin", "initialize commands[].name" },
            reply.Where(key => key.Value).Select(key => key.Key).ToArray());
    }

    // Every key path of every recorded init frame and initialize reply, and whether a model reads it.
    private static readonly Lazy<Dictionary<string, bool>> RecordedKeys = new(() =>
    {
        var keys = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var fixture in RecordedFixtureTests.RecordedStdouts().Select(row => (string)row[0]))
        {
            var loaded = Fixture.Load(fixture);
            var asked = RecordedFixtureTests.Asked(loaded.Stdin);
            foreach (var line in loaded.Stdout.Where(line => !string.IsNullOrWhiteSpace(line)))
            {
                foreach (var (path, read) in Keys(line, asked))
                {
                    keys[path] = read || keys.GetValueOrDefault(path);
                }
            }
        }

        return keys;
    });

    // The key paths of one line and whether taking each out changes what the line hands out; nothing
    // for a line that is neither an init frame nor the reply to an initialize this library wrote. A
    // path is the frame's name and the keys down to it, a list's entries as [].
    private static Dictionary<string, bool> Keys(string line, IReadOnlyDictionary<string, string> asked)
    {
        var keys = new Dictionary<string, bool>(StringComparer.Ordinal);
        var root = JsonNode.Parse(line)!.AsObject();
        var (frame, carrier) = Carrier(root, asked);
        if (carrier is null)
        {
            return keys;
        }

        var whole = HandedOutAs(root.ToJsonString(), asked);
        foreach (var path in Paths(carrier, string.Empty).Distinct())
        {
            var taken = root.DeepClone().AsObject();
            TakeOut(Carrier(taken, asked).Carrier!, path.Split('.'), 0);
            keys[$"{frame} {path}"] = HandedOutAs(taken.ToJsonString(), asked) != whole;
        }

        return keys;
    }

    // The init frame is its own carrier; the initialize reply carries its body under response.response.
    private static (string Frame, JsonObject? Carrier) Carrier(JsonObject root, IReadOnlyDictionary<string, string> asked)
    {
        if (Text(root, "type") == "system" && Text(root, "subtype") == "init")
        {
            return ("init", root);
        }

        if (Text(root, "type") == "control_response"
            && root["response"] is JsonObject response
            && Text(response, "subtype") == "success"
            && Text(response, "request_id") is { } id
            && asked.GetValueOrDefault(id) == "initialize"
            && response["response"] is JsonObject body)
        {
            return ("initialize", body);
        }

        return (string.Empty, null);
    }

    private static string? Text(JsonObject node, string key) =>
        node[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static IEnumerable<string> Paths(JsonObject map, string prefix)
    {
        foreach (var (key, value) in map)
        {
            var path = prefix + key;
            yield return path;
            var inside = value switch
            {
                JsonObject nested => Paths(nested, $"{path}."),
                JsonArray list => list.OfType<JsonObject>().SelectMany(entry => Paths(entry, $"{path}[].")),
                _ => [],
            };
            foreach (var inner in inside)
            {
                yield return inner;
            }
        }
    }

    private static void TakeOut(JsonNode? node, string[] path, int depth)
    {
        var key = path[depth];
        var inList = key.EndsWith("[]", StringComparison.Ordinal);
        var name = inList ? key[..^2] : key;
        if (node is not JsonObject map)
        {
            return;
        }

        if (depth == path.Length - 1)
        {
            map.Remove(name);
            return;
        }

        if (inList && map[name] is JsonArray list)
        {
            foreach (var entry in list)
            {
                TakeOut(entry, path, depth + 1);
            }
        }
        else if (!inList)
        {
            TakeOut(map[name], path, depth + 1);
        }
    }

    // What a line hands out, written down member by member, the line as the binary wrote it left out:
    // two lines hand out the same when this is the same.
    private static string HandedOutAs(string line, IReadOnlyDictionary<string, string> asked)
    {
        var decoded = new FrameCodec().Decode(line);
        if (!decoded.IsOk)
        {
            return "rejected";
        }

        var written = new System.Text.StringBuilder();
        foreach (var handed in RecordedFixtureTests.HandedOut(decoded.Value, asked))
        {
            WriteDown(handed, written, 0);
        }

        return written.ToString();
    }

    private static void WriteDown(object? value, System.Text.StringBuilder written, int depth)
    {
        if (value is null || depth > 32)
        {
            written.Append("null;");
            return;
        }

        var type = value.GetType();
        if (ClosedSets.Is(type) || type.IsPrimitive || type.IsEnum || value is string or decimal or TimeSpan or DateTimeOffset or Guid or Uri)
        {
            written.Append(type.Name).Append(':').Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)).Append(';');
            return;
        }

        if (value is JsonElement element)
        {
            written.Append(element.GetRawText()).Append(';');
            return;
        }

        if (value is IDictionary map)
        {
            written.Append('{');
            foreach (DictionaryEntry entry in map)
            {
                written.Append(entry.Key).Append('=');
                WriteDown(entry.Value, written, depth + 1);
            }

            written.Append('}');
            return;
        }

        if (value is IEnumerable items)
        {
            written.Append('[');
            foreach (var item in items)
            {
                WriteDown(item, written, depth + 1);
            }

            written.Append(']');
            return;
        }

        written.Append(type.Name).Append('{');
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0 && property.Name != nameof(AgentMessage.Raw)))
        {
            written.Append(property.Name).Append('=');
            WriteDown(property.GetValue(value), written, depth + 1);
        }

        written.Append('}');
    }

    private sealed record Selection(Type Model, PropertyInfo Kind, PropertyInfo Member, string SetUnder, string NeverUnder)
    {
        public override string ToString() =>
            $"{Model.Name}.{Kind.Name} selects {Member.Name}: set under {SetUnder}, never under {NeverUnder}";
    }

    private static IEnumerable<Selection> Selections(IEnumerable<object> instances)
    {
        foreach (var model in instances.GroupBy(instance => instance.GetType()))
        {
            var properties = model.Key.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetIndexParameters().Length == 0)
                .ToArray();
            foreach (var kind in properties.Where(IsKind))
            {
                var values = model
                    .Where(instance => IsSet(kind.GetValue(instance)))
                    .GroupBy(instance => kind.GetValue(instance)!.ToString()!, StringComparer.Ordinal)
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .ToArray();
                foreach (var member in properties.Where(property => property != kind))
                {
                    var always = values.FirstOrDefault(value => value.All(instance => IsSet(member.GetValue(instance))));
                    var never = values.FirstOrDefault(value => value.All(instance => !IsSet(member.GetValue(instance))));
                    if (always is not null && never is not null)
                    {
                        yield return new Selection(model.Key, kind, member, always.Key, never.Key);
                    }
                }
            }
        }
    }

    // A kind member: a closed set carried under the name the wire gives a kind, a type, a subtype, a
    // kind or an event, alone or ending the member's name.
    private static bool IsKind(PropertyInfo property) =>
        ClosedSets.Is(Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType)
        && KindName().IsMatch(property.Name);

    [GeneratedRegex("(Type|Subtype|Kind|Event)$")]
    private static partial Regex KindName();

    // Whether a member carries anything: null does not, nor an empty list, which is what the library
    // hands out for a list the binary left out, nor a closed set's default, which no value was given.
    private static bool IsSet(object? value) => value switch
    {
        null => false,
        string => true,
        JsonElement element => element.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null),
        IEnumerable items => items.GetEnumerator().MoveNext(),
        _ when ClosedSets.Is(value.GetType()) => value.ToString()!.Length != 0,
        _ => true,
    };

    // Every model the recorded stdouts hand out, as RecordedFixtureTests reads them, and every model
    // of the library their members reach.
    private static readonly Lazy<object[]> RecordedModels = new(() =>
    {
        var found = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var fixture in RecordedFixtureTests.RecordedStdouts().Select(row => (string)row[0]))
        {
            var codec = new FrameCodec();
            var loaded = Fixture.Load(fixture);
            var asked = RecordedFixtureTests.Asked(loaded.Stdin);
            foreach (var line in loaded.Stdout.Where(line => !string.IsNullOrWhiteSpace(line)))
            {
                var decoded = codec.Decode(line);
                Assert.IsTrue(decoded.IsOk, $"{fixture}.stdout.jsonl: a line was rejected");
                foreach (var handed in RecordedFixtureTests.HandedOut(decoded.Value, asked))
                {
                    Gather(handed, found, 0);
                }
            }
        }

        return [.. found];
    });

    private static void Gather(object? value, HashSet<object> found, int depth)
    {
        if (value is null || depth > 32 || value is string or JsonElement)
        {
            return;
        }

        if (value is IDictionary map)
        {
            foreach (DictionaryEntry entry in map)
            {
                Gather(entry.Value, found, depth + 1);
            }

            return;
        }

        if (value is IEnumerable items)
        {
            foreach (var item in items)
            {
                Gather(item, found, depth + 1);
            }

            return;
        }

        var type = value.GetType();
        if (!type.IsClass || type.Assembly != Library || !found.Add(value))
        {
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.GetIndexParameters().Length == 0))
        {
            Gather(property.GetValue(value), found, depth + 1);
        }
    }

    private static string? MissingBuilder(Type type) =>
        type.IsAbstract
        || typeof(ClaudeAgentModelFactory).GetMethods(BindingFlags.Public | BindingFlags.Static).Any(method => method.ReturnType == type)
            ? null
            : $"ClaudeAgentModelFactory has no method returning {type.Name}";

    private static IEnumerable<Type> ModelsOf(IEnumerable<Type> exported, IEnumerable<Type> notModels)
    {
        var left = notModels.ToHashSet();
        return exported.Where(type => type.IsClass && !(type.IsAbstract && type.IsSealed) && !left.Contains(type));
    }

    // The concrete types deriving from the bases, and every class of the same assembly their public
    // properties reach, concrete subclasses of a reached abstract class included.
    private static HashSet<Type> RoundTripTypes(Assembly assembly, params Type[] bases) =>
        Reached(assembly, assembly.GetTypes().Where(type => !type.IsAbstract && bases.Any(toolBase => type.IsSubclassOf(toolBase))));

    // The start types and every class of the same assembly their public properties reach, concrete
    // subclasses of a reached abstract class included.
    private static HashSet<Type> Reached(Assembly assembly, IEnumerable<Type> start)
    {
        var types = assembly.GetTypes();
        var found = new HashSet<Type>();
        var pending = new Queue<Type>(start);
        while (pending.TryDequeue(out var type))
        {
            if (!found.Add(type))
            {
                continue;
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var reached in Carried(property.PropertyType).Where(carried => carried.IsClass && !carried.IsArray && carried.Assembly == assembly))
                {
                    if (!reached.IsAbstract)
                    {
                        pending.Enqueue(reached);
                    }

                    foreach (var derived in types.Where(candidate => !candidate.IsAbstract && candidate.IsSubclassOf(reached)))
                    {
                        pending.Enqueue(derived);
                    }
                }
            }
        }

        return found;
    }

    // A property's type and every type it carries: an array's element, a generic's arguments.
    private static IEnumerable<Type> Carried(Type type)
    {
        yield return type;
        if (type.IsArray)
        {
            foreach (var element in Carried(type.GetElementType()!))
            {
                yield return element;
            }
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments().SelectMany(Carried))
            {
                yield return argument;
            }
        }
    }

    private abstract class SampleToolBase;

    private sealed class SampleToolInput(IReadOnlyList<SampleEdit> edits, SampleRange? range, IReadOnlyDictionary<string, SampleNote[]> notes, SampleNoteBase? pinned, string path, SampleMode mode)
        : SampleToolBase
    {
        public IReadOnlyList<SampleEdit> Edits { get; } = edits;

        public SampleNoteBase? Pinned { get; } = pinned;

        public SampleRange? Range { get; } = range;

        public IReadOnlyDictionary<string, SampleNote[]> Notes { get; } = notes;

        public string Path { get; } = path;

        public SampleMode Mode { get; } = mode;
    }

    private sealed class SampleEdit(string text, SampleSpan span)
    {
        public string Text { get; } = text;

        public SampleSpan Span { get; } = span;
    }

    private sealed class SampleSpan(int length)
    {
        public int Length { get; } = length;
    }

    private sealed class SampleRange(int start)
    {
        public int Start { get; } = start;
    }

    private abstract class SampleNoteBase;

    private sealed class SampleNote(string text) : SampleNoteBase
    {
        public string Text { get; } = text;
    }

    private sealed class SampleFlag(bool set) : SampleNoteBase
    {
        public bool Set { get; } = set;
    }

    // Reached by nothing: the walk leaves it.
    private sealed class SampleUnreached(string text)
    {
        public string Text { get; } = text;
    }

    private enum SampleMode
    {
        Replace,
    }

    // A kind that selects a member: a prompt under one value, never under the other.
    private sealed class SampleBag(TaskType taskType, string? prompt, bool? isBackgrounded)
    {
        public TaskType TaskType { get; } = taskType;

        public string? Prompt { get; } = prompt;

        public bool? IsBackgrounded { get; } = isBackgrounded;
    }

    // A kind that selects nothing: its member is set under each value some of the time.
    private sealed class SampleKindSelectingNothing(TaskType taskType, string? command)
    {
        public TaskType TaskType { get; } = taskType;

        public string? Command { get; } = command;
    }

    // A kind named once and absent once: an absent kind names no value to hold a member against.
    private sealed class SampleAbsentKind(TaskType? taskType, string? command)
    {
        public TaskType? TaskType { get; } = taskType;

        public string? Command { get; } = command;
    }

    // A closed set that is not a kind.
    private sealed class SampleStatus(BackgroundTaskStatus status, string? output)
    {
        public BackgroundTaskStatus Status { get; } = status;

        public string? Output { get; } = output;
    }
}
