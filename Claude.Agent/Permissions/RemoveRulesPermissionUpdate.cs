namespace Claude.Agent;

/// <summary>Rules removed, each reaching a tool or a part of one, all with the same <see cref="Behavior"/>.</summary>
public sealed class RemoveRulesPermissionUpdate : PermissionUpdate
{
    internal RemoveRulesPermissionUpdate(IReadOnlyList<PermissionRule> rules, PermissionRuleAction behavior, PermissionUpdateDestination destination)
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
