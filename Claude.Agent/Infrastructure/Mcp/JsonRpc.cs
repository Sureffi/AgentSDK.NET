using System.Buffers;
using System.Text.Json;

namespace Claude.Agent;

internal static class JsonRpc
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;

    public static readonly JsonElement EmptyObject = Build(writer =>
    {
        writer.WriteStartObject();
        writer.WriteEndObject();
    });

    public static string? Method(JsonElement message) =>
        Property(message, "method") is { ValueKind: JsonValueKind.String } method ? method.GetString() : null;

    public static bool Has(JsonElement message, string name) => Property(message, name) is not null;

    public static JsonElement? Property(JsonElement message, string name) =>
        message.ValueKind == JsonValueKind.Object && message.TryGetProperty(name, out var value) ? value : null;

    public static JsonElement Response(JsonElement request, Action<Utf8JsonWriter> result) =>
        Build(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            WriteId(writer, request);
            writer.WritePropertyName("result");
            result(writer);
            writer.WriteEndObject();
        });

    public static JsonElement Failure(JsonElement request, int code, string message) =>
        Build(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            WriteId(writer, request);
            writer.WritePropertyName("error");
            writer.WriteStartObject();
            writer.WriteNumber("code", code);
            writer.WriteString("message", message);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });

    public static JsonElement Build(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static void WriteId(Utf8JsonWriter writer, JsonElement request)
    {
        writer.WritePropertyName("id");

        if (Property(request, "id") is { } id)
        {
            id.WriteTo(writer);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
