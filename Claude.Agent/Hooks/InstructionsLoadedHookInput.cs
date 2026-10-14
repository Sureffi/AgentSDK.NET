namespace Claude.Agent;

/// <summary>An instructions file has been read into the context.</summary>
public sealed class InstructionsLoadedHookInput : HookInput
{
    internal InstructionsLoadedHookInput(HookInputCommon common,
        string filePath,
        InstructionsMemoryType memoryType,
        InstructionsLoadReason loadReason,
        IReadOnlyList<string> globs,
        string? triggerFilePath,
        string? parentFilePath)
        : base(HookEvent.InstructionsLoaded, common)
    {
        FilePath = filePath;
        MemoryType = memoryType;
        LoadReason = loadReason;
        Globs = globs;
        TriggerFilePath = triggerFilePath;
        ParentFilePath = parentFilePath;
    }

    /// <summary>The file.</summary>
    public string FilePath { get; }

    /// <summary>Which memory it belongs to.</summary>
    public InstructionsMemoryType MemoryType { get; }

    /// <summary>Why it was read.</summary>
    public InstructionsLoadReason LoadReason { get; }

    /// <summary>The patterns that decide which files it applies to; empty when it applies everywhere.</summary>
    public IReadOnlyList<string> Globs { get; }

    /// <summary>The file whose being touched brought it in; null when nothing did.</summary>
    public string? TriggerFilePath { get; }

    /// <summary>The instructions file that pulled it in; null when none did.</summary>
    public string? ParentFilePath { get; }
}
