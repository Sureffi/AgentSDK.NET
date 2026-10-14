using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Claude.Agent;

internal sealed class SubprocessTransportFactory : ITransportFactory
{
    private readonly FrameCodec _codec;
    private readonly CliLocator _locator;
    private readonly ILoggerFactory _loggerFactory;
    private readonly TransportTap? _tap;

    public SubprocessTransportFactory(FrameCodec codec, CliLocator locator, ILoggerFactory? loggerFactory = null, TransportTap? tap = null)
    {
        _codec = codec;
        _locator = locator;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _tap = tap;
    }

    public ITransport Create(ClaudeAgentClientOptions options, ClaudeAgentSessionOptions session, TransportNeeds needs) =>
        new SubprocessCliTransport(
            options,
            session,
            needs,
            _codec,
            _locator,
            ReadFile,
            _loggerFactory.CreateLogger<SubprocessCliTransport>(),
            _tap);

    private static string? ReadFile(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
