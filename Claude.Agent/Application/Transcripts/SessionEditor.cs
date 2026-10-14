
namespace Claude.Agent;

// The writes behind ClaudeSessionClient: the configuration directory's transcripts edited, and the
// user's own directory read by the import alone.
internal sealed class SessionEditor
{
    private readonly TranscriptWorkspace _workspace;
    private readonly TranscriptWorkspace _user;

    public SessionEditor(TranscriptDirectory transcripts, TranscriptDirectory user)
    {
        ArgumentNullException.ThrowIfNull(transcripts);
        ArgumentNullException.ThrowIfNull(user);

        _workspace = new TranscriptWorkspace(transcripts);
        _user = new TranscriptWorkspace(user);
    }

    public ValueTask<Result<Unit, Error>> RenameAsync(
        string session,
        string title,
        string? cwd,
        CancellationToken ct) =>
        RenameSessionHandler.Handle(new RenameSession(session, title, cwd), _workspace, ct);

    public ValueTask<Result<Unit, Error>> TagAsync(
        string session,
        string? tag,
        string? cwd,
        CancellationToken ct) =>
        TagSessionHandler.Handle(new TagSession(session, tag, cwd), _workspace, ct);

    public ValueTask<Result<Unit, Error>> DeleteAsync(
        string session,
        string? cwd,
        CancellationToken ct) =>
        DeleteSessionHandler.Handle(new DeleteSession(session, cwd), _workspace, ct);

    public ValueTask<Result<string, Error>> ForkAsync(
        string session,
        string? upTo,
        string? title,
        string? cwd,
        CancellationToken ct) =>
        ForkSessionHandler.Handle(new ForkSessionAt(session, upTo, title, cwd), _workspace, ct);

    public ValueTask<Result<Unit, Error>> ImportAsync(
        string session,
        string? cwd,
        bool includeSubagents,
        CancellationToken ct) =>
        ImportSessionHandler.Handle(
            new ImportSession(session, cwd, includeSubagents),
            _user,
            _workspace.Transcripts,
            ct);
}
