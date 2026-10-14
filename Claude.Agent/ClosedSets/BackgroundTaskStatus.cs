namespace Claude.Agent;

/// <summary>Where a background task stands, as the binary keeps it: what an update's patch says the task moved to, and what a stop hook is told of a task still in flight, which the binary lists only while it is pending or running. A status this library does not name is read as it is.</summary>
public readonly struct BackgroundTaskStatus : IEquatable<BackgroundTaskStatus>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public BackgroundTaskStatus(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>pending</c>: not started yet.</summary>
    public static BackgroundTaskStatus Pending { get; } = new("pending");

    /// <summary><c>running</c>: working.</summary>
    public static BackgroundTaskStatus Running { get; } = new("running");

    /// <summary><c>completed</c>: finished its work.</summary>
    public static BackgroundTaskStatus Completed { get; } = new("completed");

    /// <summary><c>failed</c>: ended in an error.</summary>
    public static BackgroundTaskStatus Failed { get; } = new("failed");

    /// <summary><c>killed</c>: stopped from outside.</summary>
    public static BackgroundTaskStatus Killed { get; } = new("killed");

    /// <summary><c>paused</c>: held, to be resumed.</summary>
    public static BackgroundTaskStatus Paused { get; } = new("paused");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "pending" or "running" or "completed" or "failed" or "killed" or "paused";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(BackgroundTaskStatus left, BackgroundTaskStatus right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(BackgroundTaskStatus left, BackgroundTaskStatus right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator BackgroundTaskStatus(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(BackgroundTaskStatus other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="BackgroundTaskStatus"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is BackgroundTaskStatus other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
