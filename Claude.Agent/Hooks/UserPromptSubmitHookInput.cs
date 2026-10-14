namespace Claude.Agent;

/// <summary>A prompt has been submitted and the model has not seen it yet. The hook may add to it or stop it.</summary>
public sealed class UserPromptSubmitHookInput : HookInput
{
    internal UserPromptSubmitHookInput(HookInputCommon common,
        string prompt,
        UserPromptSubmitSource? source,
        string? sessionTitle)
        : base(HookEvent.UserPromptSubmit, common)
    {
        Prompt = prompt;
        Source = source;
        SessionTitle = sessionTitle;
    }

    /// <summary>The prompt as submitted.</summary>
    public string Prompt { get; }

    /// <summary>What submitted it; null when the binary did not say.</summary>
    public UserPromptSubmitSource? Source { get; }

    /// <summary>The session's title at the time; null when the binary sent none.</summary>
    public string? SessionTitle { get; }
}
