namespace Claude.Agent;

/// <summary>Why the API's code execution did not answer. A code this library does not name is read as it is.</summary>
public readonly struct CodeExecutionErrorCode : IEquatable<CodeExecutionErrorCode>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public CodeExecutionErrorCode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>invalid_tool_input</c>: the call's arguments did not fit the tool.</summary>
    public static CodeExecutionErrorCode InvalidToolInput { get; } = new("invalid_tool_input");

    /// <summary><c>unavailable</c>: the tool could not be reached.</summary>
    public static CodeExecutionErrorCode Unavailable { get; } = new("unavailable");

    /// <summary><c>too_many_requests</c>: the account is asking too often.</summary>
    public static CodeExecutionErrorCode TooManyRequests { get; } = new("too_many_requests");

    /// <summary><c>execution_time_exceeded</c>: the code ran longer than it may.</summary>
    public static CodeExecutionErrorCode ExecutionTimeExceeded { get; } = new("execution_time_exceeded");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "invalid_tool_input" or "unavailable" or "too_many_requests" or "execution_time_exceeded";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(CodeExecutionErrorCode left, CodeExecutionErrorCode right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(CodeExecutionErrorCode left, CodeExecutionErrorCode right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator CodeExecutionErrorCode(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(CodeExecutionErrorCode other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="CodeExecutionErrorCode"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is CodeExecutionErrorCode other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
