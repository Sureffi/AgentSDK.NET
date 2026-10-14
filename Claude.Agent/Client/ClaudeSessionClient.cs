namespace Claude.Agent;

/// <summary>
/// Reads and edits the transcripts the client's sessions wrote, in its
/// <see cref="ClaudeAgentClientOptions.ConfigurationDirectory"/>, and copies a session in from the
/// user's own directory. Obtained from <see cref="ClaudeAgentClient.GetSessionClient"/>.
/// </summary>
public class ClaudeSessionClient
{
    private readonly SessionReader _reader;
    private readonly SessionEditor _editor;

    /// <summary>For mocking.</summary>
    protected ClaudeSessionClient()
    {
        _reader = null!;
        _editor = null!;
    }

    // `transcripts` is the client's configuration directory, `user` the user's own.
    internal ClaudeSessionClient(TranscriptDirectory transcripts, TranscriptDirectory user)
    {
        _reader = new SessionReader(transcripts);
        _editor = new SessionEditor(transcripts, user);
    }

    /// <summary>The sessions of a project, newest first.</summary>
    public virtual async Task<IReadOnlyList<SessionInfo>> GetSessionsAsync(
        SessionQueryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var listed = await _reader.ListAsync(options ?? new SessionQueryOptions(), cancellationToken).ConfigureAwait(false);
        return listed.OrThrow();
    }

    /// <summary>One session, read as <see cref="GetSessionsAsync"/> reads each. Null when no project holds a transcript of it with anything in it, when a line at the transcript's head marks it a sidechain, and when nothing the listing reads of it gives it a summary: no title, no last prompt or summary line, no first prompt. A rename, a tag and a delete find a session of the last two all the same.</summary>
    public virtual async Task<SessionInfo?> GetSessionAsync(
        string sessionId,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        var info = await _reader.GetInfoAsync(sessionId, workingDirectory, cancellationToken).ConfigureAwait(false);
        return info.OrThrow();
    }

    /// <summary>The conversation's turns in order, without the meta and sidechain lines.</summary>
    public virtual async Task<IReadOnlyList<SessionMessage>> GetMessagesAsync(
        string sessionId,
        SessionQueryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        var messages = await _reader.GetMessagesAsync(sessionId, options ?? new SessionQueryOptions(), cancellationToken).ConfigureAwait(false);
        return messages.OrThrow();
    }

    /// <summary>The ids of the subagents the session ran.</summary>
    public virtual async Task<IReadOnlyList<string>> GetSubagentsAsync(
        string sessionId,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        var agents = await _reader.ListSubagentsAsync(sessionId, workingDirectory, cancellationToken).ConfigureAwait(false);
        return agents.OrThrow();
    }

    /// <summary>The turns of one subagent of a session, oldest first; empty when the session or the agent is not there.</summary>
    public virtual async Task<IReadOnlyList<SessionMessage>> GetSubagentMessagesAsync(
        string sessionId,
        string agentId,
        SessionQueryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentException.ThrowIfNullOrEmpty(agentId);
        var messages = await _reader.GetSubagentMessagesAsync(sessionId, agentId, options ?? new SessionQueryOptions(), cancellationToken).ConfigureAwait(false);
        return messages.OrThrow();
    }

    /// <summary>Gives the session a title, which its <see cref="SessionInfo.Summary"/> then shows. Half of a surrogate pair without its other half is written escaped, as JSON.stringify writes it, and reads back as U+FFFD. Throws <see cref="ClaudeAgentException"/> with SessionNotFound when the session is not there.</summary>
    public virtual async Task RenameSessionAsync(
        string sessionId,
        string title,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentNullException.ThrowIfNull(title);
        var renamed = await _editor.RenameAsync(sessionId, title, workingDirectory, cancellationToken).ConfigureAwait(false);
        renamed.OrThrow();
    }

    /// <summary>A null <paramref name="tag"/> clears the tag. Half of a surrogate pair without its other half is written escaped, as JSON.stringify writes it, and reads back as U+FFFD. Throws <see cref="ClaudeAgentException"/> with SessionNotFound when the session is not there.</summary>
    public virtual async Task TagSessionAsync(
        string sessionId,
        string? tag,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        var tagged = await _editor.TagAsync(sessionId, tag, workingDirectory, cancellationToken).ConfigureAwait(false);
        tagged.OrThrow();
    }

    /// <summary>Removes the session's transcript and the directory beside it that holds its subagents'. Throws <see cref="ClaudeAgentException"/> with SessionNotFound when the session is not there.</summary>
    public virtual async Task DeleteSessionAsync(
        string sessionId,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        var deleted = await _editor.DeleteAsync(sessionId, workingDirectory, cancellationToken).ConfigureAwait(false);
        deleted.OrThrow();
    }

    /// <summary>Copies the session up to <paramref name="upToMessageId"/> (or all of it) into a new session and returns the new id. After the copies the new transcript carries the source's atis-latch line when the source has one, then a custom-title line and an agent-name line, both with the fork's title: <paramref name="title"/> with its runs of whitespace made one space and its ends trimmed, or, when none is given, the fork's first prompt cut at 100 characters, "Branched conversation" when it has none, followed by " (Branch)", or by " (Branch 2)" and up when a session of the project already carries that title. Throws <see cref="ClaudeAgentException"/> with SessionNotFound when the session is not there, and with Fork when it holds nothing to copy or no message <paramref name="upToMessageId"/>.</summary>
    public virtual async Task<string> ForkSessionAsync(
        string sessionId,
        string? upToMessageId = null,
        string? title = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        var forked = await _editor.ForkAsync(sessionId, upToMessageId, title, workingDirectory, cancellationToken).ConfigureAwait(false);
        return forked.OrThrow();
    }

    /// <summary>Copies the session from the user's own configuration directory into this client's, under the same project, so a session started with <see cref="ClaudeAgentSessionOptions.ResumeSessionId"/> takes it up. The user's directory is CLAUDE_CONFIG_DIR as this process inherited it, else ~/.claude; an inherited CLAUDE_CONFIG_DIR that is this client's own directory is an outer SDK session's and not the user's, whose directory is then the one CLAUDE_SECURESTORAGE_CONFIG_DIR as this process inherited it names, ~/.claude when it is set to nothing, else ~/.claude. The transcript is copied byte for byte, and with <paramref name="includeSubagents"/> every file under the session's subagents directory. The user's directory is only read. Throws <see cref="ClaudeAgentException"/> with SessionNotFound when the user's directory does not hold the session, and with SessionExists when any file the copy would write is already in this client's directory, in which case nothing is written. A copy that fails or is cancelled part-way removes, before it throws, the files it made, the one it was writing included, and the directories it made that nothing else has been put in since; the import can then be asked again.</summary>
    public virtual async Task ImportSessionAsync(
        string sessionId,
        string? workingDirectory = null,
        bool includeSubagents = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        var imported = await _editor.ImportAsync(sessionId, workingDirectory, includeSubagents, cancellationToken).ConfigureAwait(false);
        imported.OrThrow();
    }
}
