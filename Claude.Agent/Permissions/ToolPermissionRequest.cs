namespace Claude.Agent;

/// <summary>A tool use the binary wants a decision on, in place of the person it would have asked.</summary>
public sealed class ToolPermissionRequest
{
    internal ToolPermissionRequest(ToolCall toolCall, IReadOnlyList<PermissionUpdate> suggestions)
    {
        ToolCall = toolCall;
        Suggestions = suggestions;
    }

    /// <summary>The call being asked about: the tool, its input and its id. A snapshot of the ask, not the pairing a run keeps.</summary>
    public ToolCall ToolCall { get; }

    /// <summary>What the binary would remember if the person chose "always allow": hand them back on an allow to apply them.</summary>
    public IReadOnlyList<PermissionUpdate> Suggestions { get; }

    /// <summary>The MCP server the tool belongs to; null for a tool the binary runs itself.</summary>
    public McpServerOrigin? McpServer { get; internal init; }

    /// <summary>The subagent asking; null on the main thread.</summary>
    public string? AgentId { get; internal init; }

    /// <summary>The path that made the binary ask, when a path did.</summary>
    public string? BlockedPath { get; internal init; }

    /// <summary>Why the binary is asking, in its words. It may carry terminal escapes: clean it before showing it.</summary>
    public string? DecisionReason { get; internal init; }

    /// <summary>Why the binary is asking, as a value to decide by rather than prose to read; null when the binary sent none.</summary>
    public PermissionDecisionReasonType? DecisionReasonType { get; internal init; }

    /// <summary>Whether every safety check on this use may be answered by the binary's classifier; false when at least one wants a person. Null when no safety check is involved.</summary>
    public bool? IsClassifierApprovable { get; internal init; }

    /// <summary>Whether to leave out the offer to stop asking: taking it would write a rule wider than this ask. Null when the binary did not say.</summary>
    public bool? SuppressAlwaysAllowRule { get; internal init; }

    /// <summary>Whether the ask must not be allowed by a stray keystroke: do not put the allow under the hand. Null when the binary did not say.</summary>
    public bool? DefaultsToNo { get; internal init; }

    /// <summary>The rule that forced the ask, when a rule of the person's own did; null otherwise.</summary>
    public MatchedPermissionRule? MatchedRule { get; internal init; }

    /// <summary>The binary's title for the prompt it would have shown.</summary>
    public string? Title { get; internal init; }

    /// <summary>The tool's name as the binary would show it.</summary>
    public string? DisplayName { get; internal init; }

    /// <summary>The binary's description of what the tool is about to do.</summary>
    public string? Description { get; internal init; }

    /// <summary>Whether the decision cannot be a plain allow or deny from here: the person has to answer on the tool's own surface. Null when the binary did not say.</summary>
    public bool? RequiresUserInteraction { get; internal init; }
}
