using System.Text.Json;

namespace Claude.Agent;

internal static class JsonReading
{
    public static ParseError Missing(string key, string line) =>
        new(ParseErrorKind.MissingField, $"Missing required field '{key}'.", line);

    public static ParseError Invalid(string key, string detail, string line) =>
        new(ParseErrorKind.InvalidValue, $"Invalid value for '{key}': {detail}", line);

    public static bool TryProperty(this JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out value)
            && value.ValueKind != JsonValueKind.Null
            && value.ValueKind != JsonValueKind.Undefined)
        {
            return true;
        }

        value = default;
        return false;
    }

    public static string? StringOrNull(this JsonElement element, string name) =>
        element.TryProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static long? Int64OrNull(this JsonElement element, string name) =>
        element.TryProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var number)
            ? number
            : null;

    public static decimal? DecimalOrNull(this JsonElement element, string name) =>
        element.TryProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDecimal(out var number)
            ? number
            : null;

    public static double? DoubleOrNull(this JsonElement element, string name) =>
        element.TryProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
            ? number
            : null;

    public static bool? BoolOrNull(this JsonElement element, string name) =>
        element.TryProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            }
            : null;

    public static JsonElement? ElementOrNull(this JsonElement element, string name) =>
        element.TryProperty(name, out var value) ? value : null;

    public static string? ToolUseIdOrNull(this JsonElement element, string name)
    {
        return Ids.NameOrNull(element.StringOrNull(name));
    }

    public static string? ToolNameOrNull(this JsonElement element, string name)
    {
        return Ids.NameOrNull(element.StringOrNull(name));
    }

    public static Result<string, ParseError> RequireString(
        this JsonElement element,
        string name,
        string line,
        string? path = null)
    {
        var key = path ?? name;
        if (!element.TryProperty(name, out var value))
        {
            return Missing(key, line);
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : Invalid(key, "expected a string.", line);
    }

    public static Result<JsonElement, ParseError> RequireElement(
        this JsonElement element,
        string name,
        string line,
        string? path = null) =>
        element.TryProperty(name, out var value) ? value : Missing(path ?? name, line);

    public static Result<bool, ParseError> RequireBool(
        this JsonElement element,
        string name,
        string line,
        string? path = null)
    {
        var key = path ?? name;
        if (!element.TryProperty(name, out var value))
        {
            return Missing(key, line);
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => Invalid(key, "expected a boolean.", line),
        };
    }

    public static Result<string, ParseError> RequireToolUseId(
        this JsonElement element,
        string name,
        string line,
        string? path = null)
    {
        var key = path ?? name;
        return element.RequireString(name, line, key)
            .Bind(text => Ids.IsName(text)
                ? Result<string, ParseError>.Ok(text)
                : Invalid(key, "not a tool use id.", line));
    }

    public static Result<string, ParseError> RequireToolName(
        this JsonElement element,
        string name,
        string line,
        string? path = null)
    {
        var key = path ?? name;
        return element.RequireString(name, line, key)
            .Bind(text => Ids.IsName(text)
                ? Result<string, ParseError>.Ok(text)
                : Invalid(key, "not a tool name.", line));
    }
}
