namespace Claude.Agent;

/// <summary>What the API's advisor answered a <see cref="ServerToolUseBlock"/> with.</summary>
public sealed class AdvisorToolResultBlock : ContentBlock
{
    internal AdvisorToolResultBlock(string toolUseId, IReadOnlyList<AdvisorToolResultContent> content)
    {
        ToolUseId = toolUseId;
        Content = content;
    }

    /// <summary>The <see cref="ServerToolUseBlock.Id"/> of the call this answers.</summary>
    public string ToolUseId { get; }

    /// <summary>The advisor's answer, or the one part saying why it did not answer.</summary>
    public IReadOnlyList<AdvisorToolResultContent> Content { get; }
}
