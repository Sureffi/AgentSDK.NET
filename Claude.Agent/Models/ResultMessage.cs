using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// The last message of a turn: how it ended and what it cost. <see cref="Subtype"/> says which
/// ending; a member only some endings carry is null on the others.
/// </summary>
public sealed class ResultMessage : AgentMessage
{
    internal ResultMessage(
        ResultMessageSubtype subtype,
        TimeSpan duration,
        TimeSpan apiDuration,
        bool isError,
        int turnCount,
        string sessionId,
        string? uuid,
        JsonElement? raw)
        : base(uuid, sessionId, raw)
    {
        Subtype = subtype;
        Duration = duration;
        ApiDuration = apiDuration;
        IsError = isError;
        TurnCount = turnCount;
    }

    /// <summary>How the turn ended.</summary>
    public ResultMessageSubtype Subtype { get; }

    /// <summary>How long the turn took.</summary>
    public TimeSpan Duration { get; }

    /// <summary>How much of that was spent waiting on the API.</summary>
    public TimeSpan ApiDuration { get; }

    /// <summary>Whether the turn ended in an error.</summary>
    public bool IsError { get; }

    /// <summary>The turns taken.</summary>
    public int TurnCount { get; }

    /// <summary>Why the model stopped writing, when the frame says.</summary>
    public StopReason? StopReason { get; internal init; }

    /// <summary>What the session has cost so far, in US dollars.</summary>
    public decimal? TotalCostUsd { get; internal init; }

    /// <summary>What the turn spent.</summary>
    public Usage? Usage { get; internal init; }

    /// <summary>The final text, when the turn ended with one.</summary>
    public string? Result { get; internal init; }

    /// <summary>What each model spent, by model id.</summary>
    public IReadOnlyDictionary<string, ModelUsage> ModelUsage { get; internal init; } = EmptyModelUsage;

    /// <summary>The tool uses the binary refused during the turn.</summary>
    public IReadOnlyList<PermissionDenial> PermissionDenials { get; internal init; } = [];

    /// <summary>A tool use the binary handed back instead of running, when a hook deferred it.</summary>
    public DeferredToolUse? DeferredToolUse { get; internal init; }

    /// <summary>The errors of a turn that ended in one.</summary>
    public IReadOnlyList<string> Errors { get; internal init; } = [];

    /// <summary>Why the binary never got as far as a turn, when it did not.</summary>
    public StartupFailureReason? StartupFailureReason { get; internal init; }

    /// <summary>The HTTP status of the API error that ended the turn, when one did.</summary>
    public int? ApiErrorStatus { get; internal init; }

    /// <summary>Why the turn ended, in the binary's own words, when the frame says.</summary>
    public TerminalReason? TerminalReason { get; internal init; }

    /// <summary>The prompts still waiting when the turn ended.</summary>
    public long? QueuedTurnCount { get; internal init; }

    /// <summary>The prompt this result is for, when the binary stamps one.</summary>
    public string? UserMessageUuid { get; internal init; }

    /// <summary>The prompts this result is for, when the turn answered more than one.</summary>
    public IReadOnlyList<string> UserMessageUuids { get; internal init; } = [];

    /// <summary>Why the session was resumed, when this result belongs to a resumed turn.</summary>
    public string? ResumeReason { get; internal init; }

    /// <summary>Where this result stands among the results of one prompt, when the binary numbers them.</summary>
    public long? ResultIndex { get; internal init; }

    /// <summary>Whether the binary's fast mode was in effect.</summary>
    public FastModeState? FastModeState { get; internal init; }

    /// <summary>Why fast mode was off, when it was.</summary>
    public FastModeDisabledReason? FastModeDisabledReason { get; internal init; }

    /// <summary>Where the turn came from, when the frame says: a turn the binary opened itself names what opened it.</summary>
    public MessageOrigin? Origin { get; internal init; }

    /// <summary>What the turn's subagents did, when the frame counts them.</summary>
    public SubagentStatistics? SubagentStatistics { get; internal init; }

    /// <summary>The local command the turn ran, when it was one.</summary>
    public string? LocalCommand { get; internal init; }

    /// <summary>Whether the turn ran on a process that was already warm.</summary>
    public bool? WarmSpareClaimed { get; internal init; }

    /// <summary>How long until the model's first token.</summary>
    public TimeSpan? TimeToFirstToken { get; internal init; }

    /// <summary>How long until the first token reached this library.</summary>
    public TimeSpan? TimeToFirstStreamedToken { get; internal init; }

    /// <summary>How long until the binary sent the request.</summary>
    public TimeSpan? TimeToRequest { get; internal init; }

    /// <summary>How long from the process starting until the binary sent the request.</summary>
    public TimeSpan? TimeToRequestFromSpawn { get; internal init; }

    /// <summary>How long until the first frame carrying content.</summary>
    public TimeSpan? TimeToFirstContentFrame { get; internal init; }

    /// <summary>How long until the request was sent, by the wall clock.</summary>
    public TimeSpan? RequestSentWallTime { get; internal init; }

    /// <summary>How long until the first post of the streamed answer.</summary>
    public TimeSpan? FirstStreamPost { get; internal init; }

    /// <summary>How long until the first post of the streamed answer was acknowledged.</summary>
    public TimeSpan? FirstStreamPostAcknowledgement { get; internal init; }

    /// <summary>How long until the first post of the streamed answer, by the wall clock.</summary>
    public TimeSpan? FirstStreamPostWallTime { get; internal init; }

    /// <summary>The instant the binary measured the turn's other times from.</summary>
    public DateTimeOffset? TimeOrigin { get; internal init; }

    private static readonly IReadOnlyDictionary<string, ModelUsage> EmptyModelUsage = new Dictionary<string, ModelUsage>();
}
