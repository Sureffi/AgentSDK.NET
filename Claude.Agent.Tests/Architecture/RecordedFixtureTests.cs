using System.Collections;
using System.Reflection;
using System.Text.Json;
using Claude.Agent.Tests.Replay;
using Claude.Agent.Tests.Wire;

namespace Claude.Agent.Tests.Architecture;

// The recordings read whole. Every line of every recorded stdout is decoded and everything it hands
// out is walked — the message, the hook input or the permission request a control request carries,
// and every model nested in them, through properties, lists and dictionaries — and two things fail
// the run: a model of an unknown kind, and a closed-set value the library does not name. Both are
// the page's word: "a recorded fixture that reads as one fails the tests", and "a recorded value it
// does not name fails the tests". The tools lane adds the third, a recorded built-in tool whose
// typed view is null, at TheTypedViews below.
[TestClass]
public sealed class RecordedFixtureTests
{
    private static readonly Assembly Library = typeof(ClaudeAgentClient).Assembly;

    [TestMethod]
    [DynamicData(nameof(RecordedStdouts), DynamicDataSourceType.Method)]
    public void EveryModelEveryRecordedStdoutHandsOutIsKnownWholeAndNamesItsClosedSetValues(string fixture)
    {
        Walk(fixture, new Reach());
        Assert.AreNotEqual(0, Fixture.Load(fixture).Stdout.Length, $"{fixture}.stdout.jsonl is empty");
    }

    // The guard on the walk itself: a recording of control responses alone, as stop-task is, hands
    // out no message, so one fixture's walk is held only to its stdout. That the walk reads the tree at all is
    // held here, over every recording at once — the frames it enters, the nodes it descends to, and
    // the closed-set values it checks, each with the floor the recordings reach today. The nodes and
    // the values are what holds the recursion: a walk that stopped at the models handed out would
    // keep the first count and lose these two.
    [TestMethod]
    public void TheRecordedStdoutsHandOutTheModelsTheWalkReads()
    {
        var reach = new Reach();
        foreach (var fixture in RecordedStdouts())
        {
            Walk((string)fixture[0], reach);
        }

        Assert.IsTrue(reach.Frames >= 832, $"{reach.Frames} models handed out, fewer than the 832 the recordings hand out today");
        Assert.IsTrue(reach.Nodes >= 46656, $"{reach.Nodes} nodes walked, fewer than the 46656 the recordings reach today: the walk has stopped descending");
        Assert.IsTrue(reach.Values >= 2741, $"{reach.Values} closed-set values checked, fewer than the 2741 the recordings carry today");
        Assert.IsTrue(reach.Sets.Count >= 44, $"{reach.Sets.Count} closed sets reached, fewer than the 44 the recordings carry today");
    }

    // What the walk reached, so a walk that silently stops descending is a failure and not a pass.
    private sealed class Reach
    {
        public int Frames { get; set; }

        public int Nodes { get; set; }

        public int Values { get; set; }

        public HashSet<string> Sets { get; } = new(StringComparer.Ordinal);
    }

    private static void Walk(string fixture, Reach reach)
    {
        var codec = new FrameCodec();
        var loaded = Fixture.Load(fixture);
        var asked = Asked(loaded.Stdin);
        var lines = loaded.Stdout;
        for (var number = 1; number <= lines.Length; number++)
        {
            var line = lines[number - 1];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var decoded = codec.Decode(line);
            Assert.IsTrue(decoded.IsOk, $"{fixture}.stdout.jsonl line {number} was rejected: {Recorded.Reason(decoded)}");

            var where = $"{fixture}.stdout.jsonl line {number}";
            foreach (var handed in HandedOut(decoded.Value, asked))
            {
                reach.Frames++;
                Walk(handed, where, handed.GetType().Name, [], 0, reach);
            }
        }
    }

    // What a decoded frame hands a caller: the message, for a control request the hook input or the
    // permission request the library builds from it, and for a control response the payload a verb
    // hands back. A response says only which request it answers, so what it is read as comes from the
    // request the library itself wrote, off the recorded stdin beside it. An interrupt's receipt is
    // read for the log and handed to no caller: it must read, and is not walked.
    internal static IEnumerable<object> HandedOut(InboundFrame frame, IReadOnlyDictionary<string, string> asked)
    {
        switch (frame)
        {
            case MessageFrame message:
                yield return message.Message;
                break;
            case ControlRequestFrame { Request: HookCallbackRequest hook }:
                yield return hook.Input;
                break;
            case ControlRequestFrame { Request: CanUseToolRequest permission }:
                yield return permission.ToPermissionRequest();
                break;
            case ControlResponseFrame { Response: ControlSuccess success }:
                if (Payload(success, asked) is { } payload)
                {
                    yield return payload;
                }

                break;
            default:
                break;
        }
    }

    private static object? Payload(ControlSuccess success, IReadOnlyDictionary<string, string> asked)
    {
        if (!asked.TryGetValue(success.RequestId.ToString(), out var subtype))
        {
            return null;
        }

        var payloads = new PayloadParser();
        return subtype switch
        {
            "initialize" => payloads.ServerInfo(success.Response).OrThrow(),
            "get_context_usage" => payloads.ContextUsage(success.Response).OrThrow(),
            "mcp_status" => payloads.McpStatus(success.Response).OrThrow(),
            "set_permission_mode" => payloads.PermissionMode(success.Response).OrThrow(),
            "rewind_files" => payloads.Rewind(success.Response).OrThrow(),
            "interrupt" => Unwalked(payloads.Interrupt(success.Response).OrThrow()),
            _ => null,
        };
    }

    private static object? Unwalked(object receipt)
    {
        Assert.IsNotNull(receipt);
        return null;
    }

    // The requests this library wrote on the same recording, by id: what each response is read as.
    internal static Dictionary<string, string> Asked(IReadOnlyList<string> stdin)
    {
        var asked = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in stdin.Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            using var document = JsonDocument.Parse(line);
            if (document.RootElement.StringOrNull("type") != "control_request"
                || document.RootElement.ElementOrNull("request") is not { } request
                || document.RootElement.StringOrNull("request_id") is not { } id
                || request.StringOrNull("subtype") is not { } subtype)
            {
                continue;
            }

            asked[id] = subtype;
        }

        return asked;
    }

    private static void Walk(object? value, string where, string member, HashSet<object> seen, int depth, Reach reach)
    {
        if (value is null || depth > 32)
        {
            return;
        }

        reach.Nodes++;
        var type = value.GetType();

        if (ClosedSets.Is(type))
        {
            reach.Values++;
            reach.Sets.Add(type.Name);
            Assert.IsTrue(
                ClosedSets.IsDefined(value),
                $"{where}: {member} carries '{value}', which {type.Name} does not name");
            return;
        }

        if (type.IsPrimitive || type.IsEnum || value is string or JsonElement or decimal or TimeSpan or DateTimeOffset or Guid or Uri)
        {
            return;
        }

        if (value is IDictionary map)
        {
            foreach (DictionaryEntry entry in map)
            {
                Walk(entry.Value, where, $"{member}[{entry.Key}]", seen, depth + 1, reach);
            }

            return;
        }

        if (value is IEnumerable items)
        {
            var index = 0;
            foreach (var item in items)
            {
                Walk(item, where, $"{member}[{index++}]", seen, depth + 1, reach);
            }

            return;
        }

        if (type.Assembly != Library || !seen.Add(value))
        {
            return;
        }

        Assert.IsFalse(
            type.Name.StartsWith("Unknown", StringComparison.Ordinal),
            $"{where}: {member} read as {type.Name}, a kind this library has no type for");

        TheTypedViews(value, where, member);

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            Walk(Read(property, value), where, $"{member}.{property.Name}", seen, depth + 1, reach);
        }
    }

    // The third check: a recorded call of a tool this library types, whose typed view is null beside
    // its raw, fails. It reaches whatever a walked model carries as a tool name beside a typed view
    // and the raw it was read from — the tool-use block, the permission denial, the deferred call,
    // the tool call a hook's input carries — and asks nothing of a model that carries no such pair.
    // A raw that is not a JSON object is no call's arguments: a failed tool answers with a string,
    // and nothing is typed from that.
    private static void TheTypedViews(object value, string where, string member)
    {
        var views = Views.GetOrAdd(value.GetType(), Pairs);
        if (views.Name is null || views.Pairs.Length == 0)
        {
            return;
        }

        if (Read(views.Name, value) is not string name || !TypedTools.Contains(name))
        {
            return;
        }

        foreach (var (typed, raw) in views.Pairs)
        {
            if (Read(raw, value) is not JsonElement element || element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            Assert.IsNotNull(
                Read(typed, value),
                $"{where}: {member}.{typed.Name} is a {name} call this library types and its typed view is null");
        }
    }

    private static readonly HashSet<string> TypedTools = [.. ToolTypes.Names];

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, (PropertyInfo? Name, (PropertyInfo Typed, PropertyInfo Raw)[] Pairs)> Views = new();

    // A model's tool name, and each typed view of its that has a raw member beside it.
    private static (PropertyInfo? Name, (PropertyInfo Typed, PropertyInfo Raw)[] Pairs) Pairs(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var pairs = new List<(PropertyInfo, PropertyInfo)>();
        foreach (var property in properties)
        {
            if (property.PropertyType != typeof(ToolInput) && property.PropertyType != typeof(ToolOutput))
            {
                continue;
            }

            var raw = properties.FirstOrDefault(other => other.Name == $"Raw{property.Name}");
            if (raw is not null)
            {
                pairs.Add((property, raw));
            }
        }

        var name = properties.FirstOrDefault(property => property.Name is "Name" or "ToolName" && property.PropertyType == typeof(string));
        return (name, [.. pairs]);
    }

    private static object? Read(PropertyInfo property, object target)
    {
        try
        {
            return property.GetValue(target);
        }
        catch (TargetInvocationException failure)
        {
            throw new AssertFailedException($"{property.DeclaringType!.Name}.{property.Name} threw: {failure.InnerException?.Message}", failure);
        }
    }

    public static IEnumerable<object[]> RecordedStdouts() =>
        Directory.EnumerateFiles(Fixture.Root, "*.stdout.jsonl")
            .Select(file => Path.GetFileName(file)[..^".stdout.jsonl".Length])
            .Order(StringComparer.Ordinal)
            .Select(name => new object[] { name });
}
