namespace Claude.Agent;

/// <summary>How one file of a batch upload ended, where it did not go up.</summary>
public readonly struct ArtifactAssetUploadStatus : IEquatable<ArtifactAssetUploadStatus>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public ArtifactAssetUploadStatus(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>failed</c>: it was tried and did not go up.</summary>
    public static ArtifactAssetUploadStatus Failed { get; } = new("failed");

    /// <summary><c>not_attempted</c>: it was never tried.</summary>
    public static ArtifactAssetUploadStatus NotAttempted { get; } = new("not_attempted");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "failed" or "not_attempted";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(ArtifactAssetUploadStatus left, ArtifactAssetUploadStatus right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(ArtifactAssetUploadStatus left, ArtifactAssetUploadStatus right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator ArtifactAssetUploadStatus(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(ArtifactAssetUploadStatus other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="ArtifactAssetUploadStatus"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is ArtifactAssetUploadStatus other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
