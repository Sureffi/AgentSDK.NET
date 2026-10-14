using System.Text.Json;

namespace Claude.Agent;

/// <summary>A prompt the binary suggests sending next.</summary>
public sealed class PromptSuggestionMessage : AgentMessage
{
    internal PromptSuggestionMessage(string suggestion, string? uuid, string? sessionId, JsonElement? raw)
        : base(uuid, sessionId, raw)
    {
        Suggestion = suggestion;
    }

    /// <summary>The suggested prompt.</summary>
    public string Suggestion { get; }
}
