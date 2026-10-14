using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Claude.Agent;

// The one fork transform, as the binary's /branch writes a fork: which entries it copies, how each is
// rewritten, the lines that follow them and the title it is given.
internal static class SessionFork
{
    // The longest a derived title's base runs, in UTF-16 code units.
    public const int TitleLimit = 100;

    // The entries the fork copies: those of the conversation the source resolves to that are no
    // sidechain, up to and with the entry upTo names. Refused when nothing is left to copy, or upTo
    // names no entry of them.
    public static Result<ImmutableArray<SessionEntry>, Error> Slice(
        string source,
        IReadOnlyList<SessionEntry> conversation,
        string? upTo)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        var kept = ImmutableArray.CreateRange(conversation.Where(entry => entry.Uuid is not null && !entry.Flag("isSidechain")));
        if (kept.IsEmpty)
        {
            return new ForkError($"Session {source} holds no conversation to copy into a fork.");
        }

        if (upTo is not { } cutoffId)
        {
            return kept;
        }

        for (var i = 0; i < kept.Length; i++)
        {
            if (kept[i].Uuid == cutoffId)
            {
                return ImmutableArray.Create(kept.AsSpan(0, i + 1));
            }
        }

        return new ForkError($"Session {source} holds no message {cutoffId} for a fork to end at.");
    }

    // The copied entries, in the conversation's order. Each keeps its uuid, its timestamp and every
    // key it carries, in its own order; the fork rewrites four and adds one. sessionId is the fork's;
    // parentUuid is the entry copied before it, none for the first; isSidechain is false; sessionKind
    // goes; forkedFrom names the source and the entry's uuid. A key the entry already carries keeps
    // its place. A system entry of subtype model_refusal_fallback is marked neutralizedByFork.
    public static ImmutableArray<SessionEntry> Copy(string source, ImmutableArray<SessionEntry> kept, string forked)
    {
        var lines = ImmutableArray.CreateBuilder<SessionEntry>(kept.Length);
        string? parent = null;
        foreach (var entry in kept)
        {
            var json = entry.Json;
            if (entry.Type == "system" && entry.Field("subtype") == "model_refusal_fallback")
            {
                json["neutralizedByFork"] = true;
            }

            json["sessionId"] = forked;
            json["parentUuid"] = parent;
            json["isSidechain"] = false;
            json.Remove("sessionKind");
            json["forkedFrom"] = new JsonObject
            {
                ["sessionId"] = source,
                ["messageUuid"] = entry.Uuid,
            };

            lines.Add(new SessionEntry(json));
            parent = entry.Uuid;
        }

        return lines.MoveToImmutable();
    }

    // What the binary writes after the copies: the source's latch, when the source carries one, then
    // the title line and the agent-name line, both with the fork's title.
    public static ImmutableArray<SessionEntry> Trailer(
        IEnumerable<SessionEntry> source,
        string sourceSession,
        string title,
        string forked)
    {
        var trailer = ImmutableArray.CreateBuilder<SessionEntry>(3);
        if (Latch(source, sourceSession) is { } latch)
        {
            trailer.Add(new SessionEntry(new JsonObject
            {
                ["type"] = "atis-latch",
                ["sessionId"] = forked,
                ["atis"] = latch,
            }));
        }

        trailer.Add(new SessionEntry(TitleLine(title, forked)));
        trailer.Add(new SessionEntry(AgentNameLine(title, forked)));
        return trailer.ToImmutable();
    }

    // The title line the binary writes, in its key order: type, customTitle, sessionId.
    public static JsonObject TitleLine(string title, string forked) => new()
    {
        ["type"] = "custom-title",
        ["customTitle"] = title,
        ["sessionId"] = forked,
    };

    // The agent-name line the binary writes beside it, in its key order: type, agentName, sessionId.
    public static JsonObject AgentNameLine(string title, string forked) => new()
    {
        ["type"] = "agent-name",
        ["agentName"] = title,
        ["sessionId"] = forked,
    };

    // A title the caller gave, as the binary takes one: its runs of whitespace made one space and its
    // ends trimmed. Null when nothing is left, and the title is derived.
    public static string? GivenTitle(string? title) =>
        title is null ? null : TranscriptScan.Empty(UnicodeText.Collapse(title));

    // What a derived title is built on: the fork's first prompt by the first-prompt rule, or the
    // command name standing in for it, with its runs of whitespace made one space, trimmed, cut at
    // the limit and trimmed again, no ellipsis added; "Branched conversation" when nothing is left.
    // A cut that would split a surrogate pair drops its high half.
    public static string BaseTitle(IEnumerable<SessionEntry> kept)
    {
        var (prompt, commandFallback) = TranscriptScan.FirstPromptParts(kept);
        var text = UnicodeText.Collapse(prompt ?? commandFallback ?? string.Empty);
        if (text.Length > TitleLimit)
        {
            var end = char.IsHighSurrogate(text[TitleLimit - 1]) ? TitleLimit - 1 : TitleLimit;
            text = UnicodeText.Trim(text[..end]);
        }

        return text.Length == 0 ? "Branched conversation" : text;
    }

    // A derived title: the base and " (Branch)". When a session is listed under that title — compared
    // as the binary compares, lowercased with both sides trimmed — the base takes " (Branch N)"
    // instead, N the least from 2 that no session's renamed title "<base> (Branch N)", read exactly,
    // holds; a bare "<base> (Branch)" holds 1. A session is listed under its renamed title, else its
    // generated one; the binary reads the numbers off renamed titles alone.
    public static string BranchTitle(string baseTitle, IEnumerable<(string? Listed, string? Renamed)> sessions)
    {
        ArgumentNullException.ThrowIfNull(baseTitle);
        ArgumentNullException.ThrowIfNull(sessions);

        var candidate = baseTitle + " (Branch)";
        var sought = Lowered(candidate);
        var carried = false;
        var taken = new HashSet<int> { 1 };
        foreach (var (listed, renamed) in sessions)
        {
            carried |= !string.IsNullOrEmpty(listed) && string.Equals(Lowered(listed), sought, StringComparison.Ordinal);
            if (!string.IsNullOrEmpty(renamed) && BranchNumber(renamed, baseTitle) is { } number)
            {
                taken.Add(number);
            }
        }

        if (!carried)
        {
            return candidate;
        }

        var next = 2;
        while (taken.Contains(next))
        {
            next++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{baseTitle} (Branch {next})");
    }

    // The source session's latch: the atis of its last atis-latch line, when that is printable ASCII
    // with no space, as the binary takes a latch when it loads a session.
    private static string? Latch(IEnumerable<SessionEntry> source, string sourceSession)
    {
        string? latch = null;
        foreach (var entry in source)
        {
            if (entry.Type == "atis-latch"
                && entry.Field("sessionId") == sourceSession
                && entry.Field("atis") is { } atis
                && atis.All(c => c is >= '!' and <= '~'))
            {
                latch = atis;
            }
        }

        return latch;
    }

    // The number a title "<base> (Branch N)" holds, 1 for "<base> (Branch)", and null for any other
    // title. N is ASCII digits; one past int's range can never be the least free, and reads as null.
    private static int? BranchNumber(string title, string baseTitle)
    {
        var opening = baseTitle + " (Branch";
        if (title.Length <= opening.Length
            || !title.StartsWith(opening, StringComparison.Ordinal)
            || title[^1] != ')')
        {
            return null;
        }

        var middle = title.AsSpan(opening.Length, title.Length - opening.Length - 1);
        if (middle.IsEmpty)
        {
            return 1;
        }

        if (middle.Length < 2 || middle[0] != ' ')
        {
            return null;
        }

        var digits = middle[1..];
        foreach (var digit in digits)
        {
            if (!char.IsAsciiDigit(digit))
            {
                return null;
            }
        }

        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    private static string Lowered(string title) => UnicodeText.Trim(title).ToLowerInvariant();
}
