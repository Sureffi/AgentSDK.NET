namespace Claude.Agent;

/// <summary>A file the binary watches has changed. The hook may say which paths to watch from here.</summary>
public sealed class FileChangedHookInput : HookInput
{
    internal FileChangedHookInput(HookInputCommon common,
        string filePath,
        FileChangedEvent change)
        : base(HookEvent.FileChanged, common)
    {
        FilePath = filePath;
        Change = change;
    }

    /// <summary>The file.</summary>
    public string FilePath { get; }

    /// <summary>What happened to it.</summary>
    public FileChangedEvent Change { get; }
}
