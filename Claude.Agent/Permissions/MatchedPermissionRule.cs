namespace Claude.Agent;

/// <summary>
/// The rule of the person's own that forced a permission ask, carried beside the ask so that a host
/// deciding by <see cref="ToolPermissionRequest.DecisionReasonType"/> can see the ask was asked for.
/// </summary>
public sealed class MatchedPermissionRule
{
    internal MatchedPermissionRule(PermissionRuleSource source, string toolName, string? ruleContent)
    {
        Source = source;
        ToolName = toolName;
        RuleContent = ruleContent;
    }

    /// <summary>Where the rule comes from, as the binary names it: a settings file, the command line, a command, the session or a policy.</summary>
    public PermissionRuleSource Source { get; }

    /// <summary>The tool the rule is about.</summary>
    public string ToolName { get; }

    /// <summary>What of the tool it reaches; null when it reaches the whole tool.</summary>
    public string? RuleContent { get; }
}
