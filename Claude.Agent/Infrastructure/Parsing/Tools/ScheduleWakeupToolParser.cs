using System.Text.Json;

namespace Claude.Agent;

// The ScheduleWakeup tool's input and output. Every key of the input is optional, since a call that
// ends the loop needs none of the others. No recording calls this tool: the shape stands on the
// declaration alone.
internal static class ScheduleWakeupToolParser
{
    public static ScheduleWakeupToolInput? ReadInput(JsonElement raw)
    {
        return new ScheduleWakeupToolInput(
            raw.SecondsOrNull("delaySeconds"),
            raw.TextOrNull("reason"),
            raw.TextOrNull("prompt"),
            raw.FlagOrNull("stop"),
            raw.FlagOrNull("noop"));
    }

    public static ScheduleWakeupToolOutput? ReadOutput(JsonElement raw)
    {
        if (!raw.EpochMilliseconds("scheduledFor", out var scheduledFor)
            || !raw.Seconds("clampedDelaySeconds", out var clampedDelay)
            || !raw.Flag("wasClamped", out var wasClamped))
        {
            return null;
        }

        return new ScheduleWakeupToolOutput(
            scheduledFor,
            clampedDelay,
            wasClamped,
            raw.FlagOrNull("stopped"),
            raw.CountOrNull("cancelledWakeups"));
    }

    public static void WriteInput(Utf8JsonWriter writer, ScheduleWakeupToolInput input)
    {
        writer.WriteStartObject();
        WriteScheduleWakeupToolInput(writer, input);
        writer.WriteEndObject();
    }

    private static void WriteScheduleWakeupToolInput(Utf8JsonWriter writer, ScheduleWakeupToolInput value)
    {
        writer.Seconds("delaySeconds", value.Delay);
        writer.Text("reason", value.Reason);
        writer.Text("prompt", value.Prompt);
        writer.Flag("stop", value.Stop);
        writer.Flag("noop", value.NothingChanged);
    }

    public static void WriteOutput(Utf8JsonWriter writer, ScheduleWakeupToolOutput output)
    {
        writer.WriteStartObject();
        WriteScheduleWakeupToolOutput(writer, output);
        writer.WriteEndObject();
    }

    private static void WriteScheduleWakeupToolOutput(Utf8JsonWriter writer, ScheduleWakeupToolOutput value)
    {
        writer.EpochMilliseconds("scheduledFor", value.ScheduledFor);
        writer.Seconds("clampedDelaySeconds", value.ClampedDelay);
        writer.WriteBoolean("wasClamped", value.WasClamped);
        writer.Flag("stopped", value.Stopped);
        writer.Count("cancelledWakeups", value.CancelledWakeupCount);
    }
}
