using System.Text.Json;

namespace Claude.Agent;

// The Glob tool's input and output.
internal static class GlobToolParser
{
    public static GlobToolInput? ReadInput(JsonElement raw) =>
        raw.Text("pattern", out var pattern) ? new GlobToolInput(pattern, raw.TextOrNull("path")) : null;

    public static void WriteInput(Utf8JsonWriter writer, GlobToolInput input)
    {
        writer.WriteStartObject();
        writer.WriteString("pattern", input.Pattern);
        writer.Text("path", input.Path);
        writer.WriteEndObject();
    }

    public static GlobToolOutput? ReadOutput(JsonElement raw)
    {
        if (!raw.Milliseconds("durationMs", out var duration)
            || !raw.Count("numFiles", out var fileCount)
            || !raw.Texts("filenames", out var fileNames)
            || !raw.Flag("truncated", out var truncated))
        {
            return null;
        }

        return new GlobToolOutput(
            duration,
            fileCount,
            fileNames,
            truncated,
            raw.CountOrNull("totalMatches"),
            raw.FlagOrNull("countIsComplete"));
    }

    public static void WriteOutput(Utf8JsonWriter writer, GlobToolOutput output)
    {
        writer.WriteStartObject();
        writer.Milliseconds("durationMs", output.Duration);
        writer.WriteNumber("numFiles", output.FileCount);
        writer.Texts("filenames", output.FileNames, whenEmpty: true);
        writer.WriteBoolean("truncated", output.Truncated);
        writer.Count("totalMatches", output.TotalMatchCount);
        writer.Flag("countIsComplete", output.CountIsComplete);
        writer.WriteEndObject();
    }
}
