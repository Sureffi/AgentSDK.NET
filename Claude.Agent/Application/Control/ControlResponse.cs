using System.Text.Json;

namespace Claude.Agent;

internal abstract record ControlResponse
{
    private protected ControlResponse(RequestId requestId) => RequestId = requestId;

    public RequestId RequestId { get; init; }
}

internal sealed record ControlSuccess(RequestId RequestId, JsonElement? Response) : ControlResponse(RequestId);

internal sealed record ControlFailure(RequestId RequestId, string Error) : ControlResponse(RequestId);
