namespace Claude.Agent;

/// <summary>Where the price of the model being switched to was taken from. A source this library does not name is read as it is.</summary>
public readonly struct ModelSwitchPricing : IEquatable<ModelSwitchPricing>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public ModelSwitchPricing(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>configured</c>: a price set for this account.</summary>
    public static ModelSwitchPricing Configured { get; } = new("configured");

    /// <summary><c>catalog</c>: the published price of the model.</summary>
    public static ModelSwitchPricing Catalog { get; } = new("catalog");

    /// <summary><c>default</c>: the fallback price, the model being unpriced.</summary>
    public static ModelSwitchPricing Default { get; } = new("default");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "configured" or "catalog" or "default";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(ModelSwitchPricing left, ModelSwitchPricing right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(ModelSwitchPricing left, ModelSwitchPricing right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator ModelSwitchPricing(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(ModelSwitchPricing other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="ModelSwitchPricing"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is ModelSwitchPricing other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
