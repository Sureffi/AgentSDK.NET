namespace Claude.Agent;

/// <summary>
/// How a <see cref="ClaudeAgentClient"/> reaches the binary: where it is, what it is started with
/// and closed by, and the configuration directory its sessions keep. What is asked of it is
/// <see cref="ClaudeAgentSessionOptions"/>, set per call over <see cref="Defaults"/>.
/// </summary>
public sealed class ClaudeAgentClientOptions
{
    /// <summary>Options with nothing set.</summary>
    public ClaudeAgentClientOptions()
        : this(new ClaudeAgentSessionOptions())
    {
    }

    private ClaudeAgentClientOptions(ClaudeAgentSessionOptions defaults) => Defaults = defaults;

    /// <summary>The claude executable. Null searches PATH and the well-known install locations.</summary>
    public string? ExecutablePath { get; set; }

    /// <summary>Environment variables handed to the binary over the inherited environment. CLAUDE_CONFIG_DIR and CLAUDE_SECURESTORAGE_CONFIG_DIR are the client's own and cannot be named here.</summary>
    public IDictionary<string, string> EnvironmentVariables { get; } = new OrderedDictionary<string, string>(StringComparer.Ordinal);

    /// <summary>How long each step of closing the process, and the version probe, may take.</summary>
    public TimeSpan ProcessGracePeriod { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>The longest stdout line accepted, counted in UTF-16 units of the decoded text, which is under the line's bytes for anything but ASCII; a longer one ends the session. Default 1048576.</summary>
    public int? MaxMessageSize { get; set; }

    /// <summary>Where the binary's stderr goes, written to it as the text arrives: decoded as UTF-8, a character split between two reads of the pipe whole, neither cut into lines nor bounded, <see cref="MaxMessageSize"/> being stdout's alone. Stderr is read to its end whatever the writer does; a write that throws is logged at debug and its text is lost. Null leaves stderr alone.</summary>
    public TextWriter? StandardError { get; set; }

    /// <summary>The configuration directory the binary is started with, as CLAUDE_CONFIG_DIR: where it keeps its settings, state and transcripts, and where <see cref="ClaudeAgentClient.GetSessionClient"/> reads and edits them. Null is ~/.config/claude-agent: .config/claude-agent under <see cref="Environment.SpecialFolder.UserProfile"/>, on every platform. A relative path is taken against the current directory when the client is made. It is created before the first process starts, readable by its owner alone off Windows; on Windows it takes the permissions its parent passes down. It cannot be the user's own directory: CLAUDE_CONFIG_DIR as this process inherited it, else ~/.claude. An inherited CLAUDE_CONFIG_DIR that is this directory is an outer SDK session's and not the user's, whose directory is then the one the inherited CLAUDE_SECURESTORAGE_CONFIG_DIR names, else ~/.claude. Nor can it be the directory the sessions read the user's login from: the one CLAUDE_SECURESTORAGE_CONFIG_DIR as this process inherited it names, ~/.claude when it is set to nothing, else the user's directory. Directories are compared as they resolve on disk, every symbolic link in either followed, an ancestor's included.</summary>
    public string? ConfigurationDirectory { get; set; }

    /// <summary>What every call is asked: a scalar or a selection unless the call says otherwise, and what accumulates — hooks, MCP servers, agents, the rule and directory lists — always, with the call's added. This is what binds from configuration. It names no session.</summary>
    public ClaudeAgentSessionOptions Defaults { get; }

    // Set on the copy a client holds, and only there: the directories resolved when it was made.
    internal ConfigurationDirectories? Directories { get; private set; }

    // The copy a client holds: validated against `host`, with the configuration directory as the
    // full path it resolved to.
    internal ClaudeAgentClientOptions Resolve(HostEnvironment host)
    {
        var directories = Validate(host);
        var clone = Clone();
        clone.ConfigurationDirectory = directories.Configuration;
        clone.Directories = directories;
        return clone;
    }

    internal ClaudeAgentClientOptions Clone()
    {
        var clone = new ClaudeAgentClientOptions(Defaults.Clone())
        {
            ExecutablePath = ExecutablePath,
            ProcessGracePeriod = ProcessGracePeriod,
            MaxMessageSize = MaxMessageSize,
            StandardError = StandardError,
            ConfigurationDirectory = ConfigurationDirectory,
            Directories = Directories,
        };

        foreach (var (name, value) in EnvironmentVariables)
        {
            clone.EnvironmentVariables[name] = value;
        }

        return clone;
    }

    // The first problem, as an ArgumentException naming the member; the directories the options
    // resolve to against `host` when there is none.
    internal ConfigurationDirectories Validate(HostEnvironment host)
    {
        Validate();

        var directories = ConfigurationDirectories.Resolve(ConfigurationDirectory, host);
#pragma warning disable CA2208 // The refusal names the member, as every other refusal of these options does.
        if (ConfigurationDirectories.SameDirectory(directories.Configuration, directories.User))
        {
            throw new ArgumentException(
                $"The configuration directory {directories.Configuration} is the user's own; a client's sessions keep a directory of their own.",
                nameof(ConfigurationDirectory));
        }

        if (ConfigurationDirectories.SameDirectory(directories.Configuration, directories.SecureStorage))
        {
            throw new ArgumentException(
                $"The configuration directory {directories.Configuration} is the one the user's login is read from; a client's sessions keep a directory of their own.",
                nameof(ConfigurationDirectory));
        }
#pragma warning restore CA2208

        return directories;
    }

    private void Validate()
    {
        if (ExecutablePath is not null && string.IsNullOrWhiteSpace(ExecutablePath))
        {
            throw new ArgumentException("An executable path must not be blank.", nameof(ExecutablePath));
        }

        if (ProcessGracePeriod <= TimeSpan.Zero)
        {
            throw new ArgumentException("ProcessGracePeriod must be positive.", nameof(ProcessGracePeriod));
        }

        if (MaxMessageSize is <= 0)
        {
            throw new ArgumentException("MaxMessageSize must be positive.", nameof(MaxMessageSize));
        }

        if (ConfigurationDirectory is not null && string.IsNullOrWhiteSpace(ConfigurationDirectory))
        {
            throw new ArgumentException("A configuration directory must not be blank.", nameof(ConfigurationDirectory));
        }

        foreach (var name in EnvironmentVariables.Keys)
        {
            if (ConfigurationDirectories.IsSetByClient(name))
            {
                throw new ArgumentException(
                    $"{name} is set by the client: the configuration directory is ConfigurationDirectory, and the login is the user's own.",
                    nameof(EnvironmentVariables));
            }
        }

        Defaults.ValidateAsDefaults();
    }
}
