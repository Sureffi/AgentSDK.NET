using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Claude.Agent;

// Where the binary is when ExecutablePath does not say: the first directory on PATH that holds it,
// then where its own installers put it, which is where its own doctor looks for an installation — the
// native installer's launcher, .local/bin under the user's profile directory, and the local npm
// install's, local under the user's configuration directory. The native installer names it
// claude.exe on Windows and claude elsewhere; the local install's launcher is a shell script, and is
// not looked for on Windows. A global npm install is found on PATH or not at all: where npm puts it is
// npm's to say, and only the transport starts a process.
internal sealed class CliLocator
{
    private readonly ILogger<CliLocator> _logger;

    public CliLocator(ILogger<CliLocator>? logger = null) => _logger = logger ?? NullLogger<CliLocator>.Instance;

    public Result<string, Error> Find(string? configured, string userDirectory) => Find(
        configured,
        Environment.GetEnvironmentVariable("PATH"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        userDirectory);

    internal Result<string, Error> Find(string? configured, string? searchPath, string home, string userDirectory)
    {
        var located = string.IsNullOrEmpty(configured)
            ? Search(searchPath, home, userDirectory)
            : Result<string, Error>.Ok(configured);
        if (!located.TryGet(out var candidate, out _))
        {
            _logger.LogDebug("The Claude Code CLI could not be located.");
            return located;
        }

        var accepted = Accept(candidate);
        if (accepted.IsOk && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Using the Claude Code CLI at {Path}.", candidate);
        }

        return accepted;
    }

    // A batch file is run through cmd.exe, which reads the arguments it is handed as commands of its
    // own; no quoting of them holds there, so this SDK starts none.
    private static Result<string, Error> Accept(string path) =>
        IsBatchFile(path)
            ? new ConnectionError(
                $"{path} is a batch file, and Windows would run it through cmd.exe, which cannot be handed " +
                "a prompt safely. Set ClaudeAgentClientOptions.ExecutablePath to a claude.exe, or put the " +
                "directory that holds one on PATH.")
            : path;

    private static Result<string, Error> Search(string? searchPath, string home, string userDirectory)
    {
        var name = OperatingSystem.IsWindows() ? "claude.exe" : "claude";

        foreach (var directory in (searchPath ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var installed = Installed(name, home, userDirectory);
        foreach (var launcher in installed)
        {
            if (File.Exists(launcher))
            {
                return launcher;
            }
        }

        return new CliNotFoundError(
            $"There is no {name} on PATH, nor at {string.Join(", ", installed)}. " +
            "Set ClaudeAgentClientOptions.ExecutablePath to the binary, or put the directory that holds it on PATH.");
    }

    private static string[] Installed(string name, string home, string userDirectory) =>
        OperatingSystem.IsWindows()
            ? [Path.Combine(home, ".local", "bin", name)]
            : [Path.Combine(home, ".local", "bin", name), Path.Combine(userDirectory, "local", name)];

    // By the extension the name has once the platform has resolved it, the dots and spaces Windows
    // drops from a file name's end dropped.
    private static bool IsBatchFile(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var extension = Path.GetExtension(Path.GetFullPath(path));
        return extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase);
    }
}
