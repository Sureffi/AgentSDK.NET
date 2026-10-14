using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Claude.Agent;

internal sealed class SdkMcpBridge : IMcpBridge
{
    private const string DefaultProtocolVersion = "2025-11-25";
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

    private readonly FrozenDictionary<string, IToolServer> _servers;
    private readonly ConcurrentDictionary<Task<ToolResult>, byte> _calls = new();
    private readonly Dictionary<(string Server, string Id), CancellationTokenSource> _running = [];
    private readonly HashSet<(string Server, string Id)> _cancelledBeforeTheyRan = [];
    private readonly Lock _runningGate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ILogger _logger;
    private int _closed;

    public SdkMcpBridge(IReadOnlyList<IToolServer> servers, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(servers);

        _logger = logger ?? NullLogger.Instance;

        var named = new Dictionary<string, IToolServer>();
        foreach (var server in servers)
        {
            if (!named.TryAdd(server.Name, server))
            {
                _logger.LogWarning("Two SDK MCP servers are named {Server}; the first one serves it.", server.Name);
            }
        }

        _servers = named.ToFrozenDictionary();
    }

    public bool Has(string serverName) => _servers.ContainsKey(serverName);

    public async ValueTask<JsonElement?> HandleAsync(string serverName, JsonElement message, CancellationToken ct)
    {
        if (message.ValueKind == JsonValueKind.Undefined)
        {
            return JsonRpc.Failure(message, JsonRpc.ParseError, "Parse error");
        }

        if (message.ValueKind != JsonValueKind.Object || !JsonRpc.Has(message, "jsonrpc"))
        {
            return JsonRpc.Failure(message, JsonRpc.InvalidRequest, "Invalid Request");
        }

        if (JsonRpc.Method(message) is not { } method)
        {
            return JsonRpc.Has(message, "result") || JsonRpc.Has(message, "error")
                ? null
                : JsonRpc.Failure(message, JsonRpc.InvalidRequest, "Invalid Request");
        }

        if (!JsonRpc.Has(message, "id"))
        {
            if (method == "notifications/cancelled")
            {
                Cancel(serverName, message);
            }

            return null;
        }

        if (!_servers.TryGetValue(serverName, out var server))
        {
            return JsonRpc.Failure(message, JsonRpc.MethodNotFound, $"This session serves no MCP server named '{serverName}'.");
        }

        if (Volatile.Read(ref _closed) != 0)
        {
            return JsonRpc.Failure(
                message,
                JsonRpc.InternalError,
                $"SDK MCP server '{serverName}' is closed");
        }

        return method switch
        {
            "initialize" => Initialize(message, server),
            "ping" => JsonRpc.Response(message, writer =>
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
            }),
            "tools/list" => ToolsList(message, server),
            "tools/call" => await CallAsync(serverName, message, server, ct).ConfigureAwait(false),
            _ => JsonRpc.Failure(message, JsonRpc.MethodNotFound, "Method not found"),
        };
    }

    public async ValueTask CloseAsync()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        await _cancellation.CancelAsync().ConfigureAwait(false);

        var pending = _calls.Keys.ToArray();
        if (pending.Length > 0)
        {
            try
            {
                await Task.WhenAll(pending).WaitAsync(CloseTimeout, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception failure)
            {
                _logger.LogDebug(failure, "An SDK MCP tool call did not settle while the bridge closed.");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        _cancellation.Dispose();
    }

    private static JsonElement Initialize(JsonElement message, IToolServer server)
    {
        var requested = JsonRpc.Property(message, "params") is { } parameters
            && JsonRpc.Property(parameters, "protocolVersion") is { ValueKind: JsonValueKind.String } version
                ? version.GetString()
                : null;

        return JsonRpc.Response(message, writer =>
        {
            writer.WriteStartObject();
            writer.WritePropertyName("capabilities");
            writer.WriteStartObject();
            writer.WritePropertyName("experimental");
            writer.WriteStartObject();
            writer.WriteEndObject();
            writer.WritePropertyName("tools");
            writer.WriteStartObject();
            writer.WriteBoolean("listChanged", false);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteString("protocolVersion", requested ?? DefaultProtocolVersion);
            writer.WritePropertyName("serverInfo");
            writer.WriteStartObject();
            writer.WriteString("name", server.Name);
            writer.WriteString("version", server.Version);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static JsonElement ToolsList(JsonElement message, IToolServer server) =>
        JsonRpc.Response(message, writer =>
        {
            writer.WriteStartObject();
            writer.WritePropertyName("tools");
            writer.WriteStartArray();
            foreach (var tool in server.Tools)
            {
                WriteTool(writer, tool);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });

    private static void WriteTool(Utf8JsonWriter writer, ToolDefinition tool)
    {
        writer.WriteStartObject();

        if (tool.MaxResultSizeChars is { } maxResultSize)
        {
            writer.WritePropertyName("_meta");
            writer.WriteStartObject();
            writer.WriteNumber("anthropic/maxResultSizeChars", maxResultSize);
            writer.WriteEndObject();
        }

        if (tool.Annotations is { } annotations)
        {
            writer.WritePropertyName("annotations");
            writer.WriteStartObject();
            WriteHint(writer, "destructiveHint", annotations.DestructiveHint);
            WriteHint(writer, "idempotentHint", annotations.IdempotentHint);
            WriteHint(writer, "openWorldHint", annotations.OpenWorldHint);
            WriteHint(writer, "readOnlyHint", annotations.ReadOnlyHint);
            if (annotations.Title is { } title)
            {
                writer.WriteString("title", title);
            }

            writer.WriteEndObject();
        }

        writer.WriteString("description", tool.Description);
        writer.WritePropertyName("inputSchema");
        tool.InputSchema.WriteTo(writer);
        writer.WriteString("name", tool.Name);
        writer.WriteEndObject();
    }

    private static void WriteHint(Utf8JsonWriter writer, string name, bool? hint)
    {
        if (hint is { } value)
        {
            writer.WriteBoolean(name, value);
        }
    }

    private static void WriteContent(Utf8JsonWriter writer, ToolContent content)
    {
        writer.WriteStartObject();
        if (content is ToolImage image)
        {
            writer.WriteString("data", image.Base64Data);
            writer.WriteString("mimeType", image.MimeType);
            writer.WriteString("type", "image");
        }
        else
        {
            writer.WriteString("text", content is ToolText text ? text.Text : string.Empty);
            writer.WriteString("type", "text");
        }

        writer.WriteEndObject();
    }

    // The binary gives up on a tool call, on an interrupt, with notifications/cancelled naming the
    // call's JSON-RPC id. A call of that id running on that server has its token cancelled. Each
    // control request is handled on its own task, so the cancellation can be handled before the call
    // it names has started; the call then starts cancelled. MCP never reuses a request id within a
    // session, so a cancellation of a call already done names no call to come. A callback on the
    // token that throws is the tool's failure, not the notification's: it is logged and the notification still takes no
    // response.
    private void Cancel(string serverName, JsonElement message)
    {
        if (JsonRpc.Property(message, "params") is not { } parameters
            || JsonRpc.Property(parameters, "requestId") is not { } requestId)
        {
            return;
        }

        var call = (serverName, requestId.GetRawText());
        CancellationTokenSource? running;
        lock (_runningGate)
        {
            if (!_running.TryGetValue(call, out running))
            {
                _cancelledBeforeTheyRan.Add(call);
                return;
            }
        }

        try
        {
            running.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (AggregateException failure)
        {
            _logger.LogWarning(failure, "A cancellation callback of the SDK MCP call {Call} on {Server} failed.", call.Item2, serverName);
        }
    }

    private async ValueTask<JsonElement> CallAsync(string serverName, JsonElement message, IToolServer server, CancellationToken ct)
    {
        if (JsonRpc.Property(message, "params") is not { ValueKind: JsonValueKind.Object } parameters
            || JsonRpc.Property(parameters, "name") is not { ValueKind: JsonValueKind.String } named)
        {
            return JsonRpc.Failure(message, JsonRpc.InvalidParams, "Invalid request parameters");
        }

        var arguments = JsonRpc.Property(parameters, "arguments") is { ValueKind: JsonValueKind.Object } given
            ? given
            : JsonRpc.EmptyObject;

        var id = (serverName, JsonRpc.Property(message, "id")!.Value.GetRawText());
        using var cancelled = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cancelled.Token);
        bool tracked;
        lock (_runningGate)
        {
            tracked = _running.TryAdd(id, cancelled);
            if (_cancelledBeforeTheyRan.Remove(id))
            {
                cancelled.Cancel();
            }
        }

        ToolResult result;
        try
        {
            result = await RunAsync(server, named.GetString() ?? string.Empty, arguments, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancelled.IsCancellationRequested)
        {
            throw new McpCallCancelledException(serverName, id.Item2);
        }
        finally
        {
            if (tracked)
            {
                lock (_runningGate)
                {
                    _running.Remove(id);
                }
            }
        }

        // A tool that returns on its cancelled token instead of throwing is not answered either.
        if (cancelled.IsCancellationRequested)
        {
            throw new McpCallCancelledException(serverName, id.Item2);
        }

        return JsonRpc.Response(message, writer =>
        {
            writer.WriteStartObject();
            writer.WritePropertyName("content");
            writer.WriteStartArray();
            foreach (var content in result.Content)
            {
                WriteContent(writer, content);
            }

            writer.WriteEndArray();
            writer.WriteBoolean("isError", result.IsError);
            writer.WriteEndObject();
        });
    }

    private async ValueTask<ToolResult> RunAsync(
        IToolServer server,
        string tool,
        JsonElement arguments,
        CancellationToken ct)
    {
        using var scope = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token, ct);

        var call = server.CallAsync(tool, arguments, scope.Token).AsTask();
        _calls.TryAdd(call, 0);
        try
        {
            return await call.ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            _logger.LogWarning(failure, "The SDK MCP tool {Tool} failed.", tool);
            return ToolResult.FromError(failure.Message is { Length: > 0 } text ? text : failure.GetType().Name);
        }
        finally
        {
            _calls.TryRemove(call, out _);
        }
    }
}
