using System.Text.Json;

namespace Claude.Agent;

// The Edit tool's input and output. Its patch and its diff against the repository are the shapes a
// Write carries too, read and written by WriteToolParser.
internal static class EditToolParser
{
    public static EditToolInput? ReadInput(JsonElement raw)
    {
        if (!raw.Text("file_path", out var filePath)
            || !raw.Text("old_string", out var oldString)
            || !raw.Text("new_string", out var newString))
        {
            return null;
        }

        return new EditToolInput(filePath, oldString, newString, raw.FlagOrNull("replace_all"));
    }

    public static void WriteInput(Utf8JsonWriter writer, EditToolInput input)
    {
        writer.WriteStartObject();
        writer.WriteString("file_path", input.FilePath);
        writer.WriteString("old_string", input.OldString);
        writer.WriteString("new_string", input.NewString);
        writer.Flag("replace_all", input.ReplaceAll);
        writer.WriteEndObject();
    }

    public static EditToolOutput? ReadOutput(JsonElement raw)
    {
        if (!raw.Text("filePath", out var filePath)
            || !raw.Text("oldString", out var oldString)
            || !raw.Text("newString", out var newString)
            || !raw.Items("structuredPatch", WriteToolParser.Hunk, out var patch)
            || !raw.Flag("userModified", out var userModified)
            || !raw.Flag("replaceAll", out var replaceAll)
            || !raw.Nested("gitDiff", WriteToolParser.Diff, out var diff))
        {
            return null;
        }

        return new EditToolOutput(
            filePath,
            oldString,
            newString,
            patch,
            userModified,
            replaceAll,
            raw.TextOrNull("originalFile"),
            diff,
            raw.FlagOrNull("staged"));
    }

    public static void WriteOutput(Utf8JsonWriter writer, EditToolOutput output)
    {
        writer.WriteStartObject();
        writer.WriteString("filePath", output.FilePath);
        writer.WriteString("oldString", output.OldString);
        writer.WriteString("newString", output.NewString);
        writer.TextOrNull("originalFile", output.OriginalFile);
        writer.Items("structuredPatch", output.StructuredPatch, WriteToolParser.WriteHunk, whenEmpty: true);
        writer.WriteBoolean("userModified", output.UserModified);
        writer.WriteBoolean("replaceAll", output.ReplaceAll);
        writer.Nested("gitDiff", output.GitDiff, WriteToolParser.WriteDiff);
        writer.Flag("staged", output.Staged);
        writer.WriteEndObject();
    }
}
