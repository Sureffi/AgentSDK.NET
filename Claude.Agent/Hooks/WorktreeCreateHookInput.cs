namespace Claude.Agent;

/// <summary>A worktree is being made. The hook may say where it goes.</summary>
public sealed class WorktreeCreateHookInput : HookInput
{
    internal WorktreeCreateHookInput(HookInputCommon common,
        string name)
        : base(HookEvent.WorktreeCreate, common)
    {
        Name = name;
    }

    /// <summary>The worktree's name.</summary>
    public string Name { get; }
}
