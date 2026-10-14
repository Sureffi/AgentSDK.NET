namespace Claude.Agent;

/// <summary>A subagent has finished its turn. Which one is on the base's agent members, which this event always carries.</summary>
public sealed class SubagentStopHookInput : HookInput
{
    internal SubagentStopHookInput(HookInputCommon common,
        bool isStopHookActive,
        string agentTranscriptPath,
        string? lastAssistantMessage,
        IReadOnlyList<StopHookBackgroundTask> backgroundTasks,
        IReadOnlyList<SessionCron> sessionCrons)
        : base(HookEvent.SubagentStop, common)
    {
        IsStopHookActive = isStopHookActive;
        AgentTranscriptPath = agentTranscriptPath;
        LastAssistantMessage = lastAssistantMessage;
        BackgroundTasks = backgroundTasks;
        SessionCrons = sessionCrons;
    }

    /// <summary>Whether a stop hook already sent the subagent back for this turn, so that a hook can refuse to do it twice.</summary>
    public bool IsStopHookActive { get; }

    /// <summary>The subagent's own transcript file.</summary>
    public string AgentTranscriptPath { get; }

    /// <summary>The text of the last thing the subagent said; null when the binary sent none.</summary>
    public string? LastAssistantMessage { get; }

    /// <summary>What is still running in the background; empty when nothing is.</summary>
    public IReadOnlyList<StopHookBackgroundTask> BackgroundTasks { get; }

    /// <summary>The schedules this session holds; empty when it holds none.</summary>
    public IReadOnlyList<SessionCron> SessionCrons { get; }
}
