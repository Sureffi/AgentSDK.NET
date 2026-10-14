namespace Claude.Agent;

/// <summary>
/// One turn asked of the binary, handed back at once by
/// <see cref="ClaudeAgentClient.QueryAsync(string, CancellationToken)"/> and
/// <see cref="ClaudeAgentSession.SendAsync(string, CancellationToken)"/>. Read it one way: enumerate
/// it for its messages, and <see cref="WaitForCompletionAsync"/> returns the trace once the stream
/// has ended; or await <see cref="WaitForCompletionAsync"/> alone, and the messages are kept for the
/// trace without being read. The run keeps what arrives whether or not anyone reads it, so a loop
/// over it may stop to call <see cref="InterruptAsync"/>. A loop left before the stream ended
/// interrupts the turn and waits for the run to end.
/// </summary>
public class ClaudeAgentRun : IAsyncEnumerable<AgentMessage>
{
    private const int NotRead = 0;
    private const int Enumerated = 1;
    private const int Awaited = 2;

    private readonly RunStream _stream;
    private readonly Func<CancellationToken, Task<SessionRuntime?>> _runtime;
    private int _reading;

    /// <summary>For mocking.</summary>
    protected ClaudeAgentRun()
    {
        _stream = null!;
        _runtime = null!;
    }

    internal ClaudeAgentRun(RunStream stream, Func<CancellationToken, Task<SessionRuntime?>> runtime)
    {
        _stream = stream;
        _runtime = runtime;
    }

    internal RunStream Stream => _stream;

    // The runtime the run's session is on, once it has started; null when it failed to.
    internal Task<SessionRuntime?> RuntimeAsync(CancellationToken cancellationToken) => _runtime(cancellationToken);

    /// <summary>
    /// The run's messages, in order: every message the binary wrote for the turn, a
    /// <see cref="ToolCallMessage"/> after each message that starts or closes a tool call, and the
    /// <see cref="ResultMessage"/> last. A failure is thrown as <see cref="ClaudeAgentException"/>
    /// after the messages that arrived before it; a run that ended cancelled hands out what it has,
    /// its open calls' closes among it, then throws <see cref="OperationCanceledException"/>.
    /// Disposing the enumerator before the stream ended — a <c>break</c>, an exception, a
    /// <c>FirstAsync</c> out of <c>await foreach</c> — interrupts the turn as
    /// <see cref="InterruptAsync"/> does, and waits for the run to end: when the loop is left the
    /// turn is stopped and its run has ended, with the interrupted trace
    /// <see cref="WaitForCompletionAsync"/> returns; a query's session is closed too. The wait does
    /// not throw; the run's failure, if it failed, is <see cref="WaitForCompletionAsync"/>'s.
    /// Enumerated to its end, disposing interrupts nothing. <paramref name="cancellationToken"/>
    /// stops this enumeration's wait for the next message; the loop it ends is left as any other.
    /// A run is enumerated once, and not after its completion was awaited alone: either throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    public virtual IAsyncEnumerator<AgentMessage> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var before = Interlocked.CompareExchange(ref _reading, Enumerated, NotRead);
        if (before != NotRead)
        {
            throw new InvalidOperationException(before == Enumerated
                ? "The run was enumerated already; a run is read once. Its trace is WaitForCompletionAsync's."
                : "The run's completion was awaited without enumerating it; its messages are in the trace WaitForCompletionAsync returns.");
        }

        return new Enumerator(this, ReadAsync(cancellationToken));
    }

    /// <summary>
    /// The trace of the run once it has ended: every message it handed out, the result last. Awaited
    /// without enumerating the run, the run is kept to its end all the same; awaited after an
    /// enumeration that stopped part-way, it returns once the rest has arrived. A failure throws the
    /// <see cref="ClaudeAgentException"/> the stream throws, with the messages so far. An interrupted
    /// turn completes with its trace. <paramref name="cancellationToken"/> stops this wait, and
    /// nothing else.
    /// </summary>
    public virtual Task<AgentTrace> WaitForCompletionAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.CompareExchange(ref _reading, Awaited, NotRead);
        return _stream.Completion.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Stops the run's turn; the binary answers with the turn's result, which ends the run with its
    /// trace. The interrupt is sent only while this run's turn is the one under way: a run whose
    /// prompt has not yet started — a session's the binary has not started, a query's whose session
    /// is still starting — is waited for, and the check that its turn is still the one under way is
    /// made as the interrupt is written, behind any other write. A session's run whose prompt is
    /// still waiting in the session behind an earlier run has its prompt taken out, nothing is
    /// written, and the run ends with <see cref="OperationCanceledException"/>, since no turn ran.
    /// Once the run's result has been read, or the run has ended, nothing is sent. The binary may
    /// still have finished the turn before it reads the interrupt; then the interrupt finds no turn
    /// of this run to stop. <paramref name="cancellationToken"/> stops the wait and the send.
    /// </summary>
    public virtual async Task InterruptAsync(CancellationToken cancellationToken = default)
    {
        if (_stream.IsEnded)
        {
            return;
        }

        var runtime = await _runtime(cancellationToken).ConfigureAwait(false);
        if (runtime is null)
        {
            return;
        }

        await runtime.InterruptAsync(_stream, cancellationToken).ConfigureAwait(false);
    }

    private async IAsyncEnumerator<AgentMessage> ReadAsync(CancellationToken cancellationToken)
    {
        var ended = false;
        try
        {
            var index = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_stream.Read(index, out var more) is { } message)
                {
                    index++;
                    yield return message;
                    continue;
                }

                if (more is null)
                {
                    break;
                }

                await more.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            // The end: the trace when the run completed, its failure thrown when it did not.
            ended = true;
            await _stream.Completion.ConfigureAwait(false);
        }
        finally
        {
            if (!ended)
            {
                await AbandonAsync().ConfigureAwait(false);
            }
        }
    }

    // The loop was left before the stream ended: the turn is interrupted, or its pending prompt taken
    // out, and the run's end is awaited. A failure of either is the run's to report, not the loop's.
    private async Task AbandonAsync()
    {
        try
        {
            await InterruptAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The session closed or the process went: the run ends all the same, and says why.
        }

        try
        {
            await _stream.Completion.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Observed here; WaitForCompletionAsync throws it.
        }
    }

    // The iterator with its own end: an async iterator disposed before its first move never enters
    // its body, so its finally — the interrupt and the wait for the run's end — would never run.
    // Leaving the enumeration before the stream ended is leaving it, whether or not a message was
    // read for it.
    private sealed class Enumerator(ClaudeAgentRun run, IAsyncEnumerator<AgentMessage> reading)
        : IAsyncEnumerator<AgentMessage>
    {
        private bool _moved;

        public AgentMessage Current => reading.Current;

        public ValueTask<bool> MoveNextAsync()
        {
            _moved = true;
            return reading.MoveNextAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await reading.DisposeAsync().ConfigureAwait(false);
            if (!_moved)
            {
                await run.AbandonAsync().ConfigureAwait(false);
            }
        }
    }
}
