using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Claude.Agent;

/// <summary>
/// Drives the claude binary on the account it is logged into. One client, configured once with how
/// the binary is reached, starts any number of sessions, each asked what its call says over the
/// client's defaults; <see cref="QueryAsync(string, CancellationToken)"/> is a session that takes
/// one prompt and ends with its result. Every session runs in the client's own configuration
/// directory, <see cref="ClaudeAgentClientOptions.ConfigurationDirectory"/>, on the user's login.
/// </summary>
public class ClaudeAgentClient
{
    private readonly ClaudeAgentClientOptions _options;
    private readonly SessionLauncher _launcher;

    /// <summary>For mocking.</summary>
    protected ClaudeAgentClient()
    {
        _options = null!;
        _launcher = null!;
    }

    /// <summary>A client that reaches the binary with a copy of <paramref name="options"/>; a change made to them afterwards does not reach it. The configuration directory is resolved to a full path here. Throws <see cref="ArgumentException"/> for an option, or a default, the binary would refuse, and for a configuration directory that is the user's own.</summary>
    public ClaudeAgentClient(ClaudeAgentClientOptions options)
        : this(options, null)
    {
    }

    /// <summary>The same, logging through <paramref name="loggerFactory"/>.</summary>
    public ClaudeAgentClient(ClaudeAgentClientOptions options, ILoggerFactory? loggerFactory)
        : this(options, loggerFactory, null, null)
    {
    }

    // `host` is where the user's directory and the default configuration directory are read from:
    // this process when null.
    internal ClaudeAgentClient(
        ClaudeAgentClientOptions options,
        ILoggerFactory? loggerFactory,
        ITransportFactory? transports,
        HostEnvironment? host)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Resolve(host ?? HostEnvironment.Current);

        var loggers = loggerFactory ?? NullLoggerFactory.Instance;
        transports ??= new SubprocessTransportFactory(
            new FrameCodec(loggers.CreateLogger<FrameCodec>()),
            new CliLocator(loggers.CreateLogger<CliLocator>()),
            loggers);

        _launcher = new SessionLauncher(transports, new PayloadParser(), loggers);
    }

    /// <summary>The transcripts this client's sessions are written to, in <see cref="ClaudeAgentClientOptions.ConfigurationDirectory"/>, and the one way in from the user's own directory: <see cref="ClaudeSessionClient.ImportSessionAsync"/>.</summary>
    public virtual ClaudeSessionClient GetSessionClient() =>
        new(
            new TranscriptDirectory(new TranscriptLayout(Directories.Configuration)),
            new TranscriptDirectory(new TranscriptLayout(Directories.User)));

    internal ClaudeAgentClientOptions Options => _options;

    internal ConfigurationDirectories Directories => _options.Directories!;

    /// <summary>
    /// Runs one prompt in a fresh session asked the client's defaults, and hands back its run at once;
    /// the session starts as the call is made. See
    /// <see cref="QueryAsync(string, ClaudeAgentSessionOptions?, CancellationToken)"/>.
    /// </summary>
    public virtual ClaudeAgentRun QueryAsync(string prompt, CancellationToken cancellationToken = default) =>
        QueryAsync(prompt, null, cancellationToken);

    /// <summary>
    /// Runs one prompt in a session asked <paramref name="sessionOptions"/> over the client's defaults:
    /// its own model, directory, prompt, tools, schema, caps, hooks and permission callback, and
    /// resumed, forked or under an id of its own. The options are copied as the call is made, and an
    /// <see cref="ArgumentException"/> for a whole the binary would refuse is thrown there. The run is
    /// handed back at once and the session starts as the call is made; a session that fails to start
    /// fails the run. The run is everything the session writes until it is closed: while a background
    /// agent or workflow the turn started is in flight the session stays open, so the turn the binary
    /// opens when it is done is part of the run; a background shell does not hold the session open.
    /// The session's stdin stays open until the result that ends the turn, so the run can be
    /// interrupted all the while. At that result the session is closed —
    /// stdin closed, the process awaited to its exit, what it wrote as it shut down read — and only
    /// then is that result handed out, last: whatever the process wrote after it comes before it,
    /// and a tool call still open is closed unanswered before it. When the run's completion is there the process is gone. A
    /// process that does not exit when its stdin closes is ended by the close sequence of
    /// <see cref="ClaudeAgentClientOptions.ProcessGracePeriod"/>, which delays the result by up to
    /// that grace, twice when SIGTERM does not end it; the run still completes with its trace.
    /// <paramref name="cancellationToken"/> is the session's: cancelled before the turn's result, the
    /// session is closed and the run ends with <see cref="OperationCanceledException"/> from its
    /// stream and its completion. An enumeration left part-way interrupts the turn, as
    /// <see cref="ClaudeAgentRun.GetAsyncEnumerator"/> says.
    /// </summary>
    public virtual ClaudeAgentRun QueryAsync(
        string prompt,
        ClaudeAgentSessionOptions? sessionOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var asked = ClaudeAgentSessionOptions.Layer(_options.Defaults, sessionOptions);
        asked.Validate();
        return Run(prompt, asked, evidenceType: null, cancellationToken);
    }

    /// <summary>
    /// Asks the binary one question over typed evidence, and hands back its run at once, as
    /// <see cref="QueryAsync(string, ClaudeAgentSessionOptions?, CancellationToken)"/> does: what the
    /// model reads and what the code holds are one type. The answer's schema is
    /// <typeparamref name="TAnswer"/>'s, taken as
    /// <see cref="ClaudeAgentSessionOptions.SetOutputSchema{T}(JsonSerializerOptions, JsonSchemaExporterOptions)"/>
    /// takes it, and the answer is read off the trace as any structured output is, with
    /// <see cref="AgentTrace.GetStructuredOutput{T}(JsonSerializerOptions)"/> through the same
    /// <paramref name="serializerOptions"/>. The prompt is <paramref name="task"/> as it is given,
    /// then the evidence's schema in an <c>&lt;evidence_schema&gt;</c> tag, then
    /// <paramref name="evidence"/> in an <c>&lt;evidence&gt;</c> tag, a blank line between each. Schema
    /// and evidence are JSON written off the contract <paramref name="serializerOptions"/> holds for
    /// <typeparamref name="TEvidence"/>, or <see cref="JsonSerializerOptions.Default"/>'s when it is
    /// null, indented two spaces with line feeds, a character escaped only where JSON needs it: a
    /// quotation mark, a backslash, a control character, a lone surrogate. The
    /// evidence's schema is written as the answer's is, with <paramref name="exporterOptions"/>
    /// applied to both, so a member's <see cref="System.ComponentModel.DescriptionAttribute"/> is in
    /// what the model reads; its root is any type the contract writes. The system prompt is the
    /// caller's, on <paramref name="sessionOptions"/> or the defaults. The trace names
    /// <typeparamref name="TEvidence"/> as its <see cref="AgentTrace.EvidenceType"/> and
    /// <typeparamref name="TAnswer"/> as its <see cref="AgentTrace.AnswerType"/>. Thrown at the call,
    /// before any process starts: <see cref="ArgumentNullException"/> for no evidence,
    /// <see cref="ArgumentException"/> for a task that is empty or white space, for options that
    /// already carry an <see cref="ClaudeAgentSessionOptions.OutputSchema"/>, the defaults' or the
    /// call's, since an ask's answer is <typeparamref name="TAnswer"/>, for an answer schema whose
    /// root is not an object, for an evidence schema a transform of
    /// <paramref name="exporterOptions"/> made null, and for a whole the binary would refuse; and
    /// what System.Text.Json
    /// throws taking a schema from either type or writing the evidence, as it threw it.
    /// </summary>
    public virtual ClaudeAgentRun AskAsync<TEvidence, TAnswer>(
        string task,
        TEvidence evidence,
        ClaudeAgentSessionOptions? sessionOptions = null,
        JsonSerializerOptions? serializerOptions = null,
        JsonSchemaExporterOptions? exporterOptions = null,
        CancellationToken cancellationToken = default)
    {
        var contract = serializerOptions ?? JsonSerializerOptions.Default;
        return Ask(
            task,
            evidence,
            sessionOptions,
            asked => asked.SetOutputSchema<TAnswer>(contract, exporterOptions),
            () =>
            {
                // What JsonSerializer does on its first write through these options: give them the
                // reflecting resolver when they carry none. Taking the answer's schema has already.
                contract.MakeReadOnly(populateMissingResolver: true);
                return (JsonTypeInfo<TEvidence>)contract.GetTypeInfo(typeof(TEvidence));
            },
            exporterOptions,
            cancellationToken);
    }

    /// <summary>
    /// The same ask, with the evidence written and its schema taken off
    /// <paramref name="evidenceTypeInfo"/> and the answer's schema off
    /// <paramref name="answerTypeInfo"/>, the contracts a source generator wrote, so neither needs
    /// reflection. Read the answer back through <paramref name="answerTypeInfo"/> with
    /// <see cref="AgentTrace.GetStructuredOutput{T}(JsonTypeInfo{T})"/>. A null contract is an
    /// <see cref="ArgumentNullException"/>; everything else is thrown as the other overload throws
    /// it.
    /// </summary>
    public virtual ClaudeAgentRun AskAsync<TEvidence, TAnswer>(
        string task,
        TEvidence evidence,
        JsonTypeInfo<TEvidence> evidenceTypeInfo,
        JsonTypeInfo<TAnswer> answerTypeInfo,
        ClaudeAgentSessionOptions? sessionOptions = null,
        JsonSchemaExporterOptions? exporterOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidenceTypeInfo);
        ArgumentNullException.ThrowIfNull(answerTypeInfo);
        return Ask(
            task,
            evidence,
            sessionOptions,
            asked => asked.SetOutputSchema(answerTypeInfo, exporterOptions),
            () => evidenceTypeInfo,
            exporterOptions,
            cancellationToken);
    }

    // An ask, whichever contracts it is written off: its options refused when they already ask for a
    // schema, the answer's schema set, the prompt written, the whole validated, then the one-shot run.
    private ClaudeAgentRun Ask<TEvidence>(
        string task,
        TEvidence evidence,
        ClaudeAgentSessionOptions? sessionOptions,
        Action<ClaudeAgentSessionOptions> setAnswerSchema,
        Func<JsonTypeInfo<TEvidence>> evidenceContract,
        JsonSchemaExporterOptions? exporterOptions,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(task);
        if (evidence is null)
        {
            throw new ArgumentNullException(nameof(evidence));
        }

        var asked = ClaudeAgentSessionOptions.Layer(_options.Defaults, sessionOptions);
        if (asked.OutputSchema is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null })
        {
            throw new ArgumentException(
                "An ask's answer schema is its answer type's; the options, or the client's defaults, already carry an OutputSchema.",
                nameof(sessionOptions));
        }

        setAnswerSchema(asked);
        var prompt = EvidencePrompt.Write(task, evidence, evidenceContract(), exporterOptions);
        asked.Validate();
        return Run(prompt, asked, typeof(TEvidence), cancellationToken);
    }

    private ClaudeAgentRun Run(string prompt, ClaudeAgentSessionOptions asked, Type? evidenceType, CancellationToken cancellationToken)
    {
        var stream = new RunStream(prompt: null)
        {
            EvidenceType = evidenceType,
            AnswerType = asked.AnswerType,
        };
        var started = new TaskCompletionSource<SessionRuntime?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = RunOneShotAsync(prompt, asked, stream, started, cancellationToken);
        return new ClaudeAgentRun(stream, ct => started.Task.WaitAsync(ct));
    }

    // The one-shot session, from its start to its close: the run's turn read to its ending result,
    // the session disposed — the process gone, its output read — and the run ended after it, in that
    // order.
    private async Task RunOneShotAsync(
        string prompt,
        ClaudeAgentSessionOptions asked,
        RunStream stream,
        TaskCompletionSource<SessionRuntime?> started,
        CancellationToken cancellationToken)
    {
        ClaudeAgentSession session;
        try
        {
            session = await _launcher.StartAsync(_options, asked, this, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            started.TrySetResult(null);
            stream.Cancel(cancellationToken);
            return;
        }
        catch (Exception ex)
        {
            started.TrySetResult(null);
            stream.Fail(ex);
            return;
        }

        var runtime = session.Runtime;
        started.TrySetResult(runtime);
        var canceled = false;
        Exception? failure = null;
        try
        {
            await using (session.ConfigureAwait(false))
            {
                try
                {
                    await runtime.RunOneShotAsync(stream, new UserInputFrame(prompt, null, null, UserInputForm.OneShot), cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    canceled = true;
                }
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        if (canceled)
        {
            stream.Cancel(cancellationToken);
        }
        else if (failure is not null)
        {
            stream.Fail(failure);
        }
        else
        {
            runtime.Finish(stream);
        }
    }

    /// <summary>Starts a session asked the client's defaults, which takes prompts until it is disposed.</summary>
    public virtual Task<ClaudeAgentSession> StartSessionAsync(CancellationToken cancellationToken = default) =>
        StartSessionAsync(null, cancellationToken);

    /// <summary>Starts a session asked <paramref name="sessionOptions"/> over the client's defaults. The options are copied as the call is made. The task fails with <see cref="ArgumentException"/> when the whole is something the binary would refuse, before any process starts.</summary>
    public virtual Task<ClaudeAgentSession> StartSessionAsync(
        ClaudeAgentSessionOptions? sessionOptions,
        CancellationToken cancellationToken = default) =>
        _launcher.StartAsync(_options, ClaudeAgentSessionOptions.Layer(_options.Defaults, sessionOptions), this, cancellationToken);

    // What a fork or a resume of a live session is asked was layered when that session started. It
    // goes to the launcher as it is: layered again, what accumulates would be the defaults' twice.
    internal Task<ClaudeAgentSession> StartAskedAsync(ClaudeAgentSessionOptions asked, CancellationToken cancellationToken) =>
        _launcher.StartAsync(_options, asked, this, cancellationToken);
}
