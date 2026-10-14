namespace Claude.Agent.Tests.Replay;

// Hosts a client resolves its directories against: handed to the client's internal constructor, or
// registered in the container beside AddClaudeAgentClient, whose validator and client then take it.
// So no test reads the real user's directory or the real default configuration directory; the tests
// that make a client through its public constructor are refused before it resolves either.
internal static class TestHostEnvironments
{
    // A profile under `root`: the user's directory is root/profile/.claude unless `variables` name
    // another as ConfigurationDirectories reads them, and the default configuration directory is
    // root/profile/.config/claude-agent.
    // `variables` is the environment the host inherited. A folder the client does not read throws.
    public static HostEnvironment Under(string root, IReadOnlyDictionary<string, string>? variables = null) =>
        new(
            name => variables is not null && variables.TryGetValue(name, out var value) ? value : null,
            folder => folder switch
            {
                Environment.SpecialFolder.UserProfile => Path.Combine(root, "profile"),
                _ => throw new ArgumentOutOfRangeException(nameof(folder), folder, "a folder the client does not read"),
            });

    // Under a directory no test creates: for a client that starts no process and writes no transcript.
    public static HostEnvironment Unmade { get; } =
        Under(Path.Combine(Path.GetTempPath(), "claude-agent-tests-unmade"));

    public static string UserDirectory(string root) => Path.Combine(root, "profile", ".claude");

    public static string DefaultConfigurationDirectory(string root) =>
        Path.Combine(root, "profile", ".config", "claude-agent");
}
