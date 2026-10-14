using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Claude.Agent.Tests.Replay;

internal sealed record FixtureData(
    ImmutableArray<string> Stdout,
    ImmutableArray<string> Stdin,
    ImmutableArray<string> Argv);

internal static partial class Fixture
{
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "fixtures");

    public static FixtureData Load(string name) => new(
        Lines($"{name}.stdout.jsonl"),
        Lines($"{name}.stdin.jsonl"),
        Argv($"{name}.argv.json"));

    public static string Line(string name, string type, int index = 0) =>
        Load(name).Stdout.Where(line => TypeOf(line) == type).ElementAt(index);

    public static ImmutableArray<string> MessageLines(string name) =>
        [.. Load(name).Stdout.Where(
            line => TypeOf(line) is { } type && !type.StartsWith("control_", StringComparison.Ordinal))];

    public static string Anonymise(string line) => RequestIdPattern().Replace(line, "req_n");

    // The lines with request ids masked, and each prompt's uuid, wherever a line carries it, masked
    // to that prompt's place among the lines' prompts: two stdins compare equal when they carry the
    // same uuid members in the same places, whatever the uuids are.
    public static string[] Anonymised(this IEnumerable<string> lines)
    {
        var all = lines.ToArray();
        var prompts = PromptUuids(all);
        return [.. all.Select(line =>
        {
            var masked = Anonymise(line);
            for (var ordinal = 0; ordinal < prompts.Length; ordinal++)
            {
                if (prompts[ordinal] is { } uuid)
                {
                    masked = masked.Replace(uuid, $"prompt-uuid-{ordinal + 1}", StringComparison.Ordinal);
                }
            }

            return masked;
        })];
    }

    // A recorded result line given a duration no TimeSpan holds, so it misfits its type.
    public static string WithDurationOutOfRange(string result)
    {
        var line = System.Text.Json.Nodes.JsonNode.Parse(result)!;
        line["duration_ms"] = long.MaxValue;
        return line.ToJsonString();
    }

    // The uuid each prompt among `stdin` was written with, in order; null for one written without.
    public static ImmutableArray<string?> PromptUuids(IEnumerable<string> stdin) =>
        [.. stdin.Where(line => TypeOf(line) == "user").Select(UuidOf)];

    public static string? UuidOf(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.TryGetProperty("uuid", out var uuid) && uuid.ValueKind == JsonValueKind.String
            ? uuid.GetString()
            : null;
    }

    public static string? TypeOf(string line)
    {
        if (line.Length == 0 || line[0] != '{')
        {
            return null;
        }

        using var document = JsonDocument.Parse(line);
        return document.RootElement.TryGetProperty("type", out var type) ? type.GetString() : null;
    }

    public static string RequestIdOf(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.GetProperty("request_id").GetString() ?? string.Empty;
    }

    public static string? ResponseRequestIdOf(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.TryGetProperty("response", out var response)
            && response.TryGetProperty("request_id", out var id)
                ? id.GetString()
                : null;
    }

    private static ImmutableArray<string> Lines(string file) =>
        [.. File.ReadAllLines(Path.Combine(Root, file)).Where(line => !line.StartsWith('#'))];

    private static ImmutableArray<string> Argv(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, file)));
        return [.. document.RootElement.EnumerateArray().Select(element => element.GetString() ?? string.Empty)];
    }

    [GeneratedRegex("req_[0-9]+_[0-9a-f]+")]
    private static partial Regex RequestIdPattern();
}
