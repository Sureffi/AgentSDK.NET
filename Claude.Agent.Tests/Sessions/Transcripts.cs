using System.Collections.Immutable;

namespace Claude.Agent.Tests.Sessions;

// The recorded transcript directory: a CLAUDE_CONFIG_DIR the binary wrote during this tree's recording
// run, copied to the test output like every other fixture. Read it directly; copy it before writing.
internal static class Transcripts
{
    public const string ProjectDirectoryName = "-tmp-claude-agent-fixtures-cwd";

    public static readonly string Cwd = new("/tmp/claude-agent-fixtures/cwd");

    public static readonly string Project = new(ProjectDirectoryName);

    public static string ConfigDirectory { get; } =
        Path.Combine(AppContext.BaseDirectory, "fixtures", "transcripts");

    public static string Directory => Path.Combine(ConfigDirectory, "projects", ProjectDirectoryName);

    public static TranscriptLayout Layout() => Layout(ConfigDirectory);

    public static TranscriptLayout Layout(string configDirectory) => new(configDirectory);

    public static ImmutableArray<string> SessionIds() =>
        [.. System.IO.Directory.GetFiles(Directory, "*.jsonl")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(name => name!)
            .Order(StringComparer.Ordinal)];

    public static string PathOf(string session) => Path.Combine(Directory, session + ".jsonl");

    public static string TextOf(string session) => File.ReadAllText(PathOf(session));

    public static ImmutableArray<string> LinesOf(string session) =>
        [.. TextOf(session).Split('\n').Where(line => line.Length != 0)];

    public static ImmutableArray<SessionEntry> EntriesOf(string session) =>
        [.. LinesOf(session).Select(line => SessionEntry.Parse(line).Value)];

    // A writable copy of the whole recorded config directory, removed when the test is done.
    public static TemporaryConfig Copy()
    {
        var root = Path.Combine(Path.GetTempPath(), "claude-agent-tests-" + Guid.NewGuid().ToString("n"));
        CopyTree(ConfigDirectory, root);
        return new TemporaryConfig(root);
    }

    public static TemporaryConfig Empty()
    {
        var root = Path.Combine(Path.GetTempPath(), "claude-agent-tests-" + Guid.NewGuid().ToString("n"));
        System.IO.Directory.CreateDirectory(Path.Combine(root, "projects"));
        return new TemporaryConfig(root);
    }

    private static void CopyTree(string from, string to)
    {
        System.IO.Directory.CreateDirectory(to);
        foreach (var file in System.IO.Directory.GetFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
        }

        foreach (var directory in System.IO.Directory.GetDirectories(from))
        {
            CopyTree(directory, Path.Combine(to, Path.GetFileName(directory)));
        }
    }
}

internal sealed class TemporaryConfig(string root) : IDisposable
{
    public string Root { get; } = root;

    public string Projects => Path.Combine(Root, "projects");

    public ITranscriptLayout Layout => Transcripts.Layout(Root);

    public string ProjectDirectory => Path.Combine(Projects, Transcripts.ProjectDirectoryName);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // a test's temp tree is not worth failing a run over
        }
    }
}
