using System.Collections.Immutable;
using System.Text;

namespace Claude.Agent;

internal static class ForkSessionHandler
{
    public static async ValueTask<Result<string, Error>> Handle(
        ForkSessionAt command,
        TranscriptWorkspace workspace,
        CancellationToken ct)
    {
        if (TranscriptWorkspace.NotUuid(command.Session) is { } invalid)
        {
            return invalid;
        }

        // Both ids are checked before the source is located, so a bad cutoff fails the same way
        // whether or not the session is here.
        if (command.UpTo is { } upTo && TranscriptWorkspace.NotUuidMessage(upTo) is { } cutoff)
        {
            return cutoff;
        }

        var forked = Guid.NewGuid().ToString("D");

        var candidates = workspace.Directories(command.Session, command.Cwd);
        if (!candidates.TryGet(out var directories, out var missing))
        {
            return missing is SessionNotFoundError { Scope: var scope }
                ? NothingToFork(command.Session, scope, command.Cwd)
                : missing;
        }

        foreach (var directory in directories)
        {
            ct.ThrowIfCancellationRequested();
            if (TranscriptWorkspace.Transcript(directory, command.Session) is { } path)
            {
                return await ForkAsync(command, workspace, directory, path, forked, ct).ConfigureAwait(false);
            }
        }

        return NothingToFork(
            command.Session,
            command.Cwd is null ? SessionSearchScope.AnyProjectDirectory : SessionSearchScope.ProjectDirectory,
            command.Cwd);
    }

    // The fork is written beside the session it copies, in the same project directory.
    private static async ValueTask<Result<string, Error>> ForkAsync(
        ForkSessionAt command,
        TranscriptWorkspace workspace,
        string directory,
        string path,
        string forked,
        CancellationToken ct)
    {
        var content = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
        var entries = TranscriptScan.Entries(Encoding.UTF8.GetString(content));

        var sliced = SessionFork.Slice(command.Session, TranscriptChain.Resolve(entries), command.UpTo);
        if (!sliced.TryGet(out var kept, out var refused))
        {
            return refused;
        }

        var title = Title(command.Title, kept, workspace, TranscriptWorkspace.KeyOf(directory), ct);

        var text = new StringBuilder();
        var lines = SessionFork.Copy(command.Session, kept, forked)
            .AddRange(SessionFork.Trailer(entries, command.Session, title, forked));
        foreach (var line in lines)
        {
            text.Append(line.ToCompactJson()).Append('\n');
        }

        var target = Path.Combine(directory, forked + TranscriptWorkspace.Extension);
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        try
        {
            var stream = new FileStream(target, options);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(text.ToString()), ct).ConfigureAwait(false);
            }
        }
        catch (IOException) when (File.Exists(target))
        {
            // O_EXCL: a target that is already there is a hard failure.
            return new ForkError($"A transcript for session {forked} is already at {target}.");
        }

        return forked;
    }

    // The fork's title: the one given, or one built on the fork's first prompt and set apart from the
    // titles the sessions of the project directory the fork is written to carry.
    private static string Title(
        string? given,
        ImmutableArray<SessionEntry> kept,
        TranscriptWorkspace workspace,
        string project,
        CancellationToken ct)
    {
        if (SessionFork.GivenTitle(given) is { } title)
        {
            return title;
        }

        var titles = new SessionReader(workspace.Transcripts)
            .ListProject(project, ct)
            .Select(info => (info.CustomTitle, info.RenamedTitle));
        return SessionFork.BranchTitle(SessionFork.BaseTitle(kept), titles);
    }

    private static SessionNotFoundError NothingToFork(string session, SessionSearchScope scope, string? cwd) =>
        new(
            scope switch
            {
                SessionSearchScope.ProjectDirectory => $"There is no session {session} to fork in the project directory for {cwd}.",
                SessionSearchScope.NoProjectsDirectory => $"There is no session {session} to fork: there is no projects directory.",
                _ => $"There is no session {session} to fork in any project directory.",
            },
            session,
            scope);
}
