using System.Collections.Immutable;

namespace Claude.Agent;

// Where the binary keeps a configuration directory's transcripts: projects/ under it, one directory a
// project.
internal sealed class TranscriptLayout : ITranscriptLayout
{
    private readonly string _configurationDirectory;
    private readonly Func<string, string?>? _git;

    // `git` runs `git worktree list --porcelain` in the canonical directory and returns its stdout, or
    // null for anything wrong — a non-zero exit, no git, a timeout. SubprocessCliTransport is the
    // only place a Process starts and SurfaceTests.OneTypeStartsAProcess holds that line, so the
    // library ships no git runner: without one the paths are read off the repository's own records
    // instead (WorktreePaths). The injected runner is for the tests that need a shape the filesystem
    // cannot give.
    public TranscriptLayout(string configurationDirectory, Func<string, string?>? git = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(configurationDirectory);
        _configurationDirectory = configurationDirectory;
        _git = git;
    }

    public string ProjectsDirectory => Path.Combine(TranscriptPaths.Normalize(_configurationDirectory), "projects");

    public string ProjectFor(string cwd) =>
        TranscriptPaths.Sanitize(TranscriptPaths.Canonicalize(cwd));

    public string? ProjectDirectory(string project) =>
        TranscriptPaths.FindProjectDirectory(ProjectsDirectory, project);

    public string CanonicalPath(string cwd) => TranscriptPaths.Canonicalize(cwd);

    public string? ProjectForSpelling(string path) =>
        !string.IsNullOrEmpty(path)
        && TranscriptPaths.Sanitize(TranscriptPaths.Normalize(path)) is { } project && Ids.IsKeyPart(project)
            ? project
            : null;

    // What `git worktree list --porcelain` would print, read off the repository's own records instead
    // of off git: the main worktree first, then every linked one in ordinal order. Anything unreadable
    // is no repository — an empty set, which is also what the single-directory listing takes.
    public ImmutableArray<string> WorktreePaths(string cwd)
    {
        var canonical = TranscriptPaths.Canonicalize(cwd);
        try
        {
            return _git is not null ? FromGit(_git.Invoke(canonical)) : FromRecords(canonical);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A host's injected runner is arbitrary code: whatever it throws is "no repository".
            return [];
        }
    }

    private static ImmutableArray<string> FromGit(string? output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return [];
        }

        var paths = ImmutableArray.CreateBuilder<string>();
        foreach (var line in output.Split('\n'))
        {
            if (line.StartsWith("worktree ", StringComparison.Ordinal))
            {
                paths.Add(TranscriptPaths.Normalize(line["worktree ".Length..]));
            }
        }

        return paths.ToImmutable();
    }

    private static ImmutableArray<string> FromRecords(string canonical)
    {
        if (CommonGitDirectory(canonical) is not { } common
            || Directory.GetParent(common)?.FullName is not { } main)
        {
            return [];
        }

        var paths = ImmutableArray.CreateBuilder<string>();
        paths.Add(TranscriptPaths.Normalize(main));

        var linked = Path.Combine(common, "worktrees");
        if (!Directory.Exists(linked))
        {
            return paths.ToImmutable();
        }

        foreach (var entry in Directory.GetDirectories(linked).OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            // <common>/worktrees/<name>/gitdir holds the path of that worktree's own .git file, so its
            // parent is the worktree.
            var record = Path.Combine(entry, "gitdir");
            if (!File.Exists(record))
            {
                continue;
            }

            var gitdir = File.ReadAllText(record).Trim();
            if (gitdir.Length != 0 && Directory.GetParent(gitdir)?.FullName is { } worktree)
            {
                paths.Add(TranscriptPaths.Normalize(worktree));
            }
        }

        return paths.ToImmutable();
    }

    // Walk up to the first .git entry. A directory is the common git dir itself; a file names a linked
    // worktree's git dir at <common>/worktrees/<name>, whose parent's parent is the common dir.
    private static string? CommonGitDirectory(string from)
    {
        for (var directory = from; directory is not null; directory = Directory.GetParent(directory)?.FullName)
        {
            var entry = Path.Combine(directory, ".git");
            if (Directory.Exists(entry))
            {
                return entry;
            }

            if (!File.Exists(entry))
            {
                continue;
            }

            foreach (var line in File.ReadAllText(entry).Split('\n'))
            {
                if (!line.StartsWith("gitdir:", StringComparison.Ordinal))
                {
                    continue;
                }

                var gitdir = line["gitdir:".Length..].Trim();
                if (gitdir.Length == 0)
                {
                    return null;
                }

                var resolved = Path.IsPathRooted(gitdir) ? gitdir : Path.Combine(directory, gitdir);
                return Directory.GetParent(resolved)?.Parent?.FullName;
            }

            return null;
        }

        return null;
    }
}
