namespace Claude.Agent;

/// <summary>Why overage use is off for the account. A reason this library does not name is read as it is.</summary>
public readonly struct OverageDisabledReason : IEquatable<OverageDisabledReason>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public OverageDisabledReason(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>fetch_error</c>: the binary could not read the account's limits.</summary>
    public static OverageDisabledReason FetchError { get; } = new("fetch_error");

    /// <summary><c>group_zero_credit_limit</c>: the group's credit limit is zero.</summary>
    public static OverageDisabledReason GroupZeroCreditLimit { get; } = new("group_zero_credit_limit");

    /// <summary><c>member_level_disabled</c>: turned off for this member.</summary>
    public static OverageDisabledReason MemberLevelDisabled { get; } = new("member_level_disabled");

    /// <summary><c>member_zero_credit_limit</c>: the member's credit limit is zero.</summary>
    public static OverageDisabledReason MemberZeroCreditLimit { get; } = new("member_zero_credit_limit");

    /// <summary><c>no_limits_configured</c>: no limits are configured.</summary>
    public static OverageDisabledReason NoLimitsConfigured { get; } = new("no_limits_configured");

    /// <summary><c>org_level_disabled</c>: turned off for the organization.</summary>
    public static OverageDisabledReason OrganizationLevelDisabled { get; } = new("org_level_disabled");

    /// <summary><c>org_level_disabled_until</c>: turned off for the organization until a date.</summary>
    public static OverageDisabledReason OrganizationLevelDisabledUntil { get; } = new("org_level_disabled_until");

    /// <summary><c>org_service_level_disabled</c>: turned off by the organization's service level.</summary>
    public static OverageDisabledReason OrganizationServiceLevelDisabled { get; } = new("org_service_level_disabled");

    /// <summary><c>out_of_credits</c>: the credits are spent.</summary>
    public static OverageDisabledReason OutOfCredits { get; } = new("out_of_credits");

    /// <summary><c>overage_not_provisioned</c>: overage was never set up.</summary>
    public static OverageDisabledReason OverageNotProvisioned { get; } = new("overage_not_provisioned");

    /// <summary><c>seat_tier_level_disabled</c>: turned off by the seat's tier.</summary>
    public static OverageDisabledReason SeatTierLevelDisabled { get; } = new("seat_tier_level_disabled");

    /// <summary><c>seat_tier_zero_credit_limit</c>: the seat tier's credit limit is zero.</summary>
    public static OverageDisabledReason SeatTierZeroCreditLimit { get; } = new("seat_tier_zero_credit_limit");

    /// <summary><c>unknown</c>: the binary could not say why.</summary>
    public static OverageDisabledReason Unknown { get; } = new("unknown");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "fetch_error" or "group_zero_credit_limit" or "member_level_disabled" or "member_zero_credit_limit" or
        "no_limits_configured" or "org_level_disabled" or "org_level_disabled_until" or
        "org_service_level_disabled" or "out_of_credits" or "overage_not_provisioned" or
        "seat_tier_level_disabled" or "seat_tier_zero_credit_limit" or "unknown";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(OverageDisabledReason left, OverageDisabledReason right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(OverageDisabledReason left, OverageDisabledReason right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator OverageDisabledReason(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(OverageDisabledReason other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="OverageDisabledReason"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is OverageDisabledReason other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
