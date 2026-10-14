namespace Claude.Agent;

/// <summary>A session has ended.</summary>
public sealed class SessionEndHookInput : HookInput
{
    internal SessionEndHookInput(HookInputCommon common,
        SessionEndReason reason)
        : base(HookEvent.SessionEnd, common)
    {
        Reason = reason;
    }

    /// <summary>Why it ended.</summary>
    public SessionEndReason Reason { get; }
}
