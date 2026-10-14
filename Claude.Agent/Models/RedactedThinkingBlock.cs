namespace Claude.Agent;

/// <summary>Thinking the API kept back: opaque, and sent back unchanged when the turn is sent again.</summary>
public sealed class RedactedThinkingBlock : ContentBlock
{
    internal RedactedThinkingBlock(string data)
    {
        Data = data;
    }

    /// <summary>The thinking as the API kept it, opaque; it goes back unchanged.</summary>
    public string Data { get; }
}
