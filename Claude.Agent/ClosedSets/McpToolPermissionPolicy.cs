namespace Claude.Agent;

/// <summary>What the binary does when a named tool of an MCP server is called. A policy this library does not name is sent and read as it is.</summary>
public readonly struct McpToolPermissionPolicy : IEquatable<McpToolPermissionPolicy>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public McpToolPermissionPolicy(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>always_allow</c>: run it without asking.</summary>
    public static McpToolPermissionPolicy AlwaysAllow { get; } = new("always_allow");

    /// <summary><c>always_ask</c>: ask every time.</summary>
    public static McpToolPermissionPolicy AlwaysAsk { get; } = new("always_ask");

    /// <summary><c>always_deny</c>: refuse it.</summary>
    public static McpToolPermissionPolicy AlwaysDeny { get; } = new("always_deny");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "always_allow" or "always_ask" or "always_deny";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(McpToolPermissionPolicy left, McpToolPermissionPolicy right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(McpToolPermissionPolicy left, McpToolPermissionPolicy right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator McpToolPermissionPolicy(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(McpToolPermissionPolicy other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="McpToolPermissionPolicy"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is McpToolPermissionPolicy other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
