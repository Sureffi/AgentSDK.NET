namespace Claude.Agent;

/// <summary>The binary is setting up the working directory. The hook may add to the context.</summary>
public sealed class SetupHookInput : HookInput
{
    internal SetupHookInput(HookInputCommon common,
        SetupTrigger trigger)
        : base(HookEvent.Setup, common)
    {
        Trigger = trigger;
    }

    /// <summary>Why the setup is running.</summary>
    public SetupTrigger Trigger { get; }
}
