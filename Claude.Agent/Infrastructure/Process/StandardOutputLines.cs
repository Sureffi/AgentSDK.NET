using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Claude.Agent;

// The binary's stdout, read as the lines it wrote. It writes each message as JSON.stringify's text
// with '\n' after it, in one write, and a string goes to the pipe as UTF-8; JSON.stringify escapes
// every control character inside a string and puts no whitespace outside one, so that '\n' is the
// only one on the line. A line ends at '\n' and at nothing else, and is yielded as soon as its '\n'
// is read, without it and otherwise as it came. The bytes are decoded as UTF-8 across reads, so a
// character split between two reads is whole on its line; a byte that is not UTF-8 is U+FFFD. When
// the stream ends, the text after the last '\n' is one more line, a character its bytes left
// unfinished U+FFFD. A read that fails because the pipe broke or was closed ends the stream; a
// cancelled one throws.
internal static class StandardOutputLines
{
    private const int BufferSize = 65536;

    // A line longer than maxLineLength UTF-16 units ends the read with a ProcessError as soon as more
    // than that much of it has been read, whether or not its '\n' has come.
    public static async IAsyncEnumerable<string> ReadAsync(
        Stream standardOutput,
        int maxLineLength,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(standardOutput);

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var decoder = encoding.GetDecoder();
        var bytes = new byte[BufferSize];
        var text = new char[encoding.GetMaxCharCount(BufferSize)];
        var line = new StringBuilder();

        while (true)
        {
            int read;
            try
            {
                read = await standardOutput.ReadAsync(bytes, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException)
            {
                read = 0;
            }

            var length = decoder.GetChars(bytes, 0, read, text, 0, flush: read == 0);
            var start = 0;
            while (true)
            {
                var newline = text.AsSpan(start, length - start).IndexOf('\n');
                var count = newline < 0 ? length - start : newline;
                if (line.Length + count > maxLineLength)
                {
                    throw new TransportException(new ProcessError(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"The binary wrote a line longer than MaxMessageSize, {maxLineLength} characters."),
                        null));
                }

                if (newline < 0)
                {
                    line.Append(text, start, count);
                    break;
                }

                string whole;
                if (line.Length == 0)
                {
                    whole = new string(text, start, count);
                }
                else
                {
                    whole = line.Append(text, start, count).ToString();
                    line.Clear();
                }

                yield return whole;
                start += count + 1;
            }

            if (read == 0)
            {
                if (line.Length != 0)
                {
                    yield return line.ToString();
                }

                yield break;
            }
        }
    }
}
