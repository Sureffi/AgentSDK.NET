namespace Claude.Agent;

/// <summary>A teammate has run out of work.</summary>
public sealed class TeammateIdleHookInput : HookInput
{
    internal TeammateIdleHookInput(HookInputCommon common,
        string teammateName,
        string teamName)
        : base(HookEvent.TeammateIdle, common)
    {
        TeammateName = teammateName;
        TeamName = teamName;
    }

    /// <summary>The teammate.</summary>
    public string TeammateName { get; }

    /// <summary>The team it belongs to. The declaration marks this one on its way out.</summary>
    public string TeamName { get; }
}
