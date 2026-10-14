namespace Claude.Agent;

/// <summary>Why the binary never got as far as a turn, on a result that failed at startup. A reason this library does not name is read as it is.</summary>
public readonly struct StartupFailureReason : IEquatable<StartupFailureReason>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public StartupFailureReason(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>org_pin_api_key_conflict</c>: the organization pins a credential the caller overrode.</summary>
    public static StartupFailureReason OrgPinApiKeyConflict { get; } = new("org_pin_api_key_conflict");

    /// <summary><c>org_verify_failed</c>: the organization could not be verified.</summary>
    public static StartupFailureReason OrgVerifyFailed { get; } = new("org_verify_failed");

    /// <summary><c>org_pin_mismatch</c>: the account is not in the pinned organization.</summary>
    public static StartupFailureReason OrgPinMismatch { get; } = new("org_pin_mismatch");

    /// <summary><c>managed_settings_invalid</c>: the managed settings file did not parse.</summary>
    public static StartupFailureReason ManagedSettingsInvalid { get; } = new("managed_settings_invalid");

    /// <summary><c>remote_settings_required_unavailable</c>: settings the account requires could not be fetched.</summary>
    public static StartupFailureReason RemoteSettingsRequiredUnavailable { get; } = new("remote_settings_required_unavailable");

    /// <summary><c>gateway_signin_required</c>: the gateway wants a sign-in first.</summary>
    public static StartupFailureReason GatewaySigninRequired { get; } = new("gateway_signin_required");

    /// <summary><c>gateway_access_denied</c>: the gateway refused the account.</summary>
    public static StartupFailureReason GatewayAccessDenied { get; } = new("gateway_access_denied");

    /// <summary><c>proxy_invalid</c>: the configured proxy could not be used.</summary>
    public static StartupFailureReason ProxyInvalid { get; } = new("proxy_invalid");

    /// <summary><c>temp_dir_unusable</c>: the temporary directory could not be written to.</summary>
    public static StartupFailureReason TempDirUnusable { get; } = new("temp_dir_unusable");

    /// <summary><c>cwd_unavailable</c>: the working directory does not exist or cannot be entered.</summary>
    public static StartupFailureReason CwdUnavailable { get; } = new("cwd_unavailable");

    /// <summary><c>shell_tool_missing</c>: the shell the binary needs is not on the machine.</summary>
    public static StartupFailureReason ShellToolMissing { get; } = new("shell_tool_missing");

    /// <summary><c>session_held_by_background</c>: a background task still holds the session.</summary>
    public static StartupFailureReason SessionHeldByBackground { get; } = new("session_held_by_background");

    /// <summary><c>worktree_resume_refused</c>: the session belongs to another worktree.</summary>
    public static StartupFailureReason WorktreeResumeRefused { get; } = new("worktree_resume_refused");

    /// <summary><c>worktree_unverified</c>: the worktree could not be verified.</summary>
    public static StartupFailureReason WorktreeUnverified { get; } = new("worktree_unverified");

    /// <summary><c>cli_version_too_old</c>: the binary is older than the session needs.</summary>
    public static StartupFailureReason CliVersionTooOld { get; } = new("cli_version_too_old");

    /// <summary><c>bypass_root</c>: permissions were bypassed while running as root.</summary>
    public static StartupFailureReason BypassRoot { get; } = new("bypass_root");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "org_pin_api_key_conflict" or "org_verify_failed" or "org_pin_mismatch" or
        "managed_settings_invalid" or "remote_settings_required_unavailable" or
        "gateway_signin_required" or "gateway_access_denied" or "proxy_invalid" or
        "temp_dir_unusable" or "cwd_unavailable" or "shell_tool_missing" or
        "session_held_by_background" or "worktree_resume_refused" or "worktree_unverified" or
        "cli_version_too_old" or "bypass_root";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(StartupFailureReason left, StartupFailureReason right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(StartupFailureReason left, StartupFailureReason right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator StartupFailureReason(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(StartupFailureReason other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="StartupFailureReason"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is StartupFailureReason other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
