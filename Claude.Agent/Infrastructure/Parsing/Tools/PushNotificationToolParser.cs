using System.Text.Json;

namespace Claude.Agent;

// The PushNotification tool's input and output. No recording calls this tool: the shape stands on the
// declaration alone, and a JSON that does not fit it reads as null beside the raw.
internal static class PushNotificationToolParser
{
    public static PushNotificationToolInput? ReadInput(JsonElement raw)
    {
        if (!raw.Text("message", out var message)
            || !raw.Text("status", out var status))
        {
            return null;
        }

        return new PushNotificationToolInput(message, status);
    }

    public static PushNotificationToolOutput? ReadOutput(JsonElement raw)
    {
        if (!raw.Text("message", out var message))
        {
            return null;
        }

        return new PushNotificationToolOutput(
            message,
            raw.FlagOrNull("pushSent"),
            raw.FlagOrNull("localSent"),
            raw.SetOrNull("disabledReason", value => new PushNotificationDisabledReason(value)),
            raw.TextOrNull("sentAt"));
    }

    public static void WriteInput(Utf8JsonWriter writer, PushNotificationToolInput input)
    {
        writer.WriteStartObject();
        WritePushNotificationToolInput(writer, input);
        writer.WriteEndObject();
    }

    private static void WritePushNotificationToolInput(Utf8JsonWriter writer, PushNotificationToolInput value)
    {
        writer.WriteString("message", value.Message);
        writer.WriteString("status", value.Status.ToString());
    }

    public static void WriteOutput(Utf8JsonWriter writer, PushNotificationToolOutput output)
    {
        writer.WriteStartObject();
        WritePushNotificationToolOutput(writer, output);
        writer.WriteEndObject();
    }

    private static void WritePushNotificationToolOutput(Utf8JsonWriter writer, PushNotificationToolOutput value)
    {
        writer.WriteString("message", value.Message);
        writer.Flag("pushSent", value.PushSent);
        writer.Flag("localSent", value.LocalSent);
        writer.Text("disabledReason", value.DisabledReason?.ToString());
        writer.Text("sentAt", value.SentAt);
    }
}
