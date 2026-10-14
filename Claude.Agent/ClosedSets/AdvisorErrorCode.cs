namespace Claude.Agent;

/// <summary>Why the API's advisor did not answer. A code this library does not name is read as it is.</summary>
public readonly struct AdvisorErrorCode : IEquatable<AdvisorErrorCode>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public AdvisorErrorCode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>max_uses_exceeded</c>: the turn had used the tool as often as it may.</summary>
    public static AdvisorErrorCode MaxUsesExceeded { get; } = new("max_uses_exceeded");

    /// <summary><c>prompt_too_long</c>: what the advisor was asked did not fit its window.</summary>
    public static AdvisorErrorCode PromptTooLong { get; } = new("prompt_too_long");

    /// <summary><c>too_many_requests</c>: the account is asking too often.</summary>
    public static AdvisorErrorCode TooManyRequests { get; } = new("too_many_requests");

    /// <summary><c>overloaded</c>: the advisor model had no capacity.</summary>
    public static AdvisorErrorCode Overloaded { get; } = new("overloaded");

    /// <summary><c>unavailable</c>: the tool could not be reached.</summary>
    public static AdvisorErrorCode Unavailable { get; } = new("unavailable");

    /// <summary><c>execution_time_exceeded</c>: the advisor ran longer than it may.</summary>
    public static AdvisorErrorCode ExecutionTimeExceeded { get; } = new("execution_time_exceeded");

    /// <summary><c>model_not_found</c>: the advisor model the call named does not exist.</summary>
    public static AdvisorErrorCode ModelNotFound { get; } = new("model_not_found");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "max_uses_exceeded" or "prompt_too_long" or "too_many_requests" or "overloaded"
            or "unavailable" or "execution_time_exceeded" or "model_not_found";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(AdvisorErrorCode left, AdvisorErrorCode right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(AdvisorErrorCode left, AdvisorErrorCode right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator AdvisorErrorCode(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(AdvisorErrorCode other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is an <see cref="AdvisorErrorCode"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is AdvisorErrorCode other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
