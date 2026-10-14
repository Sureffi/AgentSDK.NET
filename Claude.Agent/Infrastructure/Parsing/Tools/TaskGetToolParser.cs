using System.Text.Json;

namespace Claude.Agent;

// The TaskGet tool's input and output. The task is the one member of the output and the binary writes
// the key even where the list holds no such task, so a null there is read as no task rather than as a
// misfit. No recording calls this tool: the shape stands on the declaration alone.
internal static class TaskGetToolParser
{
    public static TaskGetToolInput? ReadInput(JsonElement raw) =>
        raw.Text("taskId", out var taskId) ? new TaskGetToolInput(taskId) : null;

    private static TaskGetToolTask? AsTaskGetToolTask(JsonElement raw)
    {
        if (!raw.Text("id", out var id)
            || !raw.Text("subject", out var subject)
            || !raw.Text("description", out var description)
            || !raw.Text("status", out var status)
            || !raw.Texts("blocks", out var blocks)
            || !raw.Texts("blockedBy", out var blockedBy))
        {
            return null;
        }

        return new TaskGetToolTask(id, subject, description, status, blocks, blockedBy);
    }

    public static TaskGetToolOutput? ReadOutput(JsonElement raw) =>
        raw.NestedOrNull("task", AsTaskGetToolTask, out var task) ? new TaskGetToolOutput(task) : null;

    public static void WriteInput(Utf8JsonWriter writer, TaskGetToolInput input)
    {
        writer.WriteStartObject();
        WriteTaskGetToolInput(writer, input);
        writer.WriteEndObject();
    }

    private static void WriteTaskGetToolInput(Utf8JsonWriter writer, TaskGetToolInput value)
    {
        writer.WriteString("taskId", value.TaskId);
    }

    private static void WriteTaskGetToolTask(Utf8JsonWriter writer, TaskGetToolTask value)
    {
        writer.WriteString("id", value.Id);
        writer.WriteString("subject", value.Subject);
        writer.WriteString("description", value.Description);
        writer.WriteString("status", value.Status.ToString());
        writer.Texts("blocks", value.Blocks, whenEmpty: true);
        writer.Texts("blockedBy", value.BlockedBy, whenEmpty: true);
    }

    public static void WriteOutput(Utf8JsonWriter writer, TaskGetToolOutput output)
    {
        writer.WriteStartObject();
        WriteTaskGetToolOutput(writer, output);
        writer.WriteEndObject();
    }

    private static void WriteTaskGetToolOutput(Utf8JsonWriter writer, TaskGetToolOutput value)
    {
        writer.NestedOrNull("task", value.Task, WriteTaskGetToolTask);
    }
}
