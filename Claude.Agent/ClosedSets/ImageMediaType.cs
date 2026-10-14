namespace Claude.Agent;

/// <summary>The format of an image carried as data. A format this library does not name is read as it is.</summary>
public readonly struct ImageMediaType : IEquatable<ImageMediaType>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public ImageMediaType(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>image/jpeg</c>.</summary>
    public static ImageMediaType Jpeg { get; } = new("image/jpeg");

    /// <summary><c>image/png</c>.</summary>
    public static ImageMediaType Png { get; } = new("image/png");

    /// <summary><c>image/gif</c>.</summary>
    public static ImageMediaType Gif { get; } = new("image/gif");

    /// <summary><c>image/webp</c>.</summary>
    public static ImageMediaType Webp { get; } = new("image/webp");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined => _value is "image/jpeg" or "image/png" or "image/gif" or "image/webp";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(ImageMediaType left, ImageMediaType right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(ImageMediaType left, ImageMediaType right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator ImageMediaType(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(ImageMediaType other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is an <see cref="ImageMediaType"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is ImageMediaType other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
