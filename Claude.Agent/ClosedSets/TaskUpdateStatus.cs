namespace Claude.Agent;

/// <summary>What a TaskUpdate sets a task's status to: the three a task stands in, and the one that takes it off the list.</summary>
public readonly struct TaskUpdateStatus : IEquatable<TaskUpdateStatus>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public TaskUpdateStatus(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>pending</c>: nobody has started it.</summary>
    public static TaskUpdateStatus Pending { get; } = new("pending");

    /// <summary><c>in_progress</c>: someone is working on it now.</summary>
    public static TaskUpdateStatus InProgress { get; } = new("in_progress");

    /// <summary><c>completed</c>: it is done.</summary>
    public static TaskUpdateStatus Completed { get; } = new("completed");

    /// <summary><c>deleted</c>: it is off the list.</summary>
    public static TaskUpdateStatus Deleted { get; } = new("deleted");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "pending" or "in_progress" or "completed" or "deleted";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(TaskUpdateStatus left, TaskUpdateStatus right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(TaskUpdateStatus left, TaskUpdateStatus right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator TaskUpdateStatus(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(TaskUpdateStatus other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="TaskUpdateStatus"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is TaskUpdateStatus other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
