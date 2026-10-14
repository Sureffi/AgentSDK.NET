using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;

namespace Claude.Agent.Tests.Sessions;

// Transcript lines built in the test, for the shapes the recorded directory does not carry: a
// session with a sidechain first line, a cycle, a summary entry or a meta-only prompt is not
// something the binary wrote here. Every line goes down as the binary writes one.
internal static class Built
{
    public static SessionEntry Entry(JsonObject json) => new(json);

    public static string Line(JsonObject json) => new SessionEntry(json).ToCompactJson();

    public static ImmutableArray<SessionEntry> Entries(params JsonObject[] entries) =>
        [.. entries.Select(Entry)];

    public static string ProjectDirectory(TemporaryConfig config, string path)
    {
        var project = config.Layout.ProjectFor(path);
        var directory = Path.Combine(config.Projects, project);
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static string Transcript(
        string projectDirectory,
        string session,
        long? mtimeEpochMs = null,
        params JsonObject[] entries)
    {
        var path = Path.Combine(projectDirectory, session + ".jsonl");
        var text = new StringBuilder();
        foreach (var entry in entries)
        {
            text.Append(Line(entry)).Append('\n');
        }

        File.WriteAllText(path, text.ToString());
        Stamp(path, mtimeEpochMs);
        return path;
    }

    public static string Raw(string projectDirectory, string session, string text, long? mtimeEpochMs = null)
    {
        var path = Path.Combine(projectDirectory, session + ".jsonl");
        File.WriteAllText(path, text);
        Stamp(path, mtimeEpochMs);
        return path;
    }

    // A minimal session file: a first user line, an assistant answer, a summary line, and a
    // custom-title line when the case gives a title.
    public static string Session(
        string projectDirectory,
        string? session = null,
        string firstPrompt = "Hello Claude",
        string? summary = null,
        string? customTitle = null,
        string? gitBranch = null,
        string? cwd = null,
        bool sidechain = false,
        bool metaOnly = false,
        long? mtimeEpochMs = null)
    {
        var id = session ?? Guid.NewGuid().ToString();

        var first = new JsonObject
        {
            ["type"] = "user",
            ["message"] = new JsonObject { ["role"] = "user", ["content"] = firstPrompt },
        };
        if (cwd is not null)
        {
            first["cwd"] = cwd;
        }

        if (gitBranch is not null)
        {
            first["gitBranch"] = gitBranch;
        }

        if (sidechain)
        {
            first["isSidechain"] = true;
        }

        if (metaOnly)
        {
            first["isMeta"] = true;
        }

        var tail = new JsonObject { ["type"] = "summary" };
        if (summary is not null)
        {
            tail["summary"] = summary;
        }

        if (gitBranch is not null)
        {
            tail["gitBranch"] = gitBranch;
        }

        var answer = new JsonObject
        {
            ["type"] = "assistant",
            ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = "Hi there!" },
        };
        JsonObject[] lines = customTitle is null
            ? [first, answer, tail]
            : [first, answer, tail, new JsonObject { ["type"] = "custom-title", ["customTitle"] = customTitle, ["sessionId"] = id }];
        Transcript(projectDirectory, id, mtimeEpochMs, lines);

        return id;
    }

    // <project>/<session>/subagents/[<nested>/]agent-<id>.jsonl, and its sidecar beside it.
    public static string SubagentTranscript(
        string projectDirectory,
        string session,
        string agent,
        string? nested = null,
        params JsonObject[] entries)
    {
        var directory = Path.Combine(projectDirectory, session, "subagents");
        if (nested is not null)
        {
            directory = Path.Combine(directory, nested);
        }

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "agent-" + agent + ".jsonl");
        var text = new StringBuilder();
        foreach (var entry in entries)
        {
            text.Append(Line(entry)).Append('\n');
        }

        File.WriteAllText(path, text.ToString());
        return path;
    }

    public static void Sidecar(string transcript, string json) =>
        File.WriteAllText(transcript[..^".jsonl".Length] + ".meta.json", json);

    public static SessionReader Reader(TemporaryConfig config) => Reader(config.Layout);

    public static SessionReader Reader(ITranscriptLayout layout) => new(new TranscriptDirectory(layout));

    // A layout whose git runner answers with the porcelain listing the caller dictates: the worktree
    // shapes a filesystem cannot be talked into.
    public static ITranscriptLayout Layout(TemporaryConfig config, params string[] worktrees) =>
        new TranscriptLayout(config.Root, _ => string.Join('\n', worktrees.Select(path => "worktree " + path)));

    private static void Stamp(string path, long? mtimeEpochMs)
    {
        if (mtimeEpochMs is { } ms)
        {
            File.SetLastWriteTimeUtc(path, DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime);
        }
    }
}

// A real directory to be a project's working directory: the sanitized name is taken off the resolved
// path, so the path has to exist for the two sides to agree.
internal sealed class TemporaryProject : IDisposable
{
    public TemporaryProject()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "claude-agent-project-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // a test's temp tree is not worth failing a run over
        }
    }
}

// What a guard that runs "before any I/O" is held to: every question of the layout fails the test,
// and every path the reader reads comes through the layout.
internal sealed class UntouchableLayout : ITranscriptLayout
{
    public string ProjectsDirectory => throw Touch();

    public string ProjectFor(string cwd) => throw Touch();

    public string? ProjectDirectory(string project) => throw Touch();

    public string CanonicalPath(string cwd) => throw Touch();

    public string? ProjectForSpelling(string path) => throw Touch();

    public ImmutableArray<string> WorktreePaths(string cwd) => throw Touch();

    private static InvalidOperationException Touch() => new("the layout was asked for something.");
}
