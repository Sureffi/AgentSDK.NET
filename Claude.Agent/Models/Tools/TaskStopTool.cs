namespace Claude.Agent;

/// <summary>What the model asked the TaskStop tool to stop.</summary>
/// <param name="taskId">The task to stop.</param>
/// <param name="shellId">The shell to stop, under the older key the declaration marks as going.</param>
public sealed class TaskStopToolInput(string? taskId = null, string? shellId = null) : ToolInput
{
    /// <summary>The task to stop; null when the call named the shell instead.</summary>
    public string? TaskId { get; } = taskId;

    /// <summary>The shell to stop, under the older key the declaration marks as going; null when the call named the task.</summary>
    public string? ShellId { get; } = shellId;
}

/// <summary>What a TaskStop handed back.</summary>
/// <param name="message">What the binary said about the stop.</param>
/// <param name="taskId">The task that was stopped.</param>
/// <param name="taskType">What kind of task it was, by the binary's own word.</param>
/// <param name="command">The command the task was running.</param>
public sealed class TaskStopToolOutput(string message, string taskId, TaskType taskType, string? command = null) : ToolOutput
{
    /// <summary>What the binary said about the stop.</summary>
    public string Message { get; } = message;

    /// <summary>The task that was stopped.</summary>
    public string TaskId { get; } = taskId;

    /// <summary>What kind of task it was, by the binary's own word.</summary>
    public TaskType TaskType { get; } = taskType;

    /// <summary>The command the task was running; null when the binary named none.</summary>
    public string? Command { get; } = command;
}
