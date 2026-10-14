namespace Claude.Agent;

/// <summary>How a turn ended, as the result's subtype says. A subtype this library does not name is read as it is.</summary>
public readonly struct ResultMessageSubtype : IEquatable<ResultMessageSubtype>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public ResultMessageSubtype(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>success</c>: the turn ran to its end.</summary>
    public static ResultMessageSubtype Success { get; } = new("success");

    /// <summary><c>error_max_turns</c>: the turn count ran out.</summary>
    public static ResultMessageSubtype ErrorMaxTurns { get; } = new("error_max_turns");

    /// <summary><c>error_during_execution</c>: the turn failed while running.</summary>
    public static ResultMessageSubtype ErrorDuringExecution { get; } = new("error_during_execution");

    /// <summary><c>error_max_budget_usd</c>: the session's budget ran out.</summary>
    public static ResultMessageSubtype ErrorMaxBudgetUsd { get; } = new("error_max_budget_usd");

    /// <summary><c>error_max_structured_output_retries</c>: the model never produced output matching the schema.</summary>
    public static ResultMessageSubtype ErrorMaxStructuredOutputRetries { get; } = new("error_max_structured_output_retries");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "success" or "error_max_turns" or "error_during_execution" or "error_max_budget_usd" or
        "error_max_structured_output_retries";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(ResultMessageSubtype left, ResultMessageSubtype right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(ResultMessageSubtype left, ResultMessageSubtype right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator ResultMessageSubtype(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(ResultMessageSubtype other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="ResultMessageSubtype"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is ResultMessageSubtype other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
