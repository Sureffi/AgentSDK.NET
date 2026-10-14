namespace Claude.Agent;

/// <summary>Why fast mode is not available to the session. A reason this library does not name is read as it is.</summary>
public readonly struct FastModeDisabledReason : IEquatable<FastModeDisabledReason>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public FastModeDisabledReason(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>free</c>: the account's plan does not carry it.</summary>
    public static FastModeDisabledReason Free { get; } = new("free");

    /// <summary><c>preference</c>: it is turned off in the settings.</summary>
    public static FastModeDisabledReason Preference { get; } = new("preference");

    /// <summary><c>extra_usage_disabled</c>: usage beyond the plan is turned off, and fast mode needs it.</summary>
    public static FastModeDisabledReason ExtraUsageDisabled { get; } = new("extra_usage_disabled");

    /// <summary><c>network_error</c>: whether it is available could not be read.</summary>
    public static FastModeDisabledReason NetworkError { get; } = new("network_error");

    /// <summary><c>unknown</c>: the binary could not say why.</summary>
    public static FastModeDisabledReason Unknown { get; } = new("unknown");

    /// <summary><c>not_first_party</c>: the session runs against a provider other than Anthropic's own API.</summary>
    public static FastModeDisabledReason NotFirstParty { get; } = new("not_first_party");

    /// <summary><c>disabled_by_env</c>: an environment variable turns it off.</summary>
    public static FastModeDisabledReason DisabledByEnvironment { get; } = new("disabled_by_env");

    /// <summary><c>model_not_allowed</c>: the chosen model does not offer it.</summary>
    public static FastModeDisabledReason ModelNotAllowed { get; } = new("model_not_allowed");

    /// <summary><c>sdk_opt_in_required</c>: a session driven over stream-json must ask for it; every recording carries this one.</summary>
    public static FastModeDisabledReason SdkOptInRequired { get; } = new("sdk_opt_in_required");

    /// <summary><c>pending</c>: whether it is available is still being read.</summary>
    public static FastModeDisabledReason Pending { get; } = new("pending");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "free" or "preference" or "extra_usage_disabled" or "network_error" or "unknown" or
        "not_first_party" or "disabled_by_env" or "model_not_allowed" or "sdk_opt_in_required" or "pending";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(FastModeDisabledReason left, FastModeDisabledReason right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(FastModeDisabledReason left, FastModeDisabledReason right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator FastModeDisabledReason(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(FastModeDisabledReason other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="FastModeDisabledReason"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is FastModeDisabledReason other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
