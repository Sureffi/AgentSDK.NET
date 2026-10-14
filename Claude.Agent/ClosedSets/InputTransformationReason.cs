namespace Claude.Agent;

/// <summary>Why the API changed the request. A reason this library does not name is read as it is.</summary>
public readonly struct InputTransformationReason : IEquatable<InputTransformationReason>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public InputTransformationReason(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>model_binding_mismatch</c>: the thinking was bound to another model.</summary>
    public static InputTransformationReason ModelBindingMismatch { get; } = new("model_binding_mismatch");

    /// <summary><c>prefix_binding_mismatch</c>: the thinking was bound to another prefix.</summary>
    public static InputTransformationReason PrefixBindingMismatch { get; } = new("prefix_binding_mismatch");

    /// <summary><c>organization_binding_mismatch</c>: the thinking was bound to another organization.</summary>
    public static InputTransformationReason OrganizationBindingMismatch { get; } = new("organization_binding_mismatch");

    /// <summary><c>end_user_binding_mismatch</c>: the thinking was bound to another end user.</summary>
    public static InputTransformationReason EndUserBindingMismatch { get; } = new("end_user_binding_mismatch");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "model_binding_mismatch" or "prefix_binding_mismatch" or
        "organization_binding_mismatch" or "end_user_binding_mismatch";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(InputTransformationReason left, InputTransformationReason right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(InputTransformationReason left, InputTransformationReason right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator InputTransformationReason(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(InputTransformationReason other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="InputTransformationReason"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is InputTransformationReason other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
