using System.Text.Json;

namespace Claude.Agent;

// The WebSearch tool's input and output. A result is one of two shapes, told apart by its JSON kind
// and not by a key; anything else in the list makes the whole view null beside the raw.
internal static class WebSearchToolParser
{
    public static WebSearchToolInput? ReadInput(JsonElement raw)
    {
        if (!raw.Text("query", out var query)
            || !raw.TextsOrEmpty("allowed_domains", out var allowed)
            || !raw.TextsOrEmpty("blocked_domains", out var blocked))
        {
            return null;
        }

        return new WebSearchToolInput(query, allowed, blocked);
    }

    public static void WriteInput(Utf8JsonWriter writer, WebSearchToolInput input)
    {
        writer.WriteStartObject();
        writer.WriteString("query", input.Query);
        writer.Texts("allowed_domains", input.AllowedDomains);
        writer.Texts("blocked_domains", input.BlockedDomains);
        writer.WriteEndObject();
    }

    public static WebSearchToolOutput? ReadOutput(JsonElement raw)
    {
        if (!raw.Text("query", out var query)
            || !raw.Items("results", Result, out var results)
            || !raw.Seconds("durationSeconds", out var duration))
        {
            return null;
        }

        return new WebSearchToolOutput(query, results, duration, raw.CountOrNull("searchCount"));
    }

    public static void WriteOutput(Utf8JsonWriter writer, WebSearchToolOutput output)
    {
        writer.WriteStartObject();
        writer.WriteString("query", output.Query);
        writer.WriteStartArray("results");
        foreach (var result in output.Results)
        {
            switch (result)
            {
                case WebSearchTextResult text:
                    writer.WriteStringValue(text.Text);
                    break;
                case WebSearchHitResult hit:
                    writer.WriteStartObject();
                    writer.WriteString("tool_use_id", hit.ToolUseId);
                    writer.WriteStartArray("content");
                    foreach (var link in hit.Content)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("title", link.Title);
                        writer.WriteString("url", link.Url);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                    break;
                default:
                    throw new NotSupportedException($"No WebSearch result of type {result.GetType().Name} can be written.");
            }
        }

        writer.WriteEndArray();
        writer.Seconds("durationSeconds", output.Duration);
        writer.Count("searchCount", output.SearchCount);
        writer.WriteEndObject();
    }

    private static WebSearchToolResultItem? Result(JsonElement raw)
    {
        if (raw.ValueKind == JsonValueKind.String)
        {
            return new WebSearchTextResult(raw.GetString() ?? string.Empty);
        }

        if (raw.ValueKind != JsonValueKind.Object
            || !raw.Text("tool_use_id", out var toolUseId)
            || !raw.Items("content", Link, out var content))
        {
            return null;
        }

        return new WebSearchHitResult(toolUseId, content);
    }

    private static WebSearchLink? Link(JsonElement raw) =>
        raw.Text("title", out var title) && raw.Text("url", out var url) ? new WebSearchLink(title, url) : null;
}
