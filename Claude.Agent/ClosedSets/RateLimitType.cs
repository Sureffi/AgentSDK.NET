namespace Claude.Agent;

/// <summary>Which rate limit a standing is about. A type this library does not name is read as it is.</summary>
public readonly struct RateLimitType : IEquatable<RateLimitType>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public RateLimitType(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>five_hour</c>: the five-hour window.</summary>
    public static RateLimitType FiveHour { get; } = new("five_hour");

    /// <summary><c>seven_day</c>: the seven-day window.</summary>
    public static RateLimitType SevenDay { get; } = new("seven_day");

    /// <summary><c>seven_day_opus</c>: the seven-day window for the Opus models.</summary>
    public static RateLimitType SevenDayOpus { get; } = new("seven_day_opus");

    /// <summary><c>seven_day_sonnet</c>: the seven-day window for the Sonnet models.</summary>
    public static RateLimitType SevenDaySonnet { get; } = new("seven_day_sonnet");

    /// <summary><c>seven_day_overage_included</c>: the seven-day window with overage counted in.</summary>
    public static RateLimitType SevenDayOverageIncluded { get; } = new("seven_day_overage_included");

    /// <summary><c>overage</c>: the overage allowance.</summary>
    public static RateLimitType Overage { get; } = new("overage");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "five_hour" or "seven_day" or "seven_day_opus" or "seven_day_sonnet" or "seven_day_overage_included" or
        "overage";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(RateLimitType left, RateLimitType right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(RateLimitType left, RateLimitType right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator RateLimitType(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(RateLimitType other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="RateLimitType"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is RateLimitType other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
