using System.Text;

namespace Claude.Agent;

// A JSON string may carry one half of a surrogate pair as a \uXXXX escape with no other half beside
// it. JsonDocument.Parse and JsonNode.Parse both take such a line; reading that one string throws
// InvalidOperationException — not JsonException — wherever it is read, which is anywhere a parser
// reaches. A line is dealt with once, where it arrives, instead: a frame carrying one does not parse,
// and a transcript line is read with each such half made U+FFFD.
//
// Node writes them: JSON.stringify has escaped a lone surrogate as \udXXX since ES2019, so a string
// cut through a pair on that side is written exactly this way, as the binary's /branch title cut at
// 100 UTF-16 units can be.
internal static class JsonSurrogateEscapes
{
    private const string ReplacementEscape = "\\ufffd";

    public static bool Unpaired(string text) => NextUnpaired(text, 0) >= 0;

    // The text with each escaped half of a pair that has no other half made \ufffd, the replacement
    // character; the text itself when there is none.
    public static string ReplaceUnpaired(string text)
    {
        var escape = NextUnpaired(text, 0);
        if (escape < 0)
        {
            return text;
        }

        var replaced = new StringBuilder(text.Length);
        var copied = 0;
        while (escape >= 0)
        {
            replaced.Append(text, copied, escape - copied).Append(ReplacementEscape);
            copied = escape + 6;
            escape = NextUnpaired(text, copied);
        }

        return replaced.Append(text, copied, text.Length - copied).ToString();
    }

    // Where the first \uXXXX escape at or after `from` that is half a pair without its other half
    // starts, or -1.
    private static int NextUnpaired(string text, int from)
    {
        var index = from;
        while (index < text.Length)
        {
            var slash = text.IndexOf('\\', index);
            if (slash < 0)
            {
                return -1;
            }

            // A run of backslashes is half that many literal ones; an odd run leaves the last to
            // escape what follows it.
            var run = 0;
            while (slash + run < text.Length && text[slash + run] == '\\')
            {
                run++;
            }

            if (run % 2 == 0)
            {
                index = slash + run;
                continue;
            }

            var escape = slash + run - 1;
            if (!Utf16(text, escape, out var value))
            {
                index = escape + 2;
                continue;
            }

            if (char.IsLowSurrogate(value))
            {
                return escape;
            }

            if (!char.IsHighSurrogate(value))
            {
                index = escape + 6;
                continue;
            }

            if (!Utf16(text, escape + 6, out var low) || !char.IsLowSurrogate(low))
            {
                return escape;
            }

            index = escape + 12;
        }

        return -1;
    }

    // The \uXXXX at this position, when one is there: the backslash, the u, and four hex digits.
    private static bool Utf16(string text, int escape, out char value)
    {
        value = default;
        if (escape + 5 >= text.Length || text[escape] != '\\' || text[escape + 1] != 'u')
        {
            return false;
        }

        var code = 0;
        for (var digit = escape + 2; digit <= escape + 5; digit++)
        {
            var place = Digit(text[digit]);
            if (place < 0)
            {
                return false;
            }

            code = (code * 16) + place;
        }

        value = (char)code;
        return true;
    }

    private static int Digit(char character) => character switch
    {
        >= '0' and <= '9' => character - '0',
        >= 'a' and <= 'f' => character - 'a' + 10,
        >= 'A' and <= 'F' => character - 'A' + 10,
        _ => -1,
    };
}
