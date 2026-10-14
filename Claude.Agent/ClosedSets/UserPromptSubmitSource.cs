namespace Claude.Agent;

/// <summary>What submitted a prompt. A source this library does not name is read as it is.</summary>
public readonly struct UserPromptSubmitSource : IEquatable<UserPromptSubmitSource>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public UserPromptSubmitSource(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>user</c>: a person typed it.</summary>
    public static UserPromptSubmitSource User { get; } = new("user");

    /// <summary><c>sdk</c>: a program sent it over the wire.</summary>
    public static UserPromptSubmitSource Sdk { get; } = new("sdk");

    /// <summary><c>system</c>: the binary raised it itself.</summary>
    public static UserPromptSubmitSource System { get; } = new("system");

    /// <summary><c>loop_wakeup</c>: a running loop came round again.</summary>
    public static UserPromptSubmitSource LoopWakeup { get; } = new("loop_wakeup");

    /// <summary><c>schedule_wakeup</c>: a schedule came due.</summary>
    public static UserPromptSubmitSource ScheduleWakeup { get; } = new("schedule_wakeup");

    /// <summary><c>poll_event</c>: something the binary was watching happened.</summary>
    public static UserPromptSubmitSource PollEvent { get; } = new("poll_event");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "user" or "sdk" or "system" or "loop_wakeup" or "schedule_wakeup" or "poll_event";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(UserPromptSubmitSource left, UserPromptSubmitSource right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(UserPromptSubmitSource left, UserPromptSubmitSource right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator UserPromptSubmitSource(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(UserPromptSubmitSource other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="UserPromptSubmitSource"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is UserPromptSubmitSource other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
