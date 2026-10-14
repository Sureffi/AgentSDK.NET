using System.Text.Json;

namespace Claude.Agent;

// The in-process MCP servers, as the control channel reaches them. HandleAsync answers one JSON-RPC
// message; null is a message that takes no response (a notification, or a response of the binary's
// own). A tools/call the binary cancels with notifications/cancelled, naming the call's id, has its
// token cancelled and ends in McpCallCancelledException: MCP sends no response to a cancelled
// request, and the binary has already answered the call itself.
internal interface IMcpBridge : IAsyncDisposable
{
    ValueTask<JsonElement?> HandleAsync(string serverName, JsonElement message, CancellationToken ct);

    bool Has(string serverName);

    ValueTask CloseAsync();
}

internal sealed class McpCallCancelledException(string serverName, string requestId)
    : OperationCanceledException($"The binary cancelled the call {requestId} of the MCP server {serverName}.")
{
    public string ServerName { get; } = serverName;

    public string RequestId { get; } = requestId;
}
