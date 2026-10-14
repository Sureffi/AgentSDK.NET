namespace Claude.Agent;

/// <summary>Where an MCP server came from, as the binary names it under two keys: its source, <c>sdk</c> for a server this library serves in process and otherwise where the binary read the server's configuration, and its scope, where the binary read the configuration. A value this library does not name is kept as it is.</summary>
public readonly struct McpServerSource : IEquatable<McpServerSource>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public McpServerSource(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>claudeai</c>: a connector of the claude.ai account the binary is logged into.</summary>
    public static McpServerSource ClaudeAI { get; } = new("claudeai");

    /// <summary><c>dynamic</c>: a server the host handed the binary for this session.</summary>
    public static McpServerSource Dynamic { get; } = new("dynamic");

    /// <summary><c>sdk</c>: a server this library serves in process.</summary>
    public static McpServerSource Sdk { get; } = new("sdk");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "claudeai" or "dynamic" or "sdk";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(McpServerSource left, McpServerSource right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(McpServerSource left, McpServerSource right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator McpServerSource(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(McpServerSource other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is an <see cref="McpServerSource"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is McpServerSource other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
