namespace Claude.Agent;

/// <summary>The permission mode set to another.</summary>
public sealed class SetModePermissionUpdate : PermissionUpdate
{
    internal SetModePermissionUpdate(PermissionMode mode, PermissionUpdateDestination destination)
        : base(destination)
    {
        Mode = mode;
    }

    /// <summary>The mode to run in.</summary>
    public PermissionMode Mode { get; }
}
