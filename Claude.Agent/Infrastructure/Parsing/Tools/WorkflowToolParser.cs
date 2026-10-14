using System.Text.Json;

namespace Claude.Agent;

// The Workflow tool's input and output. The arguments a script is handed are arbitrary JSON, read
// through the raw beside the view. The output is a union on `status`, one type a value. No recording
// calls this tool: the shape stands on the declaration, and on the binary's own build of the output,
// which launches here only and names the transcripts, the script and the error there.
internal static class WorkflowToolParser
{
    public static WorkflowToolInput? ReadInput(JsonElement raw)
    {
        return new WorkflowToolInput(
            raw.TextOrNull("script"),
            raw.TextOrNull("name"),
            raw.TextOrNull("description"),
            raw.TextOrNull("title"),
            raw.TextOrNull("scriptPath"),
            raw.TextOrNull("resumeFromRunId"));
    }

    // The status is which type the output is. A remote launch without the session to watch does not
    // fit it and reads as null, as does a status the declaration does not name.
    public static WorkflowToolOutput? ReadOutput(JsonElement raw)
    {
        if (!raw.Text("taskId", out var taskId))
        {
            return null;
        }

        var taskType = raw.SetOrNull("taskType", value => new WorkflowTaskType(value));
        var workflowName = raw.TextOrNull("workflowName");
        var summary = raw.TextOrNull("summary");
        return raw.TextOrNull("status") switch
        {
            "async_launched" => new WorkflowAsyncLaunchedToolOutput(
                taskId,
                taskType,
                workflowName,
                summary,
                raw.TextOrNull("runId"),
                raw.TextOrNull("transcriptDir"),
                raw.TextOrNull("scriptPath"),
                raw.TextOrNull("error")),
            "remote_launched" => raw.Text("sessionUrl", out var sessionUrl)
                ? new WorkflowRemoteLaunchedToolOutput(taskId, sessionUrl, taskType, workflowName, summary, raw.TextOrNull("warning"))
                : null,
            _ => null,
        };
    }

    public static void WriteInput(Utf8JsonWriter writer, WorkflowToolInput input)
    {
        writer.WriteStartObject();
        WriteWorkflowToolInput(writer, input);
        writer.WriteEndObject();
    }

    private static void WriteWorkflowToolInput(Utf8JsonWriter writer, WorkflowToolInput value)
    {
        writer.Text("script", value.Script);
        writer.Text("name", value.Name);
        writer.Text("description", value.Description);
        writer.Text("title", value.Title);
        writer.Text("scriptPath", value.ScriptPath);
        writer.Text("resumeFromRunId", value.ResumeFromRunId);
    }

    public static void WriteOutput(Utf8JsonWriter writer, WorkflowToolOutput output)
    {
        writer.WriteStartObject();
        switch (output)
        {
            case WorkflowAsyncLaunchedToolOutput launched:
                writer.WriteString("status", "async_launched");
                WriteShared(writer, launched);
                writer.Text("runId", launched.RunId);
                writer.Text("transcriptDir", launched.TranscriptDirectory);
                writer.Text("scriptPath", launched.ScriptPath);
                writer.Text("error", launched.Error);
                break;
            case WorkflowRemoteLaunchedToolOutput remote:
                writer.WriteString("status", "remote_launched");
                WriteShared(writer, remote);
                writer.WriteString("sessionUrl", remote.SessionUrl);
                writer.Text("warning", remote.Warning);
                break;
            default:
                throw new NotSupportedException($"No Workflow output of type {output.GetType().Name} can be written.");
        }

        writer.WriteEndObject();
    }

    private static void WriteShared(Utf8JsonWriter writer, WorkflowToolOutput value)
    {
        writer.WriteString("taskId", value.TaskId);
        writer.Text("taskType", value.TaskType?.ToString());
        writer.Text("workflowName", value.WorkflowName);
        writer.Text("summary", value.Summary);
    }
}
