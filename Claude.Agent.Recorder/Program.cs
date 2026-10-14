namespace Claude.Agent.Recorder;

// Record raw stream-json both ways from the real binary, once per fixture, through this SDK's
// own transport. Spends real usage on the account the binary is logged into.
//
//     dotnet run --project Claude.Agent.Recorder -- [--executable PATH] OUT CWD [fixture ...]
//
// The fixtures default to the first six; the binary to ~/.local/bin/claude, or the path
// --executable names, made absolute. Nothing on PATH is searched for.
internal static class Program
{
    private static readonly string[] DefaultFixtures = ["trivial", "tool-use", "hooks", "streaming", "client", "sdk-mcp"];

    private static async Task<int> Main(string[] args)
    {
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "claude");
        if (args.Length >= 2 && args[0] == "--executable")
        {
            executable = Path.GetFullPath(args[1]);
            args = args[2..];
        }

        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: Claude.Agent.Recorder [--executable PATH] OUT CWD [fixture ...]");
            return 2;
        }

        using var recording = new Recording(Path.GetFullPath(args[0]));
        var fixtures = new Fixtures(recording, Path.GetFullPath(args[1]), executable);
        var failed = 0;
        foreach (var name in args.Length > 2 ? args[2..] : DefaultFixtures)
        {
            try
            {
                await fixtures.RunAsync(name);
            }
            catch (Exception e)
            {
                failed++;
                Console.WriteLine($"  !! {name} failed: {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                recording.End();
            }
        }

        return failed == 0 ? 0 : 1;
    }
}
