namespace Claude.Agent;

/// <summary>Why a session ended. A reason this library does not name is read as it is.</summary>
public readonly struct SessionEndReason : IEquatable<SessionEndReason>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public SessionEndReason(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>clear</c>: the conversation was cleared.</summary>
    public static SessionEndReason Clear { get; } = new("clear");

    /// <summary><c>resume</c>: another session was resumed in its place.</summary>
    public static SessionEndReason Resume { get; } = new("resume");

    /// <summary><c>logout</c>: the account was logged out.</summary>
    public static SessionEndReason Logout { get; } = new("logout");

    /// <summary><c>prompt_input_exit</c>: the person left at the prompt.</summary>
    public static SessionEndReason PromptInputExit { get; } = new("prompt_input_exit");

    /// <summary><c>other</c>: anything else the binary does not distinguish.</summary>
    public static SessionEndReason Other { get; } = new("other");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "clear" or "resume" or "logout" or "prompt_input_exit" or "other";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(SessionEndReason left, SessionEndReason right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(SessionEndReason left, SessionEndReason right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator SessionEndReason(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(SessionEndReason other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="SessionEndReason"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is SessionEndReason other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
