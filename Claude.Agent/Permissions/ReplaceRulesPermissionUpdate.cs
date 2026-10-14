namespace Claude.Agent;

/// <summary>Every rule of one <see cref="Behavior"/> replaced by these, each reaching a tool or a part of one.</summary>
public sealed class ReplaceRulesPermissionUpdate : PermissionUpdate
{
    internal ReplaceRulesPermissionUpdate(IReadOnlyList<PermissionRule> rules, PermissionRuleAction behavior, PermissionUpdateDestination destination)
        : base(destination)
    {
        Rules = rules;
        Behavior = behavior;
    }

    /// <summary>The rules.</summary>
    public IReadOnlyList<PermissionRule> Rules { get; }

    /// <summary>What the rules do with the tool uses they match.</summary>
    public PermissionRuleAction Behavior { get; }
}
