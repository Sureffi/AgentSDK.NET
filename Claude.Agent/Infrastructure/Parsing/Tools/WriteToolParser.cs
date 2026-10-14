using System.Text.Json;

namespace Claude.Agent;

// The Write tool's input and output, and the two shapes an Edit shares with it: the patch and the
// diff against the repository.
internal static class WriteToolParser
{
    public static WriteToolInput? ReadInput(JsonElement raw) =>
        raw.Text("file_path", out var filePath) && raw.Text("content", out var content)
            ? new WriteToolInput(filePath, content)
            : null;

    public static void WriteInput(Utf8JsonWriter writer, WriteToolInput input)
    {
        writer.WriteStartObject();
        writer.WriteString("file_path", input.FilePath);
        writer.WriteString("content", input.Content);
        writer.WriteEndObject();
    }

    // A union on `type`: a file made or a file replaced, one type each. A kind the library has no
    // type for, or one whose members do not fit, makes the view null beside the raw.
    public static WriteToolOutput? ReadOutput(JsonElement raw)
    {
        if (!raw.Text("filePath", out var filePath)
            || !raw.Text("content", out var content)
            || !raw.Nested("gitDiff", Diff, out var diff))
        {
            return null;
        }

        return raw.TextOrNull("type") switch
        {
            "create" => new WriteCreateToolOutput(filePath, content, diff, raw.FlagOrNull("userModified"), raw.FlagOrNull("staged")),
            "update" when raw.Items("structuredPatch", Hunk, out var patch) =>
                new WriteUpdateToolOutput(
                    filePath,
                    content,
                    patch,
                    raw.TextOrNull("originalFile"),
                    diff,
                    raw.FlagOrNull("userModified"),
                    raw.FlagOrNull("staged")),
            _ => null,
        };
    }

    // A file made is written with the empty patch and the null contents the binary writes beside it.
    public static void WriteOutput(Utf8JsonWriter writer, WriteToolOutput output)
    {
        writer.WriteStartObject();
        switch (output)
        {
            case WriteCreateToolOutput:
                writer.WriteString("type", "create");
                writer.WriteString("filePath", output.FilePath);
                writer.WriteString("content", output.Content);
                writer.Items<FilePatchHunk>("structuredPatch", [], WriteHunk, whenEmpty: true);
                writer.TextOrNull("originalFile", null);
                break;
            case WriteUpdateToolOutput update:
                writer.WriteString("type", "update");
                writer.WriteString("filePath", output.FilePath);
                writer.WriteString("content", output.Content);
                writer.Items("structuredPatch", update.StructuredPatch, WriteHunk, whenEmpty: true);
                writer.TextOrNull("originalFile", update.OriginalFile);
                break;
            default:
                throw new NotSupportedException($"No Write output of type {output.GetType().Name} can be written.");
        }

        writer.Nested("gitDiff", output.GitDiff, WriteDiff);
        writer.Flag("userModified", output.UserModified);
        writer.Flag("staged", output.Staged);
        writer.WriteEndObject();
    }

    public static FilePatchHunk? Hunk(JsonElement raw)
    {
        if (!raw.Count("oldStart", out var oldStart)
            || !raw.Count("oldLines", out var oldLines)
            || !raw.Count("newStart", out var newStart)
            || !raw.Count("newLines", out var newLines)
            || !raw.Texts("lines", out var lines))
        {
            return null;
        }

        return new FilePatchHunk(oldStart, oldLines, newStart, newLines, lines);
    }

    public static void WriteHunk(Utf8JsonWriter writer, FilePatchHunk hunk)
    {
        writer.WriteStartObject();
        writer.WriteNumber("oldStart", hunk.OldStart);
        writer.WriteNumber("oldLines", hunk.OldLines);
        writer.WriteNumber("newStart", hunk.NewStart);
        writer.WriteNumber("newLines", hunk.NewLines);
        writer.Texts("lines", hunk.Lines, whenEmpty: true);
        writer.WriteEndObject();
    }

    public static FileGitDiff? Diff(JsonElement raw)
    {
        if (!raw.Text("filename", out var fileName)
            || !raw.Text("status", out var status)
            || !raw.Count("additions", out var additions)
            || !raw.Count("deletions", out var deletions)
            || !raw.Count("changes", out var changes)
            || !raw.Text("patch", out var patch))
        {
            return null;
        }

        return new FileGitDiff(fileName, status, additions, deletions, changes, patch, raw.TextOrNull("repository"));
    }

    public static void WriteDiff(Utf8JsonWriter writer, FileGitDiff diff)
    {
        writer.WriteString("filename", diff.FileName);
        writer.WriteString("status", diff.Status.ToString());
        writer.WriteNumber("additions", diff.Additions);
        writer.WriteNumber("deletions", diff.Deletions);
        writer.WriteNumber("changes", diff.Changes);
        writer.WriteString("patch", diff.Patch);
        writer.Text("repository", diff.Repository);
    }
}
