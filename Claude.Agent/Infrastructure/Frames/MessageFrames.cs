using System.Text.Json;
using System.Text.Json.Serialization;

namespace Claude.Agent;

// What a message frame is bound to before it is read. A member is bound here when its type has to
// hold on the wire for the frame to mean anything — a result's durations and counts, whose wrong
// kind makes the line the unknown — and read off the line itself otherwise, where a member of the
// wrong kind is that member's own loss and never the frame's.
internal sealed class UserFrame : Frame
{
    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; set; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("tool_use_result")]
    public JsonElement? ToolUseResult { get; set; }

    [JsonPropertyName("origin")]
    public JsonElement? Origin { get; set; }
}

internal sealed class AssistantFrame : Frame
{
    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; set; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("error")]
    public JsonElement? Error { get; set; }
}

internal sealed class SystemFrame : Frame
{
    [JsonPropertyName("subtype")]
    public string? Subtype { get; set; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }
}

internal sealed class ResultFrame : Frame
{
    [JsonPropertyName("subtype")]
    public string? Subtype { get; set; }

    [JsonPropertyName("duration_ms")]
    public long? DurationMs { get; set; }

    [JsonPropertyName("duration_api_ms")]
    public long? DurationApiMs { get; set; }

    [JsonPropertyName("is_error")]
    public bool? IsError { get; set; }

    [JsonPropertyName("num_turns")]
    public int? NumTurns { get; set; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }

    [JsonPropertyName("total_cost_usd")]
    public decimal? TotalCostUsd { get; set; }

    [JsonPropertyName("usage")]
    public JsonElement? Usage { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("structured_output")]
    public JsonElement? StructuredOutput { get; set; }

    [JsonPropertyName("modelUsage")]
    public JsonElement? ModelUsage { get; set; }

    [JsonPropertyName("permission_denials")]
    public JsonElement? PermissionDenials { get; set; }

    [JsonPropertyName("deferred_tool_use")]
    public DeferredToolUseBody? DeferredToolUse { get; set; }

    [JsonPropertyName("errors")]
    public JsonElement? Errors { get; set; }

    [JsonPropertyName("api_error_status")]
    public int? ApiErrorStatus { get; set; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("terminal_reason")]
    public string? TerminalReason { get; set; }

    [JsonPropertyName("origin")]
    public JsonElement? Origin { get; set; }
}

internal sealed class DeferredToolUseBody
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("input")]
    public JsonElement Input { get; set; }
}

internal sealed class StreamEventFrame : Frame
{
    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("event")]
    public JsonElement? Event { get; set; }

    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; set; }
}

internal sealed class RateLimitEventFrame : Frame
{
    [JsonPropertyName("rate_limit_info")]
    public RateLimitInfoBody? RateLimitInfo { get; set; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }
}

internal sealed class RateLimitInfoBody
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("resetsAt")]
    public long? ResetsAt { get; set; }

    [JsonPropertyName("rateLimitType")]
    public string? RateLimitType { get; set; }

    [JsonPropertyName("utilization")]
    public double? Utilization { get; set; }

    [JsonPropertyName("overageStatus")]
    public string? OverageStatus { get; set; }

    [JsonPropertyName("overageResetsAt")]
    public long? OverageResetsAt { get; set; }

    [JsonPropertyName("overageDisabledReason")]
    public string? OverageDisabledReason { get; set; }

    [JsonPropertyName("isUsingOverage")]
    public bool? IsUsingOverage { get; set; }

    [JsonPropertyName("overageInUse")]
    public bool? OverageInUse { get; set; }

    [JsonPropertyName("surpassedThreshold")]
    public double? SurpassedThreshold { get; set; }

    [JsonPropertyName("limitScope")]
    public string? LimitScope { get; set; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("canUserPurchaseCredits")]
    public bool? CanUserPurchaseCredits { get; set; }

    [JsonPropertyName("hasChargeableSavedPaymentMethod")]
    public bool? HasChargeableSavedPaymentMethod { get; set; }
}

internal sealed class CommandLifecycleFrame : Frame
{
    [JsonPropertyName("command_uuid")]
    public string? CommandUuid { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }
}

internal sealed class ConversationResetFrame : Frame
{
    [JsonPropertyName("new_conversation_id")]
    public string? NewConversationId { get; set; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }
}

// The frames outside the message union, and the four inside it that carry nothing the binding has
// to hold: each is read off its line whole.
internal sealed class ToolProgressFrame : Frame;

internal sealed class ToolUseSummaryFrame : Frame;

internal sealed class AuthStatusFrame : Frame;

internal sealed class PromptSuggestionFrame : Frame;

internal sealed class ActiveGoalFrame : Frame;
