using System.Text.Json;

namespace Claude.Agent;

/// <summary>Where the binary stands with signing the account in.</summary>
public sealed class AuthStatusMessage : AgentMessage
{
    internal AuthStatusMessage(bool isAuthenticating, IReadOnlyList<string> output, string? uuid, string? sessionId, JsonElement? raw)
        : base(uuid, sessionId, raw)
    {
        IsAuthenticating = isAuthenticating;
        Output = output;
    }

    /// <summary>Whether it is signing in now.</summary>
    public bool IsAuthenticating { get; }

    /// <summary>What it has to say about it, line by line.</summary>
    public IReadOnlyList<string> Output { get; }

    /// <summary>What went wrong, when something did.</summary>
    public string? Error { get; internal init; }
}
