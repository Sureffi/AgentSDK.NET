namespace Claude.Agent;

/// <summary>A settings file the session reads has changed under it.</summary>
public sealed class ConfigChangeHookInput : HookInput
{
    internal ConfigChangeHookInput(HookInputCommon common,
        ConfigChangeSource source,
        string? filePath)
        : base(HookEvent.ConfigChange, common)
    {
        Source = source;
        FilePath = filePath;
    }

    /// <summary>Which configuration changed.</summary>
    public ConfigChangeSource Source { get; }

    /// <summary>The file that changed; null when the binary did not name one.</summary>
    public string? FilePath { get; }
}
