namespace Claude.Agent;

/// <summary>How much effort the model spends on a turn, as <c>--effort</c> names it. A level this library does not name is sent as it is.</summary>
public readonly struct Effort : IEquatable<Effort>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public Effort(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>low</c>: the least the model will spend.</summary>
    public static Effort Low { get; } = new("low");

    /// <summary><c>medium</c>: the middle of the range.</summary>
    public static Effort Medium { get; } = new("medium");

    /// <summary><c>high</c>: more than the middle.</summary>
    public static Effort High { get; } = new("high");

    /// <summary><c>xhigh</c>: more than high.</summary>
    public static Effort XHigh { get; } = new("xhigh");

    /// <summary><c>max</c>: the most the model will spend.</summary>
    public static Effort Max { get; } = new("max");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "low" or "medium" or "high" or "xhigh" or "max";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(Effort left, Effort right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(Effort left, Effort right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator Effort(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(Effort other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="Effort"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is Effort other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
