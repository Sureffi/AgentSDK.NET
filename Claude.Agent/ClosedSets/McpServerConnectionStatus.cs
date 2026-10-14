namespace Claude.Agent;

/// <summary>Where an MCP server stands with the binary. A status this library does not name is read as it is.</summary>
public readonly struct McpServerConnectionStatus : IEquatable<McpServerConnectionStatus>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public McpServerConnectionStatus(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>connected</c>: the server answered and its tools are loaded.</summary>
    public static McpServerConnectionStatus Connected { get; } = new("connected");

    /// <summary><c>failed</c>: the server could not be reached.</summary>
    public static McpServerConnectionStatus Failed { get; } = new("failed");

    /// <summary><c>needs-auth</c>: the server wants a login first.</summary>
    public static McpServerConnectionStatus NeedsAuthentication { get; } = new("needs-auth");

    /// <summary><c>pending</c>: the binary is still connecting.</summary>
    public static McpServerConnectionStatus Pending { get; } = new("pending");

    /// <summary><c>disabled</c>: the server is configured and turned off.</summary>
    public static McpServerConnectionStatus Disabled { get; } = new("disabled");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "connected" or "failed" or "needs-auth" or "pending" or "disabled";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(McpServerConnectionStatus left, McpServerConnectionStatus right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(McpServerConnectionStatus left, McpServerConnectionStatus right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator McpServerConnectionStatus(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(McpServerConnectionStatus other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="McpServerConnectionStatus"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is McpServerConnectionStatus other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
