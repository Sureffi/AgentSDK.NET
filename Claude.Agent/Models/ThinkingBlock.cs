namespace Claude.Agent;

/// <summary>The model's thinking, with the signature the API issued for it.</summary>
public sealed class ThinkingBlock : ContentBlock
{
    internal ThinkingBlock(string thinking, string signature)
    {
        Thinking = thinking;
        Signature = signature;
    }

    /// <summary>What the model thought, as it wrote it.</summary>
    public string Thinking { get; }

    /// <summary>The API's signature over the thinking; it goes back unchanged when the turn is sent again.</summary>
    public string Signature { get; }
}
