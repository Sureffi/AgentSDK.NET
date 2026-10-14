namespace Claude.Agent;

/// <summary>What the model asked the TaskUpdate tool to change about a task.</summary>
/// <param name="taskId">The task to change.</param>
/// <param name="subject">The title to set; null when the call left it alone.</param>
/// <param name="description">The description to set; null when the call left it alone.</param>
/// <param name="activeForm">The present-continuous form to set; null when the call left it alone.</param>
/// <param name="status">The status to set; null when the call left it alone.</param>
/// <param name="addBlocks">Tasks this one is to hold up, added to the ones it already does.</param>
/// <param name="addBlockedBy">Tasks that are to hold this one up, added to the ones that already do.</param>
/// <param name="owner">The owner to set; null when the call left it alone.</param>
public sealed class TaskUpdateToolInput(
    string taskId,
    string? subject = null,
    string? description = null,
    string? activeForm = null,
    TaskUpdateStatus? status = null,
    IReadOnlyList<string>? addBlocks = null,
    IReadOnlyList<string>? addBlockedBy = null,
    string? owner = null) : ToolInput
{
    /// <summary>The task to change.</summary>
    public string TaskId { get; } = taskId;

    /// <summary>The title to set; null when the call left it alone.</summary>
    public string? Subject { get; } = subject;

    /// <summary>The description to set; null when the call left it alone.</summary>
    public string? Description { get; } = description;

    /// <summary>The present-continuous form to set; null when the call left it alone.</summary>
    public string? ActiveForm { get; } = activeForm;

    /// <summary>The status to set; null when the call left it alone.</summary>
    public TaskUpdateStatus? Status { get; } = status;

    /// <summary>Tasks this one is to hold up, added to the ones it already does.</summary>
    public IReadOnlyList<string> AddBlocks { get; } = addBlocks ?? [];

    /// <summary>Tasks that are to hold this one up, added to the ones that already do.</summary>
    public IReadOnlyList<string> AddBlockedBy { get; } = addBlockedBy ?? [];

    /// <summary>The owner to set; null when the call left it alone.</summary>
    public string? Owner { get; } = owner;
}

/// <summary>The move a TaskUpdate made from one status to another. The binary writes both as plain names, not as the set the input takes.</summary>
/// <param name="from">The status the task stood in.</param>
/// <param name="to">The status it stands in now.</param>
public sealed class TaskUpdateToolStatusChange(string from, string to)
{
    /// <summary>The status the task stood in.</summary>
    public string From { get; } = from;

    /// <summary>The status it stands in now.</summary>
    public string To { get; } = to;
}

/// <summary>What a TaskUpdate handed back.</summary>
/// <param name="success">Whether the change went through.</param>
/// <param name="taskId">The task that was addressed.</param>
/// <param name="updatedFields">The names of what changed.</param>
/// <param name="error">Why the change did not go through; null when it did.</param>
/// <param name="statusChange">The move the status made; null when the status did not change.</param>
public sealed class TaskUpdateToolOutput(
    bool success,
    string taskId,
    IReadOnlyList<string> updatedFields,
    string? error = null,
    TaskUpdateToolStatusChange? statusChange = null) : ToolOutput
{
    /// <summary>Whether the change went through.</summary>
    public bool Success { get; } = success;

    /// <summary>The task that was addressed.</summary>
    public string TaskId { get; } = taskId;

    /// <summary>The names of what changed.</summary>
    public IReadOnlyList<string> UpdatedFields { get; } = updatedFields;

    /// <summary>Why the change did not go through; null when it did.</summary>
    public string? Error { get; } = error;

    /// <summary>The move the status made; null when the status did not change.</summary>
    public TaskUpdateToolStatusChange? StatusChange { get; } = statusChange;
}
