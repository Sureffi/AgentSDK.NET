namespace Claude.Agent;

/// <summary>
/// A live session: each prompt goes in with <see cref="SendAsync(string, CancellationToken)"/>, which
/// hands back that turn's run; what no run reads comes out of <see cref="ReceiveMessagesAsync"/>; the
/// rest are requests the binary answers while a turn runs. Disposing closes the process.
/// </summary>
public class ClaudeAgentSession : IAsyncDisposable
{
    private readonly SessionRuntime _runtime;
    private readonly ClaudeAgentClient _client;

    /// <summary>For mocking.</summary>
    protected ClaudeAgentSession()
    {
        _runtime = null!;
        _client = null!;
    }

    internal ClaudeAgentSession(SessionRuntime runtime, ClaudeAgentClient client)
    {
        _runtime = runtime;
        _client = client;
    }

    /// <summary>The binary's session id, once a frame has carried it.</summary>
    public virtual string? Id => _runtime.Id;

    /// <summary>What the binary said about itself in the initialize reply.</summary>
    public virtual ServerInfo? ServerInfo => _runtime.ServerInfo;

    internal SessionRuntime Runtime => _runtime;

    /// <summary>
    /// The messages no run reads, as they arrive, until the session ends: everything outside the
    /// turn of a prompt this session sent, such as a turn the binary opens by itself when a
    /// background task finishes, the frames of a stopped task, or what the binary writes between a
    /// prompt's frames that say it was queued and that it started. Each message is handed to one
    /// enumeration. A process failure ends the stream with a <see cref="ClaudeAgentException"/>
    /// carrying what this enumeration handed out. <paramref name="cancellationToken"/> stops this
    /// enumeration, and nothing else.
    /// </summary>
    public virtual IAsyncEnumerable<AgentMessage> ReceiveMessagesAsync(CancellationToken cancellationToken = default) =>
        _runtime.ReadMessagesAsync(cancellationToken);

    /// <summary>
    /// Sends a prompt, and hands back its turn's run at once. One prompt of a session is in flight
    /// at a time: a prompt waits in the session, in the order the calls were made, until the run of
    /// the prompt before it has ended, and is then written, with a uuid of its own; a write that
    /// fails ends the run with the failure. The binary names that uuid in <see cref="CommandLifecycleMessage"/>
    /// frames, and the run takes its prompt's turn: those frames, and every message from the one that
    /// says the prompt started to that turn's result, the frame that says the prompt is done placed
    /// before the result, which is last. A turn the binary opens by itself, and whatever it writes
    /// between turns, reaches <see cref="ReceiveMessagesAsync"/> instead. A prompt the binary takes
    /// into a turn it opened itself is still this run's from the frame that says it started.
    /// <paramref name="cancellationToken"/> takes the prompt out of the session while it waits there,
    /// which ends the run with <see cref="OperationCanceledException"/>, and stops nothing once the
    /// prompt is written. 2.1.276 is the first version recorded naming its prompts. On a binary that
    /// does not name them, a run takes no turn: its turn reaches <see cref="ReceiveMessagesAsync"/>,
    /// the run fails without its result when the session ends, and no prompt sent after it is
    /// written.
    /// </summary>
    public virtual ClaudeAgentRun SendAsync(string prompt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return SendAsync(new UserInputFrame(prompt, null, null, UserInputForm.Client, Guid.NewGuid()), cancellationToken);
    }

    private ClaudeAgentRun SendAsync(UserInputFrame frame, CancellationToken cancellationToken)
    {
        var runtime = _runtime;
        var run = new RunStream(frame.Uuid ?? throw new ArgumentException("A session's prompt carries a uuid.", nameof(frame)))
        {
            AnswerType = runtime.SessionOptions.AnswerType,
        };
        runtime.Send(run, frame, cancellationToken);
        return new ClaudeAgentRun(run, _ => Task.FromResult<SessionRuntime?>(runtime));
    }

    /// <summary>
    /// Sends the binary an interrupt whenever it is called, a turn under way or not, and returns once
    /// the binary has answered it. A turn under way is stopped: its result arrives saying the turn
    /// was aborted — <see cref="ResultMessage.TerminalReason"/> is "aborted_streaming" when the binary
    /// was streaming its answer and "aborted_tools" when it was in a tool — and ends that turn's run.
    /// Sent between turns while a background shell a turn started was still running, it was
    /// answered and stopped nothing on 2.1.283: the shell ran to its end, and the binary then opened
    /// a turn of its own on the shell's notification, which reaches <see cref="ReceiveMessagesAsync"/>.
    /// </summary>
    public virtual Task InterruptAsync(CancellationToken cancellationToken = default) =>
        _runtime.InterruptAsync(cancellationToken);

    /// <summary>Null returns to the binary's default model.</summary>
    public virtual Task SetModelAsync(string? model, CancellationToken cancellationToken = default) =>
        _runtime.SendControlAsync(new SetModelRequest(model), cancellationToken);

    /// <summary>Changes the permission mode for the turns that follow.</summary>
    public virtual Task SetPermissionModeAsync(PermissionMode mode, CancellationToken cancellationToken = default) =>
        mode.ToString().Length == 0
            ? throw new ArgumentException("A permission mode must name one; the value is empty.", nameof(mode))
            : _runtime.SendControlAsync(new SetPermissionModeRequest(mode), cancellationToken);

    /// <summary>Restores the files the binary checkpointed to their state at <paramref name="userMessageId"/>; needs <see cref="ClaudeAgentSessionOptions.EnableFileCheckpointing"/>.</summary>
    public virtual async Task RewindFilesAsync(string userMessageId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userMessageId);

        var payload = await _runtime.SendControlAsync(new RewindFilesRequest(userMessageId), cancellationToken).ConfigureAwait(false);
        var verdict = _runtime.Payloads.Rewind(payload).OrThrow();
        if (!verdict.CanRewind)
        {
            var reason = verdict.Error;
            var message = string.IsNullOrEmpty(reason)
                ? $"Claude Code cannot rewind files to message {userMessageId}"
                : $"Claude Code cannot rewind files to message {userMessageId}: {reason}";
            throw new CannotRewindError(message, userMessageId, reason).ToException();
        }
    }

    /// <summary>Has the binary connect to an MCP server again.</summary>
    public virtual Task ReconnectMcpServerAsync(string serverName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(serverName);
        return _runtime.SendControlAsync(new McpReconnectRequest(serverName), cancellationToken);
    }

    /// <summary>Turns an MCP server on or off for the session.</summary>
    public virtual Task ToggleMcpServerAsync(string serverName, bool enabled, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(serverName);
        return _runtime.SendControlAsync(new McpToggleRequest(serverName, enabled), cancellationToken);
    }

    /// <summary>Stops a background task by the id its <see cref="TaskMessage"/> carries.</summary>
    public virtual Task StopTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(taskId);
        return _runtime.SendControlAsync(new StopTaskRequest(taskId), cancellationToken);
    }

    /// <summary>How full the context window is.</summary>
    public virtual async Task<ContextUsage> GetContextUsageAsync(CancellationToken cancellationToken = default)
    {
        var payload = await _runtime.SendControlAsync(new GetContextUsageRequest(), cancellationToken).ConfigureAwait(false);
        return _runtime.Payloads.ContextUsage(payload).OrThrow();
    }

    /// <summary>Every MCP server the session knows, with its status and tools.</summary>
    public virtual async Task<IReadOnlyList<McpServerStatus>> GetMcpStatusAsync(CancellationToken cancellationToken = default)
    {
        var payload = await _runtime.SendControlAsync(new McpStatusRequest(), cancellationToken).ConfigureAwait(false);
        return _runtime.Payloads.McpStatus(payload).OrThrow();
    }

    /// <summary>A new session continuing this one's transcript under a new id, asked what this one was; this session stays open.</summary>
    public virtual Task<ClaudeAgentSession> ForkAsync(CancellationToken cancellationToken = default) =>
        _client.StartAskedAsync(_runtime.SessionOptions.Resuming(KnownId("forked"), fork: true), cancellationToken);

    /// <summary>A new session resuming this one's transcript under the same id, asked what this one was.</summary>
    public virtual Task<ClaudeAgentSession> ResumeAsync(CancellationToken cancellationToken = default) =>
        _client.StartAskedAsync(_runtime.SessionOptions.Resuming(KnownId("resumed"), fork: false), cancellationToken);

    /// <summary>Closes stdin, waits for the binary to exit, and ends it if it does not; routes what it wrote until then as any output is routed. No prompt is written once the close begins, and a run whose result has not come by the end of the output fails for the close; a process that exits non-zero at a close its caller began is no failure of the session's. Safe to call twice; every call awaits the one close. <paramref name="cancellationToken"/> is not the close's. <see cref="ClaudeAgentClientOptions.ProcessGracePeriod"/> bounds the process's exit — SIGTERM after it, SIGKILL after a second one — and, inside the transport's close, the write lock it takes and the last of stderr. The close's own waits, in the order it makes them: a write in flight (5 seconds), the transport's close as a whole (30 seconds), the read loop reaching the end of the output (5 seconds, and 5 more once it is stopped) and the control handlers still out (5 seconds).</summary>
    public virtual Task CloseAsync(CancellationToken cancellationToken = default) =>
        _runtime.CloseAsync(cancellationToken).AsTask();

    /// <summary>The same as <see cref="CloseAsync"/>.</summary>
    public virtual async ValueTask DisposeAsync()
    {
        await CloseAsync(CancellationToken.None).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private string KnownId(string verb) =>
        _runtime.Id is { Length: > 0 } id
            ? id
            : throw new InvalidOperationException($"A session without an id cannot be {verb}.");
}
