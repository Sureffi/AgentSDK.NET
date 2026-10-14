namespace Claude.Agent;

/// <summary>The conversation has been compacted.</summary>
public sealed class PostCompactHookInput : HookInput
{
    internal PostCompactHookInput(HookInputCommon common,
        CompactTrigger trigger,
        string compactSummary)
        : base(HookEvent.PostCompact, common)
    {
        Trigger = trigger;
        CompactSummary = compactSummary;
    }

    /// <summary>What set the compaction going.</summary>
    public CompactTrigger Trigger { get; }

    /// <summary>The summary the conversation was folded into.</summary>
    public string CompactSummary { get; }
}
