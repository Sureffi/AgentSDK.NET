using System.Text;

namespace Claude.Agent.Tests.Domain;

// The binary's reading of an --allowedTools value, step by step, for a rule the library writes to be
// held against: the value is split into rules at a comma or a space outside parentheses, where '('
// opens and any ')' closes, and each rule is trimmed; a rule's tool is the text before its first '('
// no odd run of backslashes escapes, and its content the text from there to its last ')' no odd run
// escapes, which has to end the rule, or the whole rule is read as a tool's name; a content of "" or
// "*" is the whole tool; and a content has \( and \) unescaped, then \\.
internal static class AllowedToolsReading
{
    public static IReadOnlyList<(string Tool, string? Content)> Read(string value) =>
        [.. Split(value).Select(Rule)];

    private static List<string> Split(string value)
    {
        var rules = new List<string>();
        var current = new StringBuilder();
        var inside = false;
        foreach (var c in value)
        {
            switch (c)
            {
                case '(':
                    inside = true;
                    current.Append(c);
                    break;
                case ')':
                    inside = false;
                    current.Append(c);
                    break;
                case ',' or ' ' when inside:
                    current.Append(c);
                    break;
                case ',' or ' ':
                    if (UnicodeText.Trim(current.ToString()).Length > 0)
                    {
                        rules.Add(UnicodeText.Trim(current.ToString()));
                        current.Clear();
                    }
                    else if (c == ',')
                    {
                        current.Clear();
                    }

                    break;
                default:
                    current.Append(c);
                    break;
            }
        }

        if (UnicodeText.Trim(current.ToString()).Length > 0)
        {
            rules.Add(UnicodeText.Trim(current.ToString()));
        }

        return rules;
    }

    private static (string Tool, string? Content) Rule(string rule)
    {
        var open = Unescaped(rule, '(', fromEnd: false);
        var close = Unescaped(rule, ')', fromEnd: true);
        if (open == -1 && close == -1)
        {
            return (rule, null);
        }

        var tool = open == -1 ? string.Empty : rule[..open];
        if (open == -1 || close <= open || close != rule.Length - 1 || tool.IndexOfAny(['(', ')']) >= 0 || tool.Length == 0)
        {
            return (rule, null);
        }

        var content = rule[(open + 1)..close];
        if (content is "" or "*")
        {
            return (tool, null);
        }

        return (tool, content.Replace(@"\(", "(", StringComparison.Ordinal)
            .Replace(@"\)", ")", StringComparison.Ordinal)
            .Replace(@"\\", @"\", StringComparison.Ordinal));
    }

    private static int Unescaped(string rule, char wanted, bool fromEnd)
    {
        for (var step = 0; step < rule.Length; step++)
        {
            var index = fromEnd ? rule.Length - 1 - step : step;
            if (rule[index] != wanted)
            {
                continue;
            }

            var backslashes = 0;
            for (var before = index - 1; before >= 0 && rule[before] == '\\'; before--)
            {
                backslashes++;
            }

            if (backslashes % 2 == 0)
            {
                return index;
            }
        }

        return -1;
    }
}
