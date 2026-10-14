using System.Text.Json.Serialization;

namespace Claude.Agent;

/// <summary>Why a fallback credit presented with a request did not change what it was billed. A reason this library does not name is read as it is. Serialized, it is the name as a string, the default the empty string; the empty string reads back as the default.</summary>
[JsonConverter(typeof(FallbackCreditNotAppliedReasonJsonConverter))]
public readonly struct FallbackCreditNotAppliedReason : IEquatable<FallbackCreditNotAppliedReason>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public FallbackCreditNotAppliedReason(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>body_mismatch</c>: the request is not the one the credit was issued against.</summary>
    public static FallbackCreditNotAppliedReason BodyMismatch { get; } = new("body_mismatch");

    /// <summary><c>continuation_excluded</c>: the credit does not cover a continuation of this shape.</summary>
    public static FallbackCreditNotAppliedReason ContinuationExcluded { get; } = new("continuation_excluded");

    /// <summary><c>continuation_only</c>: the credit covers a continuation alone, and this request is not one.</summary>
    public static FallbackCreditNotAppliedReason ContinuationOnly { get; } = new("continuation_only");

    /// <summary><c>expired</c>: the credit's window had closed.</summary>
    public static FallbackCreditNotAppliedReason Expired { get; } = new("expired");

    /// <summary><c>invalid_target_model</c>: the request names a model the credit does not cover.</summary>
    public static FallbackCreditNotAppliedReason InvalidTargetModel { get; } = new("invalid_target_model");

    /// <summary><c>not_enabled</c>: fallback credits are not turned on for the account.</summary>
    public static FallbackCreditNotAppliedReason NotEnabled { get; } = new("not_enabled");

    /// <summary><c>reprice_unavailable</c>: the price could not be recomputed.</summary>
    public static FallbackCreditNotAppliedReason RepriceUnavailable { get; } = new("reprice_unavailable");

    /// <summary><c>temporarily_unavailable</c>: the check could not run this time.</summary>
    public static FallbackCreditNotAppliedReason TemporarilyUnavailable { get; } = new("temporarily_unavailable");

    /// <summary><c>variant_fields_present</c>: the request carries fields the credit does not cover; they are listed to remove.</summary>
    public static FallbackCreditNotAppliedReason VariantFieldsPresent { get; } = new("variant_fields_present");

    /// <summary><c>wrong_organization</c>: the credit belongs to another organization.</summary>
    public static FallbackCreditNotAppliedReason WrongOrganization { get; } = new("wrong_organization");

    /// <summary><c>wrong_platform</c>: the credit belongs to another platform.</summary>
    public static FallbackCreditNotAppliedReason WrongPlatform { get; } = new("wrong_platform");

    /// <summary><c>wrong_workspace</c>: the credit belongs to another workspace.</summary>
    public static FallbackCreditNotAppliedReason WrongWorkspace { get; } = new("wrong_workspace");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "body_mismatch" or "continuation_excluded" or "continuation_only" or "expired" or
        "invalid_target_model" or "not_enabled" or "reprice_unavailable" or "temporarily_unavailable" or
        "variant_fields_present" or "wrong_organization" or "wrong_platform" or "wrong_workspace";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(FallbackCreditNotAppliedReason left, FallbackCreditNotAppliedReason right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(FallbackCreditNotAppliedReason left, FallbackCreditNotAppliedReason right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator FallbackCreditNotAppliedReason(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(FallbackCreditNotAppliedReason other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="FallbackCreditNotAppliedReason"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is FallbackCreditNotAppliedReason other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
