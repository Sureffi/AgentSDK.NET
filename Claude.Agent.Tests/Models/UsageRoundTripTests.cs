using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Claude.Agent.Tests.Replay;
using Claude.Agent.Tests.Wire;

namespace Claude.Agent.Tests.Models;

// Usage round-trips: JsonSerializer.Serialize and JsonSerializer.Deserialize<Usage> under default
// options, and under options that refuse an unmapped member, hand back the same Usage member for
// member, off its own contract, so a caller can keep what it was handed. The members go under the
// names the binary writes the usage with; the kind of an iteration or of a fallback credit's
// outcome goes under "type", as the binary writes it, and the unknown of either set carries the
// JSON it was read from under "raw". That only Usage and what it holds are marked for this is held
// in Architecture/ShapeTests.cs.
[TestClass]
public sealed class UsageRoundTripTests
{
    [TestMethod]
    public void EveryRecordedResultsUsageReadsBackMemberForMember()
    {
        var usages = RecordedResults().Select(result => result.Usage!).ToArray();
        Assert.IsTrue(usages.Length >= 40, $"{usages.Length} recorded results carry a usage: the scan has lost them");

        foreach (var usage in usages)
        {
            Same(usage, RoundTrip(usage), nameof(Usage));
        }
    }

    // Written without its nulls, a recorded result's Usage is the usage object the binary wrote for
    // it, key for key: the names are the wire's, and nothing the binary sent is left out.
    [TestMethod]
    public void EveryRecordedResultsUsageIsWrittenUnderTheNamesTheBinaryWroteItWith()
    {
        foreach (var result in RecordedResults())
        {
            var wire = result.Raw!.Value.GetProperty("usage");
            AssertWrittenAs(wire, result.Usage!);
        }
    }

    // The iteration kinds and the fallback credit no recording carries, over hand-written lines of
    // the declared shape.
    [TestMethod]
    public void EveryIterationKindAndEveryFallbackCreditOutcomeIsWrittenUnderTheNamesTheBinaryWritesThemWith()
    {
        string[] lines =
        [
            """
            {"iterations": [
              {"type": "message", "input_tokens": 1, "output_tokens": 2, "model": "claude-haiku-4-5"},
              {"type": "compaction", "input_tokens": 3, "output_tokens": 4, "cache_creation": {"ephemeral_1h_input_tokens": 5, "ephemeral_5m_input_tokens": 6}},
              {"type": "advisor_message", "cache_read_input_tokens": 7, "model": "claude-haiku-4-5"},
              {"type": "fallback_message", "cache_creation_input_tokens": 8, "model": "claude-opus-4-5"}],
             "fallback_credit": {"status": {"type": "not_applied", "reason": "variant_fields_present", "remove_to_redeem": ["tools", "system"]}}}
            """,
            """{"fallback_credit": {"status": {"type": "redeemed"}}, "speed": "fast", "service_tier": "priority"}""",
        ];

        foreach (var line in lines)
        {
            var wire = Recorded.Json(line);
            AssertWrittenAs(wire, UsageParser.ParseUsage(wire)!);
        }
    }

    [TestMethod]
    public void AUsageTheFactoryBuildsWithEveryMemberSetReadsBackMemberForMember()
    {
        FallbackCreditStatus[] outcomes =
        [
            ClaudeAgentModelFactory.NotAppliedFallbackCreditStatus(FallbackCreditNotAppliedReason.VariantFieldsPresent, ["tools", "system"]),
            ClaudeAgentModelFactory.RedeemedFallbackCreditStatus(),
            ClaudeAgentModelFactory.UnknownFallbackCreditStatus("deferred", Recorded.Json("""{"type": "deferred", "until": "later"}""")),
        ];

        foreach (var outcome in outcomes)
        {
            var usage = Built(outcome);
            Same(usage, RoundTrip(usage), $"{nameof(Usage)} with {outcome.GetType().Name}");
        }
    }

    // A closed set's default, which names nothing, is written as the empty string and reads back as
    // the default: a service tier, a speed and a not-applied reason. A string cannot keep the default
    // apart from a value the empty string names, so that value reads back as the default too.
    [TestMethod]
    public void AUsageTheFactoryBuildsWithEachClosedSetsDefaultReadsBackMemberForMember()
    {
        var usage = ClaudeAgentModelFactory.Usage(
            serviceTier: default(ServiceTier),
            speed: default(InferenceSpeed),
            fallbackCredit: ClaudeAgentModelFactory.FallbackCredit(
                ClaudeAgentModelFactory.NotAppliedFallbackCreditStatus(default(FallbackCreditNotAppliedReason))));

        var written = JsonSerializer.SerializeToNode(usage)!;
        Assert.AreEqual(string.Empty, written["service_tier"]!.GetValue<string>());
        Assert.AreEqual(string.Empty, written["speed"]!.GetValue<string>());
        Assert.AreEqual(string.Empty, written["fallback_credit"]!["status"]!["reason"]!.GetValue<string>());
        Same(usage, RoundTrip(usage), nameof(Usage));

        var named = RoundTrip(ClaudeAgentModelFactory.Usage(serviceTier: new ServiceTier(string.Empty)));
        Assert.AreEqual(default(ServiceTier), named.ServiceTier);
    }

    // What decides that an entry reads back as the unknown is the JSON it carries, not its kind: an
    // unknown of a kind the library types, which the factory can build, reads back as that unknown.
    [TestMethod]
    public void AnUnknownOfAKindTheLibraryTypesReadsBackAsTheUnknown()
    {
        var usage = ClaudeAgentModelFactory.Usage(
            iterations: [ClaudeAgentModelFactory.UnknownUsageIteration("message", Recorded.Json("""{"type": "message", "input_tokens": "many"}"""))],
            fallbackCredit: ClaudeAgentModelFactory.FallbackCredit(
                ClaudeAgentModelFactory.UnknownFallbackCreditStatus("redeemed", Recorded.Json("""{"type": "redeemed", "shift": "none"}"""))));

        var read = RoundTrip(usage);

        Assert.IsInstanceOfType<UnknownUsageIteration>(read.Iterations.Single());
        Assert.IsInstanceOfType<UnknownFallbackCreditStatus>(read.FallbackCredit?.Status);
        Same(usage, read, nameof(Usage));
    }

    // A caller whose options refuse a member the contract does not name reads back what it wrote, and
    // reads the usage the binary wrote: the kind under "type" is the family's to read, not a member
    // of the kind's own contract. Every recorded usage, every iteration kind and fallback credit
    // outcome, and each unknown.
    [TestMethod]
    public void UnderDisallowedUnmappedMembersEveryUsageReadsBackAndTheBinarysOwnIsRead()
    {
        var strict = new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        string[] lines =
        [
            """
            {"input_tokens": 1, "output_tokens": 2, "iterations": [
              {"type": "message", "input_tokens": 1, "output_tokens": 2, "model": "claude-haiku-4-5"},
              {"type": "compaction", "input_tokens": 3, "output_tokens": 4, "cache_creation": {"ephemeral_1h_input_tokens": 5, "ephemeral_5m_input_tokens": 6}},
              {"type": "advisor_message", "cache_read_input_tokens": 7, "model": "claude-haiku-4-5"},
              {"type": "fallback_message", "cache_creation_input_tokens": 8, "model": "claude-opus-4-5"}],
             "fallback_credit": {"status": {"type": "not_applied", "reason": "variant_fields_present", "remove_to_redeem": ["tools", "system"]}}}
            """,
            """{"fallback_credit": {"status": {"type": "redeemed"}}, "speed": "fast", "service_tier": "priority"}""",
        ];

        var recorded = RecordedResults().Select(result => result.Usage!).ToArray();
        Assert.IsTrue(recorded.Any(usage => usage.Iterations.Count > 0), "no recorded usage carries an iteration: the scan has lost them");
        Usage[] usages =
        [
            .. recorded,
            .. lines.Select(line => UsageParser.ParseUsage(Recorded.Json(line))!),
            Built(ClaudeAgentModelFactory.RedeemedFallbackCreditStatus()),
            Built(ClaudeAgentModelFactory.UnknownFallbackCreditStatus("deferred", Recorded.Json("""{"type": "deferred", "until": "later"}"""))),
            ClaudeAgentModelFactory.Usage(iterations: [ClaudeAgentModelFactory.UnknownUsageIteration("message", Recorded.Json("""{"type": "message", "input_tokens": "many"}"""))]),
        ];

        foreach (var usage in usages)
        {
            Same(usage, RoundTrip(usage, strict), nameof(Usage));
        }

        foreach (var line in lines)
        {
            Same(UsageParser.ParseUsage(Recorded.Json(line)), JsonSerializer.Deserialize<Usage>(line, strict), nameof(Usage));
        }
    }

    private static Usage Built(FallbackCreditStatus outcome)
    {
        var cache = ClaudeAgentModelFactory.CacheCreationTokens(5, 6);
        return ClaudeAgentModelFactory.Usage(
            1,
            2,
            3,
            4,
            cache,
            ClaudeAgentModelFactory.OutputTokensDetails(7),
            ClaudeAgentModelFactory.ServerToolUsage(8, 9),
            new ServiceTier("gold"),
            InferenceSpeed.Fast,
            "us",
            [
                ClaudeAgentModelFactory.MessageUsageIteration("claude-haiku-4-5", 10, 11, 12, 13, cache),
                ClaudeAgentModelFactory.CompactionUsageIteration(14, 15, 16, 17, cache),
                ClaudeAgentModelFactory.AdvisorMessageUsageIteration("claude-haiku-4-5", 18, 19, 20, 21, cache),
                ClaudeAgentModelFactory.FallbackMessageUsageIteration("claude-opus-4-5", 22, 23, 24, 25, cache),
                ClaudeAgentModelFactory.UnknownUsageIteration("rehearsal", Recorded.Json("""{"type": "rehearsal", "input_tokens": 26}"""), 26, 27, 28, 29, cache),
                ClaudeAgentModelFactory.UnknownUsageIteration(string.Empty, Recorded.Json("30")),
            ],
            ClaudeAgentModelFactory.FallbackCredit(outcome));
    }

    private static IEnumerable<ResultMessage> RecordedResults() =>
        Directory.EnumerateFiles(Fixture.Root, "*.stdout.jsonl")
            .Select(file => Path.GetFileName(file)[..^".stdout.jsonl".Length])
            .Order(StringComparer.Ordinal)
            .SelectMany(Recorded.Messages<ResultMessage>)
            .Where(result => result.Usage is not null);

    private static Usage RoundTrip(Usage usage, JsonSerializerOptions? options = null)
    {
        var written = JsonSerializer.Serialize(usage, options);
        var read = JsonSerializer.Deserialize<Usage>(written, options);
        Assert.IsNotNull(read, written);
        return read;
    }

    private static void AssertWrittenAs(JsonElement wire, Usage usage)
    {
        var written = WithoutAbsent(JsonSerializer.SerializeToNode(usage));
        var expected = WithoutAbsent(JsonNode.Parse(wire.GetRawText()));
        Assert.IsTrue(JsonNode.DeepEquals(expected, written), $"the binary wrote {expected?.ToJsonString()}, the Usage is written {written?.ToJsonString()}");
    }

    // A member the binary left out is null on the model, or an empty list, and is written so; the
    // comparison is of what either side says.
    private static JsonNode? WithoutAbsent(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject members:
                foreach (var name in members.Where(member => member.Value is null or JsonArray { Count: 0 }).Select(member => member.Key).ToArray())
                {
                    members.Remove(name);
                }

                foreach (var member in members)
                {
                    WithoutAbsent(member.Value);
                }

                break;
            case JsonArray items:
                foreach (var item in items)
                {
                    WithoutAbsent(item);
                }

                break;
        }

        return node;
    }

    private static void Same(object? expected, object? actual, string where)
    {
        if (expected is null || actual is null)
        {
            Assert.AreEqual(expected, actual, where);
            return;
        }

        if (expected is JsonElement json)
        {
            Assert.IsTrue(JsonElement.DeepEquals(json, (JsonElement)actual), $"{where}: {json.GetRawText()} read back as {((JsonElement)actual).GetRawText()}");
            return;
        }

        // A list read off the wire and a list read back are the same when they carry the same items
        // in the same order; the concrete list type, an immutable array or a list, is not the claim.
        if (expected is IEnumerable left and not string && actual is IEnumerable right)
        {
            var one = left.Cast<object?>().ToArray();
            var other = right.Cast<object?>().ToArray();
            Assert.AreEqual(one.Length, other.Length, $"{where}.Count");
            for (var index = 0; index < one.Length; index++)
            {
                Same(one[index], other[index], $"{where}[{index}]");
            }

            return;
        }

        var type = expected.GetType();
        if (type.IsValueType || expected is string)
        {
            Assert.AreEqual(expected, actual, where);
            return;
        }

        Assert.AreEqual(type, actual.GetType(), where);
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Same(property.GetValue(expected), property.GetValue(actual), $"{where}.{property.Name}");
        }
    }
}
