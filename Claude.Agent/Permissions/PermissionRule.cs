namespace Claude.Agent;

/// <summary>
/// One rule of a permission update: a tool, and what of that tool the rule reaches. A rule that
/// names no content reaches the whole tool, and goes out with no content beside it.
/// </summary>
public sealed class PermissionRule
{
    internal PermissionRule(string toolName, string? ruleContent)
    {
        ToolName = toolName;
        RuleContent = ruleContent;
    }

    /// <summary>The tool the rule is about.</summary>
    public string ToolName { get; }

    /// <summary>What of the tool it reaches, in the binary's own syntax — a command prefix, a path glob; null when the rule reaches the whole tool.</summary>
    public string? RuleContent { get; }

    // The rule as a caller writes it on the public factories: "Read", "Bash(git:*)".
    internal static PermissionRule Parse(string rule)
    {
        var parsed = ToolRule.Parse(rule);
        return parsed.Tool is { } tool ? new PermissionRule(tool, parsed.Specifier) : new PermissionRule(rule, null);
    }
}
