using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Claude.Agent;

// The reads behind ClaudeSessionClient, over one configuration directory's transcripts. A listing
// reads 64 KiB off each end of every transcript and stats it; a session's messages read the whole
// file.
internal sealed class SessionReader
{
    // The longest project name the sanitiser leaves whole; a longer one is cut to this and hashed.
    private const int MaxSanitizedLength = 200;

    private const string AgentPrefix = "agent-";

    private readonly TranscriptDirectory _directory;
    private readonly ITranscriptLayout _layout;

    public SessionReader(TranscriptDirectory directory)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _layout = directory.Layout;
    }

    public ValueTask<Result<ImmutableArray<SessionInfo>, Error>> ListAsync(
        SessionQueryOptions options,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        ct.ThrowIfCancellationRequested();

        return ValueTask.FromResult<Result<ImmutableArray<SessionInfo>, Error>>(List(Scopes(options), options, ct));
    }

    // One project's sessions by its key, unpaged and without its worktrees: the titles a fork's
    // derived title is set apart from.
    public ImmutableArray<SessionInfo> ListProject(string project, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        ct.ThrowIfCancellationRequested();

        return List([new Scope(project, null)], new SessionQueryOptions(), ct);
    }

    public ValueTask<Result<SessionInfo?, Error>> GetInfoAsync(
        string session,
        string? cwd,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // A session id that is not a uuid names no transcript: answered before any I/O.
        if (!Ids.IsUuid(session))
        {
            return ValueTask.FromResult(Result<SessionInfo?, Error>.Ok(null));
        }

        foreach (var scope in ProbeScopes(cwd))
        {
            if (ProjectDirectory(scope) is { } directory && TranscriptDirectory.Transcript(directory, session) is { } file)
            {
                return ValueTask.FromResult(Result<SessionInfo?, Error>.Ok(TranscriptDirectory.Info(session, file, scope.Path)));
            }
        }

        return ValueTask.FromResult(Result<SessionInfo?, Error>.Ok(null));
    }

    public ValueTask<Result<ImmutableArray<SessionMessage>, Error>> GetMessagesAsync(
        string session,
        SessionQueryOptions options,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        ct.ThrowIfCancellationRequested();

        return ValueTask.FromResult<Result<ImmutableArray<SessionMessage>, Error>>(Messages(session, options));
    }

    public ValueTask<Result<ImmutableArray<string>, Error>> ListSubagentsAsync(
        string session,
        string? cwd,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return ValueTask.FromResult<Result<ImmutableArray<string>, Error>>(Subagents(session, cwd));
    }

    public ValueTask<Result<ImmutableArray<SessionMessage>, Error>> GetSubagentMessagesAsync(
        string session,
        string agent,
        SessionQueryOptions options,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        ct.ThrowIfCancellationRequested();

        return ValueTask.FromResult<Result<ImmutableArray<SessionMessage>, Error>>(SubagentMessages(session, agent, options));
    }

    private ImmutableArray<SessionMessage> Messages(string session, SessionQueryOptions options)
    {
        if (!Ids.IsUuid(session))
        {
            return [];
        }

        foreach (var scope in ProbeScopes(options.WorkingDirectory))
        {
            if (ProjectDirectory(scope) is not { } directory
                || TranscriptDirectory.Transcript(directory, session) is not { } file
                || TranscriptDirectory.Entries(file.FullName) is not { IsEmpty: false } entries)
            {
                continue;
            }

            var visible = TranscriptChain.Resolve(entries)
                .Where(IsVisible)
                .Select(entry => Message(entry, session));
            return Paged([.. visible], options.Limit, options.Offset);
        }

        return [];
    }

    private ImmutableArray<string> Subagents(string session, string? cwd)
    {
        // The uuid guard runs before any read: an id that names no session is empty.
        if (!Ids.IsUuid(session))
        {
            return [];
        }

        foreach (var scope in ProbeScopes(cwd))
        {
            if (ProjectDirectory(scope) is { } directory
                && Agents(TranscriptDirectory.Subagents(directory, session)) is { IsEmpty: false } agents)
            {
                return agents;
            }
        }

        return [];
    }

    private ImmutableArray<SessionMessage> SubagentMessages(string session, string agent, SessionQueryOptions options)
    {
        // A session id that is not a uuid returns empty before any I/O.
        if (!Ids.IsUuid(session))
        {
            return [];
        }

        foreach (var scope in ProbeScopes(options.WorkingDirectory))
        {
            if (ProjectDirectory(scope) is not { } directory || Subagent(directory, session, agent) is not { } subagent)
            {
                continue;
            }

            // The first non-empty transcript of the agent answers, with whatever its lines hold.
            var path = TranscriptDirectory.SubagentTranscript(directory, session, subagent);
            if (new FileInfo(path) is not { Exists: true, Length: > 0 })
            {
                continue;
            }

            ImmutableArray<SessionEntry> transcript = [.. TranscriptDirectory.Entries(path).Where(TranscriptChain.IsConversation)];

            // The sidecar says which tool use started the agent, and which agent started it.
            var meta = TranscriptDirectory.Metadata(path);
            var tool = meta is not null && SessionEntry.Text(meta, "toolUseId") is { Length: > 0 } toolUse
                ? toolUse
                : null;
            var parent = meta is not null && SessionEntry.Text(meta, "parentAgentId") is { Length: > 0 } parentAgent
                ? parentAgent
                : null;

            // A subagent read does not apply the visibility filter: an isMeta or isSidechain line
            // inside a subagent transcript is kept, and every line in one is a sidechain line.
            var messages = SubagentChain(transcript)
                .Where(entry => entry.Type is "user" or "assistant")
                .Select(entry => Message(entry, session, tool, parent));
            return Paged([.. messages], options.Limit, options.Offset);
        }

        return [];
    }

    private string? ProjectDirectory(Scope scope) => _directory.ProjectDirectory(scope.Project);

    // The agent ids of the subagent transcripts TranscriptDirectory.Subagents names, each of which
    // ends in agent-<id>, once each and in its order.
    private static ImmutableArray<string> Agents(ImmutableArray<string> subagents)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var agents = ImmutableArray.CreateBuilder<string>();
        foreach (var subagent in subagents)
        {
            var last = subagent[(subagent.LastIndexOf('/') + 1)..];

            // A bare "agent-" names no agent.
            if (last[AgentPrefix.Length..] is { Length: > 0 } agent && seen.Add(agent))
            {
                agents.Add(agent);
            }
        }

        return agents.ToImmutable();
    }

    // The subagent's transcript among the session's, and none when no transcript is the agent's.
    private static string? Subagent(string directory, string session, string agent)
    {
        var target = AgentPrefix + agent;
        foreach (var subagent in TranscriptDirectory.Subagents(directory, session))
        {
            if (subagent[(subagent.LastIndexOf('/') + 1)..] == target)
            {
                return subagent;
            }
        }

        return null;
    }

    // Every transcript of every scope, the sidechains and the ones with nothing to summarise dropped,
    // one row a session, newest first, and only then paged.
    private ImmutableArray<SessionInfo> List(ImmutableArray<Scope> scopes, SessionQueryOptions options, CancellationToken ct)
    {
        var slots = new List<Slot>();
        foreach (var scope in scopes)
        {
            if (ProjectDirectory(scope) is not { } directory)
            {
                continue;
            }

            foreach (var (session, file) in TranscriptDirectory.Transcripts(directory, ct))
            {
                if (TranscriptDirectory.Info(session, file, scope.Path) is { } info)
                {
                    slots.Add(new Slot(session, info));
                }
            }
        }

        return [.. Paginate(Deduplicate(slots), options).Select(slot => slot.Info)];
    }

    // The same session in two project directories keeps the newer row, and a tie keeps the one
    // seen first.
    private static List<Slot> Deduplicate(List<Slot> slots)
    {
        var at = new Dictionary<string, int>(StringComparer.Ordinal);
        var kept = new List<Slot>();
        foreach (var slot in slots)
        {
            if (!at.TryGetValue(slot.Session, out var index))
            {
                at[slot.Session] = kept.Count;
                kept.Add(slot);
                continue;
            }

            if (slot.Info.LastModified > kept[index].Info.LastModified)
            {
                kept[index] = slot;
            }
        }

        return kept;
    }

    private static List<Slot> Paginate(List<Slot> slots, SessionQueryOptions options) =>
        [.. Paged([.. slots.OrderByDescending(slot => slot.Info.LastModified)], options.Limit, options.Offset)];

    // A negative offset is clamped to zero: a page cannot start behind the first row.
    private static ImmutableArray<T> Paged<T>(ImmutableArray<T> items, int? limit, int offset)
    {
        var rest = items[Math.Min(Math.Max(offset, 0), items.Length)..];
        return limit is { } take && take > 0 ? rest[..Math.Min(take, rest.Length)] : rest;
    }

    private static string Uuid(SessionEntry entry) => entry.Uuid!;

    private static string? Parent(SessionEntry entry) => entry.Field("parentUuid") is { Length: > 0 } parent
        ? parent
        : null;

    // A turn a person or the model wrote into the conversation itself. Each flag is read as the
    // binary reads a condition, so "isMeta":1 hides a line and "isMeta":0 does not. A compaction's
    // summary is a user turn and stays: after a compaction it is what holds the turns before it.
    private static bool IsVisible(SessionEntry entry) =>
        entry.Type is "user" or "assistant"
        && !entry.IsTruthy("isMeta")
        && !entry.IsTruthy("isSidechain")
        && !entry.IsTruthy("teamName");

    private static SessionMessage Message(
        SessionEntry entry,
        string session,
        string? tool = null,
        string? agent = null)
    {
        // An entry with no sessionId, or an empty one, carries the session it was read under.
        var carried = entry.Field("sessionId") is { Length: > 0 } own
            ? own
            : session;

        var (content, api) = Body(entry);
        return new SessionMessage(
            entry.Type == "user" ? SessionMessageRole.User : SessionMessageRole.Assistant,
            entry.Uuid!,
            carried,
            content)
        {
            Message = api,
            ParentToolUseId = tool,
            ParentAgentId = agent,
        };
    }

    // A transcript line's `message`: the API's own message on a turn the model wrote, and content
    // alone on a person's. A line whose message does not read is a turn with no content, never a
    // read that fails: the transcript is the binary's, and a caller asked for what is in it.
    private static (IReadOnlyList<ContentBlock> Content, ApiMessage? Message) Body(SessionEntry entry)
    {
        if (entry.Json["message"] is not JsonObject body)
        {
            return ([], null);
        }

        using var document = JsonDocument.Parse(body.ToJsonString(), SessionEntry.ReadOptions);
        var element = document.RootElement.Clone();
        var where = entry.ToCompactJson();
        if (entry.Type == "assistant")
        {
            var api = MessageShapeParser.ReadApiMessage(element, where, "message");
            return (api?.Content ?? [], api);
        }

        return element.ElementOrNull("content") is { } content
            && ContentBlockParser.ParseContent(content, where, "message.content").TryGet(out var blocks, out _)
                ? (blocks, null)
                : ([], null);
    }

    // A subagent's transcript is one agent's run, every line of it a sidechain: its leaf is its last
    // user or assistant entry in file order, and its conversation the walk back from there.
    private static List<SessionEntry> SubagentChain(ImmutableArray<SessionEntry> entries)
    {
        var byUuid = new Dictionary<string, SessionEntry>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            byUuid[Uuid(entry)] = entry;
        }

        for (var i = entries.Length - 1; i >= 0; i--)
        {
            if (entries[i].Type is "user" or "assistant")
            {
                return WalkBack(entries[i], byUuid);
            }
        }

        return [];
    }

    private static List<SessionEntry> WalkBack(SessionEntry leaf, Dictionary<string, SessionEntry> byUuid)
    {
        var chain = new List<SessionEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = leaf; current is not null; current = Next(current, byUuid))
        {
            if (!seen.Add(Uuid(current)))
            {
                break;
            }

            chain.Add(current);
        }

        chain.Reverse();
        return chain;
    }

    private static SessionEntry? Next(SessionEntry entry, Dictionary<string, SessionEntry> byUuid) =>
        Parent(entry) is { } parent && byUuid.TryGetValue(parent, out var next) ? next : null;

    // Where a single-session read looks: the named directory and then its sibling worktrees, or every
    // project. A scope that reaches a project already offered is dropped. The editor's candidate walk
    // is the same list (TranscriptWorkspace.Directories), so every verb searches alike.
    private ImmutableArray<Scope> ProbeScopes(string? cwd)
    {
        if (cwd is { } directory)
        {
            var widened = ImmutableArray.CreateBuilder<Scope>();
            var offered = new HashSet<string>(StringComparer.Ordinal);
            Offer(widened, offered, directory);
            foreach (var worktree in Worktrees(directory))
            {
                if (Path.IsPathRooted(worktree))
                {
                    Offer(widened, offered, worktree);
                }
            }

            return widened.ToImmutable();
        }

        return [.. _directory.Projects().Select(project => new Scope(project, null))];
    }

    private ImmutableArray<Scope> Scopes(SessionQueryOptions options)
    {
        if (options.WorkingDirectory is not { } cwd)
        {
            return ProbeScopes(null);
        }

        var single = new Scope(_layout.ProjectFor(cwd), _layout.CanonicalPath(cwd));
        if (!options.IncludeWorktrees)
        {
            return ImmutableArray.Create(single);
        }

        // One path or none is a repository with no second worktree, and the matching below is skipped
        // entirely.
        var worktrees = Worktrees(cwd);
        if (worktrees.Length <= 1)
        {
            return ImmutableArray.Create(single);
        }

        var projects = _directory.Projects();
        if (projects.IsEmpty)
        {
            return ImmutableArray.Create(single);
        }

        // The names are the paths as they were printed, sanitized and not resolved: the project
        // directory on disk was named after the cwd the binary was handed, and that spelling is what
        // is compared. A spelling that names no project simply matches nothing. The longest name is
        // tried first.
        var folded = OperatingSystem.IsWindows();
        var indexed = worktrees
            .Select(path => (Path: path, Project: _layout.ProjectForSpelling(path)))
            .Where(pair => pair.Project is not null)
            .Select(pair => (pair.Path, Exact: Fold(pair.Project!, folded)))
            .OrderByDescending(pair => pair.Exact.Length)
            .ToArray();

        // The directory the caller named is always scanned first.
        var scopes = ImmutableArray.CreateBuilder<Scope>();
        scopes.Add(single);
        var seen = new HashSet<string>(StringComparer.Ordinal) { Fold(single.Project, folded) };

        foreach (var project in projects)
        {
            var name = Fold(project, folded);
            if (seen.Contains(name))
            {
                continue;
            }

            foreach (var (path, exact) in indexed)
            {
                // The binary's rule: the worktree's name itself; for a name shorter than the
                // sanitiser's limit, one that continues it after a dash, a subdirectory's among them;
                // for a name the sanitiser cut and hashed, one under its cut and a dash whose sessions
                // ran in that worktree. A name of exactly the limit matches itself alone.
                if (name == exact
                    || (exact.Length > MaxSanitizedLength
                        ? name.StartsWith(exact[..MaxSanitizedLength] + "-", StringComparison.Ordinal) && RanIn(project, path, folded)
                        : exact.Length < MaxSanitizedLength && name.StartsWith(exact + "-", StringComparison.Ordinal)))
                {
                    seen.Add(name);
                    scopes.Add(new Scope(project, path));
                    break;
                }
            }
        }

        return scopes.ToImmutable();
    }

    // Whether a session in the project's directory ran in the worktree: the first working directory
    // its head carries names the same project as the worktree's path.
    private bool RanIn(string project, string worktree, bool folded)
    {
        if (ProjectDirectory(new Scope(project, null)) is not { } directory
            || _layout.ProjectForSpelling(worktree) is not { } sought)
        {
            return false;
        }

        foreach (var (session, file) in TranscriptDirectory.Transcripts(directory, CancellationToken.None))
        {
            if (TranscriptDirectory.Info(session, file, null)?.WorkingDirectory is { } cwd
                && _layout.ProjectForSpelling(cwd) is { } ran
                && Fold(ran, folded) == Fold(sought, folded))
            {
                return true;
            }
        }

        return false;
    }

    private static string Fold(string name, bool folded) => folded ? name.ToLowerInvariant() : name;

    private void Offer(ImmutableArray<Scope>.Builder scopes, HashSet<string> offered, string cwd)
    {
        var project = _layout.ProjectFor(cwd);
        if (offered.Add(project))
        {
            scopes.Add(new Scope(project, _layout.CanonicalPath(cwd)));
        }
    }

    // The layout says it never throws for a path it cannot read; the call is guarded anyway, because
    // the runner underneath may be a host's own.
    private ImmutableArray<string> Worktrees(string cwd)
    {
        try
        {
            return _layout.WorktreePaths(cwd);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return [];
        }
    }

    private sealed record Scope(string Project, string? Path);

    // One row of a listing, by the session it is.
    private sealed record Slot(string Session, SessionInfo Info);
}
