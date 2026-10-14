namespace Claude.Agent;

/// <summary>The binary has something to tell the person running it.</summary>
public sealed class NotificationHookInput : HookInput
{
    internal NotificationHookInput(HookInputCommon common,
        string message,
        string? title,
        NotificationType notificationType)
        : base(HookEvent.Notification, common)
    {
        Message = message;
        Title = title;
        NotificationType = notificationType;
    }

    /// <summary>The text.</summary>
    public string Message { get; }

    /// <summary>A heading for it; null when the binary sent none.</summary>
    public string? Title { get; }

    /// <summary>What kind of notification it is, as the binary names it.</summary>
    public NotificationType NotificationType { get; }
}
