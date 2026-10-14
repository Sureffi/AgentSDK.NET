using System.Collections.Immutable;
using System.Text.Json;

namespace Claude.Agent;

internal abstract record OutboundFrame
{
    private protected OutboundFrame()
    {
    }
}

internal enum UserInputForm
{
    OneShot,
    Client,
}

internal sealed record UserInputFrame(
    string? Text,
    ImmutableArray<JsonElement>? Blocks,
    string? ParentToolUseId,
    UserInputForm Form,
    Guid? Uuid = null) : OutboundFrame;

internal sealed record OutboundControlRequest(RequestId RequestId, ControlRequest Request) : OutboundFrame;

internal sealed record OutboundControlResponse(RequestId RequestId, ControlReply Reply) : OutboundFrame;

internal sealed record OutboundControlError(RequestId RequestId, string Error) : OutboundFrame;
