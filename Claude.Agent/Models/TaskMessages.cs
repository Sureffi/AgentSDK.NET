using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// A background task's lifecycle, one type per notice: <see cref="TaskStartedMessage"/> when it
/// begins, <see cref="TaskProgressMessage"/> while it works, <see cref="TaskUpdatedMessage"/> when
/// its record changes, <see cref="TaskNotificationMessage"/> when it is done.
/// </summary>
public abstract class TaskMessage : SystemMessage
{
    private protected TaskMessage(string subtype, string? taskId, string? uuid, string? sessionId, JsonElement? raw)
        : base(subtype, uuid, sessionId, raw)
    {
        TaskId = taskId;
    }

    /// <summary>The task, by the id the binary gives it. Always set, except on an update the binary wrote without one.</summary>
    public string? TaskId { get; }
}

/// <summary>
/// A background task has begun. One type per kind of task the binary names, each with only its own
/// members over the ones every kind carries; a kind this library has no type for is an
/// <see cref="UnknownTaskStartedMessage"/>.
/// </summary>
public abstract class TaskStartedMessage : TaskMessage
{
    private protected TaskStartedMessage(TaskType taskType, string taskId, string description, string? uuid, string? sessionId, JsonElement? raw)
        : base("task_started", taskId, uuid, sessionId, raw)
    {
        TaskType = taskType;
        Description = description;
    }

    /// <summary>What kind of work it is, as the binary names it; the default, which prints empty, when the binary named none.</summary>
    public TaskType TaskType { get; }

    /// <summary>What the task is for, as the model described it.</summary>
    public string Description { get; }

    /// <summary>The tool use that started it.</summary>
    public string? ToolUseId { get; internal init; }

    /// <summary>Whether the task's turns are kept out of the transcript.</summary>
    public bool? SkipTranscript { get; internal init; }

    /// <summary>Whether it runs beside the conversation rather than for it.</summary>
    public bool? Ambient { get; internal init; }
}

/// <summary>A subagent on this machine has begun: <c>local_agent</c>.</summary>
public sealed class LocalAgentTaskStartedMessage : TaskStartedMessage
{
    internal LocalAgentTaskStartedMessage(string taskId, string description, string? uuid, string? sessionId, JsonElement? raw)
        : base(TaskType.LocalAgent, taskId, description, uuid, sessionId, raw)
    {
    }

    /// <summary>The subagent doing it, by name.</summary>
    public string? SubagentType { get; internal init; }

    /// <summary>Whether it runs in the background, past the turn that started it.</summary>
    public bool? IsBackgrounded { get; internal init; }

    /// <summary>How deep in a chain of subagents it sits: 1 for one the conversation started.</summary>
    public long? SpawnDepth { get; internal init; }

    /// <summary>What the subagent was asked for, in full.</summary>
    public string? Prompt { get; internal init; }
}

/// <summary>A shell command on this machine has begun: <c>local_bash</c>.</summary>
public sealed class LocalBashTaskStartedMessage : TaskStartedMessage
{
    internal LocalBashTaskStartedMessage(string taskId, string description, string? uuid, string? sessionId, JsonElement? raw)
        : base(TaskType.LocalBash, taskId, description, uuid, sessionId, raw)
    {
    }

    /// <summary>Whether it runs in the background, past the turn that started it.</summary>
    public bool? IsBackgrounded { get; internal init; }
}

/// <summary>A workflow on this machine has begun: <c>local_workflow</c>.</summary>
public sealed class LocalWorkflowTaskStartedMessage : TaskStartedMessage
{
    internal LocalWorkflowTaskStartedMessage(string taskId, string description, string? uuid, string? sessionId, JsonElement? raw)
        : base(TaskType.LocalWorkflow, taskId, description, uuid, sessionId, raw)
    {
    }

    /// <summary>The workflow's name, as its script gives it.</summary>
    public string? WorkflowName { get; internal init; }

    /// <summary>The source text of the script the workflow runs, in full, which the binary sends as the task's prompt; the script's path and arguments are not sent.</summary>
    public string? Prompt { get; internal init; }
}

/// <summary>
/// A background task of a kind this library has no type for has begun: its kind's name in
/// <see cref="TaskStartedMessage.TaskType"/>, the members every kind carries, and the line itself in
/// <see cref="AgentMessage.Raw"/>.
/// </summary>
public sealed class UnknownTaskStartedMessage : TaskStartedMessage
{
    internal UnknownTaskStartedMessage(TaskType taskType, string taskId, string description, string? uuid, string? sessionId, JsonElement? raw)
        : base(taskType, taskId, description, uuid, sessionId, raw)
    {
    }
}

/// <summary>What a background task has spent so far.</summary>
public sealed class TaskProgressMessage : TaskMessage
{
    internal TaskProgressMessage(string taskId, string description, string? uuid, string? sessionId, JsonElement? raw)
        : base("task_progress", taskId, uuid, sessionId, raw)
    {
        Description = description;
    }

    /// <summary>What the task is for, as the model described it.</summary>
    public string Description { get; }

    /// <summary>The tool use that started it.</summary>
    public string? ToolUseId { get; internal init; }

    /// <summary>The subagent doing it, by name.</summary>
    public string? SubagentType { get; internal init; }

    /// <summary>Tokens the task has spent; null when the binary wrote a number no count holds.</summary>
    public long? TotalTokens { get; internal init; }

    /// <summary>Tool uses the task has made; null when the binary wrote a number no count holds.</summary>
    public long? ToolUseCount { get; internal init; }

    /// <summary>How long the task has run; null when the binary wrote a number no duration holds.</summary>
    public TimeSpan? Duration { get; internal init; }

    /// <summary>The tool it used last.</summary>
    public string? LastToolName { get; internal init; }

    /// <summary>What it has to say about where it is.</summary>
    public string? Summary { get; internal init; }
}

/// <summary>A background task is done, and this is what came of it.</summary>
public sealed class TaskNotificationMessage : TaskMessage
{
    internal TaskNotificationMessage(
        string taskId,
        TaskNotificationStatus status,
        string outputFile,
        string summary,
        string? uuid,
        string? sessionId,
        JsonElement? raw)
        : base("task_notification", taskId, uuid, sessionId, raw)
    {
        Status = status;
        OutputFile = outputFile;
        Summary = summary;
    }

    /// <summary>How it ended.</summary>
    public TaskNotificationStatus Status { get; }

    /// <summary>The file its output was written to.</summary>
    public string OutputFile { get; }

    /// <summary>What it reported when it ended.</summary>
    public string Summary { get; }

    /// <summary>The tool use that started it.</summary>
    public string? ToolUseId { get; internal init; }

    /// <summary>Why it is reporting again, when it is.</summary>
    public TaskNotificationReason? Reason { get; internal init; }

    /// <summary>Tokens it spent; absent when the binary does not count them.</summary>
    public long? TotalTokens { get; internal init; }

    /// <summary>Tool uses it made; absent when the binary does not count them.</summary>
    public long? ToolUseCount { get; internal init; }

    /// <summary>How long it ran; absent when the binary does not count it.</summary>
    public TimeSpan? Duration { get; internal init; }

    /// <summary>What it produced, as links.</summary>
    public IReadOnlyList<ResourceLink> ResourceLinks { get; internal init; } = [];

    /// <summary>Whether the task's turns are kept out of the transcript.</summary>
    public bool? SkipTranscript { get; internal init; }

    /// <summary>Whether it ran beside the conversation rather than for it.</summary>
    public bool? Ambient { get; internal init; }
}

/// <summary>A background task's record has changed, and this is what changed.</summary>
public sealed class TaskUpdatedMessage : TaskMessage
{
    internal TaskUpdatedMessage(TaskPatch patch, string? taskId, string? uuid, string? sessionId, JsonElement? raw)
        : base("task_updated", taskId, uuid, sessionId, raw)
    {
        Patch = patch;
    }

    /// <summary>What changed; the members the binary did not change are null.</summary>
    public TaskPatch Patch { get; }
}

/// <summary>What changed about a background task.</summary>
public sealed class TaskPatch
{
    internal TaskPatch()
    {
    }

    /// <summary>Where the task now stands.</summary>
    public BackgroundTaskStatus? Status { get; internal init; }

    /// <summary>What it is for, as it now reads.</summary>
    public string? Description { get; internal init; }

    /// <summary>When it ended.</summary>
    public DateTimeOffset? EndTime { get; internal init; }

    /// <summary>How long it has been held.</summary>
    public TimeSpan? TotalPausedTime { get; internal init; }

    /// <summary>What went wrong, when something did.</summary>
    public string? Error { get; internal init; }

    /// <summary>Whether it now runs in the background.</summary>
    public bool? IsBackgrounded { get; internal init; }

    /// <summary>Whether the task is done with, one way or another.</summary>
    public bool IsTerminal =>
        Status is { } status
        && (status == BackgroundTaskStatus.Completed || status == BackgroundTaskStatus.Failed || status == BackgroundTaskStatus.Killed);
}

/// <summary>Something a background task produced, as a link to it.</summary>
public sealed class ResourceLink
{
    internal ResourceLink(string uri, string name)
    {
        Uri = uri;
        Name = name;
    }

    /// <summary>Where it is.</summary>
    public string Uri { get; }

    /// <summary>What it is called.</summary>
    public string Name { get; }

    /// <summary>A name to show instead.</summary>
    public string? Title { get; internal init; }

    /// <summary>What it is.</summary>
    public string? Description { get; internal init; }

    /// <summary>The media type of what it points at.</summary>
    public string? MimeType { get; internal init; }

    /// <summary>How big it is, in bytes.</summary>
    public long? Size { get; internal init; }
}
