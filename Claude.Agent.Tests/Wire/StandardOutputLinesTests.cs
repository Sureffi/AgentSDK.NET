using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Wire;

[TestClass]
public sealed class StandardOutputLinesTests
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    public static IEnumerable<object[]> RecordedStdouts() =>
        Directory.EnumerateFiles(Fixture.Root, "*.stdout.jsonl")
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .Select(file => new object[] { file! });

    // What the recordings carry, which the reading rests on with the binary's writer — JSON.stringify,
    // U+2028 and U+2029 escaped, '\n' after it: UTF-8 with no byte order mark, one JSON object a line
    // with nothing around it, and no '\r', U+2028 or U+2029 raw. The recorder writes each line the
    // transport read and '\n' after it.
    [TestMethod]
    [DynamicData(nameof(RecordedStdouts), DynamicDataSourceType.Method)]
    public void ARecordedStdoutIsOneJsonObjectALineWithNothingAroundItAndNoOtherLineBreak(string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(Fixture.Root, file));
        Assert.IsFalse(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble), $"{file} starts with a byte order mark.");
        var text = StrictUtf8.GetString(bytes);

        Assert.IsTrue(text.EndsWith('\n'), $"{file} does not end with a newline.");
        foreach (var character in new[] { '\r', '\u2028', '\u2029' })
        {
            Assert.IsFalse(text.Contains(character), $"{file} carries U+{(int)character:X4}.");
        }

        var lines = text[..^1].Split('\n');
        for (var number = 1; number <= lines.Length; number++)
        {
            var line = lines[number - 1];
            Assert.IsTrue(line.StartsWith('{') && line.EndsWith('}'), $"{file} line {number} is not an object alone on its line.");
            using var document = JsonDocument.Parse(line);
            Assert.AreEqual(JsonValueKind.Object, document.RootElement.ValueKind, $"{file} line {number}");
        }
    }

    // However the pipe hands it over, a recorded stdout reads back into its lines, each as recorded:
    // cut a byte at a time, every character the recordings write in more than one byte is split.
    [TestMethod]
    [DynamicData(nameof(RecordedStdouts), DynamicDataSourceType.Method)]
    public async Task ARecordedStdoutReadInPiecesOfAnySizeReadsBackIntoItsLinesAsRecorded(string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(Fixture.Root, file));
        var recorded = StrictUtf8.GetString(bytes)[..^1].Split('\n');

        foreach (var size in new[] { 1, 7, 4096, 65536 })
        {
            var stream = new PieceStream();
            for (var at = 0; at < bytes.Length; at += size)
            {
                stream.Add(bytes[at..Math.Min(at + size, bytes.Length)]);
            }

            stream.End();

            CollectionAssert.AreEqual(recorded, await ReadAllAsync(stream), $"{file} in pieces of {size}");
        }
    }

    // A line is handed on as its '\n' is read, with the stream still open and nothing more written;
    // the text before it waits for it.
    [TestMethod]
    public async Task ALineIsReadAsItsNewlineArrivesWithoutWaitingForMoreOfTheStream()
    {
        var stream = new PieceStream();
        await using var lines = StandardOutputLines.ReadAsync(stream, int.MaxValue, CancellationToken.None).GetAsyncEnumerator();

        stream.Add(Utf8("""{"type": "assis"""));
        var next = lines.MoveNextAsync().AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        Assert.IsFalse(next.IsCompleted, "a line was handed on before its newline.");

        stream.Add(Utf8("""tant"}""" + "\n"));

        Assert.IsTrue(await next.WaitAsync(Patience));
        Assert.AreEqual("""{"type": "assistant"}""", lines.Current);
    }

    [TestMethod]
    public async Task ACharacterSplitBetweenTwoReadsIsWholeOnItsLine()
    {
        var bytes = Utf8("{\"text\":\"a \u2014 b\"}\n");
        var dash = Array.IndexOf(bytes, (byte)0xE2);
        var stream = new PieceStream();
        stream.Add(bytes[..(dash + 1)]);
        stream.Add(bytes[(dash + 1)..]);
        stream.End();

        CollectionAssert.AreEqual(new[] { "{\"text\":\"a \u2014 b\"}" }, await ReadAllAsync(stream));
    }

    // A blank line, whitespace around a line and a carriage return before the newline are the line's,
    // as written; only the '\n' ends it.
    [TestMethod]
    public async Task EveryLineIsReadAsWrittenBlankOnesAndWhitespaceIncluded()
    {
        var stream = new PieceStream();
        stream.Add(Utf8("first\n\n  {\"a\": 1}\t\nsecond\r\nthird"));
        stream.End();

        CollectionAssert.AreEqual(
            new[] { "first", string.Empty, "  {\"a\": 1}\t", "second\r", "third" },
            await ReadAllAsync(stream));
    }

    // Text no '\n' ended is one more line when the stream ends, and a character its bytes left
    // unfinished is U+FFFD on it.
    [TestMethod]
    public async Task TextNoNewlineEndedIsOneMoreLineWhenTheStreamEnds()
    {
        var stream = new PieceStream();
        stream.Add([.. Utf8("{\"a\": 1}\n{\"b\": \""), 0xE2, 0x80]);
        stream.End();

        CollectionAssert.AreEqual(new[] { "{\"a\": 1}", "{\"b\": \"\uFFFD" }, await ReadAllAsync(stream));
    }

    [TestMethod]
    public async Task AStreamThatEndsOnANewlineHasNoLineAfterIt()
    {
        var stream = new PieceStream();
        stream.Add(Utf8("{\"a\": 1}\n"));
        stream.End();

        CollectionAssert.AreEqual(new[] { "{\"a\": 1}" }, await ReadAllAsync(stream));
    }

    // A pipe that breaks or is closed under the read ends the stream as its end would: the text read
    // so far is the last line.
    [TestMethod]
    public async Task AReadThatFailsBecauseThePipeBrokeOrWasClosedEndsTheStream()
    {
        foreach (var failure in new Exception[] { new IOException("broken pipe"), new ObjectDisposedException("pipe") })
        {
            var stream = new PieceStream();
            stream.Add(Utf8("first\nsecond"));
            stream.Fail(failure);

            CollectionAssert.AreEqual(new[] { "first", "second" }, await ReadAllAsync(stream), failure.GetType().Name);
        }
    }

    [TestMethod]
    public async Task ACancelledReadThrows()
    {
        var stream = new PieceStream();
        using var cancellation = new CancellationTokenSource();
        await using var lines = StandardOutputLines.ReadAsync(stream, int.MaxValue, cancellation.Token).GetAsyncEnumerator();

        var next = lines.MoveNextAsync().AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => next.WaitAsync(Patience));
    }

    // The bound is on the line as it is put together: a line of the bound's length is read, and one
    // past it throws as soon as more than that much of it is read, its '\n' not yet come and the
    // stream open.
    [TestMethod]
    public async Task ALineOfTheBoundsLengthIsReadAndOnePastItThrowsBeforeItsNewlineComes()
    {
        var stream = new PieceStream();
        await using var lines = StandardOutputLines.ReadAsync(stream, 8, CancellationToken.None).GetAsyncEnumerator();

        stream.Add(Utf8("12345678\n1234"));
        Assert.IsTrue(await lines.MoveNextAsync().AsTask().WaitAsync(Patience));
        Assert.AreEqual("12345678", lines.Current);

        stream.Add(Utf8("56789"));
        var thrown = await Assert.ThrowsExactlyAsync<TransportException>(() => lines.MoveNextAsync().AsTask().WaitAsync(Patience));

        Assert.IsInstanceOfType<ProcessError>(thrown.Error);
        Assert.AreEqual("The binary wrote a line longer than MaxMessageSize, 8 characters.", thrown.Error.Message);
    }

    // Many lines in one read are each within the bound, however long the read.
    [TestMethod]
    public async Task ManyLinesInOneReadAreEachHeldToTheBoundAlone()
    {
        var stream = new PieceStream();
        stream.Add(Utf8("1234\n5678\n9012\n"));
        stream.End();

        CollectionAssert.AreEqual(new[] { "1234", "5678", "9012" }, await ReadAllAsync(stream, maxLineLength: 4));
    }

    private static async Task<string[]> ReadAllAsync(Stream stream, int maxLineLength = int.MaxValue)
    {
        var lines = new List<string>();
        await foreach (var line in StandardOutputLines.ReadAsync(stream, maxLineLength, CancellationToken.None))
        {
            lines.Add(line);
        }

        return [.. lines];
    }

    private static byte[] Utf8(string text) => StrictUtf8.GetBytes(text);

    // A pipe the test fills: each read hands back what one Add put in, or as much of it as the
    // buffer holds, and waits while nothing is added.
    private sealed class PieceStream : Stream
    {
        private readonly Channel<byte[]> _pieces = Channel.CreateUnbounded<byte[]>();
        private Exception? _failure;
        private byte[] _piece = [];
        private int _at;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public void Add(byte[] piece) => _pieces.Writer.TryWrite(piece);

        public void End() => _pieces.Writer.TryComplete();

        public void Fail(Exception failure)
        {
            _failure = failure;
            _pieces.Writer.TryComplete();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_at == _piece.Length)
            {
                if (!await _pieces.Reader.WaitToReadAsync(cancellationToken))
                {
                    return _failure is null ? 0 : throw _failure;
                }

                _piece = await _pieces.Reader.ReadAsync(cancellationToken);
                _at = 0;
            }

            var count = Math.Min(buffer.Length, _piece.Length - _at);
            _piece.AsSpan(_at, count).CopyTo(buffer.Span);
            _at += count;
            return count;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
