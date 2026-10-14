using System.Text.Json;

namespace Claude.Agent;

// The RemoteTrigger tool's input and output. The input is a union on `action`, one type a value, and
// which keys an action cannot do without stands on the binary's own checks: trigger_id for get,
// update, run and list_runs, session_id for get_run_log, and a body, a JSON object, for create, update
// and create_webhook_trigger. That body is the service's shape and goes on the view raw, written back
// as it came, so a view handed back is a call the binary takes; run's is optional, and the binary
// posts it less its trigger_id. The declaration types body as an object, so a body that is not one is
// a call the binary refuses. The output is three scalars, the service's answer among them as the
// string the binary hands back, never read inside. No recording calls this tool: the shape stands on
// the declaration and the binary's checks.
internal static class RemoteTriggerToolParser
{
    // An action the declaration does not name, one without a key the binary requires of it, the body
    // among them, or a run whose body is not an object, reads as null.
    public static RemoteTriggerToolInput? ReadInput(JsonElement raw) =>
        raw.TextOrNull("action") switch
        {
            "list" => new RemoteTriggerListToolInput(),
            "get" => raw.Text("trigger_id", out var triggerId) ? new RemoteTriggerGetToolInput(triggerId) : null,
            "create" => raw.Object("body", out var body) ? new RemoteTriggerCreateToolInput(body) : null,
            "update" => raw.Text("trigger_id", out var triggerId) && raw.Object("body", out var body)
                ? new RemoteTriggerUpdateToolInput(triggerId, body)
                : null,
            "run" => raw.Text("trigger_id", out var triggerId) && OptionalObject(raw, "body", out var body)
                ? new RemoteTriggerRunToolInput(triggerId, body)
                : null,
            "create_webhook_trigger" => raw.Object("body", out var body) ? new RemoteTriggerCreateWebhookTriggerToolInput(body) : null,
            "list_runs" => raw.Text("trigger_id", out var triggerId)
                ? new RemoteTriggerListRunsToolInput(triggerId, raw.TextOrNull("cursor"))
                : null,
            "get_run_log" => raw.Text("session_id", out var sessionId)
                ? new RemoteTriggerGetRunLogToolInput(sessionId, raw.TextOrNull("cursor"))
                : null,
            _ => null,
        };

    public static RemoteTriggerToolOutput? ReadOutput(JsonElement raw) =>
        raw.Count("status", out var status) && raw.Text("json", out var json)
            ? new RemoteTriggerToolOutput(status, json, raw.TextOrNull("summary"))
            : null;

    public static void WriteInput(Utf8JsonWriter writer, RemoteTriggerToolInput input)
    {
        writer.WriteStartObject();
        switch (input)
        {
            case RemoteTriggerListToolInput:
                writer.WriteString("action", "list");
                break;
            case RemoteTriggerGetToolInput get:
                writer.WriteString("action", "get");
                writer.WriteString("trigger_id", get.TriggerId);
                break;
            case RemoteTriggerCreateToolInput create:
                writer.WriteString("action", "create");
                Body(writer, create.Body);
                break;
            case RemoteTriggerUpdateToolInput update:
                writer.WriteString("action", "update");
                writer.WriteString("trigger_id", update.TriggerId);
                Body(writer, update.Body);
                break;
            case RemoteTriggerRunToolInput run:
                writer.WriteString("action", "run");
                writer.WriteString("trigger_id", run.TriggerId);
                if (run.Body is { } runBody)
                {
                    Body(writer, runBody);
                }

                break;
            case RemoteTriggerCreateWebhookTriggerToolInput createWebhookTrigger:
                writer.WriteString("action", "create_webhook_trigger");
                Body(writer, createWebhookTrigger.Body);
                break;
            case RemoteTriggerListRunsToolInput listRuns:
                writer.WriteString("action", "list_runs");
                writer.WriteString("trigger_id", listRuns.TriggerId);
                writer.Text("cursor", listRuns.Cursor);
                break;
            case RemoteTriggerGetRunLogToolInput getRunLog:
                writer.WriteString("action", "get_run_log");
                writer.WriteString("session_id", getRunLog.SessionId);
                writer.Text("cursor", getRunLog.Cursor);
                break;
            default:
                throw new NotSupportedException($"No RemoteTrigger input of type {input.GetType().Name} can be written.");
        }

        writer.WriteEndObject();
    }

    public static void WriteOutput(Utf8JsonWriter writer, RemoteTriggerToolOutput output)
    {
        writer.WriteStartObject();
        writer.WriteNumber("status", output.Status);
        writer.WriteString("json", output.Json);
        writer.Text("summary", output.Summary);
        writer.WriteEndObject();
    }

    // A key the call may leave out: absent or null is null, an object is itself, and anything else is
    // no call the binary takes.
    private static bool OptionalObject(JsonElement raw, string key, out JsonElement? value)
    {
        value = null;
        if (!raw.TryProperty(key, out _))
        {
            return true;
        }

        if (raw.Object(key, out var found))
        {
            value = found;
            return true;
        }

        return false;
    }

    private static void Body(Utf8JsonWriter writer, JsonElement body)
    {
        writer.WritePropertyName("body");
        body.WriteTo(writer);
    }
}
