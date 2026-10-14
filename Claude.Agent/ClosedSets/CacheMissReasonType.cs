namespace Claude.Agent;

/// <summary>Why the prompt cache was missed. A reason this library does not name is read as it is.</summary>
public readonly struct CacheMissReasonType : IEquatable<CacheMissReasonType>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public CacheMissReasonType(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>model_changed</c>: the turn asked another model.</summary>
    public static CacheMissReasonType ModelChanged { get; } = new("model_changed");

    /// <summary><c>system_changed</c>: the system prompt changed.</summary>
    public static CacheMissReasonType SystemChanged { get; } = new("system_changed");

    /// <summary><c>tools_changed</c>: the tool list changed.</summary>
    public static CacheMissReasonType ToolsChanged { get; } = new("tools_changed");

    /// <summary><c>messages_changed</c>: an earlier message changed.</summary>
    public static CacheMissReasonType MessagesChanged { get; } = new("messages_changed");

    /// <summary><c>previous_message_not_found</c>: the message the cache hung on is gone.</summary>
    public static CacheMissReasonType PreviousMessageNotFound { get; } = new("previous_message_not_found");

    /// <summary><c>unavailable</c>: the cache could not be reached.</summary>
    public static CacheMissReasonType Unavailable { get; } = new("unavailable");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "model_changed" or "system_changed" or "tools_changed" or "messages_changed" or
        "previous_message_not_found" or "unavailable";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(CacheMissReasonType left, CacheMissReasonType right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(CacheMissReasonType left, CacheMissReasonType right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator CacheMissReasonType(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(CacheMissReasonType other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="CacheMissReasonType"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is CacheMissReasonType other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
