using System.Text.Json;
using System.Text.Json.Nodes;

namespace Claude.Agent;

// One transcript line, held as the JSON object it is. Only the two fields the library reads are
// typed; every other key, and the key order, is kept as it came, because a line written back out —
// a fork's copy — keeps the binary's order. The entry owns its bag: a caller's object is copied in
// and Json hands a copy back, so neither the two fields nor the cached compact text can go stale
// under a mutation. A changed line is a new entry.
internal sealed class SessionEntry
{
    // The deepest a line is read to. JSON.parse has no bound but its stack's, and System.Text.Json
    // stops at 64 unless told otherwise; the deepest line the fixtures keep nests 15. A line nested
    // deeper than this is passed over as one that does not parse.
    public const int MaxDepth = 1000;

    private readonly JsonObject _json;

    private string? _compact;

    public SessionEntry(JsonObject json)
        : this(NotNull(json), taken: false)
    {
    }

    // Parse owns what it just built off the line and nothing else holds it; every other caller keeps
    // its object, so that one is copied.
    private SessionEntry(JsonObject json, bool taken)
    {
        _json = taken ? json : json.DeepClone().AsObject();
        Type = Text(_json, "type");
        Uuid = Text(_json, "uuid") is { Length: > 0 } uuid ? uuid : null;
    }

    // How a line, or a part of one, is parsed: to MaxDepth.
    public static JsonDocumentOptions ReadOptions { get; } = new() { MaxDepth = MaxDepth };

    // A copy of the line; changing it does not change the entry.
    public JsonObject Json => _json.DeepClone().AsObject();

    public string? Type { get; }

    public string? Uuid { get; }

    public static Result<SessionEntry, ParseError> Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return new ParseError(ParseErrorKind.MalformedJson, "A transcript line must not be blank.", line ?? "");
        }

        // JSON.parse takes half a surrogate pair escaped without its other half, and System.Text.Json
        // parses it and throws where the string is read. The line is read with each such half made
        // U+FFFD, so an entry holds text wherever it is read and written back out.
        var text = JsonSurrogateEscapes.ReplaceUnpaired(line);
        try
        {
            var node = JsonNode.Parse(text, documentOptions: ReadOptions);
            if (node is not JsonObject json)
            {
                return new ParseError(ParseErrorKind.InvalidValue, "A transcript line must be a JSON object.", line);
            }

            Materialise(json);
            return new SessionEntry(json, taken: true);
        }
        catch (JsonException exception)
        {
            return new ParseError(ParseErrorKind.MalformedJson, exception.Message, line);
        }
        catch (ArgumentException)
        {
            return DuplicateKeys(text, line);
        }
    }

    // The line as compact JSON, keys in their own order.
    public string ToCompactJson() => _compact ??= CompactJson.Write(_json);

    public static string? Text(JsonObject json, string key) =>
        json.TryGetPropertyValue(key, out var node) ? Text(node) : null;

    public static string? Text(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<JsonElement>(out var element))
        {
            return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
        }

        return value.TryGetValue<string>(out var text) ? text : null;
    }

    public static double? Number(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<JsonElement>(out var element))
        {
            return element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var raw) ? raw : null;
        }

        return value.TryGetValue<double>(out var number) ? number : null;
    }

    public static bool IsTrue(JsonObject json, string key) =>
        json.TryGetPropertyValue(key, out var node)
        && node is JsonValue value
        && ((value.TryGetValue<JsonElement>(out var element) && element.ValueKind == JsonValueKind.True)
            || (value.TryGetValue<bool>(out var flag) && flag));

    // The tree's own readers take the live bag through these; only Json copies.
    public string? Field(string key) => Text(_json, key);

    public bool Flag(string key) => IsTrue(_json, key);

    // The key read as JavaScript reads a condition on it: absent, null, false, zero and the empty
    // string are false, and anything else, an empty object or array included, is true.
    public bool IsTruthy(string key) =>
        _json.TryGetPropertyValue(key, out var node) && node switch
        {
            null => false,
            JsonObject or JsonArray => true,
            _ => node.GetValueKind() switch
            {
                JsonValueKind.Null or JsonValueKind.False => false,
                JsonValueKind.String => Text(node) is { Length: > 0 },
                JsonValueKind.Number => Number(node) is not { } number || number != 0,
                _ => true,
            },
        };

    public IReadOnlyList<string> TextBlocks() => TranscriptScan.TextBlocks(_json);

    public bool CarriesToolResult() => TranscriptScan.CarriesToolResult(_json);

    private static JsonObject NotNull(JsonObject json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return json;
    }

    // JsonNode.Parse defers building an object until something reads it, and a duplicate key is
    // refused by the dictionary at that moment. The walk pulls every node up here, so a repeated key
    // is met in Parse, which rebuilds the line through DuplicateKeys, instead of inside a later
    // ToCompactJson.
    private static void Materialise(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject json:
                foreach (var property in json)
                {
                    Materialise(property.Value);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Materialise(item);
                }

                break;
        }
    }

    // A repeated key keeps the LAST occurrence, in the first occurrence's position, and never
    // raises. JsonNode cannot hold that shape, so the
    // line is rebuilt through the reader, which hands over every occurrence in order.
    private static Result<SessionEntry, ParseError> DuplicateKeys(string text, string line)
    {
        try
        {
            using var document = JsonDocument.Parse(text, ReadOptions);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? new SessionEntry(CopyObject(document.RootElement), taken: true)
                : new ParseError(ParseErrorKind.InvalidValue, "A transcript line must be a JSON object.", line);
        }
        catch (JsonException exception)
        {
            return new ParseError(ParseErrorKind.MalformedJson, exception.Message, line);
        }
    }

    private static JsonObject CopyObject(JsonElement element)
    {
        var json = new JsonObject();
        foreach (var property in element.EnumerateObject())
        {
            json[property.Name] = CopyNode(property.Value);
        }

        return json;
    }

    private static JsonArray CopyArray(JsonElement element)
    {
        var array = new JsonArray();
        foreach (var item in element.EnumerateArray())
        {
            array.Add(CopyNode(item));
        }

        return array;
    }

    // The element is cloned off the document because the document is disposed here and the number's
    // raw text has to outlive it.
    private static JsonNode? CopyNode(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => CopyObject(element),
        JsonValueKind.Array => CopyArray(element),
        _ => JsonValue.Create(element.Clone()),
    };
}
