namespace Claude.Agent;

internal sealed record RewindVerdict(
    bool CanRewind,
    IReadOnlyList<string> FilesChanged,
    long? Insertions,
    long? Deletions,
    long? SkippedLinks,
    string? Error);
