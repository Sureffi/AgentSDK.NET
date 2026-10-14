namespace Claude.Agent;

/// <summary>What became of a prompt the binary is tracking. A state this library does not name is read as it is.</summary>
public readonly struct CommandLifecycleState : IEquatable<CommandLifecycleState>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public CommandLifecycleState(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>queued</c>: the binary has read the prompt.</summary>
    public static CommandLifecycleState Queued { get; } = new("queued");

    /// <summary><c>started</c>: the prompt's turn has begun.</summary>
    public static CommandLifecycleState Started { get; } = new("started");

    /// <summary><c>completed</c>: the prompt's turn is done.</summary>
    public static CommandLifecycleState Completed { get; } = new("completed");

    /// <summary><c>cancelled</c>: an interrupt ended the prompt's turn.</summary>
    public static CommandLifecycleState Cancelled { get; } = new("cancelled");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "queued" or "started" or "completed" or "cancelled";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(CommandLifecycleState left, CommandLifecycleState right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(CommandLifecycleState left, CommandLifecycleState right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator CommandLifecycleState(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(CommandLifecycleState other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="CommandLifecycleState"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is CommandLifecycleState other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
