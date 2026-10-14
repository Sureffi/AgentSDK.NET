namespace Claude.Agent;

/// <summary>Where the binary found the credential it runs on. One source this library does not name, though the reference declares it: the environment variable a key is set in, which nothing that ships here may spell. It is read as it is, as any source this library does not name is, and <see cref="IsDefined"/> is false for it.</summary>
public readonly struct ApiKeySource : IEquatable<ApiKeySource>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public ApiKeySource(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>none</c>: no key; the binary runs on the account it is logged into.</summary>
    public static ApiKeySource None { get; } = new("none");

    /// <summary><c>/login managed key</c>: a key the binary manages for the login.</summary>
    public static ApiKeySource LoginManagedKey { get; } = new("/login managed key");

    /// <summary><c>apiKeyHelper</c>: the helper command the settings name.</summary>
    public static ApiKeySource ApiKeyHelper { get; } = new("apiKeyHelper");

    /// <summary><c>oauth</c>: the login's own token.</summary>
    public static ApiKeySource OAuth { get; } = new("oauth");

    /// <summary><c>org</c>: the organization's.</summary>
    public static ApiKeySource Organization { get; } = new("org");

    /// <summary><c>project</c>: the project's.</summary>
    public static ApiKeySource Project { get; } = new("project");

    /// <summary><c>temporary</c>: one issued for this run alone.</summary>
    public static ApiKeySource Temporary { get; } = new("temporary");

    /// <summary><c>user</c>: the user's.</summary>
    public static ApiKeySource User { get; } = new("user");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "none" or "/login managed key" or "apiKeyHelper" or "oauth" or "org" or "project" or "temporary" or "user";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(ApiKeySource left, ApiKeySource right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(ApiKeySource left, ApiKeySource right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator ApiKeySource(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(ApiKeySource other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="ApiKeySource"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is ApiKeySource other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
