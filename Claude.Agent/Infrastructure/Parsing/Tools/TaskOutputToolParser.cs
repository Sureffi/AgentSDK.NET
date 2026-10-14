using System.Text.Json;

namespace Claude.Agent;

// The TaskOutput tool's input and output. The declaration types no output for this tool; the shape
// below is the one the task-tools recording carries.
internal static class TaskOutputToolParser
{
    public static TaskOutputToolInput? ReadInput(JsonElement raw)
    {
        if (!raw.Text("task_id", out var taskId)
            || !raw.Flag("block", out var block)
            || !raw.Milliseconds("timeout", out var timeout))
        {
            return null;
        }

        return new TaskOutputToolInput(taskId, block, timeout);
    }

    public static void WriteInput(Utf8JsonWriter writer, TaskOutputToolInput input)
    {
        writer.WriteStartObject();
        writer.WriteString("task_id", input.TaskId);
        writer.WriteBoolean("block", input.Block);
        writer.Milliseconds("timeout", input.Timeout);
        writer.WriteEndObject();
    }

    public static TaskOutputToolOutput? ReadOutput(JsonElement raw)
    {
        if (!raw.Text("retrieval_status", out var status) || !raw.Object("task", out var task))
        {
            return null;
        }

        return Task(task) is { } read ? new TaskOutputToolOutput(status, read) : null;
    }

    public static void WriteOutput(Utf8JsonWriter writer, TaskOutputToolOutput output)
    {
        writer.WriteStartObject();
        writer.WriteString("retrieval_status", output.RetrievalStatus.ToString());
        writer.WriteStartObject("task");
        writer.WriteString("task_id", output.Task.TaskId);
        writer.WriteString("task_type", output.Task.TaskType.ToString());
        writer.WriteString("status", output.Task.Status);
        writer.WriteString("description", output.Task.Description);
        writer.WriteString("output", output.Task.Output);
        writer.CountOrNull("exitCode", output.Task.ExitCode);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static TaskOutputToolTask? Task(JsonElement raw)
    {
        if (!raw.Text("task_id", out var taskId)
            || !raw.Text("task_type", out var taskType)
            || !raw.Text("status", out var status)
            || !raw.Text("description", out var description)
            || !raw.Text("output", out var output))
        {
            return null;
        }

        return new TaskOutputToolTask(taskId, taskType, status, description, output, raw.CountOrNull("exitCode"));
    }
}
