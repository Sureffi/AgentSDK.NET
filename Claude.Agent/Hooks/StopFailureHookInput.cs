namespace Claude.Agent;

/// <summary>The main thread's turn ended in an error rather than an answer.</summary>
public sealed class StopFailureHookInput : HookInput
{
    internal StopFailureHookInput(HookInputCommon common,
        AssistantErrorCode errorCode,
        string? errorDetails,
        string? lastAssistantMessage)
        : base(HookEvent.StopFailure, common)
    {
        ErrorCode = errorCode;
        ErrorDetails = errorDetails;
        LastAssistantMessage = lastAssistantMessage;
    }

    /// <summary>What went wrong, as the binary names it.</summary>
    public AssistantErrorCode ErrorCode { get; }

    /// <summary>More about it in the binary's words; null when it sent none.</summary>
    public string? ErrorDetails { get; }

    /// <summary>The text of the last thing the model said before the failure; null when the binary sent none.</summary>
    public string? LastAssistantMessage { get; }
}
