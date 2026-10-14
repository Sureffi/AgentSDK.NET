namespace Claude.Agent;

/// <summary>Where the binary keeps a permission change. A destination this library does not name is sent and read as it is.</summary>
public readonly struct PermissionUpdateDestination : IEquatable<PermissionUpdateDestination>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public PermissionUpdateDestination(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>userSettings</c>: the user's settings file.</summary>
    public static PermissionUpdateDestination UserSettings { get; } = new("userSettings");

    /// <summary><c>projectSettings</c>: the project's settings file, checked in.</summary>
    public static PermissionUpdateDestination ProjectSettings { get; } = new("projectSettings");

    /// <summary><c>localSettings</c>: the project's local settings file, not checked in.</summary>
    public static PermissionUpdateDestination LocalSettings { get; } = new("localSettings");

    /// <summary><c>session</c>: this session alone.</summary>
    public static PermissionUpdateDestination Session { get; } = new("session");

    /// <summary><c>cliArg</c>: the rules the binary was started with.</summary>
    public static PermissionUpdateDestination CommandLineArgument { get; } = new("cliArg");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "userSettings" or "projectSettings" or "localSettings" or "session" or "cliArg";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(PermissionUpdateDestination left, PermissionUpdateDestination right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(PermissionUpdateDestination left, PermissionUpdateDestination right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator PermissionUpdateDestination(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(PermissionUpdateDestination other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="PermissionUpdateDestination"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is PermissionUpdateDestination other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
