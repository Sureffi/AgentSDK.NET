using System.Buffers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Claude.Agent;

// The three requests the binary makes of this process, each answered on its own task: a hook
// callback, a permission decision, an MCP message for an in-process server. A cancelled request
// (control_cancel_request, or the session closing) gets no reply, and neither does a tool call the
// binary cancelled with MCP's own notifications/cancelled.
internal static class ControlRequestHandlers
{
    private const int MethodNotFound = -32601;
    private const int InternalError = -32603;

    private static readonly JsonElement Acknowledgement =
        JsonDocument.Parse("""{"jsonrpc":"2.0","result":{}}""").RootElement.Clone();

    public static async Task HookAsync(RequestId requestId, HookCallbackRequest request, ControlRequestScope scope, SessionRuntime runtime, ILogger logger)
    {
        using (scope)
        {
            OutboundFrame frame;
            if (runtime.Hooks.Find(request.CallbackId) is not { } registration)
            {
                frame = new OutboundControlError(requestId, $"This session registered no hook callback '{request.CallbackId}'.");
            }
            else
            {
                try
                {
                    var output = await registration.Callback(request.Input, scope.Token).ConfigureAwait(false);
                    frame = new OutboundControlResponse(requestId, new HookReply(output ?? new HookOutput(), registration.Event.ToString()));
                }
                catch (OperationCanceledException) when (scope.IsCancelled)
                {
                    return;
                }
                catch (Exception failure)
                {
                    // A hook is host code; whatever it throws is the binary's error reply, a
                    // cancellation of its own included: only this request's cancel is silence.
                    logger.LogWarning(failure, "Hook callback {CallbackId} for {RequestId} failed.", request.CallbackId, requestId.Value);
                    frame = new OutboundControlError(requestId, failure.Message);
                }
            }

            await ReplyAsync(runtime, requestId, scope, frame, logger, "Hook").ConfigureAwait(false);
        }
    }

    public static async Task PermissionAsync(RequestId requestId, CanUseToolRequest request, ControlRequestScope scope, SessionRuntime runtime, ILogger logger)
    {
        using (scope)
        {
            OutboundFrame frame;
            if (runtime.PermissionCallback is not { } decide)
            {
                frame = new OutboundControlError(requestId, "This session has no permission callback to ask.");
            }
            else
            {
                try
                {
                    var asked = request.ToPermissionRequest();

                    var decision = await decide(asked, scope.Token).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("The permission callback returned null.");
                    frame = new OutboundControlResponse(requestId, new PermissionReply(decision, request.Input));
                }
                catch (OperationCanceledException) when (scope.IsCancelled)
                {
                    return;
                }
                catch (Exception failure)
                {
                    // The callback is host code; whatever it throws is the binary's error reply.
                    logger.LogWarning(failure, "Permission decision for {RequestId} failed.", requestId.Value);
                    frame = new OutboundControlError(requestId, failure.Message);
                }
            }

            await ReplyAsync(runtime, requestId, scope, frame, logger, "Permission").ConfigureAwait(false);
        }
    }

    public static async Task McpAsync(RequestId requestId, McpMessageRequest request, ControlRequestScope scope, SessionRuntime runtime, ILogger logger)
    {
        using (scope)
        {
            if (await McpReplyAsync(requestId, request, scope, runtime.Bridge, logger).ConfigureAwait(false) is not { } reply)
            {
                return;
            }

            await ReplyAsync(runtime, requestId, scope, new OutboundControlResponse(requestId, new McpReply(reply)), logger, "MCP")
                .ConfigureAwait(false);
        }
    }

    private static async Task ReplyAsync(SessionRuntime runtime, RequestId requestId, ControlRequestScope scope, OutboundFrame frame, ILogger logger, string what)
    {
        if (scope.IsCancelled || runtime.IsClosed)
        {
            return;
        }

        try
        {
            await runtime.WriteAsync(frame, CancellationToken.None).ConfigureAwait(false);
        }
        catch (ClaudeAgentException failure)
        {
            logger.LogWarning("{What} reply for {RequestId} was not written: {Reason}", what, requestId.Value, failure.Message);
        }
    }

    private static async Task<JsonElement?> McpReplyAsync(RequestId requestId, McpMessageRequest request, ControlRequestScope scope, IMcpBridge bridge, ILogger logger)
    {
        var server = request.ServerName;
        if (!bridge.Has(server))
        {
            return JsonRpcError(request.Message, MethodNotFound, $"This session serves no MCP server named '{server}'.");
        }

        try
        {
            var response = await bridge.HandleAsync(server, request.Message, scope.Token).ConfigureAwait(false);
            return response ?? Acknowledgement;
        }
        catch (McpCallCancelledException cancelled)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("The binary cancelled the call {Call} of the MCP server {Server}; it is not answered.", cancelled.RequestId, cancelled.ServerName);
            }
            return null;
        }
        catch (OperationCanceledException) when (scope.IsCancelled)
        {
            return Acknowledgement;
        }
        catch (Exception failure)
        {
            // A tool is host code; whatever it throws is a JSON-RPC internal error the model reads.
            logger.LogError(failure, "MCP server {Server} failed the request {RequestId}.", server, requestId.Value);

            var detail = failure.Message is { Length: > 0 } text ? text : failure.GetType().Name;
            return JsonRpcError(request.Message, InternalError, detail);
        }
    }

    private static JsonElement JsonRpcError(JsonElement request, int code, string message)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            writer.WritePropertyName("id");

            if (request.ValueKind == JsonValueKind.Object && request.TryGetProperty("id", out var id))
            {
                id.WriteTo(writer);
            }
            else
            {
                writer.WriteNullValue();
            }

            writer.WritePropertyName("error");
            writer.WriteStartObject();
            writer.WriteNumber("code", code);
            writer.WriteString("message", message);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
