// A claude that misbehaves on cue.
//
// The cue is the executable's own name: a link called claude-<cue> to this file plays that cue;
// called claude, it plays the trivial fixture's turn cleanly. The name carries the cue because the
// version probe gets none of the caller's environment variables. CLAUDE_FAKE_PIDFILE names a file
// to write the pid to, so a test can check the process is gone; the environment it was started
// with goes to <pidfile>.env and its arguments to <pidfile>.args, one a line, and every byte it
// writes to stdout to <pidfile>.stdout as well; a cue that traps SIGTERM writes <pidfile>.term when
// the signal lands. The frames come from ../fixtures/trivial, so every clean line is one the real
// binary once wrote. A cue that writes to stderr writes "fake: line one" before the turn and "fake:
// line two" after its result; stderr-at-exit writes one line as the process goes, once its stdin
// has ended; stderr-as-written writes a blank line, a line ending in spaces and text no newline
// ends; stderr-past-bound writes more text than MaxMessageSize's default with no newline, a
// character split between two writes. stdout-as-written writes a blank line, one of whitespace and
// one with whitespace and a carriage return around it before the turn.
// A prompt written with a uuid is also answered as the binary answers a session's prompt: the
// lifecycle frames ../fixtures/client wrote for its first prompt, queued and started before the
// turn and completed after its result, naming the prompt's uuid and the turn's session. The cue
// exit-nonzero-after-error-result plays the client fixture's interrupted result in place of the
// trivial one, under the turn's session, and exits 1 after it, as the binary does after a turn it
// did not finish.
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

var name = Path.GetFileName(InvokedAs());
var cue = name.StartsWith("claude-", StringComparison.Ordinal) ? name["claude-".Length..] : string.Empty;
var fixtures = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "fixtures"));
var fixture = Path.Combine(fixtures, "trivial.stdout.jsonl");

var pidfile = Environment.GetEnvironmentVariable("CLAUDE_FAKE_PIDFILE");
Stream output = Console.OpenStandardOutput();
if (!string.IsNullOrEmpty(pidfile))
{
    output = new CopyingStream(output, File.Create(pidfile + ".stdout"));
}

var stdout = new StreamWriter(output, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
var stderr = new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };

if (args.Length > 0 && args[0] is "-v" or "--version")
{
    if (cue == "probe-hangs")
    {
        SleepForever();
    }

    stdout.WriteLine(cue switch
    {
        "wrong-version" => "1.0.0 (Claude Code)",
        "version-before-floor" => "2.1.275 (Claude Code)",
        _ => "2.1.276 (Claude Code)",
    });
    return 0;
}

if (!string.IsNullOrEmpty(pidfile))
{
    File.WriteAllText(pidfile, Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    File.WriteAllText(pidfile + ".env", EnvironmentJson());
    File.WriteAllText(pidfile + ".args", string.Join('\n', args));
}

if (cue == "hang-stdin")
{
    SleepForever();
}

var frames = File.ReadLines(fixture)
    .Where(line => line.StartsWith('{'))
    .Where(line =>
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.GetProperty("type").GetString() != "control_response";
    })
    .ToArray();
var init = frames[0];
var rest = frames[1..];
string turnSession;
using (var initFrame = JsonDocument.Parse(init))
{
    turnSession = initFrame.RootElement.GetProperty("session_id").GetString()!;
}

var lifecycle = File.ReadLines(Path.Combine(fixtures, "client.stdout.jsonl"))
    .Where(line => line.StartsWith('{'))
    .Select(line => JsonNode.Parse(line)!.AsObject())
    .Where(node => (string?)node["type"] == "command_lifecycle")
    .GroupBy(node => (string)node["state"]!)
    .ToDictionary(group => group.Key, group => group.First().ToJsonString());

var stdin = Console.In;
while (stdin.ReadLine() is { } raw)
{
    var line = raw.Trim();
    if (line.Length == 0)
    {
        continue;
    }

    using var frame = JsonDocument.Parse(line);
    var type = frame.RootElement.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;

    if (type == "control_request")
    {
        var requestId = frame.RootElement.GetProperty("request_id").GetString();
        stdout.WriteLine(
            "{\"type\": \"control_response\", \"response\": {\"subtype\": \"success\", \"request_id\": "
            + JsonSerializer.Serialize(requestId)
            + ", \"response\": {}}}");
        continue;
    }

    if (type != "user")
    {
        continue;
    }

    var prompt = frame.RootElement.TryGetProperty("uuid", out var uuidElement) ? uuidElement.GetString() : null;
    if (prompt is not null)
    {
        stdout.WriteLine(Lifecycle("queued", prompt));
        stdout.WriteLine(Lifecycle("started", prompt));
    }

    if (cue == "stderr")
    {
        stderr.WriteLine("fake: line one");
    }

    // A control request of 8192 bytes, its newline the last, written in one write once the reader
    // has gone quiet; the turn goes on once it is answered.
    if (cue == "request-filling-reads")
    {
        stdout.Flush();
        Thread.Sleep(TimeSpan.FromMilliseconds(300));
        const string Head = "{\"type\":\"control_request\",\"request_id\":\"fake_filling\",\"request\":{\"subtype\":\"fake_filling\",\"pad\":\"";
        const string Tail = "\"}}\n";
        var request = Head + new string('x', 8192 - Head.Length - Tail.Length) + Tail;
        stdout.BaseStream.Write(Encoding.UTF8.GetBytes(request));
        stdout.BaseStream.Flush();
        while (stdin.ReadLine() is { } answer && !answer.Contains("fake_filling", StringComparison.Ordinal))
        {
        }
    }

    if (cue == "stderr-as-written")
    {
        stderr.Write("fake: a paragraph\n\nfake: another, ending in two spaces  \nfake: no newline");
    }

    // One more character than MaxMessageSize's default, then U+00E4, whose first byte ends the first
    // write and whose second starts the second, once the reader has had the first; no newline.
    if (cue == "stderr-past-bound")
    {
        var text = Encoding.UTF8.GetBytes(new string('x', (1024 * 1024) + 1) + "\u00e4, and no newline after it");
        var cut = Array.IndexOf(text, (byte)0xC3) + 1;
        stderr.BaseStream.Write(text, 0, cut);
        stderr.BaseStream.Flush();
        Thread.Sleep(TimeSpan.FromMilliseconds(300));
        stderr.BaseStream.Write(text, cut, text.Length - cut);
        stderr.BaseStream.Flush();
    }

    if (cue == "stdout-as-written")
    {
        stdout.Write("\n \t \n  not json, padded  \r\n");
    }

    if (cue == "garbage-stdout")
    {
        stdout.WriteLine("not json at all");
        stdout.WriteLine("{\"type\": \"assistant\", \"message\": {");
    }

    stdout.WriteLine(init);

    if (cue == "hang-before-result")
    {
        stdout.Flush();
        SleepForever();
    }

    if (cue == "huge-line")
    {
        stdout.WriteLine($$"""{"type": "assistant", "pad": "{{new string('x', 2 * 1024 * 1024)}}"}""");
    }

    if (cue == "die-mid-frame")
    {
        stdout.Write("{\"type\": \"assistant\", \"message\": {\"content\": [{\"type\": \"text\", \"text\": \"half");
        stdout.Flush();
        return 1;
    }

    // The result written as a process cut off before its newline: the frame whole, no '\n' after it.
    if (cue == "result-without-newline")
    {
        foreach (var frameLine in rest[..^1])
        {
            stdout.WriteLine(frameLine);
        }

        stdout.Write(rest[^1]);
        stdout.Flush();
        return 0;
    }

    foreach (var frameLine in rest)
    {
        stdout.WriteLine(cue == "exit-nonzero-after-error-result" && IsResult(frameLine) ? ErrorResult() : frameLine);
    }

    if (prompt is not null)
    {
        stdout.WriteLine(Lifecycle("completed", prompt));
    }

    if (cue == "stderr")
    {
        stderr.WriteLine("fake: line two");
    }

    // A line on stderr written as the process goes, after its stdin ended: the close has to read
    // stderr to its end to have it.
    if (cue == "stderr-at-exit")
    {
        while (stdin.ReadLine() is not null)
        {
        }

        Thread.Sleep(TimeSpan.FromMilliseconds(50));
        stderr.WriteLine("fake: at exit");
        return 0;
    }

    // The turn's result, then a non-zero exit of the binary's own.
    if (cue == "exit-nonzero-after-result")
    {
        stdout.Flush();
        return 3;
    }

    if (cue == "exit-nonzero-after-error-result")
    {
        stdout.Flush();
        return 1;
    }

    if (cue == "hang-after-result")
    {
        SleepForever();
    }

    if (cue == "trap-term")
    {
        using var trap = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            OnTerm(pidfile, exitAfter: true);
        });
        SleepForever();
    }

    if (cue == "ignore-term")
    {
        using var ignore = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            OnTerm(pidfile, exitAfter: false);
        });
        SleepForever();
    }

    break;
}

return 0;

bool IsResult(string line)
{
    using var document = JsonDocument.Parse(line);
    return document.RootElement.GetProperty("type").GetString() == "result";
}

string ErrorResult()
{
    var result = File.ReadLines(Path.Combine(fixtures, "client.stdout.jsonl"))
        .Where(line => line.StartsWith('{'))
        .Select(line => JsonNode.Parse(line)!.AsObject())
        .Last(node => (string?)node["type"] == "result");
    result["session_id"] = turnSession;
    return result.ToJsonString();
}

string Lifecycle(string state, string prompt)
{
    var node = JsonNode.Parse(lifecycle[state])!.AsObject();
    node["command_uuid"] = prompt;
    node["session_id"] = turnSession;
    return node.ToJsonString();
}

static string InvokedAs()
{
    if (OperatingSystem.IsLinux())
    {
        var cmdline = File.ReadAllBytes("/proc/self/cmdline");
        var end = Array.IndexOf(cmdline, (byte)0);
        return Encoding.UTF8.GetString(cmdline, 0, end < 0 ? cmdline.Length : end);
    }

    return Environment.GetCommandLineArgs()[0];
}

static void SleepForever()
{
    while (true)
    {
        Thread.Sleep(TimeSpan.FromHours(1));
    }
}

static void OnTerm(string? pidfile, bool exitAfter)
{
    if (string.IsNullOrEmpty(pidfile))
    {
        return;
    }

    File.WriteAllText(pidfile + ".term", "15");
    if (exitAfter)
    {
        File.WriteAllText(pidfile + ".exit", "0");
        Environment.Exit(0);
    }
}

static string EnvironmentJson()
{
    var buffer = new MemoryStream();
    using (var writer = new Utf8JsonWriter(buffer))
    {
        writer.WriteStartObject();
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            writer.WriteString((string)entry.Key, (string?)entry.Value ?? string.Empty);
        }

        writer.WriteEndObject();
    }

    return Encoding.UTF8.GetString(buffer.ToArray());
}

// Every byte written to the output, written to the copy as well.
internal sealed class CopyingStream(Stream output, Stream copy) : Stream
{
    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        output.Write(buffer, offset, count);
        copy.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        output.Write(buffer);
        copy.Write(buffer);
    }

    public override void Flush()
    {
        output.Flush();
        copy.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
