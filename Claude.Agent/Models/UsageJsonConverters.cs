using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Claude.Agent;

// The consumer's JSON contract for what a Usage holds, beside the wire decode and apart from it:
// a caller keeping a Usage serializes it and reads it back through System.Text.Json. A closed set
// is its name as a string. Its default, which names nothing, is the empty string and reads back as
// the default, and so does a value the empty string names: a string cannot keep the two apart. A
// family over an abstract base is one object with its kind under
// "type", read back by that kind into the kind's own contract; the family's unknown carries its
// kind and the JSON it was read from as members of its own, "type" and "raw", and is read back as
// itself whenever "raw" is there. A known kind's "type" is the family's and is not handed to the
// kind's contract, so options that refuse a member no contract names read what was written.
internal abstract class ClosedSetJsonConverter<TValue>(Func<string, TValue> create) : JsonConverter<TValue>
    where TValue : struct
{
    public override TValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "" => default,
            var name => create(name!),
        };

    public override void Write(Utf8JsonWriter writer, TValue value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

internal sealed class ServiceTierJsonConverter() : ClosedSetJsonConverter<ServiceTier>(value => new ServiceTier(value));

internal sealed class InferenceSpeedJsonConverter() : ClosedSetJsonConverter<InferenceSpeed>(value => new InferenceSpeed(value));

internal sealed class FallbackCreditNotAppliedReasonJsonConverter()
    : ClosedSetJsonConverter<FallbackCreditNotAppliedReason>(value => new FallbackCreditNotAppliedReason(value));

internal abstract class KindJsonConverter<TBase, TUnknown>(IReadOnlyDictionary<string, Type> kinds) : JsonConverter<TBase>
    where TBase : class
    where TUnknown : TBase
{
    public override TBase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var element = document.RootElement;
        if (element.ValueKind == JsonValueKind.Object
            && !element.TryGetProperty("raw", out _)
            && element.TryGetProperty("type", out var type)
            && type.ValueKind == JsonValueKind.String
            && kinds.TryGetValue(type.GetString()!, out var known))
        {
            return (TBase?)WithoutKind(element).Deserialize(known, options);
        }

        return element.Deserialize<TUnknown>(options);
    }

    // The members a kind's own contract reads: the object less "type", which named the kind and is
    // no member of it.
    private static JsonElement WithoutKind(JsonElement element)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { MaxDepth = int.MaxValue }))
        {
            writer.WriteStartObject();
            foreach (var member in element.EnumerateObject())
            {
                if (!member.NameEquals("type"))
                {
                    member.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        using var members = JsonDocument.Parse(buffer.WrittenMemory, new JsonDocumentOptions { MaxDepth = int.MaxValue });
        return members.RootElement.Clone();
    }

    public override void Write(Utf8JsonWriter writer, TBase value, JsonSerializerOptions options)
    {
        if (value is TUnknown unknown)
        {
            JsonSerializer.Serialize(writer, unknown, options);
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("type", kinds.Single(kind => kind.Value == value.GetType()).Key);
        foreach (var member in JsonSerializer.SerializeToElement(value, value.GetType(), options).EnumerateObject())
        {
            member.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}

internal sealed class UsageIterationJsonConverter() : KindJsonConverter<UsageIteration, UnknownUsageIteration>(
    new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["message"] = typeof(MessageUsageIteration),
        ["compaction"] = typeof(CompactionUsageIteration),
        ["advisor_message"] = typeof(AdvisorMessageUsageIteration),
        ["fallback_message"] = typeof(FallbackMessageUsageIteration),
    });

internal sealed class FallbackCreditStatusJsonConverter() : KindJsonConverter<FallbackCreditStatus, UnknownFallbackCreditStatus>(
    new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["redeemed"] = typeof(RedeemedFallbackCreditStatus),
        ["not_applied"] = typeof(NotAppliedFallbackCreditStatus),
    });
