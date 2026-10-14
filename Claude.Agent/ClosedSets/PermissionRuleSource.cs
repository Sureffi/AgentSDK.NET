namespace Claude.Agent;

/// <summary>Where a permission rule comes from, as the binary names it: the settings file, flag, command or session that holds the rule that forced an ask. A value this library does not name is read as it is.</summary>
public readonly struct PermissionRuleSource : IEquatable<PermissionRuleSource>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public PermissionRuleSource(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>userSettings</c>: the user's settings file.</summary>
    public static PermissionRuleSource UserSettings { get; } = new("userSettings");

    /// <summary><c>projectSettings</c>: the project's settings file, checked in.</summary>
    public static PermissionRuleSource ProjectSettings { get; } = new("projectSettings");

    /// <summary><c>localSettings</c>: the project's local settings file, not checked in.</summary>
    public static PermissionRuleSource LocalSettings { get; } = new("localSettings");

    /// <summary><c>flagSettings</c>: the settings the binary was handed by flag, and those a host merged into that layer since.</summary>
    public static PermissionRuleSource FlagSettings { get; } = new("flagSettings");

    /// <summary><c>policySettings</c>: the settings an enterprise manages.</summary>
    public static PermissionRuleSource PolicySettings { get; } = new("policySettings");

    /// <summary><c>cliArg</c>: the rules the binary was started with.</summary>
    public static PermissionRuleSource CommandLineArgument { get; } = new("cliArg");

    /// <summary><c>command</c>: the configuration of the command that is running.</summary>
    public static PermissionRuleSource Command { get; } = new("command");

    /// <summary><c>session</c>: this session alone.</summary>
    public static PermissionRuleSource Session { get; } = new("session");

    /// <summary><c>toolsNarrowing</c>: the deny rules the binary makes for the tools its command line leaves out.</summary>
    public static PermissionRuleSource ToolsNarrowing { get; } = new("toolsNarrowing");

    /// <summary><c>mcpServerPolicy</c>: the policy an MCP server's tools are held to.</summary>
    public static PermissionRuleSource McpServerPolicy { get; } = new("mcpServerPolicy");

    /// <summary><c>hostCredential</c>: the guard a cloud session keeps over the host's credentials.</summary>
    public static PermissionRuleSource HostCredential { get; } = new("hostCredential");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "userSettings" or "projectSettings" or "localSettings" or "flagSettings" or "policySettings" or
        "cliArg" or "command" or "session" or "toolsNarrowing" or "mcpServerPolicy" or "hostCredential";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(PermissionRuleSource left, PermissionRuleSource right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(PermissionRuleSource left, PermissionRuleSource right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator PermissionRuleSource(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(PermissionRuleSource other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="PermissionRuleSource"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is PermissionRuleSource other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
