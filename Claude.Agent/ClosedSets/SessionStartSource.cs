namespace Claude.Agent;

/// <summary>How a session came to be. A source this library does not name is read as it is.</summary>
public readonly struct SessionStartSource : IEquatable<SessionStartSource>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public SessionStartSource(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>startup</c>: a session opened from nothing.</summary>
    public static SessionStartSource Startup { get; } = new("startup");

    /// <summary><c>resume</c>: an earlier session was taken up again.</summary>
    public static SessionStartSource Resume { get; } = new("resume");

    /// <summary><c>clear</c>: the conversation was cleared and a session opened in its place.</summary>
    public static SessionStartSource Clear { get; } = new("clear");

    /// <summary><c>compact</c>: the conversation was compacted and a session opened over the summary.</summary>
    public static SessionStartSource Compact { get; } = new("compact");

    /// <summary><c>fork</c>: a session branched off another.</summary>
    public static SessionStartSource Fork { get; } = new("fork");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "startup" or "resume" or "clear" or "compact" or "fork";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(SessionStartSource left, SessionStartSource right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(SessionStartSource left, SessionStartSource right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator SessionStartSource(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(SessionStartSource other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="SessionStartSource"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is SessionStartSource other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
