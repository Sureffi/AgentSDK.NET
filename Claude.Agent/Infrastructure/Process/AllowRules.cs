using System.Collections.Immutable;

namespace Claude.Agent;

// The allow rules --allowedTools carries: the options' own, then the Skill tool's, as they are. The
// Skill tool gathers its rules by their content, so a rule written twice grants what it grants once.
// The binary reads the rule Skill as the whole tool, so AllSkills is that rule, and Skill(<content>)
// as the skills its content names, so each name in Skills is its SkillNames.Rule.
// No setting source goes with them: with no --setting-sources the binary reads the user's, the
// project's and the local settings, and finds the user's and the project's skills among them.
internal static class AllowRules
{
    private const string WholeSkillTool = "Skill";

    public static ImmutableArray<string> For(ClaudeAgentSessionOptions session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var rules = ImmutableArray.CreateBuilder<string>();
        rules.AddRange(session.AllowedTools);

        if (session.AllSkills is true)
        {
            rules.Add(WholeSkillTool);
        }
        else
        {
            foreach (var name in session.Skills)
            {
                rules.Add(SkillNames.Rule(name));
            }
        }

        return rules.ToImmutable();
    }
}
