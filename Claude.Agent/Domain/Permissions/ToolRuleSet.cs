using System.Collections.Immutable;

namespace Claude.Agent;

internal sealed record ToolRuleSet(ImmutableArray<ToolRule> Rules)
{
    public static readonly ToolRuleSet Empty = new([]);

    public ImmutableArray<ToolRule> Rules { get; } = Rules.IsDefault ? [] : Rules;

    public IEnumerable<string> WholeTools =>
        Rules.Where(rule => rule.IsWholeTool).Select(rule => rule.Tool!).Distinct();

    public static ToolRuleSet Parse(IEnumerable<string> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var parsed = ImmutableArray.CreateBuilder<ToolRule>();
        foreach (var rule in rules)
        {
            if (!string.IsNullOrWhiteSpace(rule))
            {
                parsed.Add(ToolRule.Parse(rule));
            }
        }

        return new ToolRuleSet(parsed.ToImmutable());
    }

    public string ToWire() => string.Join(",", Rules.Select(rule => rule.ToWire()));
}
