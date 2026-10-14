namespace Claude.Agent;

/// <summary>What the model asked the TaskCreate tool to put on the session's task list.</summary>
/// <param name="subject">A short title for the task.</param>
/// <param name="description">What has to be done.</param>
/// <param name="activeForm">The present-continuous form shown while the task runs; null when the call named none.</param>
public sealed class TaskCreateToolInput(
    string subject,
    string description,
    string? activeForm = null) : ToolInput
{
    /// <summary>A short title for the task.</summary>
    public string Subject { get; } = subject;

    /// <summary>What has to be done.</summary>
    public string Description { get; } = description;

    /// <summary>The present-continuous form shown while the task runs; null when the call named none.</summary>
    public string? ActiveForm { get; } = activeForm;
}

/// <summary>The task a TaskCreate put on the list.</summary>
/// <param name="id">The identifier the other task tools take.</param>
/// <param name="subject">The task's title.</param>
public sealed class TaskCreateToolTask(string id, string subject)
{
    /// <summary>The identifier the other task tools take.</summary>
    public string Id { get; } = id;

    /// <summary>The task's title.</summary>
    public string Subject { get; } = subject;
}

/// <summary>What a TaskCreate handed back.</summary>
/// <param name="task">The task that was created.</param>
public sealed class TaskCreateToolOutput(TaskCreateToolTask task) : ToolOutput
{
    /// <summary>The task that was created.</summary>
    public TaskCreateToolTask Task { get; } = task;
}
