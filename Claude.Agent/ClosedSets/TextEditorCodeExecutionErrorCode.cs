namespace Claude.Agent;

/// <summary>Why the API's text editor did not answer. A code this library does not name is read as it is.</summary>
public readonly struct TextEditorCodeExecutionErrorCode : IEquatable<TextEditorCodeExecutionErrorCode>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public TextEditorCodeExecutionErrorCode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>invalid_tool_input</c>: the call's arguments did not fit the tool.</summary>
    public static TextEditorCodeExecutionErrorCode InvalidToolInput { get; } = new("invalid_tool_input");

    /// <summary><c>unavailable</c>: the tool could not be reached.</summary>
    public static TextEditorCodeExecutionErrorCode Unavailable { get; } = new("unavailable");

    /// <summary><c>too_many_requests</c>: the account is asking too often.</summary>
    public static TextEditorCodeExecutionErrorCode TooManyRequests { get; } = new("too_many_requests");

    /// <summary><c>execution_time_exceeded</c>: the edit ran longer than it may.</summary>
    public static TextEditorCodeExecutionErrorCode ExecutionTimeExceeded { get; } = new("execution_time_exceeded");

    /// <summary><c>file_not_found</c>: nothing sits at the path the call named.</summary>
    public static TextEditorCodeExecutionErrorCode FileNotFound { get; } = new("file_not_found");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "invalid_tool_input" or "unavailable" or "too_many_requests" or "execution_time_exceeded"
            or "file_not_found";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(TextEditorCodeExecutionErrorCode left, TextEditorCodeExecutionErrorCode right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(TextEditorCodeExecutionErrorCode left, TextEditorCodeExecutionErrorCode right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator TextEditorCodeExecutionErrorCode(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(TextEditorCodeExecutionErrorCode other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="TextEditorCodeExecutionErrorCode"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is TextEditorCodeExecutionErrorCode other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
