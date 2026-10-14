using System.Buffers;
using System.Text;

namespace Claude.Agent;

// A skill named in Skills goes out as the allow rule Skill(<name>) in --allowedTools, the name written
// as the binary writes a rule's content: each '\' doubled, then a '\' put before each '(' and ')'. The
// binary reads a content back by undoing that, \( and \) first and then \\, so the content it reads is
// the name. Every parenthesis in the written content has an odd run of backslashes before it, so the
// rule's own ')' is the last one no odd run escapes, which is where the binary ends the content.
//
// A name is refused when the binary would still not read that rule back as one that names that skill
// alone. Each check is a step of the binary's reading: the command line carries the name as UTF-8;
// the flag's value is split into rules at a comma or a space outside parentheses, where '(' opens and
// any ')' closes, a backslash before either notwithstanding; a content of "*" is the whole tool; and
// the Skill tool drops one leading '/' from a content, reads one that ends in ":*" or " *" as a prefix,
// and compares it with the name the skill is invoked by, trimmed of JavaScript's whitespace.
internal static class SkillNames
{
    public static string Rule(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var content = name.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("(", @"\(", StringComparison.Ordinal)
            .Replace(")", @"\)", StringComparison.Ordinal);
        return $"Skill({content})";
    }

    public static string? Problem(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (UnicodeText.Trim(name).Length == 0)
        {
            return "A skill name must not be blank.";
        }

        if (!CommandLineCarries(name))
        {
            return $"The skill name '{name}' holds a NUL or an unpaired surrogate, which a command line cannot carry.";
        }

        var rule = Rule(name);
        if (SplitsApart(rule))
        {
            return $"The binary would split the rule {rule} in two at the comma or space after a ')' in the name.";
        }

        if (name == "*")
        {
            return "The binary reads the skill name '*' as every skill; set AllSkills for that.";
        }

        if (name.StartsWith('/'))
        {
            return $"The binary would read the skill name '{name}' as '{name[1..]}'; name the skill without the slash.";
        }

        if (name.EndsWith(":*", StringComparison.Ordinal) || name.EndsWith(" *", StringComparison.Ordinal))
        {
            return $"The binary would read the skill name '{name}' as every skill whose name starts with '{name[..^2]}'; name each skill.";
        }

        if (!string.Equals(UnicodeText.Trim(name), name, StringComparison.Ordinal))
        {
            return $"The skill name '{name}' has whitespace at an end, and the binary trims the name a skill is invoked by, so the rule would never match.";
        }

        return null;
    }

    private static bool CommandLineCarries(string name)
    {
        if (name.Contains('\0', StringComparison.Ordinal))
        {
            return false;
        }

        var rest = name.AsSpan();
        while (!rest.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(rest, out _, out var consumed) != OperationStatus.Done)
            {
                return false;
            }

            rest = rest[consumed..];
        }

        return true;
    }

    private static bool SplitsApart(string rule)
    {
        var inside = false;
        foreach (var c in rule)
        {
            switch (c)
            {
                case '(':
                    inside = true;
                    break;
                case ')':
                    inside = false;
                    break;
                case ',' or ' ' when !inside:
                    return true;
            }
        }

        return false;
    }
}
