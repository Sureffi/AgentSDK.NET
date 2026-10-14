namespace Claude.Agent;

/// <summary>A task has been made.</summary>
public sealed class TaskCreatedHookInput : HookInput
{
    internal TaskCreatedHookInput(HookInputCommon common,
        string taskId,
        string taskSubject,
        string? taskDescription,
        string? teammateName,
        string? teamName)
        : base(HookEvent.TaskCreated, common)
    {
        TaskId = taskId;
        TaskSubject = taskSubject;
        TaskDescription = taskDescription;
        TeammateName = teammateName;
        TeamName = teamName;
    }

    /// <summary>The task.</summary>
    public string TaskId { get; }

    /// <summary>Its one-line subject.</summary>
    public string TaskSubject { get; }

    /// <summary>What it says at length; null when it says nothing.</summary>
    public string? TaskDescription { get; }

    /// <summary>The teammate it belongs to; null when it belongs to none.</summary>
    public string? TeammateName { get; }

    /// <summary>The team it belongs to; null when it belongs to none. The declaration marks this one on its way out.</summary>
    public string? TeamName { get; }
}
