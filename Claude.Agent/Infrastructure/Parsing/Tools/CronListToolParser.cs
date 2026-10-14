using System.Text.Json;

namespace Claude.Agent;

// The CronList tool's input and output. The input carries nothing: the tool lists every job.
// No recording calls this tool: the shape stands on the declaration alone.
internal static class CronListToolParser
{
    public static CronListToolInput? ReadInput(JsonElement raw) =>
        raw.ValueKind == JsonValueKind.Object ? new CronListToolInput() : null;

    private static CronListToolJob? AsCronListToolJob(JsonElement raw)
    {
        if (!raw.Text("id", out var id)
            || !raw.Text("cron", out var cron)
            || !raw.Text("humanSchedule", out var humanSchedule)
            || !raw.Text("prompt", out var prompt))
        {
            return null;
        }

        return new CronListToolJob(id, cron, humanSchedule, prompt, raw.FlagOrNull("recurring"), raw.FlagOrNull("durable"));
    }

    public static CronListToolOutput? ReadOutput(JsonElement raw) =>
        raw.Items("jobs", AsCronListToolJob, out var jobs) ? new CronListToolOutput(jobs) : null;

    public static void WriteInput(Utf8JsonWriter writer, CronListToolInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        writer.WriteStartObject();
        writer.WriteEndObject();
    }

    private static void WriteCronListToolJob(Utf8JsonWriter writer, CronListToolJob value)
    {
        writer.WriteString("id", value.Id);
        writer.WriteString("cron", value.Cron);
        writer.WriteString("humanSchedule", value.HumanSchedule);
        writer.WriteString("prompt", value.Prompt);
        writer.Flag("recurring", value.Recurring);
        writer.Flag("durable", value.Durable);
    }

    public static void WriteOutput(Utf8JsonWriter writer, CronListToolOutput output)
    {
        writer.WriteStartObject();
        WriteCronListToolOutput(writer, output);
        writer.WriteEndObject();
    }

    private static void WriteCronListToolOutput(Utf8JsonWriter writer, CronListToolOutput value)
    {
        writer.ItemObjects("jobs", value.Jobs, WriteCronListToolJob, whenEmpty: true);
    }
}
