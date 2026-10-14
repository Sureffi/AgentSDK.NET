namespace Claude.Agent;

/// <summary>Directories brought within reach of the tools.</summary>
public sealed class AddDirectoriesPermissionUpdate : PermissionUpdate
{
    internal AddDirectoriesPermissionUpdate(IReadOnlyList<string> directories, PermissionUpdateDestination destination)
        : base(destination)
    {
        Directories = directories;
    }

    /// <summary>The directories.</summary>
    public IReadOnlyList<string> Directories { get; }
}
