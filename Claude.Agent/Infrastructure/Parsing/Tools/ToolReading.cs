using System.Text.Json;

namespace Claude.Agent;

// Reading and writing the members of a tool's typed view. Every reader degrades: a required key that
// is missing, null or of the wrong JSON kind makes the reader say no, and the tool's whole view is
// then null beside its raw. Nothing here branches on a wire value; the values a view carries are
// read as they stand.
internal static class ToolReading
{
    public static bool Text(this JsonElement raw, string key, out string value)
    {
        if (raw.TryProperty(key, out var found) && found.ValueKind == JsonValueKind.String)
        {
            value = found.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public static string? TextOrNull(this JsonElement raw, string key) =>
        raw.TryProperty(key, out var found) && found.ValueKind == JsonValueKind.String ? found.GetString() : null;

    public static bool Count(this JsonElement raw, string key, out long value)
    {
        if (raw.TryProperty(key, out var found) && found.ValueKind == JsonValueKind.Number && found.TryGetInt64(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    public static long? CountOrNull(this JsonElement raw, string key) =>
        raw.Count(key, out var value) ? value : null;

    public static bool Flag(this JsonElement raw, string key, out bool value)
    {
        if (raw.TryProperty(key, out var found))
        {
            switch (found.ValueKind)
            {
                case JsonValueKind.True:
                    value = true;
                    return true;
                case JsonValueKind.False:
                    value = false;
                    return true;
                default:
                    break;
            }
        }

        value = false;
        return false;
    }

    public static bool? FlagOrNull(this JsonElement raw, string key) =>
        raw.Flag(key, out var value) ? value : null;

    // A closed set's value under a key the wire may leave out. A set converts implicitly from a
    // string, so a bare null in a conditional beside one would mean the set made from a null string;
    // the absent case goes through here instead.
    public static T? SetOrNull<T>(this JsonElement raw, string key, Func<string, T> make)
        where T : struct
    {
        var value = raw.TextOrNull(key);
        return value is null ? null : make(value);
    }

    public static bool Object(this JsonElement raw, string key, out JsonElement value)
    {
        if (raw.TryProperty(key, out var found) && found.ValueKind == JsonValueKind.Object)
        {
            value = found;
            return true;
        }

        value = default;
        return false;
    }

    // A duration the wire carries as a number of milliseconds. A fraction of a millisecond is kept:
    // the ticks are rounded, so what is written back reads as the same span again.
    public static bool Milliseconds(this JsonElement raw, string key, out TimeSpan value)
    {
        if (raw.TryProperty(key, out var found) && found.ValueKind == JsonValueKind.Number && found.TryGetDouble(out var number))
        {
            value = TimeSpan.FromTicks((long)Math.Round(number * TimeSpan.TicksPerMillisecond));
            return true;
        }

        value = default;
        return false;
    }

    public static TimeSpan? MillisecondsOrNull(this JsonElement raw, string key) =>
        raw.Milliseconds(key, out var value) ? value : null;

    // A duration the wire carries as a number of seconds, rounded to ticks for the same reason.
    public static bool Seconds(this JsonElement raw, string key, out TimeSpan value)
    {
        if (raw.TryProperty(key, out var found) && found.ValueKind == JsonValueKind.Number && found.TryGetDouble(out var number))
        {
            value = TimeSpan.FromTicks((long)Math.Round(number * TimeSpan.TicksPerSecond));
            return true;
        }

        value = default;
        return false;
    }

    public static TimeSpan? SecondsOrNull(this JsonElement raw, string key) =>
        raw.Seconds(key, out var value) ? value : null;

    // A number the declaration gives no unit and no whole-number bound: it is kept as it came.
    public static bool Number(this JsonElement raw, string key, out double value)
    {
        if (raw.TryProperty(key, out var found) && found.ValueKind == JsonValueKind.Number && found.TryGetDouble(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    public static double? NumberOrNull(this JsonElement raw, string key) =>
        raw.Number(key, out var value) ? value : null;

    // A time the wire carries as milliseconds since the epoch. A number no time can be made of is a
    // member that does not fit, like a string under a number's key: the view is null beside the raw.
    public static bool EpochMilliseconds(this JsonElement raw, string key, out DateTimeOffset value)
    {
        value = default;
        if (!raw.Count(key, out var milliseconds))
        {
            return false;
        }

        if (milliseconds < DateTimeOffset.MinValue.ToUnixTimeMilliseconds()
            || milliseconds > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds())
        {
            return false;
        }

        value = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        return true;
    }

    public static bool Texts(this JsonElement raw, string key, out IReadOnlyList<string> value)
    {
        value = [];
        if (!raw.TryProperty(key, out var found) || found.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var items = new List<string>();
        foreach (var item in found.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            items.Add(item.GetString() ?? string.Empty);
        }

        value = items;
        return true;
    }

    // An array the wire may leave out: absent reads as empty, and one bad item makes the whole view
    // null, as a required array does.
    public static bool TextsOrEmpty(this JsonElement raw, string key, out IReadOnlyList<string> value)
    {
        value = [];
        return !raw.TryProperty(key, out _) || raw.Texts(key, out value);
    }

    public static bool Items<T>(this JsonElement raw, string key, Func<JsonElement, T?> read, out IReadOnlyList<T> value)
        where T : class
    {
        value = [];
        if (!raw.TryProperty(key, out var found) || found.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var items = new List<T>();
        foreach (var item in found.EnumerateArray())
        {
            if (read(item) is not { } one)
            {
                return false;
            }

            items.Add(one);
        }

        value = items;
        return true;
    }

    public static bool ItemsOrEmpty<T>(this JsonElement raw, string key, Func<JsonElement, T?> read, out IReadOnlyList<T> value)
        where T : class
    {
        value = [];
        return !raw.TryProperty(key, out _) || raw.Items(key, read, out value);
    }

    // A nested object the wire may leave out: absent reads as null, and one that does not fit its
    // type makes the whole view null.
    public static bool Nested<T>(this JsonElement raw, string key, Func<JsonElement, T?> read, out T? value)
        where T : class
    {
        value = null;
        if (!raw.Object(key, out var found))
        {
            return !raw.TryProperty(key, out _);
        }

        value = read(found);
        return value is not null;
    }

    // A nested object the wire always carries: absent, of another JSON kind, or a misfit, and the
    // whole view is null.
    public static bool Shape<T>(this JsonElement raw, string key, Func<JsonElement, T?> read, out T value)
        where T : class
    {
        value = null!;
        if (!raw.Object(key, out var found) || read(found) is not { } one)
        {
            return false;
        }

        value = one;
        return true;
    }

    // A nested object the wire carries the key of even where there is nothing to carry: a JSON null
    // there reads as nothing, not as a misfit.
    public static bool NestedOrNull<T>(this JsonElement raw, string key, Func<JsonElement, T?> read, out T? value)
        where T : class
    {
        value = null;
        if (!raw.TryProperty(key, out var found) || found.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (found.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        value = read(found);
        return value is not null;
    }
}
