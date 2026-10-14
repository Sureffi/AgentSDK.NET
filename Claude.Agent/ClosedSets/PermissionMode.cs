namespace Claude.Agent;

/// <summary>How the binary treats a tool use no rule decides. A mode this library does not name is sent and read as it is.</summary>
public readonly struct PermissionMode : IEquatable<PermissionMode>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public PermissionMode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>default</c>: ask.</summary>
    public static PermissionMode Default { get; } = new("default");

    /// <summary><c>acceptEdits</c>: allow file edits without asking; ask for the rest.</summary>
    public static PermissionMode AcceptEdits { get; } = new("acceptEdits");

    /// <summary><c>plan</c>: plan only; nothing is edited or run.</summary>
    public static PermissionMode Plan { get; } = new("plan");

    /// <summary><c>bypassPermissions</c>: allow everything without asking.</summary>
    public static PermissionMode BypassPermissions { get; } = new("bypassPermissions");

    /// <summary><c>dontAsk</c>: deny whatever is not already allowed, without asking.</summary>
    public static PermissionMode DontAsk { get; } = new("dontAsk");

    /// <summary><c>auto</c>: let the binary's classifier decide instead of asking.</summary>
    public static PermissionMode Auto { get; } = new("auto");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "default" or "acceptEdits" or "plan" or "bypassPermissions" or "dontAsk" or "auto";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(PermissionMode left, PermissionMode right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(PermissionMode left, PermissionMode right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator PermissionMode(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(PermissionMode other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="PermissionMode"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is PermissionMode other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
