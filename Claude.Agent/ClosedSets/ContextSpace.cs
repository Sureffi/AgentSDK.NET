namespace Claude.Agent;

/// <summary>What one row of the context breakdown counts: a row is classified on this, never on its English name.</summary>
public readonly struct ContextSpace : IEquatable<ContextSpace>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public ContextSpace(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>used</c>: content that occupies the window.</summary>
    public static ContextSpace Used { get; } = new("used");

    /// <summary><c>free</c>: what is left of the window.</summary>
    public static ContextSpace Free { get; } = new("free");

    /// <summary><c>buffer</c>: the room held back for compaction.</summary>
    public static ContextSpace Buffer { get; } = new("buffer");

    /// <summary><c>deferred</c>: tool schemas kept outside the window until they are needed.</summary>
    public static ContextSpace Deferred { get; } = new("deferred");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "used" or "free" or "buffer" or "deferred";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(ContextSpace left, ContextSpace right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(ContextSpace left, ContextSpace right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator ContextSpace(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(ContextSpace other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="ContextSpace"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is ContextSpace other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
