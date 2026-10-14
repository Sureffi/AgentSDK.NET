using System.Text.Json;

namespace Claude.Agent;

// The TaskList tool's input and output. The input carries nothing: the tool lists the whole list.
// No recording calls this tool: the shape stands on the declaration alone.
internal static class TaskListToolParser
{
    public static TaskListToolInput? ReadInput(JsonElement raw) =>
        raw.ValueKind == JsonValueKind.Object ? new TaskListToolInput() : null;

    private static TaskListToolTask? AsTaskListToolTask(JsonElement raw)
    {
        if (!raw.Text("id", out var id)
            || !raw.Text("subject", out var subject)
            || !raw.Text("status", out var status)
            || !raw.Texts("blockedBy", out var blockedBy))
        {
            return null;
        }

        return new TaskListToolTask(id, subject, status, blockedBy, raw.TextOrNull("owner"));
    }

    public static TaskListToolOutput? ReadOutput(JsonElement raw) =>
        raw.Items("tasks", AsTaskListToolTask, out var tasks) ? new TaskListToolOutput(tasks) : null;

    public static void WriteInput(Utf8JsonWriter writer, TaskListToolInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        writer.WriteStartObject();
        writer.WriteEndObject();
    }

    private static void WriteTaskListToolTask(Utf8JsonWriter writer, TaskListToolTask value)
    {
        writer.WriteString("id", value.Id);
        writer.WriteString("subject", value.Subject);
        writer.WriteString("status", value.Status.ToString());
        writer.Texts("blockedBy", value.BlockedBy, whenEmpty: true);
        writer.Text("owner", value.Owner);
    }

    public static void WriteOutput(Utf8JsonWriter writer, TaskListToolOutput output)
    {
        writer.WriteStartObject();
        WriteTaskListToolOutput(writer, output);
        writer.WriteEndObject();
    }

    private static void WriteTaskListToolOutput(Utf8JsonWriter writer, TaskListToolOutput value)
    {
        writer.ItemObjects("tasks", value.Tasks, WriteTaskListToolTask, whenEmpty: true);
    }
}
