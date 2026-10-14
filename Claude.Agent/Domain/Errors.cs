
namespace Claude.Agent;

internal abstract record Error
{
    private protected Error(string message) => Message = message;

    public string Message { get; }

    public sealed override string ToString() => Message;
}

internal enum ParseErrorKind
{
    MalformedJson,
    MissingField,
    InvalidValue,
}

internal sealed record ParseError(ParseErrorKind Kind, string Message, string Line) : Error(Message);

internal sealed record CliNotFoundError(string Message) : Error(Message);

internal sealed record WorkingDirectoryNotFoundError(string Message, string Path) : Error(Message);

internal sealed record ConnectionError(string Message) : Error(Message);

internal sealed record ProcessError(string Message, int? ExitCode) : Error(Message);

internal sealed record ControlError(string Message, string? Subtype) : Error(Message);

internal sealed record CannotRewindError(string Message, string UserMessageId, string? Reason) : Error(Message);

internal sealed record SessionNotFoundError(string Message, string Session, SessionSearchScope Scope) : Error(Message);

internal enum SessionSearchScope
{
    ProjectDirectory,
    NoProjectsDirectory,
    AnyProjectDirectory,
}

// An import never overwrites: a session any file of whose copy is already in the configuration
// directory it would be copied into is refused.
internal sealed record SessionExistsError(string Message, string Session) : Error(Message);

internal sealed record ForkError(string Message) : Error(Message);

internal sealed record ValidationError(string Message, string? Member = null) : Error(Message);

internal static class ErrorCodes
{
    public const string CliNotFound = "CliNotFound";
    public const string WorkingDirectoryNotFound = "WorkingDirectoryNotFound";
    public const string Connection = "Connection";
    public const string Process = "Process";
    public const string Control = "Control";
    public const string Parse = "Parse";
    public const string CannotRewind = "CannotRewind";
    public const string SessionNotFound = "SessionNotFound";
    public const string SessionExists = "SessionExists";
    public const string Fork = "Fork";
}

internal static class ErrorExceptions
{
    // The one place an internal refusal becomes the exception a caller sees. Validation is an
    // argument problem; everything the binary, the process or a session refused is a
    // ClaudeAgentException with a code.
    public static Exception ToException(this Error error) => error.ToException([]);

    // The same, thrown by a run or a message stream: it carries what that stream handed out.
    public static Exception ToException(this Error error, IReadOnlyList<AgentMessage> messages) => error switch
    {
        ValidationError validation => new ArgumentException(validation.Message, validation.Member),
        ParseError parse => new ClaudeAgentException(parse.Message, ErrorCodes.Parse, null, messages),
        CliNotFoundError => new ClaudeAgentException(error.Message, ErrorCodes.CliNotFound, null, messages),
        WorkingDirectoryNotFoundError => new ClaudeAgentException(error.Message, ErrorCodes.WorkingDirectoryNotFound, null, messages),
        ConnectionError => new ClaudeAgentException(error.Message, ErrorCodes.Connection, null, messages),
        ProcessError process => new ClaudeAgentException(process.Message, ErrorCodes.Process, process.ExitCode, messages),
        ControlError => new ClaudeAgentException(error.Message, ErrorCodes.Control, null, messages),
        CannotRewindError => new ClaudeAgentException(error.Message, ErrorCodes.CannotRewind, null, messages),
        SessionNotFoundError => new ClaudeAgentException(error.Message, ErrorCodes.SessionNotFound, null, messages),
        SessionExistsError => new ClaudeAgentException(error.Message, ErrorCodes.SessionExists, null, messages),
        ForkError => new ClaudeAgentException(error.Message, ErrorCodes.Fork, null, messages),
        _ => new ClaudeAgentException(error.Message, null, null, messages),
    };

    // An exception a run caught from below, carrying the run's messages so far: a ClaudeAgentException
    // is thrown again with them, the one it was behind it; anything else is thrown as it is.
    public static Exception WithMessages(this Exception exception, IReadOnlyList<AgentMessage> messages) =>
        exception is ClaudeAgentException failure
            ? new ClaudeAgentException(failure.Message, failure.ErrorCode, failure.ExitCode, messages, failure)
            : exception;

    public static T OrThrow<T, TError>(this Result<T, TError> result)
        where TError : Error =>
        result.TryGet(out var value, out var error) ? value : throw error.ToException();
}
