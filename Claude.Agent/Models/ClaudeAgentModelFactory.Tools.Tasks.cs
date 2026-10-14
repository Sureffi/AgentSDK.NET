namespace Claude.Agent;

public static partial class ClaudeAgentModelFactory
{
    /// <summary>Builds a <see cref="Claude.Agent.TaskCreateToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskCreateToolInput TaskCreateToolInput(
        string subject = "",
        string description = "",
        string? activeForm = null) =>
        new(subject, description, activeForm);

    /// <summary>Builds a <see cref="Claude.Agent.TaskCreateToolTask"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskCreateToolTask TaskCreateToolTask(string id = "", string subject = "") =>
        new(id, subject);

    /// <summary>Builds a <see cref="Claude.Agent.TaskCreateToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskCreateToolOutput TaskCreateToolOutput(TaskCreateToolTask? task = null) =>
        new(task ?? TaskCreateToolTask());

    /// <summary>Builds a <see cref="Claude.Agent.TaskGetToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskGetToolInput TaskGetToolInput(string taskId = "") =>
        new(taskId);

    /// <summary>Builds a <see cref="Claude.Agent.TaskGetToolTask"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskGetToolTask TaskGetToolTask(
        string id = "",
        string subject = "",
        string description = "",
        TaskItemStatus status = default,
        IEnumerable<string>? blocks = null,
        IEnumerable<string>? blockedBy = null) =>
        new(id, subject, description, status, [.. blocks ?? []], [.. blockedBy ?? []]);

    /// <summary>Builds a <see cref="Claude.Agent.TaskGetToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskGetToolOutput TaskGetToolOutput(TaskGetToolTask? task = null) =>
        new(task);

    /// <summary>Builds a <see cref="Claude.Agent.TaskUpdateToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskUpdateToolInput TaskUpdateToolInput(
        string taskId = "",
        string? subject = null,
        string? description = null,
        string? activeForm = null,
        TaskUpdateStatus? status = null,
        IEnumerable<string>? addBlocks = null,
        IEnumerable<string>? addBlockedBy = null,
        string? owner = null) =>
        new(taskId, subject, description, activeForm, status, [.. addBlocks ?? []], [.. addBlockedBy ?? []], owner);

    /// <summary>Builds a <see cref="Claude.Agent.TaskUpdateToolStatusChange"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskUpdateToolStatusChange TaskUpdateToolStatusChange(string from = "", string to = "") =>
        new(from, to);

    /// <summary>Builds a <see cref="Claude.Agent.TaskUpdateToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskUpdateToolOutput TaskUpdateToolOutput(
        bool success = false,
        string taskId = "",
        IEnumerable<string>? updatedFields = null,
        string? error = null,
        TaskUpdateToolStatusChange? statusChange = null) =>
        new(success, taskId, [.. updatedFields ?? []], error, statusChange);

    /// <summary>Builds a <see cref="Claude.Agent.TaskListToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskListToolInput TaskListToolInput() =>
        new();

    /// <summary>Builds a <see cref="Claude.Agent.TaskListToolTask"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskListToolTask TaskListToolTask(
        string id = "",
        string subject = "",
        TaskItemStatus status = default,
        IEnumerable<string>? blockedBy = null,
        string? owner = null) =>
        new(id, subject, status, [.. blockedBy ?? []], owner);

    /// <summary>Builds a <see cref="Claude.Agent.TaskListToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static TaskListToolOutput TaskListToolOutput(IEnumerable<TaskListToolTask>? tasks = null) =>
        new([.. tasks ?? []]);
}
