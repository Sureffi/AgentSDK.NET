using System.Text.Json;

namespace Claude.Agent;

// Writing a tool's typed view back as the JSON the binary sent: the wire's keys, in the wire's
// casing, and a member the wire leaves out written only when it is set. A duration goes back in the
// unit it came in, as a whole number when it is one, so that what is written reads as the same view.
internal static class ToolWriting
{
    public static void Text(this Utf8JsonWriter writer, string key, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(key, value);
        }
    }

    public static void Count(this Utf8JsonWriter writer, string key, long? value)
    {
        if (value is { } number)
        {
            writer.WriteNumber(key, number);
        }
    }

    public static void Flag(this Utf8JsonWriter writer, string key, bool? value)
    {
        if (value is { } flag)
        {
            writer.WriteBoolean(key, flag);
        }
    }

    public static void Milliseconds(this Utf8JsonWriter writer, string key, TimeSpan? value)
    {
        if (value is not { } span)
        {
            return;
        }

        if (span.Ticks % TimeSpan.TicksPerMillisecond == 0)
        {
            writer.WriteNumber(key, span.Ticks / TimeSpan.TicksPerMillisecond);
        }
        else
        {
            writer.WriteNumber(key, span.TotalMilliseconds);
        }
    }

    public static void Seconds(this Utf8JsonWriter writer, string key, TimeSpan value)
    {
        if (value.Ticks % TimeSpan.TicksPerSecond == 0)
        {
            writer.WriteNumber(key, value.Ticks / TimeSpan.TicksPerSecond);
        }
        else
        {
            writer.WriteNumber(key, value.TotalSeconds);
        }
    }

    public static void Seconds(this Utf8JsonWriter writer, string key, TimeSpan? value)
    {
        if (value is { } span)
        {
            writer.Seconds(key, span);
        }
    }

    public static void Number(this Utf8JsonWriter writer, string key, double? value)
    {
        if (value is { } number)
        {
            writer.WriteNumber(key, number);
        }
    }

    public static void EpochMilliseconds(this Utf8JsonWriter writer, string key, DateTimeOffset value) =>
        writer.WriteNumber(key, value.ToUnixTimeMilliseconds());

    public static void Texts(this Utf8JsonWriter writer, string key, IReadOnlyList<string> value, bool whenEmpty = false)
    {
        if (value.Count == 0 && !whenEmpty)
        {
            return;
        }

        writer.WriteStartArray(key);
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }

    public static void Items<T>(this Utf8JsonWriter writer, string key, IReadOnlyList<T> value, Action<Utf8JsonWriter, T> write, bool whenEmpty = false)
    {
        if (value.Count == 0 && !whenEmpty)
        {
            return;
        }

        writer.WriteStartArray(key);
        foreach (var item in value)
        {
            write(writer, item);
        }

        writer.WriteEndArray();
    }

    // A list of objects, each written by the one that writes its members: the braces are this one's.
    public static void ItemObjects<T>(this Utf8JsonWriter writer, string key, IReadOnlyList<T> value, Action<Utf8JsonWriter, T> write, bool whenEmpty = false)
    {
        if (value.Count == 0 && !whenEmpty)
        {
            return;
        }

        writer.WriteStartArray(key);
        foreach (var item in value)
        {
            writer.WriteStartObject();
            write(writer, item);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    public static void Nested<T>(this Utf8JsonWriter writer, string key, T? value, Action<Utf8JsonWriter, T> write)
        where T : class
    {
        if (value is null)
        {
            return;
        }

        writer.WriteStartObject(key);
        write(writer, value);
        writer.WriteEndObject();
    }

    public static void TextOrNull(this Utf8JsonWriter writer, string key, string? value)
    {
        if (value is null)
        {
            writer.WriteNull(key);
        }
        else
        {
            writer.WriteString(key, value);
        }
    }

    public static void CountOrNull(this Utf8JsonWriter writer, string key, long? value)
    {
        if (value is { } number)
        {
            writer.WriteNumber(key, number);
        }
        else
        {
            writer.WriteNull(key);
        }
    }

    public static void NestedOrNull<T>(this Utf8JsonWriter writer, string key, T? value, Action<Utf8JsonWriter, T> write)
        where T : class
    {
        if (value is null)
        {
            writer.WriteNull(key);
            return;
        }

        writer.WriteStartObject(key);
        write(writer, value);
        writer.WriteEndObject();
    }

    public static JsonElement Document(Action<Utf8JsonWriter> write)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
