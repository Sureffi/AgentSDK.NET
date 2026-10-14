using System.Collections.Immutable;

namespace Claude.Agent.Tests.Replay;

internal sealed record TransportRequest(ClaudeAgentClientOptions Options, ClaudeAgentSessionOptions Session, TransportNeeds Needs)
{
    public ImmutableArray<string> Argv => CommandLine.Build(Session, Needs, _ => null);
}

internal sealed class FixtureTransportFactory : ITransportFactory
{
    private readonly Queue<FixtureServer> _servers;
    private readonly FrameCodec _codec;
    private readonly Lock _gate = new();
    private readonly List<TransportRequest> _requests = [];

    public FixtureTransportFactory(FrameCodec codec, params FixtureServer[] servers)
    {
        _servers = new Queue<FixtureServer>(servers);
        _codec = codec;
    }

    public ImmutableArray<TransportRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    public TransportRequest? Last
    {
        get
        {
            lock (_gate)
            {
                return _requests.Count == 0 ? null : _requests[^1];
            }
        }
    }

    public ITransport Create(ClaudeAgentClientOptions options, ClaudeAgentSessionOptions session, TransportNeeds needs)
    {
        FixtureServer server;
        lock (_gate)
        {
            _requests.Add(new TransportRequest(options, session, needs));
            server = _servers.Count > 0
                ? _servers.Dequeue()
                : throw new InvalidOperationException("every fixture server has been handed to a session already.");
        }

        return new SocketTransport(server.EndPoint, _codec, server.ExitCode, options.ProcessGracePeriod);
    }
}
