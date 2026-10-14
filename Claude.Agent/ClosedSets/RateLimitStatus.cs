namespace Claude.Agent;

/// <summary>Where the account stands against a rate limit. A status this library does not name is read as it is.</summary>
public readonly struct RateLimitStatus : IEquatable<RateLimitStatus>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public RateLimitStatus(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>allowed</c>: under the limit.</summary>
    public static RateLimitStatus Allowed { get; } = new("allowed");

    /// <summary><c>allowed_warning</c>: under the limit and close to it.</summary>
    public static RateLimitStatus AllowedWarning { get; } = new("allowed_warning");

    /// <summary><c>rejected</c>: over the limit.</summary>
    public static RateLimitStatus Rejected { get; } = new("rejected");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "allowed" or "allowed_warning" or "rejected";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(RateLimitStatus left, RateLimitStatus right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(RateLimitStatus left, RateLimitStatus right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator RateLimitStatus(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(RateLimitStatus other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="RateLimitStatus"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is RateLimitStatus other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
