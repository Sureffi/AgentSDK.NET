using System.Collections.Immutable;

namespace Claude.Agent;

// The conversation a transcript resolves to, as the binary resumes one: the parentUuid chain walked
// back from a leaf, root first.
internal static class TranscriptChain
{
    // The line kinds a conversation is made of.
    private static readonly string[] ConversationTypes = ["user", "assistant", "attachment", "system"];

    // The conversation's entries are those of the four kinds with a uuid; a uuid written twice is its
    // last line. The walk starts at the leaf the last last-prompt line names, one written after the
    // last compaction boundary, when it is an entry here; when the last entry that is no sidechain
    // descends from it, or no such leaf is named, at that last entry. From there it goes back to the
    // nearest user or assistant entry, the leaf, and on through the parents, stopping at an entry
    // with no parent here or at one it has already passed. Empty when no leaf is reached.
    public static ImmutableArray<SessionEntry> Resolve(IEnumerable<SessionEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var byUuid = new Dictionary<string, SessionEntry>(StringComparer.Ordinal);
        string? last = null;
        string? named = null;
        foreach (var entry in entries)
        {
            if (IsConversation(entry))
            {
                byUuid[entry.Uuid!] = entry;
                if (!entry.IsTruthy("isSidechain"))
                {
                    last = entry.Uuid;
                }

                if (entry.Type == "system" && entry.Field("subtype") == "compact_boundary")
                {
                    named = null;
                }
            }
            else if (entry.Type == "last-prompt" && entry.Field("leafUuid") is { Length: > 0 } leafUuid)
            {
                named = leafUuid;
            }
        }

        var start = named is not null && byUuid.ContainsKey(named) ? named : null;
        if (start is null || (last is not null && last != start && Descends(last, start, byUuid)))
        {
            start = last;
        }

        if (start is null || NearestTurn(byUuid[start], byUuid) is not { } leaf)
        {
            return [];
        }

        var chain = new List<SessionEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = leaf; current is not null && seen.Add(current.Uuid!); current = Parent(current, byUuid))
        {
            chain.Add(current);
        }

        chain.Reverse();
        return [.. chain];
    }

    public static bool IsConversation(SessionEntry entry) =>
        entry.Uuid is not null && entry.Type is { } type && Array.IndexOf(ConversationTypes, type) >= 0;

    // Whether the parent walk from the entry reaches the ancestor, the entry itself counting.
    private static bool Descends(string from, string ancestor, Dictionary<string, SessionEntry> byUuid)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (string? current = from; current is not null && seen.Add(current); current = byUuid.TryGetValue(current, out var entry) ? ParentUuid(entry) : null)
        {
            if (current == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    private static SessionEntry? NearestTurn(SessionEntry start, Dictionary<string, SessionEntry> byUuid)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = start; current is not null && seen.Add(current.Uuid!); current = Parent(current, byUuid))
        {
            if (current.Type is "user" or "assistant")
            {
                return current;
            }
        }

        return null;
    }

    private static SessionEntry? Parent(SessionEntry entry, Dictionary<string, SessionEntry> byUuid) =>
        ParentUuid(entry) is { } parent && byUuid.TryGetValue(parent, out var next) ? next : null;

    private static string? ParentUuid(SessionEntry entry) => entry.Field("parentUuid") is { Length: > 0 } parent ? parent : null;
}
