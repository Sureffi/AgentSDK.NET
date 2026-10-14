using System.Text.Json;

namespace Claude.Agent;

// The TaskStop tool's input and output. Both of the input's keys are optional in the declaration, so
// an empty object is a view this reads.
internal static class TaskStopToolParser
{
    public static TaskStopToolInput? ReadInput(JsonElement raw) =>
        raw.ValueKind == JsonValueKind.Object
            ? new TaskStopToolInput(raw.TextOrNull("task_id"), raw.TextOrNull("shell_id"))
            : null;

    public static void WriteInput(Utf8JsonWriter writer, TaskStopToolInput input)
    {
        writer.WriteStartObject();
        writer.Text("task_id", input.TaskId);
        writer.Text("shell_id", input.ShellId);
        writer.WriteEndObject();
    }

    public static TaskStopToolOutput? ReadOutput(JsonElement raw)
    {
        if (!raw.Text("message", out var message)
            || !raw.Text("task_id", out var taskId)
            || !raw.Text("task_type", out var taskType))
        {
            return null;
        }

        return new TaskStopToolOutput(message, taskId, taskType, raw.TextOrNull("command"));
    }

    public static void WriteOutput(Utf8JsonWriter writer, TaskStopToolOutput output)
    {
        writer.WriteStartObject();
        writer.WriteString("message", output.Message);
        writer.WriteString("task_id", output.TaskId);
        writer.WriteString("task_type", output.TaskType.ToString());
        writer.Text("command", output.Command);
        writer.WriteEndObject();
    }
}
