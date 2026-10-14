namespace Claude.Agent;

/// <summary>Why a turn failed before the model answered, as the assistant frame's error names it. A code this library does not name is read as it is.</summary>
public readonly struct AssistantErrorCode : IEquatable<AssistantErrorCode>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public AssistantErrorCode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>account_on_hold</c>: the account is on hold.</summary>
    public static AssistantErrorCode AccountOnHold { get; } = new("account_on_hold");

    /// <summary><c>authentication_failed</c>: the login was refused.</summary>
    public static AssistantErrorCode AuthenticationFailed { get; } = new("authentication_failed");

    /// <summary><c>billing_error</c>: the account's billing refused the request.</summary>
    public static AssistantErrorCode BillingError { get; } = new("billing_error");

    /// <summary><c>cloud_credential_error</c>: a cloud provider's credential was refused.</summary>
    public static AssistantErrorCode CloudCredentialError { get; } = new("cloud_credential_error");

    /// <summary><c>invalid_request</c>: the request was malformed.</summary>
    public static AssistantErrorCode InvalidRequest { get; } = new("invalid_request");

    /// <summary><c>max_output_tokens</c>: the output token limit was reached.</summary>
    public static AssistantErrorCode MaxOutputTokens { get; } = new("max_output_tokens");

    /// <summary><c>model_not_found</c>: the model asked for does not exist for this account.</summary>
    public static AssistantErrorCode ModelNotFound { get; } = new("model_not_found");

    /// <summary><c>oauth_org_not_allowed</c>: the login's organization may not use this.</summary>
    public static AssistantErrorCode OAuthOrganizationNotAllowed { get; } = new("oauth_org_not_allowed");

    /// <summary><c>overloaded</c>: the API was overloaded.</summary>
    public static AssistantErrorCode Overloaded { get; } = new("overloaded");

    /// <summary><c>rate_limit</c>: the account's rate limit was reached.</summary>
    public static AssistantErrorCode RateLimit { get; } = new("rate_limit");

    /// <summary><c>server_error</c>: the API failed.</summary>
    public static AssistantErrorCode ServerError { get; } = new("server_error");

    /// <summary><c>unknown</c>: the binary could not say why.</summary>
    public static AssistantErrorCode Unknown { get; } = new("unknown");

    /// <summary><c>verification_required</c>: the account must be verified first.</summary>
    public static AssistantErrorCode VerificationRequired { get; } = new("verification_required");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "account_on_hold" or "authentication_failed" or "billing_error" or "cloud_credential_error" or
        "invalid_request" or "max_output_tokens" or "model_not_found" or "oauth_org_not_allowed" or
        "overloaded" or "rate_limit" or "server_error" or "unknown" or "verification_required";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(AssistantErrorCode left, AssistantErrorCode right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(AssistantErrorCode left, AssistantErrorCode right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator AssistantErrorCode(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(AssistantErrorCode other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="AssistantErrorCode"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is AssistantErrorCode other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
