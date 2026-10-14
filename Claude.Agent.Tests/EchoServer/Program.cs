// The smallest stdio MCP server: one tool, echo, which returns the text it was given.
//
// Newline-delimited JSON-RPC 2.0 on stdin and stdout, as the binary speaks it to a stdio server.
// initialize, tools/list, tools/call and ping are answered; a notification is read and dropped;
// any other method is a -32601.
using System.Text;
using System.Text.Json;

const string Tool = """
    {"name": "echo", "description": "Echo the text back", "inputSchema": {"type": "object", "properties": {"text": {"type": "string"}}, "required": ["text"]}}
    """;

var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
var stdin = Console.In;

while (stdin.ReadLine() is { } raw)
{
    var line = raw.Trim();
    if (line.Length == 0)
    {
        continue;
    }

    using var message = JsonDocument.Parse(line);
    var root = message.RootElement;
    if (!root.TryGetProperty("id", out var id) || id.ValueKind == JsonValueKind.Null)
    {
        continue;
    }

    var method = root.TryGetProperty("method", out var methodElement) ? methodElement.GetString() : null;
    var parameters = root.TryGetProperty("params", out var paramsElement) && paramsElement.ValueKind == JsonValueKind.Object
        ? paramsElement
        : default;

    var result = method switch
    {
        "initialize" => Initialize(parameters),
        "tools/list" => $$"""{"tools": [{{Tool}}]}""",
        "tools/call" => $$"""{"content": [{"type": "text", "text": {{JsonSerializer.Serialize(TextArgument(parameters))}}}]}""",
        "ping" => "{}",
        _ => null,
    };

    stdout.WriteLine(result is null
        ? "{\"jsonrpc\": \"2.0\", \"id\": " + id.GetRawText() + ", \"error\": {\"code\": -32601, \"message\": "
            + JsonSerializer.Serialize($"Method not found: {method}") + "}}"
        : "{\"jsonrpc\": \"2.0\", \"id\": " + id.GetRawText() + ", \"result\": " + result + "}");
}

return 0;

static string Initialize(JsonElement parameters)
{
    var protocolVersion = parameters.ValueKind == JsonValueKind.Object
        && parameters.TryGetProperty("protocolVersion", out var version)
        && version.ValueKind == JsonValueKind.String
            ? version.GetString()!
            : "2024-11-05";

    return "{\"protocolVersion\": " + JsonSerializer.Serialize(protocolVersion)
        + ", \"capabilities\": {\"tools\": {}}, \"serverInfo\": {\"name\": \"echo\", \"version\": \"1.0.0\"}}";
}

static string TextArgument(JsonElement parameters) =>
    parameters.ValueKind == JsonValueKind.Object
    && parameters.TryGetProperty("arguments", out var arguments)
    && arguments.ValueKind == JsonValueKind.Object
    && arguments.TryGetProperty("text", out var text)
    && text.ValueKind == JsonValueKind.String
        ? text.GetString() ?? string.Empty
        : string.Empty;
