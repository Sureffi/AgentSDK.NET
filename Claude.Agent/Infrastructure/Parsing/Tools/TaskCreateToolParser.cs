using System.Text.Json;

namespace Claude.Agent;

// The TaskCreate tool's input and output. No recording calls this tool: the shape stands on the
// declaration alone, and a JSON that does not fit it reads as null beside the raw.
internal static class TaskCreateToolParser
{
    public static TaskCreateToolInput? ReadInput(JsonElement raw)
    {
        if (!raw.Text("subject", out var subject)
            || !raw.Text("description", out var description))
        {
            return null;
        }

        return new TaskCreateToolInput(subject, description, raw.TextOrNull("activeForm"));
    }

    private static TaskCreateToolTask? AsTaskCreateToolTask(JsonElement raw)
    {
        if (!raw.Text("id", out var id)
            || !raw.Text("subject", out var subject))
        {
            return null;
        }

        return new TaskCreateToolTask(id, subject);
    }

    public static TaskCreateToolOutput? ReadOutput(JsonElement raw) =>
        raw.Shape("task", AsTaskCreateToolTask, out var task) ? new TaskCreateToolOutput(task) : null;

    public static void WriteInput(Utf8JsonWriter writer, TaskCreateToolInput input)
    {
        writer.WriteStartObject();
        WriteTaskCreateToolInput(writer, input);
        writer.WriteEndObject();
    }

    private static void WriteTaskCreateToolInput(Utf8JsonWriter writer, TaskCreateToolInput value)
    {
        writer.WriteString("subject", value.Subject);
        writer.WriteString("description", value.Description);
        writer.Text("activeForm", value.ActiveForm);
    }

    private static void WriteTaskCreateToolTask(Utf8JsonWriter writer, TaskCreateToolTask value)
    {
        writer.WriteString("id", value.Id);
        writer.WriteString("subject", value.Subject);
    }

    public static void WriteOutput(Utf8JsonWriter writer, TaskCreateToolOutput output)
    {
        writer.WriteStartObject();
        WriteTaskCreateToolOutput(writer, output);
        writer.WriteEndObject();
    }

    private static void WriteTaskCreateToolOutput(Utf8JsonWriter writer, TaskCreateToolOutput value)
    {
        writer.Nested("task", value.Task, WriteTaskCreateToolTask);
    }
}
