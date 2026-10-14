using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Claude.Agent.Tests.Replay;

// A client over fixture servers instead of the binary: every session the client starts takes the
// next server in the list. What a test asks goes in as the client's defaults, so a bare QueryAsync
// is asked it; a test of the call's own options passes them on the call.
internal sealed class TestHost : IDisposable
{
    private readonly IDisposable? _owned;

    private TestHost(ClaudeAgentClient client, FixtureTransportFactory transports, IDisposable? owned = null)
    {
        Client = client;
        Transports = transports;
        _owned = owned;
    }

    public ClaudeAgentClient Client { get; }

    public FixtureTransportFactory Transports { get; }

    public static TestHost Start(FixtureServer server, Action<ClaudeAgentSessionOptions>? options = null) =>
        Start([server], options);

    public static TestHost Start(FixtureServer[] servers, Action<ClaudeAgentSessionOptions>? options = null)
    {
        var configured = new ClaudeAgentClientOptions();
        options?.Invoke(configured.Defaults);
        return Start(configured, servers);
    }

    public static TestHost Start(ClaudeAgentClientOptions options, params FixtureServer[] servers)
    {
        var transports = new FixtureTransportFactory(new FrameCodec(NullLogger<FrameCodec>.Instance), servers);
        var client = new ClaudeAgentClient(options, NullLoggerFactory.Instance, transports, TestHostEnvironments.Unmade);
        return new TestHost(client, transports);
    }

    // The real process transport: the fake binary tests run the subprocess adapter itself. `host` is
    // what the client resolves the user's directory and its default configuration directory against.
    // With a tap, the transports are built as the client builds its own and hand the tap what they
    // read and write, as the recorder's do.
    public static TestHost Real(ClaudeAgentClientOptions options, ILoggerProvider logs, HostEnvironment host, TransportTap? tap = null)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var transports = tap is null
            ? null
            : new SubprocessTransportFactory(
                new FrameCodec(loggerFactory.CreateLogger<FrameCodec>()),
                new CliLocator(loggerFactory.CreateLogger<CliLocator>()),
                loggerFactory,
                tap);
        return new TestHost(
            new ClaudeAgentClient(options, loggerFactory, transports, host),
            new FixtureTransportFactory(new FrameCodec(NullLogger<FrameCodec>.Instance)),
            loggerFactory);
    }

    public Task<ClaudeAgentSession> StartSessionAsync(ClaudeAgentSessionOptions? session = null) =>
        Client.StartSessionAsync(session);

    public void Dispose() => _owned?.Dispose();
}
