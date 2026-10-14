namespace Claude.Agent;

/// <summary>The type of an agent, the name it was started by: one the binary ships, or one a caller or a setting declares. A type this library does not name is read as it is.</summary>
public readonly struct AgentType : IEquatable<AgentType>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public AgentType(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>general-purpose</c>: the agent the binary ships for any task.</summary>
    public static AgentType GeneralPurpose { get; } = new("general-purpose");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "general-purpose";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(AgentType left, AgentType right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(AgentType left, AgentType right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator AgentType(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(AgentType other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is an <see cref="AgentType"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is AgentType other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
