namespace Claude.Agent;

/// <summary>What the model asked the PushNotification tool to send.</summary>
/// <param name="message">The notification's body.</param>
/// <param name="status">Why it is being sent.</param>
public sealed class PushNotificationToolInput(string message, PushNotificationStatus status) : ToolInput
{
    /// <summary>The notification's body.</summary>
    public string Message { get; } = message;

    /// <summary>Why it is being sent.</summary>
    public PushNotificationStatus Status { get; } = status;
}

/// <summary>What a PushNotification handed back.</summary>
/// <param name="message">The notification's body as it went out.</param>
/// <param name="pushSent">Whether it was pushed to a device; null when the binary said nothing.</param>
/// <param name="localSent">Whether it was raised on this machine; null when the binary said nothing.</param>
/// <param name="disabledReason">Why it was not pushed; null when it was.</param>
/// <param name="sentAt">When it was sent, as the timestamp the binary wrote; null on an output from before the binary wrote it.</param>
public sealed class PushNotificationToolOutput(
    string message,
    bool? pushSent = null,
    bool? localSent = null,
    PushNotificationDisabledReason? disabledReason = null,
    string? sentAt = null) : ToolOutput
{
    /// <summary>The notification's body as it went out.</summary>
    public string Message { get; } = message;

    /// <summary>Whether it was pushed to a device; null when the binary said nothing.</summary>
    public bool? PushSent { get; } = pushSent;

    /// <summary>Whether it was raised on this machine; null when the binary said nothing.</summary>
    public bool? LocalSent { get; } = localSent;

    /// <summary>Why it was not pushed; null when it was.</summary>
    public PushNotificationDisabledReason? DisabledReason { get; } = disabledReason;

    /// <summary>When it was sent, as the timestamp the binary wrote; null on an output from before the binary wrote it.</summary>
    public string? SentAt { get; } = sentAt;
}
