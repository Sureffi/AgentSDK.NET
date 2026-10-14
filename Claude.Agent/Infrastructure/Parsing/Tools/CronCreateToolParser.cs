using System.Text.Json;

namespace Claude.Agent;

// The CronCreate tool's input and output. No recording calls this tool: the shape stands on the
// declaration alone, and a JSON that does not fit it reads as null beside the raw.
internal static class CronCreateToolParser
{
    public static CronCreateToolInput? ReadInput(JsonElement raw)
    {
        if (!raw.Text("cron", out var cron)
            || !raw.Text("prompt", out var prompt))
        {
            return null;
        }

        return new CronCreateToolInput(cron, prompt, raw.FlagOrNull("recurring"), raw.FlagOrNull("durable"));
    }

    public static CronCreateToolOutput? ReadOutput(JsonElement raw)
    {
        if (!raw.Text("id", out var id)
            || !raw.Text("humanSchedule", out var humanSchedule)
            || !raw.Flag("recurring", out var recurring))
        {
            return null;
        }

        return new CronCreateToolOutput(id, humanSchedule, recurring, raw.FlagOrNull("durable"));
    }

    public static void WriteInput(Utf8JsonWriter writer, CronCreateToolInput input)
    {
        writer.WriteStartObject();
        WriteCronCreateToolInput(writer, input);
        writer.WriteEndObject();
    }

    private static void WriteCronCreateToolInput(Utf8JsonWriter writer, CronCreateToolInput value)
    {
        writer.WriteString("cron", value.Cron);
        writer.WriteString("prompt", value.Prompt);
        writer.Flag("recurring", value.Recurring);
        writer.Flag("durable", value.Durable);
    }

    public static void WriteOutput(Utf8JsonWriter writer, CronCreateToolOutput output)
    {
        writer.WriteStartObject();
        WriteCronCreateToolOutput(writer, output);
        writer.WriteEndObject();
    }

    private static void WriteCronCreateToolOutput(Utf8JsonWriter writer, CronCreateToolOutput value)
    {
        writer.WriteString("id", value.Id);
        writer.WriteString("humanSchedule", value.HumanSchedule);
        writer.WriteBoolean("recurring", value.Recurring);
        writer.Flag("durable", value.Durable);
    }
}
