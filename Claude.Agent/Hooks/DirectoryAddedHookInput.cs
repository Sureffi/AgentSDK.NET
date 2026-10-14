namespace Claude.Agent;

/// <summary>A directory has come within reach of the tools.</summary>
public sealed class DirectoryAddedHookInput : HookInput
{
    internal DirectoryAddedHookInput(HookInputCommon common,
        string directory,
        DirectoryAddedSource source)
        : base(HookEvent.DirectoryAdded, common)
    {
        Directory = directory;
        Source = source;
    }

    /// <summary>The directory.</summary>
    public string Directory { get; }

    /// <summary>What brought it in.</summary>
    public DirectoryAddedSource Source { get; }
}
