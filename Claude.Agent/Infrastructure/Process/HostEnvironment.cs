namespace Claude.Agent;

// The two things a client reads off the process it runs in to find its directories: an inherited
// environment variable and a special folder. Current is this process. A test hands in its own, to the
// client's internal constructor or registered in the container, where AddClaudeAgentClient's validator
// and client take it in place of Current; so no test resolves the user's real directory or the real
// default configuration directory.
internal sealed class HostEnvironment(Func<string, string?> variables, Func<Environment.SpecialFolder, string> folders)
{
    public static HostEnvironment Current { get; } = new(
        Environment.GetEnvironmentVariable,
        folder => Environment.GetFolderPath(folder, Environment.SpecialFolderOption.DoNotVerify));

    public string? GetEnvironmentVariable(string name) => variables(name);

    public string GetFolderPath(Environment.SpecialFolder folder) => folders(folder);
}
