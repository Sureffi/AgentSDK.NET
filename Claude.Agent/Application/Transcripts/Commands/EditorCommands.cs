
namespace Claude.Agent;

internal sealed record RenameSession(string Session, string Title, string? Cwd);

// A null tag clears: the line carries the empty string, which the reader takes as no tag.
internal sealed record TagSession(string Session, string? Tag, string? Cwd);

internal sealed record DeleteSession(string Session, string? Cwd);

internal sealed record ForkSessionAt(string Session, string? UpTo, string? Title, string? Cwd);

internal sealed record ImportSession(string Session, string? Cwd, bool IncludeSubagents);
