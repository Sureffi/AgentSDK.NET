using System.Collections.Immutable;

namespace Claude.Agent;

// What the editor handlers share: the configuration directory's transcripts, and the candidate walk
// every one of them searches along.
internal sealed class TranscriptWorkspace(TranscriptDirectory transcripts)
{
    public const string Extension = TranscriptDirectory.Extension;

    public TranscriptDirectory Transcripts => transcripts;

    public ITranscriptLayout Layout => transcripts.Layout;

    // A transcript that is there and is not a zero-byte stub, which is the "not here, keep
    // searching" signal every reader honours.
    public static string? Transcript(string directory, string session) =>
        TranscriptDirectory.Transcript(directory, session)?.FullName;

    public static string KeyOf(string directory) => Path.GetFileName(directory);

    // Every verb validates its ids as uuids before touching anything: an id is a path component,
    // and a '..' in one is the traversal this gate closes.
    public static ValidationError? NotUuid(string session) =>
        Ids.IsUuid(session) ? null : new ValidationError($"Session id {session} is not a uuid.", "session");

    public static ValidationError? NotUuidMessage(string message) =>
        Ids.IsUuid(message) ? null : new ValidationError($"Message id {message} is not a uuid.", "upTo");

    // The candidates in order: the project directory for the cwd, then each worktree's, then — only
    // when no cwd was given — every project directory. A directory reached twice is tried once.
    public Result<ImmutableArray<string>, Error> Directories(string session, string? cwd)
    {
        if (cwd is { } here)
        {
            var directories = ImmutableArray.CreateBuilder<string>();
            Offer(directories, Layout.ProjectFor(here));
            foreach (var worktree in Worktrees(here))
            {
                if (Path.IsPathRooted(worktree))
                {
                    Offer(directories, Layout.ProjectFor(worktree));
                }
            }

            return directories.ToImmutable();
        }

        return transcripts.ProjectDirectories() is { } all
            ? all
            : new SessionNotFoundError(
                $"There is no session {session}: there is no projects directory.",
                session,
                SessionSearchScope.NoProjectsDirectory);
    }

    public static SessionNotFoundError NotFound(string session, string? cwd) =>
        cwd is { } here
            ? new SessionNotFoundError(
                $"There is no session {session} in the project directory for {here}.",
                session,
                SessionSearchScope.ProjectDirectory)
            : new SessionNotFoundError(
                $"There is no session {session} in any project directory.",
                session,
                SessionSearchScope.AnyProjectDirectory);

    // The layout asks git where the worktrees are, and git is a process: it can be missing, refuse,
    // or hand back something unreadable. Every such failure reads as no worktrees and the search
    // goes on; a throw here would leave the verbs as exceptions instead of results.
    private ImmutableArray<string> Worktrees(string here)
    {
        try
        {
            return Layout.WorktreePaths(here);
        }
#pragma warning disable CA1031 // Any failure of the git probe means no worktrees.
        catch (Exception)
#pragma warning restore CA1031
        {
            return [];
        }
    }

    // The layout answers where a project's directory is, so the walk and the reader find the same
    // one, past the sanitizer's limit too.
    private void Offer(ImmutableArray<string>.Builder directories, string project)
    {
        if (Layout.ProjectDirectory(project) is { } directory && !directories.Contains(directory))
        {
            directories.Add(directory);
        }
    }
}
