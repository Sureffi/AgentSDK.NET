using System.Text;
using System.Text.RegularExpressions;

namespace Claude.Agent.Recorder;

// What a fixture must not carry, taken out of any text the recorder keeps: the recording home, every
// address wherever it stands, and the email, organization and atis values under their keys however
// deep a string nests them — a key is escaped once more for each string it sits in, and may be
// spaced. Each becomes the neutral value Architecture/TreeTests holds every fixture to, and a text
// already scrubbed comes out as it went in. The tests compile this file too, to hold it.
internal static partial class FixtureScrub
{
    public const string Home = "/home/user";

    public const string Address = "user@example.com";

    public const string Organization = "user@example.com's Organization";

    public static string Apply(string text, string home)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrEmpty(home);

        text = text.Replace(home, Home, StringComparison.Ordinal);
        text = AnyAddress().Replace(text, Address);
        text = Keyed(text, "email", Address, keepEmpty: true);
        text = Keyed(text, "organization", Organization, keepEmpty: false);
        return Keyed(text, "atis", string.Empty, keepEmpty: false);
    }

    // Every value under the key, as the text spells it, escapes and all.
    public static IEnumerable<string> Values(string text, string key)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (var (start, length) in Spans(text, key))
        {
            yield return text.Substring(start, length);
        }
    }

    private static string Keyed(string text, string key, string value, bool keepEmpty)
    {
        var scrubbed = new StringBuilder(text.Length);
        var copied = 0;
        foreach (var (start, length) in Spans(text, key))
        {
            if (start < copied || (keepEmpty && length == 0))
            {
                continue;
            }

            scrubbed.Append(text, copied, start - copied).Append(value);
            copied = start + length;
        }

        return scrubbed.Append(text, copied, text.Length - copied).ToString();
    }

    // Where each value under the key starts and how long it is. A string nested d deep opens and
    // closes on a quote after n = 2^d - 1 backslashes. Inside it, a quote of its own comes after
    // 2n + 1, and each backslash of its own is 2n + 2 more, so the quote that closes it is the first
    // whose run r is n and any number of the value's own backslashes: r - n a multiple of 2n + 2.
    private static IEnumerable<(int Start, int Length)> Spans(string text, string key)
    {
        foreach (Match opened in Opening().Matches(text).Where(match => match.Groups["key"].Value == key))
        {
            var depth = opened.Groups["open"].Length;
            var start = opened.Index + opened.Length;
            var run = 0;
            for (var index = start; index < text.Length; index++)
            {
                if (text[index] == '\\')
                {
                    run++;
                    continue;
                }

                if (text[index] == '"' && run >= depth && (run - depth) % (2 * depth + 2) == 0)
                {
                    yield return (start, index - depth - start);
                    break;
                }

                run = 0;
            }
        }
    }

    // An address starts at no character an escape holds: not the letter after a backslash, and not
    // a digit of a \u escape.
    [GeneratedRegex("(?<!\\\\(?:u[0-9A-Fa-f]{0,3})?)[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\\.[A-Za-z0-9-]+)*\\.[A-Za-z]{2,}")]
    private static partial Regex AnyAddress();

    [GeneratedRegex("\\\\*\"(?<key>email|organization|atis)\\\\*\":\\s*(?<open>\\\\*)\"")]
    private static partial Regex Opening();
}
