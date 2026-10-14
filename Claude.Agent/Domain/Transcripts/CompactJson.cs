using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Claude.Agent;

// The bytes this library appends to a transcript, as the binary writes a line with JSON.stringify:
// no space around a separator, and every character written as itself but the quote, the backslash,
// the C0 controls and a surrogate without its partner. Those are escaped: \b, \t, \n, \f and \r by
// name, the rest as a lowercase \u00xx or \udxxx. System.Text.Json's encoders escape more than that,
// and WireJson spaces its separators, so the compact form lives here.
internal static class CompactJson
{
    public static string Write(JsonObject json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var text = new StringBuilder();
        AppendObject(text, json);
        return text.ToString();
    }

    private static void AppendNode(StringBuilder text, JsonNode? node)
    {
        switch (node)
        {
            case null:
                text.Append("null");
                break;
            case JsonObject obj:
                AppendObject(text, obj);
                break;
            case JsonArray array:
                text.Append('[');
                var firstItem = true;
                foreach (var item in array)
                {
                    if (!firstItem)
                    {
                        text.Append(',');
                    }

                    firstItem = false;
                    AppendNode(text, item);
                }

                text.Append(']');
                break;
            default:
                AppendValue(text, node);
                break;
        }
    }

    private static void AppendObject(StringBuilder text, JsonObject json)
    {
        text.Append('{');
        var first = true;
        foreach (var property in json)
        {
            if (!first)
            {
                text.Append(',');
            }

            first = false;
            AppendString(text, property.Key);
            text.Append(':');
            AppendNode(text, property.Value);
        }

        text.Append('}');
    }

    private static void AppendValue(StringBuilder text, JsonNode node)
    {
        if (node is not JsonValue value || !value.TryGetValue<JsonElement>(out var element))
        {
            AppendConstructed(text, node);
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                AppendString(text, element.GetString() ?? string.Empty);
                break;
            case JsonValueKind.True:
                text.Append("true");
                break;
            case JsonValueKind.False:
                text.Append("false");
                break;
            case JsonValueKind.Null:
                text.Append("null");
                break;
            default:
                // A parsed number keeps the bytes the binary wrote; nothing reformats it.
                text.Append(element.GetRawText());
                break;
        }
    }

    private static void AppendConstructed(StringBuilder text, JsonNode node)
    {
        var value = (JsonValue)node;
        if (value.TryGetValue<string>(out var s))
        {
            AppendString(text, s);
        }
        else if (value.TryGetValue<bool>(out var b))
        {
            text.Append(b ? "true" : "false");
        }
        else if (value.TryGetValue<long>(out var l))
        {
            text.Append(l.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            text.Append(value.ToJsonString());
        }
    }

    private static void AppendString(StringBuilder text, string value)
    {
        text.Append('"');
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                text.Append(c).Append(value[++i]);
                continue;
            }

            switch (c)
            {
                case '"':
                    text.Append("\\\"");
                    break;
                case '\\':
                    text.Append("\\\\");
                    break;
                case '\b':
                    text.Append("\\b");
                    break;
                case '\f':
                    text.Append("\\f");
                    break;
                case '\n':
                    text.Append("\\n");
                    break;
                case '\r':
                    text.Append("\\r");
                    break;
                case '\t':
                    text.Append("\\t");
                    break;
                default:
                    if (c < ' ' || char.IsSurrogate(c))
                    {
                        text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        text.Append(c);
                    }

                    break;
            }
        }

        text.Append('"');
    }
}
