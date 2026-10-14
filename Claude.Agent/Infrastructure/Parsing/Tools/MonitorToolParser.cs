using System.Text.Json;

namespace Claude.Agent;

// The Monitor tool's input and output. No recording calls this tool: the shape stands on the
// declaration alone, and a JSON that does not fit it reads as null beside the raw.
internal static class MonitorToolParser
{
    private static MonitorToolWebSocket? AsMonitorToolWebSocket(JsonElement raw)
    {
        if (!raw.Text("url", out var url)
            || !raw.TextsOrEmpty("protocols", out var protocols))
        {
            return null;
        }

        return new MonitorToolWebSocket(url, protocols);
    }

    public static MonitorToolInput? ReadInput(JsonElement raw)
    {
        if (!raw.Text("description", out var description)
            || !raw.Milliseconds("timeout_ms", out var timeout)
            || !raw.Nested("ws", AsMonitorToolWebSocket, out var webSocket))
        {
            return null;
        }

        return new MonitorToolInput(description, timeout, raw.TextOrNull("command"), webSocket);
    }

    public static MonitorToolOutput? ReadOutput(JsonElement raw)
    {
        if (!raw.Text("taskId", out var taskId)
            || !raw.Milliseconds("timeoutMs", out var timeout))
        {
            return null;
        }

        return new MonitorToolOutput(taskId, timeout, raw.FlagOrNull("persistent"));
    }

    private static void WriteMonitorToolWebSocket(Utf8JsonWriter writer, MonitorToolWebSocket value)
    {
        writer.WriteString("url", value.Url);
        writer.Texts("protocols", value.Protocols);
    }

    public static void WriteInput(Utf8JsonWriter writer, MonitorToolInput input)
    {
        writer.WriteStartObject();
        WriteMonitorToolInput(writer, input);
        writer.WriteEndObject();
    }

    private static void WriteMonitorToolInput(Utf8JsonWriter writer, MonitorToolInput value)
    {
        writer.WriteString("description", value.Description);
        writer.Milliseconds("timeout_ms", value.Timeout);
        writer.Text("command", value.Command);
        writer.Nested("ws", value.WebSocket, WriteMonitorToolWebSocket);
    }

    public static void WriteOutput(Utf8JsonWriter writer, MonitorToolOutput output)
    {
        writer.WriteStartObject();
        WriteMonitorToolOutput(writer, output);
        writer.WriteEndObject();
    }

    private static void WriteMonitorToolOutput(Utf8JsonWriter writer, MonitorToolOutput value)
    {
        writer.WriteString("taskId", value.TaskId);
        writer.Milliseconds("timeoutMs", value.Timeout);
        writer.Flag("persistent", value.Persistent);
    }
}
