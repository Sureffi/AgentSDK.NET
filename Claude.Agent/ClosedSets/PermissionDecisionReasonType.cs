namespace Claude.Agent;

/// <summary>Why the binary is asking whether a tool may run, as a value rather than as prose. A reason this library does not name is read as it is.</summary>
public readonly struct PermissionDecisionReasonType : IEquatable<PermissionDecisionReasonType>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public PermissionDecisionReasonType(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>rule</c>: a permission rule says to ask.</summary>
    public static PermissionDecisionReasonType Rule { get; } = new("rule");

    /// <summary><c>mode</c>: the permission mode says to ask.</summary>
    public static PermissionDecisionReasonType Mode { get; } = new("mode");

    /// <summary><c>subcommandResults</c>: one of the parts of a compound shell command needs a decision.</summary>
    public static PermissionDecisionReasonType SubcommandResults { get; } = new("subcommandResults");

    /// <summary><c>permissionPromptTool</c>: the tool named to answer permission prompts was asked.</summary>
    public static PermissionDecisionReasonType PermissionPromptTool { get; } = new("permissionPromptTool");

    /// <summary><c>hook</c>: a hook sent it to be asked.</summary>
    public static PermissionDecisionReasonType Hook { get; } = new("hook");

    /// <summary><c>asyncAgent</c>: an agent running on its own cannot answer for itself.</summary>
    public static PermissionDecisionReasonType AsynchronousAgent { get; } = new("asyncAgent");

    /// <summary><c>sandboxOverride</c>: the tool use would step outside the sandbox.</summary>
    public static PermissionDecisionReasonType SandboxOverride { get; } = new("sandboxOverride");

    /// <summary><c>workingDir</c>: the path is outside the directories the session may reach.</summary>
    public static PermissionDecisionReasonType WorkingDirectory { get; } = new("workingDir");

    /// <summary><c>safetyCheck</c>: a safety check on the tool use wants a person.</summary>
    public static PermissionDecisionReasonType SafetyCheck { get; } = new("safetyCheck");

    /// <summary><c>classifier</c>: the binary's classifier would not decide it.</summary>
    public static PermissionDecisionReasonType Classifier { get; } = new("classifier");

    /// <summary><c>other</c>: anything else the binary does not distinguish.</summary>
    public static PermissionDecisionReasonType Other { get; } = new("other");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "rule" or "mode" or "subcommandResults" or "permissionPromptTool" or "hook" or "asyncAgent" or
        "sandboxOverride" or "workingDir" or "safetyCheck" or "classifier" or "other";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(PermissionDecisionReasonType left, PermissionDecisionReasonType right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(PermissionDecisionReasonType left, PermissionDecisionReasonType right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator PermissionDecisionReasonType(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(PermissionDecisionReasonType other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="PermissionDecisionReasonType"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is PermissionDecisionReasonType other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
