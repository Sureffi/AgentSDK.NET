namespace Claude.Agent;

/// <summary>
/// The binary's sandbox settings, merged into <see cref="ClaudeAgentSessionOptions.Settings"/> under
/// "sandbox". A null member is left out and the binary's own default stands.
/// </summary>
public sealed class SandboxOptions
{
    /// <summary>Run shell commands in the sandbox. Written as <c>enabled</c>.</summary>
    public bool? Enabled { get; set; }

    /// <summary>Allow a Bash command without asking when it runs sandboxed. Written as <c>autoAllowBashIfSandboxed</c>.</summary>
    public bool? AutoAllowBashIfSandboxed { get; set; }

    /// <summary>Commands that always run outside the sandbox. Written as <c>excludedCommands</c>.</summary>
    public IList<string>? ExcludedCommands { get; set; }

    /// <summary>Let the model ask to run a command outside the sandbox. Written as <c>allowUnsandboxedCommands</c>.</summary>
    public bool? AllowUnsandboxedCommands { get; set; }

    /// <summary>Use the weaker sandbox that works inside a container. Written as <c>enableWeakerNestedSandbox</c>.</summary>
    public bool? EnableWeakerNestedSandbox { get; set; }

    /// <summary>Domains a sandboxed command may reach. Written under <c>network</c>, as are the members below it down to the proxy ports.</summary>
    public IList<string>? AllowedDomains { get; set; }

    /// <summary>Domains a sandboxed command may not reach.</summary>
    public IList<string>? DeniedDomains { get; set; }

    /// <summary>Allow only the domains managed settings name.</summary>
    public bool? AllowManagedDomainsOnly { get; set; }

    /// <summary>Unix sockets a sandboxed command may open, by path.</summary>
    public IList<string>? AllowUnixSockets { get; set; }

    /// <summary>Let a sandboxed command open any Unix socket.</summary>
    public bool? AllowAllUnixSockets { get; set; }

    /// <summary>Let a sandboxed command bind a local port.</summary>
    public bool? AllowLocalBinding { get; set; }

    /// <summary>macOS: Mach services a sandboxed command may look up.</summary>
    public IList<string>? AllowMachLookup { get; set; }

    /// <summary>The port of an HTTP proxy of your own the sandbox sends traffic through.</summary>
    public int? HttpProxyPort { get; set; }

    /// <summary>The port of a SOCKS proxy of your own the sandbox sends traffic through.</summary>
    public int? SocksProxyPort { get; set; }

    /// <summary>File paths whose sandbox violations are ignored.</summary>
    public IList<string>? IgnoredFileViolations { get; set; }

    /// <summary>Network destinations whose sandbox violations are ignored.</summary>
    public IList<string>? IgnoredNetworkViolations { get; set; }

    internal SandboxOptions Clone() => new()
    {
        Enabled = Enabled,
        AutoAllowBashIfSandboxed = AutoAllowBashIfSandboxed,
        ExcludedCommands = Copy(ExcludedCommands),
        AllowUnsandboxedCommands = AllowUnsandboxedCommands,
        EnableWeakerNestedSandbox = EnableWeakerNestedSandbox,
        AllowedDomains = Copy(AllowedDomains),
        DeniedDomains = Copy(DeniedDomains),
        AllowManagedDomainsOnly = AllowManagedDomainsOnly,
        AllowUnixSockets = Copy(AllowUnixSockets),
        AllowAllUnixSockets = AllowAllUnixSockets,
        AllowLocalBinding = AllowLocalBinding,
        AllowMachLookup = Copy(AllowMachLookup),
        HttpProxyPort = HttpProxyPort,
        SocksProxyPort = SocksProxyPort,
        IgnoredFileViolations = Copy(IgnoredFileViolations),
        IgnoredNetworkViolations = Copy(IgnoredNetworkViolations),
    };

    private static List<string>? Copy(IList<string>? list) => list is null ? null : [.. list];

    internal bool HasNetwork =>
        AllowedDomains is not null || DeniedDomains is not null || AllowManagedDomainsOnly is not null
        || AllowUnixSockets is not null || AllowAllUnixSockets is not null || AllowLocalBinding is not null
        || AllowMachLookup is not null || HttpProxyPort is not null || SocksProxyPort is not null;

    internal bool HasIgnoredViolations => IgnoredFileViolations is not null || IgnoredNetworkViolations is not null;
}
