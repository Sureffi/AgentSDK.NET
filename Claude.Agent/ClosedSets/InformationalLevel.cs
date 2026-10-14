namespace Claude.Agent;

/// <summary>How weighty an informational notice is. A level this library does not name is read as it is.</summary>
public readonly struct InformationalLevel : IEquatable<InformationalLevel>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public InformationalLevel(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>info</c>: a plain statement.</summary>
    public static InformationalLevel Info { get; } = new("info");

    /// <summary><c>notice</c>: worth reading.</summary>
    public static InformationalLevel Notice { get; } = new("notice");

    /// <summary><c>suggestion</c>: something the caller could do.</summary>
    public static InformationalLevel Suggestion { get; } = new("suggestion");

    /// <summary><c>warning</c>: something that may go wrong.</summary>
    public static InformationalLevel Warning { get; } = new("warning");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "info" or "notice" or "suggestion" or "warning";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(InformationalLevel left, InformationalLevel right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(InformationalLevel left, InformationalLevel right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator InformationalLevel(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(InformationalLevel other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="InformationalLevel"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is InformationalLevel other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
