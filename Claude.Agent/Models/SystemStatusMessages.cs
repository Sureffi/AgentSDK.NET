using System.Text.Json;

namespace Claude.Agent;

/// <summary>What the binary is busy with, and how a compaction it ran ended.</summary>
public sealed class StatusMessage : SystemMessage
{
    internal StatusMessage(string? uuid, string? sessionId, JsonElement? raw)
        : base("status", uuid, sessionId, raw)
    {
    }

    /// <summary>What the binary is doing; null when it has stopped doing it.</summary>
    public ActivityStatus? Status { get; internal init; }

    /// <summary>The mode the session runs under, when the notice says.</summary>
    public PermissionMode? PermissionMode { get; internal init; }

    /// <summary>How a compaction ended, on the notice that follows one.</summary>
    public CompactResult? CompactResult { get; internal init; }

    /// <summary>Why a compaction failed, in the binary's own words.</summary>
    public string? CompactError { get; internal init; }
}

/// <summary>The conversation was compacted here: what the compaction cost and what it kept.</summary>
public sealed class CompactBoundaryMessage : SystemMessage
{
    internal CompactBoundaryMessage(CompactMetadata compactMetadata, string? uuid, string? sessionId, JsonElement? raw)
        : base("compact_boundary", uuid, sessionId, raw)
    {
        CompactMetadata = compactMetadata;
    }

    /// <summary>What the compaction did.</summary>
    public CompactMetadata CompactMetadata { get; }

    /// <summary>The message the compacted branch hangs off, under the wire key <c>logical_parent_uuid</c>; null when the binary sent none.</summary>
    public string? LogicalParentUuid { get; internal init; }
}

/// <summary>What a compaction did: what started it, what it cost, and what it held on to.</summary>
public sealed class CompactMetadata
{
    internal CompactMetadata(CompactTrigger trigger, long preTokens)
    {
        Trigger = trigger;
        PreTokens = preTokens;
    }

    /// <summary>What started the compaction.</summary>
    public CompactTrigger Trigger { get; }

    /// <summary>The tokens the conversation took before it.</summary>
    public long PreTokens { get; }

    /// <summary>The tokens it takes after.</summary>
    public long? PostTokens { get; internal init; }

    /// <summary>How long the compaction took.</summary>
    public TimeSpan? Duration { get; internal init; }

    /// <summary>The run of messages the compaction kept whole.</summary>
    public PreservedSegment? PreservedSegment { get; internal init; }

    /// <summary>The messages the compaction kept, one by one.</summary>
    public PreservedMessages? PreservedMessages { get; internal init; }

    /// <summary>The tokens every compaction of this session has dropped, this one included, under the wire key <c>cumulative_dropped_tokens</c>.</summary>
    public long? CumulativeDroppedTokens { get; internal init; }
}

/// <summary>The run of messages a compaction kept whole, by the ids at its ends.</summary>
public sealed class PreservedSegment
{
    internal PreservedSegment(string headUuid, string anchorUuid, string tailUuid)
    {
        HeadUuid = headUuid;
        AnchorUuid = anchorUuid;
        TailUuid = tailUuid;
    }

    /// <summary>The first message kept.</summary>
    public string HeadUuid { get; }

    /// <summary>The message the run hangs on.</summary>
    public string AnchorUuid { get; }

    /// <summary>The last message kept.</summary>
    public string TailUuid { get; }
}

/// <summary>The messages a compaction kept, by their ids.</summary>
public sealed class PreservedMessages
{
    internal PreservedMessages(string anchorUuid, IReadOnlyList<string> uuids)
    {
        AnchorUuid = anchorUuid;
        Uuids = uuids;
    }

    /// <summary>The message the kept ones hang on.</summary>
    public string AnchorUuid { get; }

    /// <summary>The messages kept.</summary>
    public IReadOnlyList<string> Uuids { get; }
}

/// <summary>The binary is asking the API again after a failure.</summary>
public sealed class ApiRetryMessage : SystemMessage
{
    internal ApiRetryMessage(long attempt, long maxRetries, TimeSpan retryDelay, string? uuid, string? sessionId, JsonElement? raw)
        : base("api_retry", uuid, sessionId, raw)
    {
        Attempt = attempt;
        MaxRetries = maxRetries;
        RetryDelay = retryDelay;
    }

    /// <summary>Which attempt this is.</summary>
    public long Attempt { get; }

    /// <summary>How many attempts the binary will make.</summary>
    public long MaxRetries { get; }

    /// <summary>How long the binary waits before the next one.</summary>
    public TimeSpan RetryDelay { get; }

    /// <summary>The HTTP status that failed, when there was one.</summary>
    public long? ErrorStatus { get; internal init; }

    /// <summary>What failed.</summary>
    public AssistantErrorCode? Error { get; internal init; }

    /// <summary>Set when nothing came back at all: how long the binary waited.</summary>
    public ApiRetryNoResponse? NoResponse { get; internal init; }
}

/// <summary>How long the binary waited for an answer that never came, and how long it waits now.</summary>
public sealed class ApiRetryNoResponse
{
    internal ApiRetryNoResponse(TimeSpan waited, TimeSpan retryWait)
    {
        Waited = waited;
        RetryWait = retryWait;
    }

    /// <summary>How long it waited.</summary>
    public TimeSpan Waited { get; }

    /// <summary>How long it waits before asking again.</summary>
    public TimeSpan RetryWait { get; }
}

/// <summary>Where a control request this library sent stands, while the binary works on it.</summary>
public sealed class ControlRequestProgressMessage : SystemMessage
{
    internal ControlRequestProgressMessage(
        string requestId,
        ControlRequestProgressStatus status,
        string? uuid,
        string? sessionId,
        JsonElement? raw)
        : base("control_request_progress", uuid, sessionId, raw)
    {
        RequestId = requestId;
        Status = status;
    }

    /// <summary>The request this is about, by the id this library sent it under.</summary>
    public string RequestId { get; }

    /// <summary>Where it stands.</summary>
    public ControlRequestProgressStatus Status { get; }

    /// <summary>Which attempt the binary is on, when it is retrying.</summary>
    public long? Attempt { get; internal init; }

    /// <summary>How many attempts it will make.</summary>
    public long? MaxRetries { get; internal init; }

    /// <summary>How long it waits before the next one.</summary>
    public TimeSpan? RetryDelay { get; internal init; }

    /// <summary>The HTTP status that failed, when there was one.</summary>
    public long? ErrorStatus { get; internal init; }
}

/// <summary>How much thinking the turn has done, as the binary counts it.</summary>
public sealed class ThinkingTokensMessage : SystemMessage
{
    internal ThinkingTokensMessage(long estimatedTokens, long estimatedTokensDelta, string? uuid, string? sessionId, JsonElement? raw)
        : base("thinking_tokens", uuid, sessionId, raw)
    {
        EstimatedTokens = estimatedTokens;
        EstimatedTokensDelta = estimatedTokensDelta;
    }

    /// <summary>The thinking tokens the turn has spent, as the binary estimates them.</summary>
    public long EstimatedTokens { get; }

    /// <summary>How many of those came since the last notice.</summary>
    public long EstimatedTokensDelta { get; }

    /// <summary>The prompt the thinking belongs to, when the binary stamps one.</summary>
    public string? UserMessageUuid { get; internal init; }
}

/// <summary>The session moved between waiting, working and wanting an answer.</summary>
public sealed class SessionStateChangedMessage : SystemMessage
{
    internal SessionStateChangedMessage(SessionState state, string? uuid, string? sessionId, JsonElement? raw)
        : base("session_state_changed", uuid, sessionId, raw)
    {
        State = state;
    }

    /// <summary>Where the session now stands.</summary>
    public SessionState State { get; }
}

/// <summary>The worker the session runs on is going away.</summary>
public sealed class WorkerShuttingDownMessage : SystemMessage
{
    internal WorkerShuttingDownMessage(string reason, string? uuid, string? sessionId, JsonElement? raw)
        : base("worker_shutting_down", uuid, sessionId, raw)
    {
        Reason = reason;
    }

    /// <summary>Why it is going away, in the binary's own words.</summary>
    public string Reason { get; }
}
