namespace Claude.Agent;

/// <summary>The conversation is about to be compacted.</summary>
public sealed class PreCompactHookInput : HookInput
{
    internal PreCompactHookInput(HookInputCommon common,
        CompactTrigger trigger,
        string? customInstructions)
        : base(HookEvent.PreCompact, common)
    {
        Trigger = trigger;
        CustomInstructions = customInstructions;
    }

    /// <summary>What set the compaction going.</summary>
    public CompactTrigger Trigger { get; }

    /// <summary>What the person asked the compaction to keep; null when they asked for nothing.</summary>
    public string? CustomInstructions { get; }
}
