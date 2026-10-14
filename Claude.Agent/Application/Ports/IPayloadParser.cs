using System.Text.Json;

namespace Claude.Agent;

internal interface IPayloadParser
{
    Result<ServerInfo, ParseError> ServerInfo(JsonElement? payload);

    Result<ContextUsage, ParseError> ContextUsage(JsonElement? payload);

    Result<IReadOnlyList<McpServerStatus>, ParseError> McpStatus(JsonElement? payload);

    Result<PermissionMode, ParseError> PermissionMode(JsonElement? payload);

    Result<RewindVerdict, ParseError> Rewind(JsonElement? payload);

    Result<InterruptReceipt, ParseError> Interrupt(JsonElement? payload);
}
