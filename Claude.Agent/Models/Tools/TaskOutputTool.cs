namespace Claude.Agent;

/// <summary>What the model asked the TaskOutput tool to fetch.</summary>
/// <param name="taskId">The task to read.</param>
/// <param name="block">Whether to wait for the task to have something to say.</param>
/// <param name="timeout">How long to wait for it; the wire carries it in milliseconds.</param>
public sealed class TaskOutputToolInput(string taskId, bool block, TimeSpan timeout) : ToolInput
{
    /// <summary>The task to read.</summary>
    public string TaskId { get; } = taskId;

    /// <summary>Whether to wait for the task to have something to say.</summary>
    public bool Block { get; } = block;

    /// <summary>How long to wait for it; the wire carries it in milliseconds.</summary>
    public TimeSpan Timeout { get; } = timeout;
}

/// <summary>
/// The task a TaskOutput reported on. The declaration types no output for that tool, so this shape
/// stands on the recordings alone.
/// </summary>
/// <param name="taskId">The task's id.</param>
/// <param name="taskType">What kind of task it is, by the binary's own word.</param>
/// <param name="status">Where the task stands, by the binary's own word.</param>
/// <param name="description">What the task is, in the model's words.</param>
/// <param name="output">What the task has written so far.</param>
/// <param name="exitCode">The code the task ended with; null while it is still running.</param>
public sealed class TaskOutputToolTask(
    string taskId,
    TaskType taskType,
    string status,
    string description,
    string output,
    long? exitCode = null)
{
    /// <summary>The task's id.</summary>
    public string TaskId { get; } = taskId;

    /// <summary>What kind of task it is, by the binary's own word.</summary>
    public TaskType TaskType { get; } = taskType;

    /// <summary>Where the task stands, by the binary's own word.</summary>
    public string Status { get; } = status;

    /// <summary>What the task is, in the model's words.</summary>
    public string Description { get; } = description;

    /// <summary>What the task has written so far; empty when it has written nothing.</summary>
    public string Output { get; } = output;

    /// <summary>The code the task ended with; null while it is still running.</summary>
    public long? ExitCode { get; } = exitCode;
}

/// <summary>
/// What a TaskOutput handed back. The declaration types no output for that tool, so this shape
/// stands on the recordings alone.
/// </summary>
/// <param name="retrievalStatus">Whether the task had anything to hand back.</param>
/// <param name="task">The task itself.</param>
public sealed class TaskOutputToolOutput(TaskRetrievalStatus retrievalStatus, TaskOutputToolTask task) : ToolOutput
{
    /// <summary>Whether the task had anything to hand back.</summary>
    public TaskRetrievalStatus RetrievalStatus { get; } = retrievalStatus;

    /// <summary>The task itself.</summary>
    public TaskOutputToolTask Task { get; } = task;
}
