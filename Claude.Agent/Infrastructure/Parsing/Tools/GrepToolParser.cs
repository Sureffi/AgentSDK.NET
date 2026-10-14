using System.Text.Json;

namespace Claude.Agent;

// The Grep tool's input and output. Six of the input's keys are the search program's own flags and
// keep the hyphen the wire spells them with. The output is a union on `mode`, one type a value.
internal static class GrepToolParser
{
    public static GrepToolInput? ReadInput(JsonElement raw)
    {
        if (!raw.Text("pattern", out var pattern))
        {
            return null;
        }

        return new GrepToolInput(
            pattern,
            raw.TextOrNull("path"),
            raw.TextOrNull("glob"),
            raw.SetOrNull("output_mode", value => new GrepOutputMode(value)),
            raw.CountOrNull("-B"),
            raw.CountOrNull("-A"),
            raw.CountOrNull("-C"),
            raw.CountOrNull("context"),
            raw.FlagOrNull("-n"),
            raw.FlagOrNull("-i"),
            raw.FlagOrNull("-o"),
            raw.TextOrNull("type"),
            raw.CountOrNull("head_limit"),
            raw.CountOrNull("offset"),
            raw.FlagOrNull("multiline"));
    }

    public static void WriteInput(Utf8JsonWriter writer, GrepToolInput input)
    {
        writer.WriteStartObject();
        writer.WriteString("pattern", input.Pattern);
        writer.Text("path", input.Path);
        writer.Text("glob", input.Glob);
        writer.Text("output_mode", input.OutputMode?.ToString());
        writer.Count("-B", input.BeforeContextLines);
        writer.Count("-A", input.AfterContextLines);
        writer.Count("-C", input.SurroundingContextLines);
        writer.Count("context", input.ContextLines);
        writer.Flag("-n", input.ShowLineNumbers);
        writer.Flag("-i", input.CaseInsensitive);
        writer.Flag("-o", input.MatchesOnly);
        writer.Text("type", input.FileType);
        writer.Count("head_limit", input.HeadLimit);
        writer.Count("offset", input.Offset);
        writer.Flag("multiline", input.Multiline);
        writer.WriteEndObject();
    }

    // The mode is which type the output is, and an output that leaves it out reads as file names, as
    // the binary reads its own. Each type requires the keys the binary writes in its mode every time,
    // its early answer for a search target that does not exist included; a mode the declaration does
    // not name, or a type without a key it requires, reads as null beside the raw.
    public static GrepToolOutput? ReadOutput(JsonElement raw)
    {
        var mode = raw.TryProperty("mode", out _) ? raw.TextOrNull("mode") : "files_with_matches";
        var appliedLimit = raw.CountOrNull("appliedLimit");
        var appliedOffset = raw.CountOrNull("appliedOffset");
        return mode switch
        {
            "files_with_matches" =>
                raw.Texts("filenames", out var fileNames)
                && raw.Count("numFiles", out var fileCount)
                && raw.Count("totalFiles", out var totalFileCount)
                    ? new GrepFilesWithMatchesToolOutput(fileNames, fileCount, totalFileCount, appliedLimit, appliedOffset)
                    : null,
            "content" =>
                raw.Text("content", out var content)
                && raw.Count("numLines", out var lineCount)
                && raw.Count("totalLines", out var totalLineCount)
                    ? new GrepContentToolOutput(content, lineCount, totalLineCount, appliedLimit, appliedOffset)
                    : null,
            "count" =>
                raw.Text("content", out var counts)
                && raw.Count("numFiles", out var countedFileCount)
                && raw.Count("numMatches", out var matchCount)
                    ? new GrepCountToolOutput(counts, countedFileCount, matchCount, appliedLimit, appliedOffset)
                    : null,
            _ => null,
        };
    }

    // Each type is written in the binary's key order, lines and counts with the file count of 0 or
    // the empty file names the binary writes beside them, since its declaration requires both of
    // every output.
    public static void WriteOutput(Utf8JsonWriter writer, GrepToolOutput output)
    {
        writer.WriteStartObject();
        switch (output)
        {
            case GrepFilesWithMatchesToolOutput files:
                writer.WriteString("mode", "files_with_matches");
                writer.Texts("filenames", files.FileNames, whenEmpty: true);
                writer.WriteNumber("numFiles", files.FileCount);
                writer.WriteNumber("totalFiles", files.TotalFileCount);
                break;
            case GrepContentToolOutput content:
                writer.WriteString("mode", "content");
                writer.WriteNumber("numFiles", 0);
                writer.Texts("filenames", [], whenEmpty: true);
                writer.WriteString("content", content.Content);
                writer.WriteNumber("numLines", content.LineCount);
                writer.WriteNumber("totalLines", content.TotalLineCount);
                break;
            case GrepCountToolOutput count:
                writer.WriteString("mode", "count");
                writer.WriteNumber("numFiles", count.FileCount);
                writer.Texts("filenames", [], whenEmpty: true);
                writer.WriteString("content", count.Content);
                writer.WriteNumber("numMatches", count.MatchCount);
                break;
            default:
                throw new NotSupportedException($"No Grep output of type {output.GetType().Name} can be written.");
        }

        writer.Count("appliedLimit", output.AppliedLimit);
        writer.Count("appliedOffset", output.AppliedOffset);
        writer.WriteEndObject();
    }
}
