namespace Claude.Agent;

/// <summary>What the model asked the TaskGet tool to read.</summary>
/// <param name="taskId">The task to read.</param>
public sealed class TaskGetToolInput(string taskId) : ToolInput
{
    /// <summary>The task to read.</summary>
    public string TaskId { get; } = taskId;
}

/// <summary>A task as TaskGet reads it, with what it blocks and what blocks it.</summary>
/// <param name="id">The identifier the other task tools take.</param>
/// <param name="subject">The task's title.</param>
/// <param name="description">What has to be done.</param>
/// <param name="status">Where the task stands.</param>
/// <param name="blocks">The tasks this one holds up.</param>
/// <param name="blockedBy">The tasks holding this one up.</param>
public sealed class TaskGetToolTask(
    string id,
    string subject,
    string description,
    TaskItemStatus status,
    IReadOnlyList<string> blocks,
    IReadOnlyList<string> blockedBy)
{
    /// <summary>The identifier the other task tools take.</summary>
    public string Id { get; } = id;

    /// <summary>The task's title.</summary>
    public string Subject { get; } = subject;

    /// <summary>What has to be done.</summary>
    public string Description { get; } = description;

    /// <summary>Where the task stands.</summary>
    public TaskItemStatus Status { get; } = status;

    /// <summary>The tasks this one holds up.</summary>
    public IReadOnlyList<string> Blocks { get; } = blocks;

    /// <summary>The tasks holding this one up.</summary>
    public IReadOnlyList<string> BlockedBy { get; } = blockedBy;
}

/// <summary>What a TaskGet handed back.</summary>
/// <param name="task">The task that was asked for; null when the list has no such task.</param>
public sealed class TaskGetToolOutput(TaskGetToolTask? task = null) : ToolOutput
{
    /// <summary>The task that was asked for; null when the list has no such task.</summary>
    public TaskGetToolTask? Task { get; } = task;
}
