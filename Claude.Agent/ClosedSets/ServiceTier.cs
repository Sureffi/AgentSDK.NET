using System.Text.Json.Serialization;

namespace Claude.Agent;

/// <summary>The API service tier that served a request. A tier this library does not name is read as it is. Serialized, it is the name as a string, the default the empty string; the empty string reads back as the default.</summary>
[JsonConverter(typeof(ServiceTierJsonConverter))]
public readonly struct ServiceTier : IEquatable<ServiceTier>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public ServiceTier(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>standard</c>: the ordinary tier.</summary>
    public static ServiceTier Standard { get; } = new("standard");

    /// <summary><c>priority</c>: the priority tier.</summary>
    public static ServiceTier Priority { get; } = new("priority");

    /// <summary><c>batch</c>: the batch tier.</summary>
    public static ServiceTier Batch { get; } = new("batch");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "standard" or "priority" or "batch";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(ServiceTier left, ServiceTier right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(ServiceTier left, ServiceTier right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator ServiceTier(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(ServiceTier other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="ServiceTier"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is ServiceTier other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
