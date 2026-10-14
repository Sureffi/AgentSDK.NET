namespace Claude.Agent;

/// <summary>What one API stream event is about. A type this library does not name is read as it is.</summary>
public readonly struct StreamEventType : IEquatable<StreamEventType>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public StreamEventType(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>message_start</c>: a message begins.</summary>
    public static StreamEventType MessageStart { get; } = new("message_start");

    /// <summary><c>message_delta</c>: the message's own fields change.</summary>
    public static StreamEventType MessageDelta { get; } = new("message_delta");

    /// <summary><c>message_stop</c>: the message ends.</summary>
    public static StreamEventType MessageStop { get; } = new("message_stop");

    /// <summary><c>content_block_start</c>: a content block begins.</summary>
    public static StreamEventType ContentBlockStart { get; } = new("content_block_start");

    /// <summary><c>content_block_delta</c>: a content block grows.</summary>
    public static StreamEventType ContentBlockDelta { get; } = new("content_block_delta");

    /// <summary><c>content_block_stop</c>: a content block ends.</summary>
    public static StreamEventType ContentBlockStop { get; } = new("content_block_stop");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "message_start" or "message_delta" or "message_stop" or "content_block_start" or "content_block_delta" or
        "content_block_stop";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(StreamEventType left, StreamEventType right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(StreamEventType left, StreamEventType right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator StreamEventType(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(StreamEventType other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="StreamEventType"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is StreamEventType other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
