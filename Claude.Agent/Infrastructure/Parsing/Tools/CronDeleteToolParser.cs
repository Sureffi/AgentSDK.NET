using System.Text.Json;

namespace Claude.Agent;

// The CronDelete tool's input and output, both the job's identifier and nothing else. No recording
// calls this tool: the shape stands on the declaration alone.
internal static class CronDeleteToolParser
{
    public static CronDeleteToolInput? ReadInput(JsonElement raw) =>
        raw.Text("id", out var id) ? new CronDeleteToolInput(id) : null;

    public static CronDeleteToolOutput? ReadOutput(JsonElement raw) =>
        raw.Text("id", out var id) ? new CronDeleteToolOutput(id) : null;

    public static void WriteInput(Utf8JsonWriter writer, CronDeleteToolInput input)
    {
        writer.WriteStartObject();
        WriteCronDeleteToolInput(writer, input);
        writer.WriteEndObject();
    }

    private static void WriteCronDeleteToolInput(Utf8JsonWriter writer, CronDeleteToolInput value)
    {
        writer.WriteString("id", value.Id);
    }

    public static void WriteOutput(Utf8JsonWriter writer, CronDeleteToolOutput output)
    {
        writer.WriteStartObject();
        WriteCronDeleteToolOutput(writer, output);
        writer.WriteEndObject();
    }

    private static void WriteCronDeleteToolOutput(Utf8JsonWriter writer, CronDeleteToolOutput value)
    {
        writer.WriteString("id", value.Id);
    }
}
