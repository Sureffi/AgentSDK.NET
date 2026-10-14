using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Claude.Agent;

// Starts one session: the transport, the runtime, and the initialize handshake. Anything that fails
// on the way cleans up what came before it and throws.
internal sealed class SessionLauncher
{
    private readonly ITransportFactory _transports;
    private readonly IPayloadParser _payloads;
    private readonly ILoggerFactory _loggers;
    private readonly ILogger _logger;

    public SessionLauncher(
        ITransportFactory transports,
        IPayloadParser payloads,
        ILoggerFactory? loggers)
    {
        _transports = transports;
        _payloads = payloads;
        _loggers = loggers ?? NullLoggerFactory.Instance;
        _logger = _loggers.CreateLogger<ClaudeAgentClient>();
    }

    public async Task<ClaudeAgentSession> StartAsync(
        ClaudeAgentClientOptions options,
        ClaudeAgentSessionOptions session,
        ClaudeAgentClient client,
        CancellationToken ct)
    {
        session.Validate();

        var hooks = new HookTable(session.Hooks);
        var permissions = session.PermissionCallback is not null;

        // The allow rules as they go out, the Skill rules the skills options add among them.
        if (permissions
            && PermissionCallbackWarning.For(session.PermissionMode, AllowRules.For(session))
                is { } warning)
        {
            _logger.LogWarning("{Warning}", warning);
        }

        ITransport? transport = null;
        SessionRuntime runtime;
        try
        {
            transport = _transports.Create(options, session, new TransportNeeds(permissions));
            await transport.ConnectAsync(ct).ConfigureAwait(false);

            var toolServers = ToolServers(session);
            runtime = new SessionRuntime(
                session,
                transport,
                _payloads,
                new SdkMcpBridge(toolServers, _loggers.CreateLogger<SdkMcpBridge>()),
                hooks,
                _loggers.CreateLogger<ClaudeAgentSession>());
        }
        catch
        {
            // Nothing owns the transport until the runtime exists, so every throw up to that point
            // closes it here: a leaked transport holds a subprocess.
            if (transport is not null)
            {
                await CloseAsync(transport).ConfigureAwait(false);
            }

            throw;
        }

        try
        {
            runtime.Start();
            await runtime.InitializeAsync(Initialize(session, hooks), ct).ConfigureAwait(false);
        }
        catch
        {
            // Cancellation is one of these.
            await runtime.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        return new ClaudeAgentSession(runtime, client);
    }

    private static async ValueTask CloseAsync(ITransport transport)
    {
        await transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        await transport.DisposeAsync().ConfigureAwait(false);
    }

    // The in-process servers this session names, under the names it gives them.
    private static IReadOnlyList<IToolServer> ToolServers(ClaudeAgentSessionOptions session) =>
        [.. session.McpServers
            .Where(pair => pair.Value is McpSdkServerConfiguration { ToolServer: not null })
            .Select(pair => ((McpSdkServerConfiguration)pair.Value).ToolServer!.Named(pair.Key))];

    private static InitializeRequest Initialize(ClaudeAgentSessionOptions session, HookTable hooks) => new(
        hooks.ToInitializeConfig(),
        session.Agents.Count > 0 ? session.Agents.AsReadOnly() : null,
        session.SystemPrompt is { IsPreset: true } preset ? preset.ExcludeDynamicSections : null,
        session.AllSkills is true || session.Skills.Count == 0 ? null : [.. session.Skills],
        session.ForwardSubagentText is true);
}
