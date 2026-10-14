using System.Text.Json;

namespace Claude.Agent;

public static partial class ClaudeAgentModelFactory
{
    /// <summary>Builds a <see cref="Claude.Agent.CronCreateToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static CronCreateToolInput CronCreateToolInput(
        string cron = "",
        string prompt = "",
        bool? recurring = null,
        bool? durable = null) =>
        new(cron, prompt, recurring, durable);

    /// <summary>Builds a <see cref="Claude.Agent.CronCreateToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static CronCreateToolOutput CronCreateToolOutput(
        string id = "",
        string humanSchedule = "",
        bool recurring = false,
        bool? durable = null) =>
        new(id, humanSchedule, recurring, durable);

    /// <summary>Builds a <see cref="Claude.Agent.CronDeleteToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static CronDeleteToolInput CronDeleteToolInput(string id = "") =>
        new(id);

    /// <summary>Builds a <see cref="Claude.Agent.CronDeleteToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static CronDeleteToolOutput CronDeleteToolOutput(string id = "") =>
        new(id);

    /// <summary>Builds a <see cref="Claude.Agent.CronListToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static CronListToolInput CronListToolInput() =>
        new();

    /// <summary>Builds a <see cref="Claude.Agent.CronListToolJob"/> for a test. Each parameter is the property of its name.</summary>
    public static CronListToolJob CronListToolJob(
        string id = "",
        string cron = "",
        string humanSchedule = "",
        string prompt = "",
        bool? recurring = null,
        bool? durable = null) =>
        new(id, cron, humanSchedule, prompt, recurring, durable);

    /// <summary>Builds a <see cref="Claude.Agent.CronListToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static CronListToolOutput CronListToolOutput(IEnumerable<CronListToolJob>? jobs = null) =>
        new([.. jobs ?? []]);

    /// <summary>Builds a <see cref="Claude.Agent.ScheduleWakeupToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static ScheduleWakeupToolInput ScheduleWakeupToolInput(
        TimeSpan? delay = null,
        string? reason = null,
        string? prompt = null,
        bool? stop = null,
        bool? nothingChanged = null) =>
        new(delay, reason, prompt, stop, nothingChanged);

    /// <summary>Builds a <see cref="Claude.Agent.ScheduleWakeupToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ScheduleWakeupToolOutput ScheduleWakeupToolOutput(
        DateTimeOffset scheduledFor = default,
        TimeSpan clampedDelay = default,
        bool wasClamped = false,
        bool? stopped = null,
        long? cancelledWakeupCount = null) =>
        new(scheduledFor, clampedDelay, wasClamped, stopped, cancelledWakeupCount);

    /// <summary>Builds a <see cref="Claude.Agent.MonitorToolWebSocket"/> for a test. Each parameter is the property of its name.</summary>
    public static MonitorToolWebSocket MonitorToolWebSocket(string url = "", IEnumerable<string>? protocols = null) =>
        new(url, [.. protocols ?? []]);

    /// <summary>Builds a <see cref="Claude.Agent.MonitorToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static MonitorToolInput MonitorToolInput(
        string description = "",
        TimeSpan timeout = default,
        string? command = null,
        MonitorToolWebSocket? webSocket = null) =>
        new(description, timeout, command, webSocket);

    /// <summary>Builds a <see cref="Claude.Agent.MonitorToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static MonitorToolOutput MonitorToolOutput(
        string taskId = "",
        TimeSpan timeout = default,
        bool? persistent = null) =>
        new(taskId, timeout, persistent);

    /// <summary>Builds a <see cref="Claude.Agent.WorkflowToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static WorkflowToolInput WorkflowToolInput(
        string? script = null,
        string? name = null,
        string? description = null,
        string? title = null,
        string? scriptPath = null,
        string? resumeFromRunId = null) =>
        new(script, name, description, title, scriptPath, resumeFromRunId);

    /// <summary>Builds a <see cref="Claude.Agent.WorkflowAsyncLaunchedToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static WorkflowAsyncLaunchedToolOutput WorkflowAsyncLaunchedToolOutput(
        string taskId = "",
        WorkflowTaskType? taskType = null,
        string? workflowName = null,
        string? summary = null,
        string? runId = null,
        string? transcriptDirectory = null,
        string? scriptPath = null,
        string? error = null) =>
        new(taskId, taskType, workflowName, summary, runId, transcriptDirectory, scriptPath, error);

    /// <summary>Builds a <see cref="Claude.Agent.WorkflowRemoteLaunchedToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static WorkflowRemoteLaunchedToolOutput WorkflowRemoteLaunchedToolOutput(
        string taskId = "",
        string sessionUrl = "",
        WorkflowTaskType? taskType = null,
        string? workflowName = null,
        string? summary = null,
        string? warning = null) =>
        new(taskId, sessionUrl, taskType, workflowName, summary, warning);

    /// <summary>Builds a <see cref="Claude.Agent.RemoteTriggerListToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static RemoteTriggerListToolInput RemoteTriggerListToolInput() =>
        new();

    /// <summary>Builds a <see cref="Claude.Agent.RemoteTriggerGetToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static RemoteTriggerGetToolInput RemoteTriggerGetToolInput(string triggerId = "") =>
        new(triggerId);

    /// <summary>Builds a <see cref="Claude.Agent.RemoteTriggerCreateToolInput"/> for a test. Each parameter is the property of its name. <c>body</c> defaults to an empty object.</summary>
    public static RemoteTriggerCreateToolInput RemoteTriggerCreateToolInput(JsonElement body = default) =>
        new(Object(body));

    /// <summary>Builds a <see cref="Claude.Agent.RemoteTriggerUpdateToolInput"/> for a test. Each parameter is the property of its name. <c>body</c> defaults to an empty object.</summary>
    public static RemoteTriggerUpdateToolInput RemoteTriggerUpdateToolInput(string triggerId = "", JsonElement body = default) =>
        new(triggerId, Object(body));

    /// <summary>Builds a <see cref="Claude.Agent.RemoteTriggerRunToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static RemoteTriggerRunToolInput RemoteTriggerRunToolInput(string triggerId = "", JsonElement? body = null) =>
        new(triggerId, body);

    /// <summary>Builds a <see cref="Claude.Agent.RemoteTriggerCreateWebhookTriggerToolInput"/> for a test. Each parameter is the property of its name. <c>body</c> defaults to an empty object.</summary>
    public static RemoteTriggerCreateWebhookTriggerToolInput RemoteTriggerCreateWebhookTriggerToolInput(JsonElement body = default) =>
        new(Object(body));

    /// <summary>Builds a <see cref="Claude.Agent.RemoteTriggerListRunsToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static RemoteTriggerListRunsToolInput RemoteTriggerListRunsToolInput(string triggerId = "", string? cursor = null) =>
        new(triggerId, cursor);

    /// <summary>Builds a <see cref="Claude.Agent.RemoteTriggerGetRunLogToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static RemoteTriggerGetRunLogToolInput RemoteTriggerGetRunLogToolInput(string sessionId = "", string? cursor = null) =>
        new(sessionId, cursor);

    /// <summary>Builds a <see cref="Claude.Agent.RemoteTriggerToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static RemoteTriggerToolOutput RemoteTriggerToolOutput(long status = 0, string json = "", string? summary = null) =>
        new(status, json, summary);
}
