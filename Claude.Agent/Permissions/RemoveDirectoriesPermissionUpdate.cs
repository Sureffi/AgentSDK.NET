namespace Claude.Agent;

/// <summary>Directories taken back out of reach of the tools.</summary>
public sealed class RemoveDirectoriesPermissionUpdate : PermissionUpdate
{
    internal RemoveDirectoriesPermissionUpdate(IReadOnlyList<string> directories, PermissionUpdateDestination destination)
        : base(destination)
    {
        Directories = directories;
    }

    /// <summary>The directories.</summary>
    public IReadOnlyList<string> Directories { get; }
}
