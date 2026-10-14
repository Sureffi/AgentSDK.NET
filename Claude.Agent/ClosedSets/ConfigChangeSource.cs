namespace Claude.Agent;

/// <summary>Which configuration changed under a running session. A source this library does not name is read as it is.</summary>
public readonly struct ConfigChangeSource : IEquatable<ConfigChangeSource>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public ConfigChangeSource(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>user_settings</c>: the account's own settings.</summary>
    public static ConfigChangeSource UserSettings { get; } = new("user_settings");

    /// <summary><c>project_settings</c>: the settings checked into the project.</summary>
    public static ConfigChangeSource ProjectSettings { get; } = new("project_settings");

    /// <summary><c>local_settings</c>: the settings kept beside the project and not checked in.</summary>
    public static ConfigChangeSource LocalSettings { get; } = new("local_settings");

    /// <summary><c>policy_settings</c>: the settings an administrator fixes.</summary>
    public static ConfigChangeSource PolicySettings { get; } = new("policy_settings");

    /// <summary><c>skills</c>: the skills on offer.</summary>
    public static ConfigChangeSource Skills { get; } = new("skills");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "user_settings" or "project_settings" or "local_settings" or "policy_settings" or "skills";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(ConfigChangeSource left, ConfigChangeSource right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(ConfigChangeSource left, ConfigChangeSource right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator ConfigChangeSource(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(ConfigChangeSource other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="ConfigChangeSource"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is ConfigChangeSource other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
