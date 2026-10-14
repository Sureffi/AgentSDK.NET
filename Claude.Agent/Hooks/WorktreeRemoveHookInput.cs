namespace Claude.Agent;

/// <summary>A worktree is being taken away.</summary>
public sealed class WorktreeRemoveHookInput : HookInput
{
    internal WorktreeRemoveHookInput(HookInputCommon common,
        string worktreePath)
        : base(HookEvent.WorktreeRemove, common)
    {
        WorktreePath = worktreePath;
    }

    /// <summary>Where it was.</summary>
    public string WorktreePath { get; }
}
