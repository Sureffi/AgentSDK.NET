using System.Globalization;
using System.Text;

namespace Claude.Agent;

// The text rules the transcript verbs share with the binary, which is JavaScript: its whitespace, as
// String.prototype.trim and a \s class read it; a session title, as /rename takes one; and the
// sanitiser, NFKC and then every format, private-use and unassigned character dropped, to a fixed
// point or ten passes, whichever comes first.
internal static class UnicodeText
{
    private const int MaxPasses = 10;

    // The most code points a title keeps.
    private const int TitleLimit = 200;

    public static string Sanitize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var current = value;
        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var previous = current;
            current = Strip(Normalize(current));
            if (string.Equals(current, previous, StringComparison.Ordinal))
            {
                break;
            }
        }

        return current;
    }

    // The ends trimmed of JavaScript's whitespace. It is not string.Trim: it takes U+FEFF, and leaves
    // U+0085 and the four C0 separators U+001C-U+001F.
    public static string Trim(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var start = 0;
        var end = value.Length;
        while (start < end && IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end > start && IsWhiteSpace(value[end - 1]))
        {
            end--;
        }

        return value[start..end];
    }

    // Every run of whitespace made one space, and the ends trimmed.
    public static string Collapse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var collapsed = new StringBuilder(value.Length);
        var inRun = false;
        foreach (var c in value)
        {
            if (IsWhiteSpace(c))
            {
                if (!inRun)
                {
                    collapsed.Append(' ');
                }

                inRun = true;
                continue;
            }

            collapsed.Append(c);
            inRun = false;
        }

        return Trim(collapsed.ToString());
    }

    // A session title as the binary's /rename takes one: trimmed; every run of control and format
    // characters and line and paragraph separators made one space; cut to its first 200 code points,
    // a surrogate without its partner counting as one; trimmed again. Empty when nothing is left.
    public static string Title(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var trimmed = Trim(value);
        var title = new StringBuilder(trimmed.Length);
        var codePoints = 0;
        var inRun = false;
        for (var i = 0; i < trimmed.Length && codePoints < TitleLimit; i++)
        {
            var paired = char.IsHighSurrogate(trimmed[i]) && i + 1 < trimmed.Length && char.IsLowSurrogate(trimmed[i + 1]);
            var category = paired
                ? Rune.GetUnicodeCategory(new Rune(trimmed[i], trimmed[i + 1]))
                : CharUnicodeInfo.GetUnicodeCategory(trimmed[i]);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format
                or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
            {
                if (!inRun)
                {
                    title.Append(' ');
                    codePoints++;
                }

                inRun = true;
            }
            else
            {
                title.Append(trimmed[i]);
                if (paired)
                {
                    title.Append(trimmed[i + 1]);
                }

                codePoints++;
                inRun = false;
            }

            if (paired)
            {
                i++;
            }
        }

        return Trim(title.ToString());
    }

    // JavaScript's whitespace: tab, line feed, vertical tab, form feed, carriage return, the space
    // separators, the line and paragraph separators and U+FEFF.
    public static bool IsWhiteSpace(char c) =>
        c is '\t' or '\n' or '\v' or '\f' or '\r' or '\u2028' or '\u2029' or '\uFEFF'
        || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.SpaceSeparator;

    // The tree builds with InvariantGlobalization, under which string.Normalize hands its input back
    // unchanged and a fullwidth A survives unfolded. That property is inert for
    // a class library, so a consumer on an ordinary host gets the fold; it is this repo's own test
    // host that does not, and TranscriptPathTests pins the same no-op for the NFC half of the paths.
    // The call therefore stands, and it is guarded: ICU refuses a string holding an unpaired
    // surrogate, so only the runs between such surrogates are handed to it.
    private static string Normalize(string value)
    {
        var lone = FirstLoneSurrogate(value, 0);
        if (lone < 0)
        {
            return value.Normalize(NormalizationForm.FormKC);
        }

        // A lone surrogate never composes, so normalising each side of one independently is the same
        // answer as normalising the whole string.
        var built = new StringBuilder(value.Length);
        var start = 0;
        while (lone >= 0)
        {
            built.Append(value[start..lone].Normalize(NormalizationForm.FormKC));
            built.Append(value[lone]);
            start = lone + 1;
            lone = FirstLoneSurrogate(value, start);
        }

        return built.Append(value[start..].Normalize(NormalizationForm.FormKC)).ToString();
    }

    private static int FirstLoneSurrogate(string value, int from)
    {
        for (var i = from; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                i++;
                continue;
            }

            if (char.IsSurrogate(value[i]))
            {
                return i;
            }
        }

        return -1;
    }

    // By code point, not by char: a private-use code point above the BMP is two surrogates to a char
    // loop and neither half carries the category the pair has. An unpaired surrogate is its own code
    // point of category Surrogate, which is in neither strip set, so it survives here as it does in
    // the sanitiser — Rune.EnumerateRunes would have rewritten it to U+FFFD. The categories answer
    // for all of it.
    private static string Strip(string value)
    {
        var kept = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var paired = char.IsHighSurrogate(value[i])
                && i + 1 < value.Length
                && char.IsLowSurrogate(value[i + 1]);
            var category = paired
                ? Rune.GetUnicodeCategory(new Rune(value[i], value[i + 1]))
                : CharUnicodeInfo.GetUnicodeCategory(value[i]);

            if (category is not (UnicodeCategory.Format
                or UnicodeCategory.PrivateUse
                or UnicodeCategory.OtherNotAssigned))
            {
                kept.Append(value[i]);
                if (paired)
                {
                    kept.Append(value[i + 1]);
                }
            }

            if (paired)
            {
                i++;
            }
        }

        return kept.ToString();
    }
}
