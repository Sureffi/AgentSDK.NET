namespace Claude.Agent;

/// <summary>Why the API's web fetch did not answer. A code this library does not name is read as it is.</summary>
public readonly struct WebFetchErrorCode : IEquatable<WebFetchErrorCode>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public WebFetchErrorCode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>invalid_tool_input</c>: the call's arguments did not fit the tool.</summary>
    public static WebFetchErrorCode InvalidToolInput { get; } = new("invalid_tool_input");

    /// <summary><c>url_too_long</c>: the address was longer than the tool takes.</summary>
    public static WebFetchErrorCode UrlTooLong { get; } = new("url_too_long");

    /// <summary><c>url_not_allowed</c>: the address is not one the call may fetch.</summary>
    public static WebFetchErrorCode UrlNotAllowed { get; } = new("url_not_allowed");

    /// <summary><c>url_not_in_prior_context</c>: the address had not appeared in the turn already.</summary>
    public static WebFetchErrorCode UrlNotInPriorContext { get; } = new("url_not_in_prior_context");

    /// <summary><c>url_not_accessible</c>: the address did not answer.</summary>
    public static WebFetchErrorCode UrlNotAccessible { get; } = new("url_not_accessible");

    /// <summary><c>unsupported_content_type</c>: what came back is not a format the tool reads.</summary>
    public static WebFetchErrorCode UnsupportedContentType { get; } = new("unsupported_content_type");

    /// <summary><c>too_many_requests</c>: the account is asking too often.</summary>
    public static WebFetchErrorCode TooManyRequests { get; } = new("too_many_requests");

    /// <summary><c>max_uses_exceeded</c>: the turn had used the tool as often as it may.</summary>
    public static WebFetchErrorCode MaxUsesExceeded { get; } = new("max_uses_exceeded");

    /// <summary><c>unavailable</c>: the tool could not be reached.</summary>
    public static WebFetchErrorCode Unavailable { get; } = new("unavailable");

    /// <summary><c>content_too_large</c>: what came back is larger than the tool takes.</summary>
    public static WebFetchErrorCode ContentTooLarge { get; } = new("content_too_large");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "invalid_tool_input" or "url_too_long" or "url_not_allowed" or "url_not_in_prior_context"
            or "url_not_accessible" or "unsupported_content_type" or "too_many_requests" or "max_uses_exceeded"
            or "unavailable" or "content_too_large";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(WebFetchErrorCode left, WebFetchErrorCode right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(WebFetchErrorCode left, WebFetchErrorCode right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator WebFetchErrorCode(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(WebFetchErrorCode other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="WebFetchErrorCode"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is WebFetchErrorCode other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
