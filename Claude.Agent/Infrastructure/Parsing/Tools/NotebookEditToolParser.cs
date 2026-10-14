using System.Text.Json;

namespace Claude.Agent;

// The NotebookEdit tool's input and output.
internal static class NotebookEditToolParser
{
    public static NotebookEditToolInput? ReadInput(JsonElement raw)
    {
        if (!raw.Text("notebook_path", out var notebookPath) || !raw.Text("new_source", out var newSource))
        {
            return null;
        }

        return new NotebookEditToolInput(
            notebookPath,
            newSource,
            raw.TextOrNull("cell_id"),
            raw.SetOrNull("cell_type", value => new NotebookCellType(value)),
            raw.SetOrNull("edit_mode", value => new NotebookEditMode(value)));
    }

    public static void WriteInput(Utf8JsonWriter writer, NotebookEditToolInput input)
    {
        writer.WriteStartObject();
        writer.WriteString("notebook_path", input.NotebookPath);
        writer.Text("cell_id", input.CellId);
        writer.WriteString("new_source", input.NewSource);
        writer.Text("cell_type", input.CellType?.ToString());
        writer.Text("edit_mode", input.EditMode?.ToString());
        writer.WriteEndObject();
    }

    public static NotebookEditToolOutput? ReadOutput(JsonElement raw)
    {
        if (!raw.Text("new_source", out var newSource)
            || !raw.Text("cell_type", out var cellType)
            || !raw.Text("language", out var language)
            || !raw.Text("edit_mode", out var editMode)
            || !raw.Text("notebook_path", out var notebookPath)
            || !raw.Text("original_file", out var originalFile)
            || !raw.Text("updated_file", out var updatedFile))
        {
            return null;
        }

        return new NotebookEditToolOutput(
            newSource,
            cellType,
            language,
            editMode,
            notebookPath,
            originalFile,
            updatedFile,
            raw.TextOrNull("old_source"),
            raw.TextOrNull("cell_id"),
            raw.TextOrNull("error"));
    }

    public static void WriteOutput(Utf8JsonWriter writer, NotebookEditToolOutput output)
    {
        writer.WriteStartObject();
        writer.WriteString("new_source", output.NewSource);
        writer.Text("old_source", output.OldSource);
        writer.Text("cell_id", output.CellId);
        writer.WriteString("cell_type", output.CellType.ToString());
        writer.WriteString("language", output.Language);
        writer.WriteString("edit_mode", output.EditMode.ToString());
        writer.Text("error", output.Error);
        writer.WriteString("notebook_path", output.NotebookPath);
        writer.WriteString("original_file", output.OriginalFile);
        writer.WriteString("updated_file", output.UpdatedFile);
        writer.WriteEndObject();
    }
}
