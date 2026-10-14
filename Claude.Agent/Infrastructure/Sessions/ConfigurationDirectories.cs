namespace Claude.Agent;

// The directories a client's sessions are started with, resolved once when the client is made.
// Configuration is the client's own: the binary is started with CLAUDE_CONFIG_DIR set to it, and
// everything it keeps — transcripts among it — is written there. User is the directory the user's own
// claude runs from: CLAUDE_CONFIG_DIR as this process inherited it, else ~/.claude. An inherited
// CLAUDE_CONFIG_DIR that is the configuration directory is an outer SDK session's, never the user's,
// and the user's directory is then the one the inherited CLAUDE_SECURESTORAGE_CONFIG_DIR names, else
// ~/.claude. SecureStorage is the directory the binary reads the login from, and SecureStorageValue
// what the child's CLAUDE_SECURESTORAGE_CONFIG_DIR is set to, so a session runs on the user's own
// login and a refresh writes it back there. CLAUDE_SECURESTORAGE_CONFIG_DIR inherited set to nothing
// is inherited, and names ~/.claude, as the binary reads it.
internal sealed record ConfigurationDirectories(string Configuration, string User, string SecureStorage, string SecureStorageValue)
{
    public const string ConfigurationVariable = "CLAUDE_CONFIG_DIR";

    public const string SecureStorageVariable = "CLAUDE_SECURESTORAGE_CONFIG_DIR";

    // `configured` is ClaudeAgentClientOptions.ConfigurationDirectory: null is the default, and a
    // relative path is taken against the current directory now.
    //
    // The child's CLAUDE_SECURESTORAGE_CONFIG_DIR is the inherited one as it stands, set to nothing or
    // not; else the inherited CLAUDE_CONFIG_DIR as it stands, when that is the user's directory; else
    // set to nothing, which the binary reads as ~/.claude. The binary names the credential store it
    // reads by that text: set to nothing, as the user's own claude names it with neither variable set,
    // and otherwise by a hash of the text, as the user's own claude names it by a hash of its
    // CLAUDE_CONFIG_DIR. So the child reads the store the user's own claude reads.
    public static ConfigurationDirectories Resolve(string? configured, HostEnvironment host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var configuration = Path.GetFullPath(configured ?? DefaultConfiguration(host));
        var inheritedSecureStorage = host.GetEnvironmentVariable(SecureStorageVariable);
        var login = inheritedSecureStorage is null ? null : LoginDirectory(host, inheritedSecureStorage);

        var inherited = Inherited(host, ConfigurationVariable);
        if (inherited is not null && !SameDirectory(Path.GetFullPath(inherited), configuration))
        {
            var user = Path.GetFullPath(inherited);
            return new(configuration, user, login ?? user, inheritedSecureStorage ?? inherited);
        }

        // No CLAUDE_CONFIG_DIR inherited, or an outer SDK session's, whose user's directory is the
        // login's.
        var claude = DefaultUser(host);
        return new(configuration, inherited is null ? claude : login ?? claude, login ?? claude, inheritedSecureStorage ?? string.Empty);
    }

    // ~/.config/claude-agent: .config/claude-agent under the user's profile directory, on every
    // platform.
    public static string DefaultConfiguration(HostEnvironment host) =>
        Path.Combine(host.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "claude-agent");

    // Two full paths name one directory when they are equal as they resolve on disk, by the transcript
    // layout's canonical path: every symbolic link in either is followed, an ancestor's included, and
    // a trailing separator is no difference. Case is folded where the platform's file system folds it
    // by default.
    public static bool SameDirectory(string left, string right) =>
        string.Equals(
            Canonical(left),
            Canonical(right),
            OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    // Whether an environment variable name is one of the two a client sets itself.
    public static bool IsSetByClient(string name) =>
        string.Equals(name, ConfigurationVariable, VariableComparison)
        || string.Equals(name, SecureStorageVariable, VariableComparison);

    private static StringComparison VariableComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    // ~/.claude, as a full path.
    private static string DefaultUser(HostEnvironment host) =>
        Path.GetFullPath(Path.Combine(host.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude"));

    // The directory the binary reads the login from under a CLAUDE_SECURESTORAGE_CONFIG_DIR value: set
    // to nothing, ~/.claude.
    private static string LoginDirectory(HostEnvironment host, string value) =>
        value.Length == 0 ? DefaultUser(host) : Path.GetFullPath(value);

    // A path Canonicalize takes as spelled keeps a separator at its end, which is trimmed here.
    private static string Canonical(string path) =>
        Path.TrimEndingDirectorySeparator(TranscriptPaths.Canonicalize(path));

    private static string? Inherited(HostEnvironment host, string name) =>
        host.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;
}
