using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Claude.Agent;

// What structured output takes at a schema's root, one rule for a schema handed in as it stands and
// one taken from a type: an object whose type is "object". A nullable root, ["object", "null"], is
// not one: the API refused it on the schema-nullable recording. Nor is a root with anyOf, oneOf or
// allOf, which the binary's own note on an MCP tool's schema says the API does not accept. The
// problem names the schema by `subject`.
//
// The rule reads the element as it is held and parses nothing again, so a schema read with comments
// or trailing commas allowed, or nested deeper than a reader's default allows, is judged by its root
// like any other. A key the root repeats is read as the binary's JSON.parse reads it: the last "type"
// is the root's type, and a combinator the root names at all is there.
internal static class OutputSchemaRoot
{
    private const string TypeKeyword = "type";

    private const string Object = "object";

    // A union of schemas where one object schema goes.
    private static readonly string[] Combinators = ["anyOf", "oneOf", "allOf"];

    public static string? Problem(JsonElement schema, string subject)
    {
        if (schema.ValueKind is not JsonValueKind.Object || !NamesObject(schema))
        {
            return $"{subject} has {Text(schema)} at its root; structured output takes a schema whose type is \"object\".";
        }

        if (Combinators.FirstOrDefault(combinator => Names(schema, combinator)) is { } found)
        {
            return $"{subject} has \"{found}\" at its root; the API does not accept anyOf, oneOf or allOf at the root of a structured output schema.";
        }

        return null;
    }

    // Whether the root's last "type" is the string "object".
    private static bool NamesObject(JsonElement root)
    {
        JsonElement? type = null;
        foreach (var member in root.EnumerateObject())
        {
            if (member.NameEquals(TypeKeyword))
            {
                type = member.Value;
            }
        }

        return type is { ValueKind: JsonValueKind.String } kind && kind.ValueEquals(Object);
    }

    private static bool Names(JsonElement root, string key)
    {
        foreach (var member in root.EnumerateObject())
        {
            if (member.NameEquals(key))
            {
                return true;
            }
        }

        return false;
    }

    // The schema written out compact, at any depth: the element's own tokens, so a comment the reader
    // skipped is not among them.
    private static string Text(JsonElement schema)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { MaxDepth = int.MaxValue }))
        {
            schema.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
