using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// Something the binary is still running when a turn ends, as a stop hook is told of it. One type per
/// kind of task whose members a recording founds, each with only its own members over the ones every
/// kind carries; any other kind is an <see cref="UnknownStopHookBackgroundTask"/>.
/// </summary>
public abstract class StopHookBackgroundTask
{
    private protected StopHookBackgroundTask(StopHookBackgroundTaskType taskType, string id, BackgroundTaskStatus status, string description)
    {
        TaskType = taskType;
        Id = id;
        Status = status;
        Description = description;
    }

    /// <summary>What kind of task it is, as the binary labels it: a shell command, a subagent, a monitor, a workflow; a kind the binary has no label for is its own name.</summary>
    public StopHookBackgroundTaskType TaskType { get; }

    /// <summary>The task.</summary>
    public string Id { get; }

    /// <summary>Where the task stands: pending or running, since the binary lists only the tasks still in flight.</summary>
    public BackgroundTaskStatus Status { get; }

    /// <summary>What the task is, in the binary's words. The binary cuts it short past a thousand characters and says so in the text.</summary>
    public string Description { get; }
}

/// <summary>A shell command running in the background: <c>shell</c>.</summary>
public sealed class ShellStopHookBackgroundTask : StopHookBackgroundTask
{
    internal ShellStopHookBackgroundTask(string id, BackgroundTaskStatus status, string description)
        : base(StopHookBackgroundTaskType.Shell, id, status, description)
    {
    }

    /// <summary>The command line. The binary cuts it short past a thousand characters and says so in the text.</summary>
    public string? Command { get; internal init; }
}

/// <summary>A subagent running in the background: <c>subagent</c>.</summary>
public sealed class SubagentStopHookBackgroundTask : StopHookBackgroundTask
{
    internal SubagentStopHookBackgroundTask(string id, BackgroundTaskStatus status, string description)
        : base(StopHookBackgroundTaskType.Subagent, id, status, description)
    {
    }

    /// <summary>The subagent's type.</summary>
    public AgentType? AgentType { get; internal init; }
}

/// <summary>
/// A background task of a kind this library has no type for: its kind's label in
/// <see cref="StopHookBackgroundTask.TaskType"/>, the members every kind carries, and the entry as the
/// binary wrote it in <see cref="Raw"/>. The binary labels a workflow, a monitor, an MCP task, a
/// teammate, a dream, an auto-mode scan and a cloud session, and no recording founds what any of them
/// carries beside the members every kind has, so each of them is this type.
/// </summary>
public sealed class UnknownStopHookBackgroundTask : StopHookBackgroundTask
{
    internal UnknownStopHookBackgroundTask(StopHookBackgroundTaskType taskType, string id, BackgroundTaskStatus status, string description, JsonElement raw)
        : base(taskType, id, status, description)
    {
        Raw = raw;
    }

    /// <summary>The entry as the binary wrote it.</summary>
    public JsonElement Raw { get; }
}
