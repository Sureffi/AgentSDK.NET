namespace Claude.Agent;

/// <summary>What changed the model, read after it has. A source this library does not name is read as it is.</summary>
public readonly struct PostModelSwitchSource : IEquatable<PostModelSwitchSource>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public PostModelSwitchSource(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>command</c>: a slash command.</summary>
    public static PostModelSwitchSource Command { get; } = new("command");

    /// <summary><c>picker</c>: the person chose from the binary's list.</summary>
    public static PostModelSwitchSource Picker { get; } = new("picker");

    /// <summary><c>sdk</c>: a program asked over the wire.</summary>
    public static PostModelSwitchSource Sdk { get; } = new("sdk");

    /// <summary><c>auto</c>: the binary chose on its own.</summary>
    public static PostModelSwitchSource Auto { get; } = new("auto");

    /// <summary><c>resume</c>: a resumed session brought its model with it.</summary>
    public static PostModelSwitchSource Resume { get; } = new("resume");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "command" or "picker" or "sdk" or "auto" or "resume";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(PostModelSwitchSource left, PostModelSwitchSource right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(PostModelSwitchSource left, PostModelSwitchSource right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator PostModelSwitchSource(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(PostModelSwitchSource other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="PostModelSwitchSource"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is PostModelSwitchSource other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
