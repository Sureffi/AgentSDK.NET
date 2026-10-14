namespace Claude.Agent;

/// <summary>What the model asked the ScheduleWakeup tool to do at the end of a turn of a paced loop. Every member is optional: stopping the loop needs none of the others.</summary>
/// <param name="delay">How long to wait before waking; the wire carries it in seconds and the binary holds it between a minute and an hour. Null when the call named none.</param>
/// <param name="reason">One sentence saying why that long; null when the call gave none.</param>
/// <param name="prompt">What to run on waking; null when the call gave none.</param>
/// <param name="stop">Whether to end the loop instead of scheduling another wake-up; null when the call said nothing.</param>
/// <param name="nothingChanged">Whether this turn found nothing worth keeping, which collapses it in the user's view; null when the call said nothing.</param>
public sealed class ScheduleWakeupToolInput(
    TimeSpan? delay = null,
    string? reason = null,
    string? prompt = null,
    bool? stop = null,
    bool? nothingChanged = null) : ToolInput
{
    /// <summary>How long to wait before waking; the wire carries it in seconds and the binary holds it between a minute and an hour. Null when the call named none.</summary>
    public TimeSpan? Delay { get; } = delay;

    /// <summary>One sentence saying why that long; null when the call gave none.</summary>
    public string? Reason { get; } = reason;

    /// <summary>What to run on waking; null when the call gave none.</summary>
    public string? Prompt { get; } = prompt;

    /// <summary>Whether to end the loop instead of scheduling another wake-up; null when the call said nothing.</summary>
    public bool? Stop { get; } = stop;

    /// <summary>Whether this turn found nothing worth keeping, which collapses it in the user's view; null when the call said nothing.</summary>
    public bool? NothingChanged { get; } = nothingChanged;
}

/// <summary>What a ScheduleWakeup handed back.</summary>
/// <param name="scheduledFor">When the next wake-up fires; the wire carries it as epoch milliseconds.</param>
/// <param name="clampedDelay">The delay actually used, after the binary held it to its bounds.</param>
/// <param name="wasClamped">Whether the delay asked for was outside those bounds.</param>
/// <param name="stopped">Whether the loop was ended instead; null when the binary said nothing.</param>
/// <param name="cancelledWakeupCount">How many pending wake-ups the stop cancelled; null when the binary said nothing.</param>
public sealed class ScheduleWakeupToolOutput(
    DateTimeOffset scheduledFor,
    TimeSpan clampedDelay,
    bool wasClamped,
    bool? stopped = null,
    long? cancelledWakeupCount = null) : ToolOutput
{
    /// <summary>When the next wake-up fires; the wire carries it as epoch milliseconds.</summary>
    public DateTimeOffset ScheduledFor { get; } = scheduledFor;

    /// <summary>The delay actually used, after the binary held it to its bounds.</summary>
    public TimeSpan ClampedDelay { get; } = clampedDelay;

    /// <summary>Whether the delay asked for was outside those bounds.</summary>
    public bool WasClamped { get; } = wasClamped;

    /// <summary>Whether the loop was ended instead; null when the binary said nothing.</summary>
    public bool? Stopped { get; } = stopped;

    /// <summary>How many pending wake-ups the stop cancelled; null when the binary said nothing.</summary>
    public long? CancelledWakeupCount { get; } = cancelledWakeupCount;
}
