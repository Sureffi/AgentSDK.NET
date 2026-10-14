namespace Claude.Agent;

/// <summary>Why the API's web search did not answer. A code this library does not name is read as it is.</summary>
public readonly struct WebSearchErrorCode : IEquatable<WebSearchErrorCode>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public WebSearchErrorCode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>invalid_tool_input</c>: the call's arguments did not fit the tool.</summary>
    public static WebSearchErrorCode InvalidToolInput { get; } = new("invalid_tool_input");

    /// <summary><c>unavailable</c>: the tool could not be reached.</summary>
    public static WebSearchErrorCode Unavailable { get; } = new("unavailable");

    /// <summary><c>max_uses_exceeded</c>: the turn had used the tool as often as it may.</summary>
    public static WebSearchErrorCode MaxUsesExceeded { get; } = new("max_uses_exceeded");

    /// <summary><c>too_many_requests</c>: the account is asking too often.</summary>
    public static WebSearchErrorCode TooManyRequests { get; } = new("too_many_requests");

    /// <summary><c>query_too_long</c>: the search terms were longer than the tool takes.</summary>
    public static WebSearchErrorCode QueryTooLong { get; } = new("query_too_long");

    /// <summary><c>request_too_large</c>: the call was larger than the tool takes.</summary>
    public static WebSearchErrorCode RequestTooLarge { get; } = new("request_too_large");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "invalid_tool_input" or "unavailable" or "max_uses_exceeded" or "too_many_requests"
            or "query_too_long" or "request_too_large";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(WebSearchErrorCode left, WebSearchErrorCode right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(WebSearchErrorCode left, WebSearchErrorCode right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator WebSearchErrorCode(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(WebSearchErrorCode other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="WebSearchErrorCode"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is WebSearchErrorCode other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
