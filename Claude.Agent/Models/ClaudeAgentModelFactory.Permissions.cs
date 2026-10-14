using System.Text.Json;

namespace Claude.Agent;

public static partial class ClaudeAgentModelFactory
{
    /// <summary>Builds a <see cref="Claude.Agent.ToolPermissionRequest"/> for a test. Each parameter is the property of its name; <c>toolCall</c> defaults to a started call with nothing set.</summary>
    public static ToolPermissionRequest ToolPermissionRequest(
        ToolCall? toolCall = null,
        IEnumerable<PermissionUpdate>? suggestions = null,
        McpServerOrigin? mcpServer = null,
        string? agentId = null,
        string? blockedPath = null,
        string? decisionReason = null,
        PermissionDecisionReasonType? decisionReasonType = null,
        bool? isClassifierApprovable = null,
        bool? suppressAlwaysAllowRule = null,
        bool? defaultsToNo = null,
        MatchedPermissionRule? matchedRule = null,
        string? title = null,
        string? displayName = null,
        string? description = null,
        bool? requiresUserInteraction = null) =>
        new(toolCall ?? ToolCall(), [.. suggestions ?? []])
        {
            McpServer = mcpServer,
            AgentId = agentId,
            BlockedPath = blockedPath,
            DecisionReason = decisionReason,
            DecisionReasonType = decisionReasonType,
            IsClassifierApprovable = isClassifierApprovable,
            SuppressAlwaysAllowRule = suppressAlwaysAllowRule,
            DefaultsToNo = defaultsToNo,
            MatchedRule = matchedRule,
            Title = title,
            DisplayName = displayName,
            Description = description,
            RequiresUserInteraction = requiresUserInteraction,
        };

    /// <summary>Builds a <see cref="Claude.Agent.PermissionRule"/> for a test. Each parameter is the property of its name.</summary>
    public static PermissionRule PermissionRule(string? toolName = null, string? ruleContent = null) =>
        new(toolName ?? string.Empty, ruleContent);

    /// <summary>Builds a <see cref="Claude.Agent.MatchedPermissionRule"/> for a test. Each parameter is the property of its name.</summary>
    public static MatchedPermissionRule MatchedPermissionRule(PermissionRuleSource source = default, string? toolName = null, string? ruleContent = null) =>
        new(source, toolName ?? string.Empty, ruleContent);

    /// <summary>Builds an <see cref="Claude.Agent.AddRulesPermissionUpdate"/> for a test. Each parameter is the property of its name.</summary>
    public static AddRulesPermissionUpdate AddRulesPermissionUpdate(
        IEnumerable<PermissionRule>? rules = null,
        PermissionRuleAction behavior = default,
        PermissionUpdateDestination destination = default) =>
        new([.. rules ?? []], behavior, destination);

    /// <summary>Builds a <see cref="Claude.Agent.ReplaceRulesPermissionUpdate"/> for a test. Each parameter is the property of its name.</summary>
    public static ReplaceRulesPermissionUpdate ReplaceRulesPermissionUpdate(
        IEnumerable<PermissionRule>? rules = null,
        PermissionRuleAction behavior = default,
        PermissionUpdateDestination destination = default) =>
        new([.. rules ?? []], behavior, destination);

    /// <summary>Builds a <see cref="Claude.Agent.RemoveRulesPermissionUpdate"/> for a test. Each parameter is the property of its name.</summary>
    public static RemoveRulesPermissionUpdate RemoveRulesPermissionUpdate(
        IEnumerable<PermissionRule>? rules = null,
        PermissionRuleAction behavior = default,
        PermissionUpdateDestination destination = default) =>
        new([.. rules ?? []], behavior, destination);

    /// <summary>Builds a <see cref="Claude.Agent.SetModePermissionUpdate"/> for a test. Each parameter is the property of its name.</summary>
    public static SetModePermissionUpdate SetModePermissionUpdate(
        PermissionMode mode = default,
        PermissionUpdateDestination destination = default) =>
        new(mode, destination);

    /// <summary>Builds an <see cref="Claude.Agent.AddDirectoriesPermissionUpdate"/> for a test. Each parameter is the property of its name.</summary>
    public static AddDirectoriesPermissionUpdate AddDirectoriesPermissionUpdate(
        IEnumerable<string>? directories = null,
        PermissionUpdateDestination destination = default) =>
        new([.. directories ?? []], destination);

    /// <summary>Builds a <see cref="Claude.Agent.RemoveDirectoriesPermissionUpdate"/> for a test. Each parameter is the property of its name.</summary>
    public static RemoveDirectoriesPermissionUpdate RemoveDirectoriesPermissionUpdate(
        IEnumerable<string>? directories = null,
        PermissionUpdateDestination destination = default) =>
        new([.. directories ?? []], destination);

    /// <summary>Builds an <see cref="Claude.Agent.UnknownPermissionUpdate"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownPermissionUpdate UnknownPermissionUpdate(string? type = null, JsonElement raw = default) =>
        new(type ?? string.Empty, Object(raw));
}
