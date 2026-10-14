namespace Claude.Agent;

internal sealed record ToolRule(string Raw, string? Tool, string? Specifier)
{
    public bool IsWholeTool => Tool is not null && Specifier is null or "" or "*";

    public static ToolRule Parse(string rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var open = rule.IndexOf('(', StringComparison.Ordinal);
        if (open == 0 || (open > 0 && !rule.EndsWith(')')))
        {
            return new ToolRule(rule, null, null);
        }

        var name = open < 0 ? rule : rule[..open];
        var specifier = open < 0 ? null : rule[(open + 1)..^1];

        return Ids.IsName(name)
            ? new ToolRule(rule, name, specifier)
            : new ToolRule(rule, null, null);
    }

    public string ToWire() => Tool is not { } tool
        ? Raw
        : Specifier is null ? tool : $"{tool}({Specifier})";
}
