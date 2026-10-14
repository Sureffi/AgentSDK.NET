namespace Claude.Agent;

/// <summary>The main thread has finished its turn. The hook may add to the context or send the model back to work.</summary>
public sealed class StopHookInput : HookInput
{
    internal StopHookInput(HookInputCommon common,
        bool isStopHookActive,
        string? lastAssistantMessage,
        IReadOnlyList<StopHookBackgroundTask> backgroundTasks,
        IReadOnlyList<SessionCron> sessionCrons)
        : base(HookEvent.Stop, common)
    {
        IsStopHookActive = isStopHookActive;
        LastAssistantMessage = lastAssistantMessage;
        BackgroundTasks = backgroundTasks;
        SessionCrons = sessionCrons;
    }

    /// <summary>Whether a stop hook already sent the model back for this turn, so that a hook can refuse to do it twice.</summary>
    public bool IsStopHookActive { get; }

    /// <summary>The text of the last thing the model said; null when the binary sent none.</summary>
    public string? LastAssistantMessage { get; }

    /// <summary>What is still running in the background; empty when nothing is.</summary>
    public IReadOnlyList<StopHookBackgroundTask> BackgroundTasks { get; }

    /// <summary>The schedules this session holds; empty when it holds none.</summary>
    public IReadOnlyList<SessionCron> SessionCrons { get; }
}
