using Claude.Agent.Tests.Replay;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Claude.Agent.Tests.Process;

/// <summary>
/// The test executable run as a child: it starts the fake binary through the real process
/// adapter, runs one turn, and then leaves without CloseAsync — by returning, by throwing
/// when told to crash, or by running the generic host until the parent's SIGTERM stops it
/// through the console lifetime. The reaper on process exit is what the parent test then
/// measures. A SIGTERM to a process that handles no signal kills it where it stands, with
/// no exit handlers run; that is the host's to arrange.
/// </summary>
internal static class OrphanHost
{
    public const string Flag = "--orphan-host";
    public const string Crash = "--crash";
    public const string Linger = "--linger";
    public const string Lingering = "lingering";

    public static async Task<int> RunAsync(string[] args)
    {
        if (args is not [var cli, var cwd, var pidfile, ..])
        {
            Console.Error.WriteLine($"usage: {Flag} <cli> <cwd> <pidfile> [{Crash}|{Linger}]");
            return 2;
        }

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(TestHostEnvironments.Under(cwd));
        builder.Services.AddClaudeAgentClient(options =>
        {
            options.ExecutablePath = cli;
            options.Defaults.WorkingDirectory = cwd;
            options.Defaults.MaxTurns = 1;
            options.ProcessGracePeriod = TimeSpan.FromMilliseconds(200);
            options.EnvironmentVariables["CLAUDE_FAKE_PIDFILE"] = pidfile;
            options.ConfigurationDirectory = Path.Combine(cwd, "configuration");
        });

        var host = builder.Build();
        var session = await host.Services.GetRequiredService<ClaudeAgentClient>().StartSessionAsync();
        await session.SendAsync("Reply with exactly one word: pong").WaitForCompletionAsync();

        if (args.Contains(Crash, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("the orphan host crashed on purpose");
        }

        if (args.Contains(Linger, StringComparer.Ordinal))
        {
            host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(
                () => Console.WriteLine(Lingering));
            await host.RunAsync();
        }

        return 0;
    }
}
