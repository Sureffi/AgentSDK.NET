using System.Text.Encodings.Web;
using System.Text.Json;

namespace Claude.Agent.Recorder;

// One fixture's three files, tee'd off the transport as bytes: the binary's stdout line by line,
// the SDK's stdin line by line, and the argv the binary was started with. Closed, each file is
// scrubbed (FixtureScrub) of the recording home and the account. Record under a neutral cwd.
internal sealed class Recording : IDisposable
{
    // Relaxed, so a quote inside an argument is \" on disk and an --mcp-config value stays readable.
    private static readonly JsonSerializerOptions ArgvJson = new()
    {
        WriteIndented = true,
        IndentSize = 1,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    private readonly string _directory;
    private readonly string _home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private string? _name;
    private StreamWriter? _stdout;
    private StreamWriter? _stdin;

    public Recording(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
        Tap = new TransportTap(Arguments, StdoutLine, StdinLine);
    }

    public TransportTap Tap { get; }

    public void Begin(string name)
    {
        End();
        _name = name;
        _stdout = new StreamWriter(File(name, "stdout.jsonl"));
        _stdin = new StreamWriter(File(name, "stdin.jsonl"));
        Console.WriteLine($"== {name}");
    }

    public void End()
    {
        if (_name is null)
        {
            return;
        }

        _stdout?.Dispose();
        _stdin?.Dispose();
        _stdout = _stdin = null;
        foreach (var suffix in new[] { "stdout.jsonl", "stdin.jsonl", "argv.json" })
        {
            var path = File(_name, suffix);
            if (System.IO.File.Exists(path))
            {
                System.IO.File.WriteAllText(path, FixtureScrub.Apply(System.IO.File.ReadAllText(path), _home));
            }
        }

        _name = null;
    }

    // The init frame's session_id, for a fixture that resumes an earlier one.
    public string SessionIdOf(string name)
    {
        foreach (var line in System.IO.File.ReadLines(File(name, "stdout.jsonl")))
        {
            using var frame = JsonDocument.Parse(line);
            if (frame.RootElement.TryGetProperty("type", out var type) && type.GetString() == "system"
                && frame.RootElement.TryGetProperty("subtype", out var subtype) && subtype.GetString() == "init")
            {
                return frame.RootElement.GetProperty("session_id").GetString()!;
            }
        }

        throw new InvalidOperationException($"{name} has no init frame");
    }

    // A session's transcript as the binary wrote it into the configuration directory it ran in, and
    // its subagents' beside it, kept under the fixtures' transcripts/ and scrubbed as the rest is. The
    // binary's other files for the session are not kept.
    public void KeepTranscripts(string configurationDirectory, string cwd, string sessionId)
    {
        var project = TranscriptPaths.Sanitize(cwd);
        var from = Path.Combine(configurationDirectory, "projects", project);
        var to = Path.Combine(_directory, "transcripts", "projects", project);
        Keep(Path.Combine(from, $"{sessionId}.jsonl"), Path.Combine(to, $"{sessionId}.jsonl"));
        var subagents = Path.Combine(from, sessionId, "subagents");
        if (Directory.Exists(subagents))
        {
            foreach (var file in Directory.EnumerateFiles(subagents))
            {
                Keep(file, Path.Combine(to, sessionId, "subagents", Path.GetFileName(file)));
            }
        }

        Console.WriteLine($"  kept the transcripts of {sessionId}");
    }

    public void Dispose() => End();

    private void Keep(string from, string to)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        System.IO.File.WriteAllText(to, FixtureScrub.Apply(System.IO.File.ReadAllText(from), _home));
    }

    private string File(string name, string suffix) => Path.Combine(_directory, $"{name}.{suffix}");

    private void Arguments(IReadOnlyList<string> arguments)
    {
        if (_name is { } name)
        {
            System.IO.File.WriteAllText(File(name, "argv.json"), JsonSerializer.Serialize(arguments, ArgvJson) + "\n");
        }
    }

    private void StdoutLine(string line)
    {
        _stdout?.WriteLine(line);
        _stdout?.Flush();
    }

    private void StdinLine(string line)
    {
        _stdin?.WriteLine(line);
        _stdin?.Flush();
    }
}
