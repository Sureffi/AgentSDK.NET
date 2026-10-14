using System.Text.Json;

namespace Claude.Agent;

internal abstract record ControlReply
{
    private protected ControlReply()
    {
    }
}

internal sealed record HookReply(HookOutput Output, string EventName) : ControlReply;

internal sealed record PermissionReply(ToolPermissionResult Result, JsonElement OriginalInput) : ControlReply;

internal sealed record McpReply(JsonElement McpResponse) : ControlReply;

internal sealed record EmptyReply : ControlReply
{
    public static readonly EmptyReply Instance = new();
}
