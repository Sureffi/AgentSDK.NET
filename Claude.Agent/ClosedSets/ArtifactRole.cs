namespace Claude.Agent;

/// <summary>What the account may do with a listed artifact.</summary>
public readonly struct ArtifactRole : IEquatable<ArtifactRole>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public ArtifactRole(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>editor</c>: change it.</summary>
    public static ArtifactRole Editor { get; } = new("editor");

    /// <summary><c>commenter</c>: comment on it.</summary>
    public static ArtifactRole Commenter { get; } = new("commenter");

    /// <summary><c>reader</c>: read it.</summary>
    public static ArtifactRole Reader { get; } = new("reader");

    /// <summary><c>viewer</c>: view it.</summary>
    public static ArtifactRole Viewer { get; } = new("viewer");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "editor" or "commenter" or "reader" or "viewer";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(ArtifactRole left, ArtifactRole right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(ArtifactRole left, ArtifactRole right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator ArtifactRole(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(ArtifactRole other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="ArtifactRole"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is ArtifactRole other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
