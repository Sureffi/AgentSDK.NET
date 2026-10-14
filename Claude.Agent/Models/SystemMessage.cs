using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// A notice from the binary about the session rather than the conversation. One type per notice the
/// binary names, each with only its own members; <see cref="Subtype"/> is the name it goes under. A
/// notice this library has no type for is an <see cref="UnknownMessage"/> of type "system" with its
/// subtype.
/// </summary>
public abstract class SystemMessage : AgentMessage
{
    private protected SystemMessage(string subtype, string? uuid, string? sessionId, JsonElement? raw)
        : base(uuid, sessionId, raw)
    {
        Subtype = subtype;
    }

    /// <summary>The notice's name, as the binary writes it.</summary>
    public string Subtype { get; }
}
