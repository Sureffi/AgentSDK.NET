using System.Text.Json;

namespace Claude.Agent;

/// <summary>The binary started a new conversation in the same session, after <c>/clear</c> or its like.</summary>
public sealed class ConversationResetMessage : AgentMessage
{
    internal ConversationResetMessage(string newConversationId, string uuid, string sessionId, JsonElement? raw)
        : base(uuid, sessionId, raw)
    {
        NewConversationId = newConversationId;
    }

    /// <summary>The id of the conversation that begins. It is not the session id the next init frame announces.</summary>
    public string NewConversationId { get; }
}
