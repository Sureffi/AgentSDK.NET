namespace Claude.Agent;

/// <summary>A subagent has started. Which one is on the base's agent members, which this event always carries.</summary>
public sealed class SubagentStartHookInput : HookInput
{
    internal SubagentStartHookInput(HookInputCommon common)
        : base(HookEvent.SubagentStart, common)
    {
    }
}
