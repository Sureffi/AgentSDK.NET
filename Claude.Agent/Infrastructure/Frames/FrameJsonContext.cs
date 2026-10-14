using System.Text.Json;
using System.Text.Json.Serialization;

namespace Claude.Agent;

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Frame))]
[JsonSerializable(typeof(UserFrame))]
[JsonSerializable(typeof(AssistantFrame))]
[JsonSerializable(typeof(SystemFrame))]
[JsonSerializable(typeof(ResultFrame))]
[JsonSerializable(typeof(DeferredToolUseBody))]
[JsonSerializable(typeof(StreamEventFrame))]
[JsonSerializable(typeof(RateLimitEventFrame))]
[JsonSerializable(typeof(RateLimitInfoBody))]
[JsonSerializable(typeof(ConversationResetFrame))]
[JsonSerializable(typeof(CommandLifecycleFrame))]
[JsonSerializable(typeof(ControlRequestFrameDto))]
[JsonSerializable(typeof(ControlResponseFrameDto))]
[JsonSerializable(typeof(ControlResponseBody))]
[JsonSerializable(typeof(ControlCancelRequestFrame))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonElement[]))]
internal sealed partial class FrameJsonContext : JsonSerializerContext;
