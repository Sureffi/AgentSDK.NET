using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Claude.Agent;

// One configuration directory's transcripts, as the binary writes them: projects/<project>/<session>.jsonl,
// and a session's subagents under projects/<project>/<session>/subagents/, each agent-<id>.jsonl
// with its agent-<id>.meta.json beside it. A zero-byte transcript is no session. A read that cannot
// be made is absence; a write that fails throws what it failed with.
internal sealed class TranscriptDirectory(ITranscriptLayout layout)
{
    public const string Extension = ".jsonl";

    private const string SidecarExtension = ".meta.json";

    private const string SubagentDirectory = "subagents";

    private const string AgentPrefix = "agent-";

    public ITranscriptLayout Layout { get; } = layout ?? throw new ArgumentNullException(nameof(layout));

    // The project directories, full paths in ordinal order; null when there is no projects directory.
    public ImmutableArray<string>? ProjectDirectories()
    {
        string[] directories;
        try
        {
            directories = Directory.GetDirectories(Layout.ProjectsDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        Array.Sort(directories, StringComparer.Ordinal);
        return [.. directories];
    }

    // The projects' names, the directory names that are unpadded, in ordinal order.
    public ImmutableArray<string> Projects() =>
        ProjectDirectories() is { } directories
            ? [.. directories.Select(Path.GetFileName).Where(Ids.IsKeyPart).Select(project => project!)]
            : [];

    public string? ProjectDirectory(string project) => Layout.ProjectDirectory(project);

    // The session's transcript in a project directory: there and not a zero-byte stub.
    public static FileInfo? Transcript(string directory, string session)
    {
        try
        {
            return new FileInfo(Path.Combine(directory, session + Extension)) is { Exists: true, Length: > 0 } info
                ? info
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Every session a project directory holds, in ordinal order of the file names.
    public static IEnumerable<(string Session, FileInfo File)> Transcripts(string directory, CancellationToken ct)
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(directory, "*" + Extension, SearchOption.TopDirectoryOnly);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        Array.Sort(files, StringComparer.Ordinal);
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var stem = Path.GetFileNameWithoutExtension(file);
            if (TranscriptPaths.IsUuid(stem) && Transcript(directory, stem) is { } info)
            {
                yield return (stem, info);
            }
        }
    }

    // To the millisecond, as the binary stamps.
    public static DateTimeOffset Modified(FileInfo file) =>
        DateTimeOffset.FromUnixTimeMilliseconds(new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero).ToUnixTimeMilliseconds());

    // The listing's read of one transcript: the entries on the whole lines of its first and its last
    // TranscriptScan.WindowSize bytes, one window when the file is no longer than that. A transcript
    // that cannot be read is a row with no summary.
    public static SessionInfo? Info(string session, FileInfo file, string? projectPath)
    {
        try
        {
            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var size = stream.Length;
            if (size == 0)
            {
                return null;
            }

            var head = Window(stream, 0, size);
            var tail = size <= TranscriptScan.WindowSize ? head : Window(stream, size - TranscriptScan.WindowSize, size);
            return TranscriptScan.Info(session, head, tail, Modified(file), size, projectPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new SessionInfo(session, string.Empty, Modified(file));
        }
    }

    // Every line of a transcript, a blank one and one that does not parse skipped.
    public static ImmutableArray<SessionEntry> Entries(string path) => TranscriptScan.Entries(File.ReadAllText(path));

    // The entries on the lines that lie wholly inside the window of TranscriptScan.WindowSize bytes
    // from offset. A line the window's edge cuts is dropped, never decoded: the byte before the window
    // says whether its first line starts there, and the byte after it whether its last line ends there.
    private static ImmutableArray<SessionEntry> Window(FileStream stream, long offset, long size)
    {
        var end = Math.Min(size, offset + TranscriptScan.WindowSize);
        var from = Math.Max(0, offset - 1);
        var bytes = new byte[Math.Min(size, end + 1) - from];
        stream.Seek(from, SeekOrigin.Begin);
        stream.ReadExactly(bytes);

        var start = 0;
        if (offset > 0)
        {
            var feed = Array.IndexOf(bytes, (byte)'\n');
            if (feed < 0)
            {
                return [];
            }

            start = feed + 1;
        }

        var entries = ImmutableArray.CreateBuilder<SessionEntry>();
        while (start < bytes.Length)
        {
            var feed = Array.IndexOf(bytes, (byte)'\n', start);
            if (feed < 0 && from + bytes.Length != size)
            {
                break;
            }

            var length = (feed < 0 ? bytes.Length : feed) - start;
            if (from + start + length > end)
            {
                break;
            }

            if (length != 0 && SessionEntry.Parse(Encoding.UTF8.GetString(bytes, start, length)) is { IsOk: true } parsed)
            {
                entries.Add(parsed.Value);
            }

            start += length + 1;
        }

        return entries.ToImmutable();
    }

    // The session's subagent transcripts, as paths under the session's directory without the
    // extension — subagents/agent-<id>, subagents/<workflow>/agent-<id> — each directory in ordinal
    // order and walked where its name sits.
    public static ImmutableArray<string> Subagents(string directory, string session)
    {
        var found = ImmutableArray.CreateBuilder<string>();
        Walk(Path.Combine(directory, session, SubagentDirectory), SubagentDirectory, found);
        return found.ToImmutable();
    }

    // The transcript of a subagent Subagents named.
    public static string SubagentTranscript(string directory, string session, string subagent) =>
        Path.Combine([directory, session, .. subagent.Split('/')]) + Extension;

    // A subagent's .meta.json: the agent's type and the tool use that started it, read as a transcript
    // line is. Missing, unreadable, not JSON or not an object is none.
    public static JsonObject? Metadata(string transcript)
    {
        try
        {
            var text = File.ReadAllText(transcript[..^Extension.Length] + SidecarExtension);
            return JsonNode.Parse(JsonSurrogateEscapes.ReplaceUnpaired(text), documentOptions: SessionEntry.ReadOptions) as JsonObject;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                             or JsonException or ArgumentException)
        {
            return null;
        }
    }

    // Appends to a transcript that is there, as the binary appends: false when it is not, a missing
    // file and a zero-byte one alike, and nothing is created.
    public static bool Append(string directory, string session, byte[] bytes)
    {
        var path = Path.Combine(directory, session + Extension);
        if (OperatingSystem.IsWindows())
        {
            return AppendOnWindows(path, bytes);
        }

        return Posix.Append(path, bytes, out var errno) switch
        {
            Posix.AppendOutcome.Written => true,
            Posix.AppendOutcome.NotHere => false,
            _ => throw new IOException(string.Create(
                CultureInfo.InvariantCulture,
                $"Appending to {path} failed with errno {errno}.")),
        };
    }

    // The transcript, and the session's directory its subagents are in; a directory that will not
    // go is left.
    public static void Delete(string directory, string session)
    {
        File.Delete(Path.Combine(directory, session + Extension));
        try
        {
            Directory.Delete(Path.Combine(directory, session), true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Not there, or not removable: the transcript is gone either way.
        }
    }

    private static void Walk(string directory, string prefix, ImmutableArray<string>.Builder found)
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

        foreach (var entry in entries.OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(entry);
            if (Directory.Exists(entry))
            {
                Walk(entry, prefix + "/" + name, found);
            }
            else if (name.StartsWith(AgentPrefix, StringComparison.Ordinal)
                     && name.EndsWith(Extension, StringComparison.Ordinal))
            {
                found.Add(prefix + "/" + name[..^Extension.Length]);
            }
        }
    }

    private static bool AppendOnWindows(string path, byte[] bytes)
    {
        // Written and unrun. FileMode.Open is the existence check, as O_WRONLY without O_CREAT is on
        // the other side: a separate stat would be the race the open exists to avoid.
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            if (stream.Length == 0)
            {
                return false;
            }

            stream.Seek(0, SeekOrigin.End);
            stream.Write(bytes);
            return true;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }
}
