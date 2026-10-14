using System.Collections.Immutable;

namespace Claude.Agent;

// Where the binary's transcripts live in one configuration directory. A TranscriptDirectory is built
// over one, and the reader and the editor ask it through that directory's Layout; a test builds the
// directory over one of its own.
internal interface ITranscriptLayout
{
    string ProjectsDirectory { get; }

    string ProjectFor(string cwd);

    // Where a project's transcripts are: the directory named exactly by the key, or — for a name the
    // sanitizer cut and hashed — the ordinally first directory under its first 200 characters and a
    // dash, or null when neither is there.
    string? ProjectDirectory(string project);

    // The spelling ProjectFor sanitizes: realpath then NFC. The reader reports it as a session's cwd
    // when the transcript carries none, and matches worktree paths against it.
    string CanonicalPath(string cwd);

    // The same sanitizer without the realpath, over a path exactly as it was spelled. The worktree
    // matcher compares the paths git printed against the project directory names on disk and sanitizes
    // them as given; null where that spelling names no project.
    string? ProjectForSpelling(string path);

    // Empty for anything that is not a repository, and for anything unreadable: this one never throws
    // either, because the listing has no Result to put a failure in and a caller that cannot ask git
    // scans the single directory instead.
    ImmutableArray<string> WorktreePaths(string cwd);
}
