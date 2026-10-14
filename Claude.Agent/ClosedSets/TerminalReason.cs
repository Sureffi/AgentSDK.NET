namespace Claude.Agent;

/// <summary>Why a turn ended, in the binary's own words, beside the result's subtype. A reason this library does not name is read as it is.</summary>
public readonly struct TerminalReason : IEquatable<TerminalReason>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public TerminalReason(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>completed</c>: the turn ran to its end.</summary>
    public static TerminalReason Completed { get; } = new("completed");

    /// <summary><c>max_turns</c>: the turn count ran out.</summary>
    public static TerminalReason MaxTurns { get; } = new("max_turns");

    /// <summary><c>aborted_streaming</c>: the turn was interrupted while the model was writing.</summary>
    public static TerminalReason AbortedStreaming { get; } = new("aborted_streaming");

    /// <summary><c>aborted_tools</c>: the turn was interrupted while a tool was running.</summary>
    public static TerminalReason AbortedTools { get; } = new("aborted_tools");

    /// <summary><c>api_error</c>: the API failed.</summary>
    public static TerminalReason ApiError { get; } = new("api_error");

    /// <summary><c>background_requested</c>: the work was moved to a background task.</summary>
    public static TerminalReason BackgroundRequested { get; } = new("background_requested");

    /// <summary><c>blocking_limit</c>: a rate limit stopped the turn.</summary>
    public static TerminalReason BlockingLimit { get; } = new("blocking_limit");

    /// <summary><c>budget_exhausted</c>: the session's budget ran out.</summary>
    public static TerminalReason BudgetExhausted { get; } = new("budget_exhausted");

    /// <summary><c>hook_stopped</c>: a hook stopped the turn.</summary>
    public static TerminalReason HookStopped { get; } = new("hook_stopped");

    /// <summary><c>image_error</c>: an image could not be read.</summary>
    public static TerminalReason ImageError { get; } = new("image_error");

    /// <summary><c>malformed_tool_use_exhausted</c>: the model kept writing tool uses that did not parse.</summary>
    public static TerminalReason MalformedToolUseExhausted { get; } = new("malformed_tool_use_exhausted");

    /// <summary><c>model_error</c>: the model failed.</summary>
    public static TerminalReason ModelError { get; } = new("model_error");

    /// <summary><c>prompt_too_long</c>: the prompt did not fit the context.</summary>
    public static TerminalReason PromptTooLong { get; } = new("prompt_too_long");

    /// <summary><c>rapid_refill_breaker</c>: the account's usage tripped the rapid-refill breaker.</summary>
    public static TerminalReason RapidRefillBreaker { get; } = new("rapid_refill_breaker");

    /// <summary><c>stop_hook_prevented</c>: a Stop hook kept the turn from ending.</summary>
    public static TerminalReason StopHookPrevented { get; } = new("stop_hook_prevented");

    /// <summary><c>structured_output_retry_exhausted</c>: the model never produced output matching the schema.</summary>
    public static TerminalReason StructuredOutputRetryExhausted { get; } = new("structured_output_retry_exhausted");

    /// <summary><c>tool_deferred</c>: a hook deferred a tool use and the turn handed it back.</summary>
    public static TerminalReason ToolDeferred { get; } = new("tool_deferred");

    /// <summary><c>tool_deferred_unavailable</c>: a tool use was deferred where deferring is not available.</summary>
    public static TerminalReason ToolDeferredUnavailable { get; } = new("tool_deferred_unavailable");

    /// <summary><c>turn_setup_failed</c>: the turn could not be set up.</summary>
    public static TerminalReason TurnSetupFailed { get; } = new("turn_setup_failed");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "completed" or "max_turns" or "aborted_streaming" or "aborted_tools" or "api_error" or
        "background_requested" or "blocking_limit" or "budget_exhausted" or "hook_stopped" or "image_error" or
        "malformed_tool_use_exhausted" or "model_error" or "prompt_too_long" or "rapid_refill_breaker" or
        "stop_hook_prevented" or "structured_output_retry_exhausted" or "tool_deferred" or
        "tool_deferred_unavailable" or "turn_setup_failed";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(TerminalReason left, TerminalReason right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(TerminalReason left, TerminalReason right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator TerminalReason(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(TerminalReason other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="TerminalReason"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is TerminalReason other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
