namespace Claude.Agent;

/// <summary>What a Grep hands back: the matching lines, the file names, or a count. A value this library does not name is sent and read as it is.</summary>
public readonly struct GrepOutputMode : IEquatable<GrepOutputMode>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public GrepOutputMode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>content</c>: the matching lines themselves.</summary>
    public static GrepOutputMode Content { get; } = new("content");

    /// <summary><c>files_with_matches</c>: the paths of the files that matched.</summary>
    public static GrepOutputMode FilesWithMatches { get; } = new("files_with_matches");

    /// <summary><c>count</c>: how many matches each file holds.</summary>
    public static GrepOutputMode Count { get; } = new("count");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "content" or "files_with_matches" or "count";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(GrepOutputMode left, GrepOutputMode right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(GrepOutputMode left, GrepOutputMode right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator GrepOutputMode(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(GrepOutputMode other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="GrepOutputMode"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is GrepOutputMode other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
