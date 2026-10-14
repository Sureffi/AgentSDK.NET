using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Sessions;

// A client whose host lives under a temporary root: the user's own directory is
// <root>/profile/.claude and the client's configuration directory the default under
// <root>/profile/.config, or the one the test names. Neither exists until the test writes into it.
// `inherited` is the environment the host inherited, made from the root. The client starts no
// process.
internal sealed class TemporaryClient : IDisposable
{
    public TemporaryClient(string? configurationDirectory = null, Func<string, IReadOnlyDictionary<string, string>>? inherited = null)
    {
        Root = Path.Combine(Path.GetTempPath(), "claude-agent-client-" + Guid.NewGuid().ToString("n"));
        Host = TestHostEnvironments.Under(Root, inherited?.Invoke(Root));
        var options = new ClaudeAgentClientOptions { ConfigurationDirectory = configurationDirectory };
        Client = new ClaudeAgentClient(
            options,
            null,
            new FixtureTransportFactory(new FrameCodec()),
            Host);
    }

    public string Root { get; }

    public HostEnvironment Host { get; }

    public ClaudeAgentClient Client { get; }

    public ClaudeSessionClient Sessions => Client.GetSessionClient();

    public string ConfigurationDirectory => Client.Directories.Configuration;

    public string UserDirectory => Client.Directories.User;

    // The project directory a cwd's sessions are kept in, made: the client's, or the user's.
    public string ConfigurationProject(string cwd) => Project(ConfigurationDirectory, cwd);

    public string UserProject(string cwd) => Project(UserDirectory, cwd);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // a test's temp tree is not worth failing a run over
        }
    }

    // Every file under a directory and its bytes, by the path under it: what a copy out of it must
    // leave as it was.
    public static Dictionary<string, byte[]> Tree(string directory) =>
        Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .ToDictionary(file => Path.GetRelativePath(directory, file), File.ReadAllBytes, StringComparer.Ordinal)
            : new Dictionary<string, byte[]>(StringComparer.Ordinal);

    public static void AssertSameTree(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after)
    {
        CollectionAssert.AreEquivalent(before.Keys.ToArray(), after.Keys.ToArray());
        foreach (var (path, bytes) in before)
        {
            CollectionAssert.AreEqual(bytes, after[path], path);
        }
    }

    private static string Project(string configurationDirectory, string cwd)
    {
        var directory = Path.Combine(configurationDirectory, "projects", new TranscriptLayout(configurationDirectory).ProjectFor(cwd));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
