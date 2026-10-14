using System.Text.Json;

namespace Claude.Agent;

// The TaskUpdate tool's input and output. No recording calls this tool: the shape stands on the
// declaration alone, and a JSON that does not fit it reads as null beside the raw.
internal static class TaskUpdateToolParser
{
    public static TaskUpdateToolInput? ReadInput(JsonElement raw)
    {
        if (!raw.Text("taskId", out var taskId)
            || !raw.TextsOrEmpty("addBlocks", out var addBlocks)
            || !raw.TextsOrEmpty("addBlockedBy", out var addBlockedBy))
        {
            return null;
        }

        return new TaskUpdateToolInput(
            taskId,
            raw.TextOrNull("subject"),
            raw.TextOrNull("description"),
            raw.TextOrNull("activeForm"),
            raw.SetOrNull("status", value => new TaskUpdateStatus(value)),
            addBlocks,
            addBlockedBy,
            raw.TextOrNull("owner"));
    }

    private static TaskUpdateToolStatusChange? AsTaskUpdateToolStatusChange(JsonElement raw)
    {
        if (!raw.Text("from", out var from)
            || !raw.Text("to", out var to))
        {
            return null;
        }

        return new TaskUpdateToolStatusChange(from, to);
    }

    public static TaskUpdateToolOutput? ReadOutput(JsonElement raw)
    {
        if (!raw.Flag("success", out var success)
            || !raw.Text("taskId", out var taskId)
            || !raw.Texts("updatedFields", out var updatedFields)
            || !raw.Nested("statusChange", AsTaskUpdateToolStatusChange, out var statusChange))
        {
            return null;
        }

        return new TaskUpdateToolOutput(success, taskId, updatedFields, raw.TextOrNull("error"), statusChange);
    }

    public static void WriteInput(Utf8JsonWriter writer, TaskUpdateToolInput input)
    {
        writer.WriteStartObject();
        WriteTaskUpdateToolInput(writer, input);
        writer.WriteEndObject();
    }

    private static void WriteTaskUpdateToolInput(Utf8JsonWriter writer, TaskUpdateToolInput value)
    {
        writer.WriteString("taskId", value.TaskId);
        writer.Text("subject", value.Subject);
        writer.Text("description", value.Description);
        writer.Text("activeForm", value.ActiveForm);
        writer.Text("status", value.Status?.ToString());
        writer.Texts("addBlocks", value.AddBlocks);
        writer.Texts("addBlockedBy", value.AddBlockedBy);
        writer.Text("owner", value.Owner);
    }

    private static void WriteTaskUpdateToolStatusChange(Utf8JsonWriter writer, TaskUpdateToolStatusChange value)
    {
        writer.WriteString("from", value.From);
        writer.WriteString("to", value.To);
    }

    public static void WriteOutput(Utf8JsonWriter writer, TaskUpdateToolOutput output)
    {
        writer.WriteStartObject();
        WriteTaskUpdateToolOutput(writer, output);
        writer.WriteEndObject();
    }

    private static void WriteTaskUpdateToolOutput(Utf8JsonWriter writer, TaskUpdateToolOutput value)
    {
        writer.WriteBoolean("success", value.Success);
        writer.WriteString("taskId", value.TaskId);
        writer.Texts("updatedFields", value.UpdatedFields, whenEmpty: true);
        writer.Text("error", value.Error);
        writer.Nested("statusChange", value.StatusChange, WriteTaskUpdateToolStatusChange);
    }
}
