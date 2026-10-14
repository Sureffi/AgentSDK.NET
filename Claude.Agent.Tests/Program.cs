using Microsoft.Testing.Platform.Builder;
using Claude.Agent.Tests.Process;

// Classes run side by side: every replay owns its own socket, host and process, and nothing is static.
[assembly: Parallelize(Scope = ExecutionScope.ClassLevel, Workers = 4)]

namespace Claude.Agent.Tests;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args is [OrphanHost.Flag, ..])
        {
            return await OrphanHost.RunAsync(args[1..]);
        }

        var builder = await TestApplication.CreateBuilderAsync(args);
        SelfRegisteredExtensions.AddSelfRegisteredExtensions(builder, args);
        using var app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}
