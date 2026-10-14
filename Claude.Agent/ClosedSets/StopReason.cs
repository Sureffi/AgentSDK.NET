namespace Claude.Agent;

/// <summary>Why the model stopped writing, as the API and the binary name it. A reason this library does not name is read as it is.</summary>
public readonly struct StopReason : IEquatable<StopReason>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public StopReason(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>end_turn</c>: the model finished its answer.</summary>
    public static StopReason EndTurn { get; } = new("end_turn");

    /// <summary><c>max_tokens</c>: the output token limit was reached.</summary>
    public static StopReason MaxTokens { get; } = new("max_tokens");

    /// <summary><c>stop_sequence</c>: a stop sequence was written.</summary>
    public static StopReason StopSequence { get; } = new("stop_sequence");

    /// <summary><c>tool_use</c>: the model asked for a tool.</summary>
    public static StopReason ToolUse { get; } = new("tool_use");

    /// <summary><c>pause_turn</c>: a long-running turn was paused and may be continued.</summary>
    public static StopReason PauseTurn { get; } = new("pause_turn");

    /// <summary><c>refusal</c>: the model declined to answer.</summary>
    public static StopReason Refusal { get; } = new("refusal");

    /// <summary><c>compaction</c>: the conversation was compacted mid-turn.</summary>
    public static StopReason Compaction { get; } = new("compaction");

    /// <summary><c>model_context_window_exceeded</c>: the conversation outgrew the model's context.</summary>
    public static StopReason ModelContextWindowExceeded { get; } = new("model_context_window_exceeded");

    /// <summary><c>tool_deferred</c>: a hook deferred the tool use the model asked for.</summary>
    public static StopReason ToolDeferred { get; } = new("tool_deferred");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "end_turn" or "max_tokens" or "stop_sequence" or "tool_use" or "pause_turn" or "refusal" or
        "compaction" or "model_context_window_exceeded" or "tool_deferred";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(StopReason left, StopReason right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(StopReason left, StopReason right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator StopReason(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(StopReason other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="StopReason"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is StopReason other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
