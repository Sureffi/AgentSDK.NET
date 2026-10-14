using System.Text.Json;
using System.Text.Json.Serialization;

namespace Claude.Agent;

internal sealed class ControlRequestFrameDto : Frame
{
    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }

    [JsonPropertyName("request")]
    public JsonElement? Request { get; set; }
}

internal sealed class ControlResponseFrameDto : Frame
{
    [JsonPropertyName("response")]
    public ControlResponseBody? Response { get; set; }
}

internal sealed class ControlResponseBody
{
    [JsonPropertyName("subtype")]
    public string? Subtype { get; set; }

    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }

    [JsonPropertyName("response")]
    public JsonElement? Response { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

internal sealed class ControlCancelRequestFrame : Frame
{
    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }
}
