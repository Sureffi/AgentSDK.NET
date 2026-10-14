using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Claude.Agent;

// What a listing reads off a transcript, and the first-prompt rule. A listing reads the whole lines
// of two windows, the first and the last WindowSize bytes, as the binary's listing reads its head and
// its tail; each field is read off the line kind the binary writes it on.
internal static partial class TranscriptScan
{
    // The bytes the binary's listing reads off each end of a transcript.
    public const int WindowSize = 65536;

    public const int FirstPromptLimit = 200;

    private const string PastedOpening = "<pasted_content id=\"";

    private const string PastedOpeningEnd = "\">\n";

    private const string PastedClosing = "</pasted_content id=\"";

    // Every line of a text that parses as a transcript entry, in order; a blank or corrupt one is
    // skipped.
    public static ImmutableArray<SessionEntry> Entries(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var entries = ImmutableArray.CreateBuilder<SessionEntry>();
        foreach (var line in text.Split('\n'))
        {
            if (line.Length != 0 && SessionEntry.Parse(line) is { IsOk: true } parsed)
            {
                entries.Add(parsed.Value);
            }
        }

        return entries.ToImmutable();
    }

    // A listing's row. No row for a session a head entry marks as a sidechain, and none for one with
    // nothing to summarise it by. The renamed title is the last custom-title line's, the tail's before
    // the head's; the generated title the last ai-title line's, likewise; the last prompt and the
    // summary are the tail's last; the tag is the tail's last tag line, an empty tag being none. The
    // git branch is the last one the tail carries, else the first in the head; the working directory
    // the first the head carries, else the project path; the creation time the head's first
    // timestamp.
    public static SessionInfo? Info(
        string session,
        IReadOnlyList<SessionEntry> head,
        IReadOnlyList<SessionEntry> tail,
        DateTimeOffset lastModified,
        long sizeBytes,
        string? projectPath)
    {
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(tail);

        if (head.Any(entry => entry.Flag("isSidechain")))
        {
            return null;
        }

        var renamedTitle = LastOfType(tail, "custom-title", "customTitle") ?? LastOfType(head, "custom-title", "customTitle");
        var customTitle = renamedTitle ?? LastOfType(tail, "ai-title", "aiTitle") ?? LastOfType(head, "ai-title", "aiTitle");
        var firstPrompt = Empty(FirstPrompt(head));
        var summary = customTitle
            ?? LastOfType(tail, "last-prompt", "lastPrompt")
            ?? LastOfType(tail, "summary", "summary")
            ?? firstPrompt;
        if (summary is null)
        {
            return null;
        }

        return new SessionInfo(session, summary, lastModified)
        {
            SizeBytes = sizeBytes,
            CustomTitle = customTitle,
            RenamedTitle = renamedTitle,
            FirstPrompt = firstPrompt,
            GitBranch = Carried(tail.Reverse(), "gitBranch") ?? Carried(head, "gitBranch"),
            WorkingDirectory = Carried(head, "cwd") ?? projectPath,
            Tag = Tag(tail),
            CreatedAt = CreatedAt(head) is { } created ? DateTimeOffset.FromUnixTimeMilliseconds(created) : null,
        };
    }

    // The last tag line's tag; an empty one clears it, and a tag key on any other line is not one.
    public static string? Tag(IEnumerable<SessionEntry> tail)
    {
        ArgumentNullException.ThrowIfNull(tail);

        return tail.LastOrDefault(entry => entry.Type == "tag") is { } line ? Empty(line.Field("tag")) : null;
    }

    public static long? CreatedAt(IEnumerable<SessionEntry> head) => EpochMs(Carried(head, "timestamp"));

    // A timestamp in the one form the binary writes, an ISO 8601 date and time with its offset, Z or
    // numeric; null for anything else.
    public static long? EpochMs(string? timestamp)
    {
        if (string.IsNullOrEmpty(timestamp) || !TimestampPattern().IsMatch(timestamp))
        {
            return null;
        }

        return DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToUnixTimeMilliseconds()
            : null;
    }

    public static string FirstPrompt(IEnumerable<SessionEntry> entries)
    {
        var (prompt, fallback) = FirstPromptParts(entries);
        return prompt ?? fallback ?? string.Empty;
    }

    // The first prompt of the entries, in order, and apart from it the command name that stands in
    // when there is none.
    public static (string? Prompt, string? CommandFallback) FirstPromptParts(IEnumerable<SessionEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        string? commandFallback = null;
        foreach (var entry in entries)
        {
            if (Prompt(entry, ref commandFallback) is { } prompt)
            {
                return (prompt, commandFallback);
            }
        }

        return (null, commandFallback);
    }

    // The binary's first-prompt rule over one entry. Only a user entry whose isMeta and
    // isCompactSummary are not true is read, and one carrying a tool_result block is passed over
    // whole. Each text block, a string content being one, has its pasted blocks expanded, its line
    // feeds made spaces, and is trimmed; then, in order: an empty text is passed over; a
    // <command-name> text is passed over, its name becoming commandFallback while that is still
    // empty; a <bash-input> text is the prompt, "! " and what the tags hold, trimmed and never cut; a
    // text IsPassedOver takes is passed over. The first text left is the prompt, cut at the limit.
    // Null when the entry holds none.
    public static string? Prompt(SessionEntry entry, ref string? commandFallback)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Type != "user"
            || entry.Flag("isMeta")
            || entry.Flag("isCompactSummary")
            || entry.CarriesToolResult())
        {
            return null;
        }

        foreach (var block in entry.TextBlocks())
        {
            var text = UnicodeText.Trim(ExpandPastedContent(block).Replace('\n', ' '));
            if (text.Length == 0)
            {
                continue;
            }

            if (CommandNamePattern().Match(text) is { Success: true } command)
            {
                if (string.IsNullOrEmpty(commandFallback))
                {
                    commandFallback = command.Groups[1].Value;
                }

                continue;
            }

            if (BashInputPattern().Match(text) is { Success: true } bash)
            {
                return "! " + UnicodeText.Trim(bash.Groups[1].Value);
            }

            if (IsPassedOver(text))
            {
                continue;
            }

            return Truncate(text);
        }

        return null;
    }

    // A text the first-prompt rule passes over: one that opens, after any whitespace, on a tag whose
    // name starts with a lowercase ASCII letter and is followed by whitespace or '>', or on
    // "[Request interrupted by user" and the bracket that closes it.
    public static bool IsPassedOver(string text) => PassedOverPattern().IsMatch(text);

    // The text with each pasted block the binary folded into a prompt replaced by what it holds. A
    // block opens on <pasted_content id="xxxx"> and a line feed, xxxx four lowercase hex digits, and
    // closes on a line feed and </pasted_content id="xxxx">; up to two line feeds before the opening
    // and after the closing go with it. An opening with no closing ends the expansion there.
    public static string ExpandPastedContent(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var expanded = new StringBuilder();
        var copied = 0;
        var searched = 0;
        var any = false;
        while (true)
        {
            var open = text.IndexOf(PastedOpening, searched, StringComparison.Ordinal);
            if (open < 0)
            {
                break;
            }

            var id = open + PastedOpening.Length;
            if (id + 4 > text.Length
                || !IsBlockId(text.AsSpan(id, 4))
                || !text.AsSpan(id + 4).StartsWith(PastedOpeningEnd, StringComparison.Ordinal))
            {
                searched = id;
                continue;
            }

            var body = id + 4 + PastedOpeningEnd.Length;
            var closing = string.Concat(PastedClosing, text.AsSpan(id, 4), "\">");
            var close = text.IndexOf("\n" + closing, body - 1, StringComparison.Ordinal) + 1;
            if (close == 0)
            {
                break;
            }

            var start = open;
            for (var fed = 0; fed < 2 && start > copied && text[start - 1] == '\n'; fed++)
            {
                start--;
            }

            expanded.Append(text, copied, start - copied);
            expanded.Append(text, body, Math.Max(0, close - 1 - body));
            copied = close + closing.Length;
            for (var fed = 0; fed < 2 && copied < text.Length && text[copied] == '\n'; fed++)
            {
                copied++;
            }

            searched = copied;
            any = true;
        }

        return any ? expanded.Append(text, copied, text.Length - copied).ToString() : text;
    }

    // The binary's cut: a text longer than 200 UTF-16 units is cut at 200, a high surrogate the cut
    // leaves last is dropped, the rest is trimmed as JavaScript trims, and an ellipsis ends it. One
    // step is this library's and not the binary's: a string it hands out holds no half of a pair, so
    // a surrogate the cut text holds without its partner becomes U+FFFD, where the binary's cut keeps
    // it.
    public static string Truncate(string result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Length <= FirstPromptLimit)
        {
            return result;
        }

        var cut = result[..FirstPromptLimit];
        if (char.IsHighSurrogate(cut[^1]))
        {
            cut = cut[..^1];
        }

        return UnicodeText.Trim(WellFormed(cut)) + "…";
    }

    public static IReadOnlyList<string> TextBlocks(JsonObject entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!entry.TryGetPropertyValue("message", out var node) || node is not JsonObject message)
        {
            return [];
        }

        if (!message.TryGetPropertyValue("content", out var content))
        {
            return [];
        }

        if (SessionEntry.Text(content) is { } whole)
        {
            return [whole];
        }

        if (content is not JsonArray blocks)
        {
            return [];
        }

        var texts = new List<string>();
        foreach (var block in blocks)
        {
            if (block is JsonObject typed
                && SessionEntry.Text(typed, "type") == "text"
                && SessionEntry.Text(typed, "text") is { } text)
            {
                texts.Add(text);
            }
        }

        return texts;
    }

    public static bool CarriesToolResult(JsonObject entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!entry.TryGetPropertyValue("message", out var node)
            || node is not JsonObject message
            || !message.TryGetPropertyValue("content", out var content)
            || content is not JsonArray blocks)
        {
            return false;
        }

        foreach (var block in blocks)
        {
            if (block is JsonObject typed && SessionEntry.Text(typed, "type") == "tool_result")
            {
                return true;
            }
        }

        return false;
    }

    internal static string? Empty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    // The value of the last line of a kind that carries the key as a non-empty string.
    private static string? LastOfType(IReadOnlyList<SessionEntry> entries, string type, string key)
    {
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].Type == type && Empty(entries[i].Field(key)) is { } value)
            {
                return value;
            }
        }

        return null;
    }

    // The first of the entries, in the order given, that carries the key as a non-empty string.
    private static string? Carried(IEnumerable<SessionEntry> entries, string key)
    {
        foreach (var entry in entries)
        {
            if (Empty(entry.Field(key)) is { } value)
            {
                return value;
            }
        }

        return null;
    }

    private static bool IsBlockId(ReadOnlySpan<char> id)
    {
        foreach (var c in id)
        {
            if (c is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
            {
                return false;
            }
        }

        return true;
    }

    // The text with each surrogate that has no partner made U+FFFD.
    private static string WellFormed(string text)
    {
        var units = text.ToCharArray();
        for (var index = 0; index < units.Length; index++)
        {
            if (char.IsHighSurrogate(units[index]) && index + 1 < units.Length && char.IsLowSurrogate(units[index + 1]))
            {
                index++;
            }
            else if (char.IsSurrogate(units[index]))
            {
                units[index] = '\uFFFD';
            }
        }

        return new string(units);
    }

    // The name is the shortest run to a closing tag on one line: it stops at a carriage return or a
    // line or paragraph separator, as the binary's match does.
    [GeneratedRegex(@"<command-name>([^\r\n\u2028\u2029]*?)</command-name>")]
    private static partial Regex CommandNamePattern();

    [GeneratedRegex(@"<bash-input>([\s\S]*?)</bash-input>")]
    private static partial Regex BashInputPattern();

    [GeneratedRegex(@"^(?:[\t\n\v\f\r\p{Zs}\u2028\u2029\uFEFF]*<[a-z][A-Za-z0-9_-]*[\t\n\v\f\r\p{Zs}\u2028\u2029\uFEFF>]|\[Request interrupted by user[^\]]*\])")]
    private static partial Regex PassedOverPattern();

    [GeneratedRegex(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]+)?(?:Z|[+-][0-9]{2}:[0-9]{2})$")]
    private static partial Regex TimestampPattern();
}
