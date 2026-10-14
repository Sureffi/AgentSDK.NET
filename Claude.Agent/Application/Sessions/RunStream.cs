namespace Claude.Agent;

// What one run keeps: every message routed to it, the tool-call snapshots among them, whether or
// not anyone reads them, and how the run ended. The session routes in; a consumer reads by
// position, so the session is never held by one.
internal sealed class RunStream
{
    private readonly Lock _gate = new();
    private readonly List<AgentMessage> _messages = [];
    private readonly List<AgentMessage> _afterEnding = [];
    private readonly TaskCompletionSource<AgentTrace> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _ending = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource _added = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _sealed;
    private ResultMessage? _endingResult;

    // A session's run carries the uuid its prompt is written with, and takes that prompt's turn: the
    // frames from the one that says the prompt started to the turn's result. A one-shot run carries
    // none and takes everything until its session is closed: a result written with no background
    // task in flight ends its turn, and is held, with what follows it, until the run ends, so the
    // run's last message is that result.
    public RunStream(Guid? prompt)
    {
        Prompt = prompt;

        // A run nobody awaits still fails observed: its failure is the reader's to see, not the
        // finalizer's to report.
        _ = _completion.Task.ContinueWith(
            static failed => _ = failed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public Guid? Prompt { get; }

    // What the trace names: the ask's evidence type, and the type the answer's schema was taken from.
    public Type? EvidenceType { get; init; }

    public Type? AnswerType { get; init; }

    public bool IsOneShot => Prompt is null;

    // The session's pairing, which closes this run's open calls when it ends. Set by the session
    // before anything is routed to the run.
    public ToolCallPairing? Pairing { get; set; }

    // Where the session's routing has this run; read and written under the session's routing lock
    // only. Started: the frame that says its prompt started has come. Answered: its result has come,
    // and waits in HeldResult for the prompt's lifecycle to close. LifecycleClosed: the frame that
    // says the prompt is done has come.
    public bool IsStarted { get; set; }

    public ResultMessage? HeldResult { get; set; }

    public bool IsLifecycleClosed { get; set; }

    // The stretch of frames that is this run's is open: its prompt started and its result has not
    // come.
    public bool IsOpen => IsStarted && HeldResult is null && !IsSealed;

    public Task<AgentTrace> Completion => _completion.Task;

    // Set once the run's turn has begun: a session's when the binary says its prompt started, a
    // one-shot's when its prompt is written.
    public Task Started => _started.Task;

    // A one-shot's: set when the result that ends its turn has come.
    public Task Ending => _ending.Task;

    // A one-shot whose turn's ending result has come: its turn is no longer under way.
    public bool HasEnding
    {
        get
        {
            lock (_gate)
            {
                return _endingResult is not null;
            }
        }
    }

    // The result that ended a one-shot's turn, while it waits to be handed out last.
    public ResultMessage? EndingResult
    {
        get
        {
            lock (_gate)
            {
                return _endingResult;
            }
        }
    }

    // No message is added after this: the run has its result, or it ended.
    public bool IsSealed
    {
        get
        {
            lock (_gate)
            {
                return _sealed;
            }
        }
    }

    public bool IsEnded => _completion.Task.IsCompleted;

    public void MarkStarted() => _started.TrySetResult();

    // The session's way in: `message` and what its pairing handed out for it, in order; `endsTurn`
    // when the message is a result that ends a turn. A session's run is sealed there. A one-shot's
    // holds that result back, and what comes after it with it, until the run ends; a later result
    // lets them go in their order, as that one was not the last.
    public void Add(AgentMessage message, List<AgentMessage> handed, bool endsTurn)
    {
        TaskCompletionSource added;
        var ending = false;
        lock (_gate)
        {
            if (_sealed)
            {
                return;
            }

            if (_endingResult is { } previous)
            {
                if (message is not ResultMessage)
                {
                    _afterEnding.AddRange(handed);
                    return;
                }

                _messages.Add(previous);
                _messages.AddRange(_afterEnding);
                _afterEnding.Clear();
                _endingResult = null;
            }

            if (endsTurn && message is ResultMessage result)
            {
                if (IsOneShot)
                {
                    _messages.AddRange(handed.Where(each => !ReferenceEquals(each, result)));
                    _endingResult = result;
                    ending = true;
                }
                else
                {
                    _messages.AddRange(handed);
                    _sealed = true;
                }
            }
            else
            {
                _messages.AddRange(handed);
            }

            added = SwapSignal();
        }

        added.TrySetResult();
        if (ending)
        {
            _ending.TrySetResult();
        }
    }

    // The trace, when the run's result is its last message once its open calls are closed: a
    // session's delivered result, or a one-shot's ending result, placed after what followed it and
    // after the closes. Otherwise the run failed for `missing`.
    public void Complete(Error missing)
    {
        TaskCompletionSource added;
        lock (_gate)
        {
            if (_completion.Task.IsCompleted)
            {
                return;
            }

            var ended = IsOneShot ? _endingResult : null;
            Seal();
            if (_messages.Count > 0 && _messages[^1] is ResultMessage result && (!IsOneShot || ReferenceEquals(result, ended)))
            {
                _completion.TrySetResult(new AgentTrace([.. _messages], result, EvidenceType, AnswerType));
            }
            else
            {
                _completion.TrySetException(missing.ToException([.. _messages]));
            }

            added = SwapSignal();
        }

        added.TrySetResult();
    }

    public void Fail(Error error) => Fail(messages => error.ToException(messages));

    public void Fail(Exception exception) => Fail(exception.WithMessages);

    // The run ends cancelled: its open calls are closed first, so a reader that reads on to the end
    // meets every call's closing snapshot before the cancellation.
    public void Cancel(CancellationToken cancellationToken)
    {
        TaskCompletionSource added;
        lock (_gate)
        {
            if (_completion.Task.IsCompleted)
            {
                return;
            }

            Seal();
            _completion.TrySetCanceled(cancellationToken);
            added = SwapSignal();
        }

        added.TrySetResult();
    }

    // The message at `index` when there is one. Without one: `more` is null when the run has ended,
    // and otherwise completes when there may be more to read.
    public AgentMessage? Read(int index, out Task? more)
    {
        lock (_gate)
        {
            if (index < _messages.Count)
            {
                more = null;
                return _messages[index];
            }

            more = _completion.Task.IsCompleted ? null : _added.Task;
            return null;
        }
    }

    private void Fail(Func<IReadOnlyList<AgentMessage>, Exception> failure)
    {
        TaskCompletionSource added;
        lock (_gate)
        {
            if (_completion.Task.IsCompleted)
            {
                return;
            }

            Seal();
            var exception = failure([.. _messages]);
            if (exception is OperationCanceledException canceled)
            {
                _completion.TrySetCanceled(canceled.CancellationToken);
            }
            else
            {
                _completion.TrySetException(exception);
            }

            added = SwapSignal();
        }

        added.TrySetResult();
    }

    // The run's end step: what followed a one-shot's ending result, then the calls still open closed
    // unanswered, then that result, last. A run that failed without an ending result puts its closes
    // before the result it does have, when its last message is one the binary sent — a turn's result
    // that did not end the run — so that result is still last, as the page's failure row has it.
    // With no result at the end, the closes go at the end of what the run has.
    private void Seal()
    {
        if (_sealed)
        {
            return;
        }

        _messages.AddRange(_afterEnding);
        _afterEnding.Clear();

        var handed = new List<AgentMessage>();
        Pairing?.CloseUnanswered(this, handed);

        if (_endingResult is { } result)
        {
            _messages.AddRange(handed);
            _messages.Add(result);
            _endingResult = null;
        }
        else if (handed.Count != 0 && _messages.Count != 0 && _messages[^1] is ResultMessage)
        {
            _messages.InsertRange(_messages.Count - 1, handed);
        }
        else
        {
            _messages.AddRange(handed);
        }

        _sealed = true;
    }

    private TaskCompletionSource SwapSignal()
    {
        var added = _added;
        _added = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return added;
    }
}
