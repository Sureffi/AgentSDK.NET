namespace Claude.Agent;

/// <summary>What the model asked the TaskList tool for. The tool takes nothing: it lists the session's whole task list.</summary>
public sealed class TaskListToolInput : ToolInput
{
}

/// <summary>A task as TaskList names it.</summary>
/// <param name="id">The identifier the other task tools take.</param>
/// <param name="subject">The task's title.</param>
/// <param name="status">Where the task stands.</param>
/// <param name="blockedBy">The tasks holding this one up.</param>
/// <param name="owner">Who owns the task; null when nobody does.</param>
public sealed class TaskListToolTask(
    string id,
    string subject,
    TaskItemStatus status,
    IReadOnlyList<string> blockedBy,
    string? owner = null)
{
    /// <summary>The identifier the other task tools take.</summary>
    public string Id { get; } = id;

    /// <summary>The task's title.</summary>
    public string Subject { get; } = subject;

    /// <summary>Where the task stands.</summary>
    public TaskItemStatus Status { get; } = status;

    /// <summary>The tasks holding this one up.</summary>
    public IReadOnlyList<string> BlockedBy { get; } = blockedBy;

    /// <summary>Who owns the task; null when nobody does.</summary>
    public string? Owner { get; } = owner;
}

/// <summary>What a TaskList handed back.</summary>
/// <param name="tasks">The tasks on the list.</param>
public sealed class TaskListToolOutput(IReadOnlyList<TaskListToolTask> tasks) : ToolOutput
{
    /// <summary>The tasks on the list.</summary>
    public IReadOnlyList<TaskListToolTask> Tasks { get; } = tasks;
}
