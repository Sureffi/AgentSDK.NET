namespace Claude.Agent;

/// <summary>
/// The binary, the process around it or a session refused or failed. <see cref="ErrorCode"/> names
/// the kind: CliNotFound, WorkingDirectoryNotFound, Connection, Process, Control, Parse, CannotRewind,
/// SessionNotFound, SessionExists, Fork. Thrown by a run or a message stream, it carries
/// what that stream had handed out when it failed.
/// </summary>
public sealed class ClaudeAgentException : Exception
{
    /// <summary>An exception with a message and no error code.</summary>
    public ClaudeAgentException(string message)
        : this(message, null, null, null, null)
    {
    }

    /// <summary>An exception with a message and the exception behind it.</summary>
    public ClaudeAgentException(string message, Exception? innerException)
        : this(message, null, null, null, innerException)
    {
    }

    /// <summary>An exception as the library throws it; public so a test double of a client or a run can throw one. <paramref name="messages"/> are what the failed run or message stream had handed out, in order.</summary>
    public ClaudeAgentException(
        string message,
        string? errorCode,
        int? exitCode = null,
        IEnumerable<AgentMessage>? messages = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        ExitCode = exitCode;
        Messages = messages is null ? [] : [.. messages];
    }

    /// <summary>The kind of failure, from the list above; null when constructed without one.</summary>
    public string? ErrorCode { get; }

    /// <summary>The binary's exit code, when it exited.</summary>
    public int? ExitCode { get; }

    /// <summary>What the failed run or message stream had handed out, in order; empty when nothing had arrived, and for a failure that is not a stream's.</summary>
    public IReadOnlyList<AgentMessage> Messages { get; }

    /// <summary>The last of <see cref="Messages"/> when it is the result the binary sent before the failure; null otherwise.</summary>
    public ResultMessage? Result => Messages.Count > 0 ? Messages[^1] as ResultMessage : null;
}
