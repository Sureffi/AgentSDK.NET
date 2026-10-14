using System.Collections.Immutable;

namespace Claude.Agent;

// One session copied from the user's own configuration directory into the client's, into the
// project directory of the same name, byte for byte: its transcript, and with the subagents asked
// for, every file under its subagents/ directory. The user's directory is only read. A session any
// of whose files is already in the client's directory is refused and nothing is written; a copy that
// fails or is cancelled part-way removes every file it made, the one it was writing included, and
// every directory it made that nothing else has been put in since.
internal static class ImportSessionHandler
{
    private const string Subagents = "subagents";

    public static ValueTask<Result<Unit, Error>> Handle(
        ImportSession command,
        TranscriptWorkspace source,
        TranscriptDirectory target,
        CancellationToken ct) =>
        Handle(command, source, target, OpenSource, ct);

    // `open` opens one of the user's files for reading; a test hands in one that fails part-way.
    internal static async ValueTask<Result<Unit, Error>> Handle(
        ImportSession command,
        TranscriptWorkspace source,
        TranscriptDirectory target,
        Func<string, Stream> open,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(open);

        if (TranscriptWorkspace.NotUuid(command.Session) is { } invalid)
        {
            return invalid;
        }

        var candidates = source.Directories(command.Session, command.Cwd);
        if (!candidates.TryGet(out var directories, out var missing))
        {
            return missing;
        }

        foreach (var directory in directories)
        {
            ct.ThrowIfCancellationRequested();
            if (TranscriptWorkspace.Transcript(directory, command.Session) is { } transcript)
            {
                return await CopyAsync(command, directory, transcript, target, open, ct).ConfigureAwait(false);
            }
        }

        return TranscriptWorkspace.NotFound(command.Session, command.Cwd);
    }

    private static async ValueTask<Result<Unit, Error>> CopyAsync(
        ImportSession command,
        string directory,
        string transcript,
        TranscriptDirectory target,
        Func<string, Stream> open,
        CancellationToken ct)
    {
        var project = Path.Combine(target.Layout.ProjectsDirectory, TranscriptWorkspace.KeyOf(directory));
        var files = ImmutableArray.CreateBuilder<(string From, string To)>();
        files.Add((transcript, Path.Combine(project, Path.GetFileName(transcript))));
        if (command.IncludeSubagents)
        {
            var subagents = Path.Combine(directory, command.Session, Subagents);
            foreach (var file in Collect(subagents))
            {
                files.Add((file, Path.Combine(project, Path.GetRelativePath(directory, file))));
            }
        }

        foreach (var (_, to) in files)
        {
            if (File.Exists(to) || Directory.Exists(to))
            {
                return new SessionExistsError(
                    $"Session {command.Session} is already in {target.Layout.ProjectsDirectory}: {to} is there.",
                    command.Session);
            }
        }

        var configuration = Path.GetDirectoryName(target.Layout.ProjectsDirectory)!;
        var madeFiles = new List<string>();
        var madeDirectories = new List<string>();
        try
        {
            // The subagents first and the transcript last: a session whose transcript is there is
            // whole.
            foreach (var (from, to) in files.Skip(1).Append(files[0]))
            {
                ct.ThrowIfCancellationRequested();
                CreateDirectory(configuration, Path.GetDirectoryName(to)!, madeDirectories);
                await CopyFileAsync(from, to, open, madeFiles, ct).ConfigureAwait(false);
            }
        }
        catch
        {
            Remove(madeFiles, madeDirectories);
            throw;
        }

        return Unit.Value;
    }

    // What a copy that failed made: its files, then its directories deepest first. One that cannot be
    // removed, or a directory that holds something else by now, is left; the copy's own failure is the
    // one reported.
    private static void Remove(List<string> files, List<string> directories)
    {
        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Left.
            }
        }

        for (var i = directories.Count - 1; i >= 0; i--)
        {
            try
            {
                Directory.Delete(directories[i], recursive: false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Left.
            }
        }
    }

    // CreateNew: a file that appeared since the check is not written over, and is not the copy's to
    // remove. The target is in `made` from the moment it is created, before a byte is copied into it.
    private static async Task CopyFileAsync(
        string from,
        string to,
        Func<string, Stream> open,
        List<string> made,
        CancellationToken ct)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        var reader = open(from);
        await using (reader.ConfigureAwait(false))
        {
            var writer = new FileStream(to, options);
            made.Add(to);
            await using (writer.ConfigureAwait(false))
            {
                await reader.CopyToAsync(writer, ct).ConfigureAwait(false);
            }
        }
    }

    private static FileStream OpenSource(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

    // Every directory down to `path` that is not there, made one at a time and added to `made` in that
    // order: from the configuration directory down readable by its owner alone, as the client makes the
    // configuration directory, and the ones above it as the platform makes them.
    private static void CreateDirectory(string configuration, string path, List<string> made)
    {
        var missing = new Stack<string>();
        var absent = path;
        while (absent is not null && !Directory.Exists(absent))
        {
            missing.Push(absent);
            absent = Path.GetDirectoryName(absent);
        }

        while (missing.TryPop(out var directory))
        {
            if (OperatingSystem.IsWindows() || !Within(configuration, directory))
            {
                Directory.CreateDirectory(directory);
            }
            else
            {
                Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            made.Add(directory);
        }
    }

    // Whether `directory` is the configuration directory or under it.
    private static bool Within(string configuration, string directory)
    {
        var relative = Path.GetRelativePath(configuration, directory);
        return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    // Every file under the directory, each directory in ordinal order; none when it is not there.
    private static ImmutableArray<string> Collect(string directory)
    {
        var files = ImmutableArray.CreateBuilder<string>();
        Walk(directory, files);
        return files.ToImmutable();
    }

    private static void Walk(string directory, ImmutableArray<string>.Builder files)
    {
        string[] entries;
        try
        {
            entries = Directory.GetFileSystemEntries(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        Array.Sort(entries, StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (Directory.Exists(entry))
            {
                Walk(entry, files);
            }
            else
            {
                files.Add(entry);
            }
        }
    }
}
