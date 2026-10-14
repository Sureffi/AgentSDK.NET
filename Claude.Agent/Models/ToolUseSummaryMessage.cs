using System.Text.Json;

namespace Claude.Agent;

/// <summary>What a run of tool uses came to, as the binary summarises it.</summary>
public sealed class ToolUseSummaryMessage : AgentMessage
{
    internal ToolUseSummaryMessage(
        string summary,
        IReadOnlyList<string> precedingToolUseIds,
        string? uuid,
        string? sessionId,
        JsonElement? raw)
        : base(uuid, sessionId, raw)
    {
        Summary = summary;
        PrecedingToolUseIds = precedingToolUseIds;
    }

    /// <summary>The summary.</summary>
    public string Summary { get; }

    /// <summary>The tool uses it is about.</summary>
    public IReadOnlyList<string> PrecedingToolUseIds { get; }
}
