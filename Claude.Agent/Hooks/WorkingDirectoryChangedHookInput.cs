namespace Claude.Agent;

/// <summary>The directory the binary runs in has changed. The hook may say which paths to watch from here.</summary>
public sealed class WorkingDirectoryChangedHookInput : HookInput
{
    internal WorkingDirectoryChangedHookInput(HookInputCommon common,
        string oldWorkingDirectory,
        string newWorkingDirectory)
        : base(HookEvent.WorkingDirectoryChanged, common)
    {
        OldWorkingDirectory = oldWorkingDirectory;
        NewWorkingDirectory = newWorkingDirectory;
    }

    /// <summary>Where it was.</summary>
    public string OldWorkingDirectory { get; }

    /// <summary>Where it is now.</summary>
    public string NewWorkingDirectory { get; }
}
