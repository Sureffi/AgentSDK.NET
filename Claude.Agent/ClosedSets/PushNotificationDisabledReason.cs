namespace Claude.Agent;

/// <summary>Why a push notification was not pushed. A value this library does not name is read as it is.</summary>
public readonly struct PushNotificationDisabledReason : IEquatable<PushNotificationDisabledReason>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public PushNotificationDisabledReason(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>config_off</c>: the configuration turns push off.</summary>
    public static PushNotificationDisabledReason ConfigurationOff { get; } = new("config_off");

    /// <summary><c>user_present</c>: the user is at the terminal, so nothing was pushed.</summary>
    public static PushNotificationDisabledReason UserPresent { get; } = new("user_present");

    /// <summary><c>no_transport</c>: nothing is wired up to carry it.</summary>
    public static PushNotificationDisabledReason NoTransport { get; } = new("no_transport");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "config_off" or "user_present" or "no_transport";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(PushNotificationDisabledReason left, PushNotificationDisabledReason right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(PushNotificationDisabledReason left, PushNotificationDisabledReason right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator PushNotificationDisabledReason(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(PushNotificationDisabledReason other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="PushNotificationDisabledReason"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is PushNotificationDisabledReason other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
