namespace Claude.Agent;

/// <summary>A task has been finished.</summary>
public sealed class TaskCompletedHookInput : HookInput
{
    internal TaskCompletedHookInput(HookInputCommon common,
        string taskId,
        string taskSubject,
        string? taskDescription,
        string? teammateName,
        string? teamName)
        : base(HookEvent.TaskCompleted, common)
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

    /// <summary>The teammate it belonged to; null when it belonged to none.</summary>
    public string? TeammateName { get; }

    /// <summary>The team it belonged to; null when it belonged to none. The declaration marks this one on its way out.</summary>
    public string? TeamName { get; }
}
