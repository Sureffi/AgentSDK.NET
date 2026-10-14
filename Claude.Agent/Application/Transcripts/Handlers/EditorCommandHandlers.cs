using System.Text;
using System.Text.Json.Nodes;

namespace Claude.Agent;

internal static class RenameSessionHandler
{
    public static ValueTask<Result<Unit, Error>> Handle(
        RenameSession command,
        TranscriptWorkspace workspace,
        CancellationToken ct)
    {
        if (TranscriptWorkspace.NotUuid(command.Session) is { } invalid)
        {
            return ValueTask.FromResult<Result<Unit, Error>>(invalid);
        }

        // The title as the binary's /rename takes a name, and refused, as it refuses one, when nothing
        // is left: an empty title is no way to clear one.
        var title = command.Title is null ? null : UnicodeText.Title(command.Title);
        if (string.IsNullOrEmpty(title))
        {
            return ValueTask.FromResult<Result<Unit, Error>>(
                new ValidationError("A session title must not be empty.", nameof(RenameSession.Title)));
        }

        return MetadataLine.AppendAsync(
            workspace,
            command.Session,
            command.Cwd,
            new JsonObject
            {
                ["type"] = "custom-title",
                ["customTitle"] = title,
                ["sessionId"] = command.Session,
            },
            ct);
    }
}

internal static class TagSessionHandler
{
    public static ValueTask<Result<Unit, Error>> Handle(
        TagSession command,
        TranscriptWorkspace workspace,
        CancellationToken ct)
    {
        if (TranscriptWorkspace.NotUuid(command.Session) is { } invalid)
        {
            return ValueTask.FromResult<Result<Unit, Error>>(invalid);
        }

        var tag = command.Tag;
        if (tag is not null)
        {
            // Sanitise, then trim: a tag of nothing but invisible characters and whitespace comes
            // to empty and is refused, where trimming first could leave whitespace behind.
            tag = UnicodeText.Trim(UnicodeText.Sanitize(tag));
            if (tag.Length == 0)
            {
                return ValueTask.FromResult<Result<Unit, Error>>(
                    new ValidationError("A tag must not be empty; pass null to clear it.", nameof(TagSession.Tag)));
            }
        }

        return MetadataLine.AppendAsync(
            workspace,
            command.Session,
            command.Cwd,
            new JsonObject
            {
                ["type"] = "tag",
                ["tag"] = tag ?? "",
                ["sessionId"] = command.Session,
            },
            ct);
    }
}

internal static class DeleteSessionHandler
{
    public static ValueTask<Result<Unit, Error>> Handle(
        DeleteSession command,
        TranscriptWorkspace workspace,
        CancellationToken ct)
    {
        if (TranscriptWorkspace.NotUuid(command.Session) is { } invalid)
        {
            return ValueTask.FromResult<Result<Unit, Error>>(invalid);
        }

        var candidates = workspace.Directories(command.Session, command.Cwd);
        if (!candidates.TryGet(out var directories, out var missing))
        {
            return ValueTask.FromResult<Result<Unit, Error>>(missing);
        }

        // A delete of a session that is not there is an error, not silence.
        foreach (var directory in directories)
        {
            ct.ThrowIfCancellationRequested();
            if (TranscriptWorkspace.Transcript(directory, command.Session) is null)
            {
                continue;
            }

            TranscriptDirectory.Delete(directory, command.Session);
            return ValueTask.FromResult<Result<Unit, Error>>(Unit.Value);
        }

        return ValueTask.FromResult<Result<Unit, Error>>(TranscriptWorkspace.NotFound(command.Session, command.Cwd));
    }
}

// Rename and tag differ in three keys and nothing else.
internal static class MetadataLine
{
    public static ValueTask<Result<Unit, Error>> AppendAsync(
        TranscriptWorkspace workspace,
        string session,
        string? cwd,
        JsonObject json,
        CancellationToken ct)
    {
        var candidates = workspace.Directories(session, cwd);
        if (!candidates.TryGet(out var directories, out var missing))
        {
            return ValueTask.FromResult<Result<Unit, Error>>(missing);
        }

        var line = Encoding.UTF8.GetBytes(new SessionEntry(json).ToCompactJson() + "\n");

        // No existence check: the append is tried at each candidate and the open is what says
        // whether the session is here. Anything but "not here" — a full disk, a refused write —
        // throws where it happened.
        foreach (var directory in directories)
        {
            ct.ThrowIfCancellationRequested();
            if (TranscriptDirectory.Append(directory, session, line))
            {
                return ValueTask.FromResult<Result<Unit, Error>>(Unit.Value);
            }
        }

        return ValueTask.FromResult<Result<Unit, Error>>(TranscriptWorkspace.NotFound(session, cwd));
    }
}
