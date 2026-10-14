using System.Collections.Immutable;
using System.Text.Json;

namespace Claude.Agent;

// One type per kind of update. A kind this library has no type for, and an update missing one of
// its kind's members, is UnknownPermissionUpdate with the whole update as its Raw, written back as
// it came.
internal static class PermissionUpdateParser
{
    public static ImmutableArray<PermissionUpdate> ParseAll(JsonElement suggestions)
    {
        if (suggestions.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<PermissionUpdate>();
        foreach (var suggestion in suggestions.EnumerateArray())
        {
            builder.Add(Parse(suggestion));
        }

        return builder.ToImmutable();
    }

    public static PermissionUpdate Parse(JsonElement suggestion)
    {
        if (suggestion.ValueKind != JsonValueKind.Object || suggestion.StringOrNull("type") is not { } type)
        {
            return PermissionUpdate.Unknown(string.Empty, suggestion);
        }

        // A closed set is open to a value this library has not named and closed against no value at
        // all, so the factories refuse an empty one. Here that refusal would throw out of the read
        // loop and end the session: a suggestion whose destination, mode or behavior is missing or
        // empty names no change this library can make — the declaration requires a destination of
        // every kind — and is kept whole as the unknown, written back as it came.
        if (suggestion.StringOrNull("destination") is not { Length: > 0 } rawDestination)
        {
            return PermissionUpdate.Unknown(type, suggestion);
        }

        var destination = new PermissionUpdateDestination(rawDestination);

        PermissionUpdate? parsed = type switch
        {
            "addRules" or "replaceRules" or "removeRules" => Rules(type, suggestion, destination),
            "setMode" => suggestion.StringOrNull("mode") is { Length: > 0 } rawMode
                ? PermissionUpdate.SetMode(new PermissionMode(rawMode), destination)
                : null,
            "addDirectories" => ParseDirectories(suggestion) is { } added ? PermissionUpdate.AddDirectories(added, destination) : null,
            "removeDirectories" => ParseDirectories(suggestion) is { } removed ? PermissionUpdate.RemoveDirectories(removed, destination) : null,
            _ => null,
        };

        return parsed ?? PermissionUpdate.Unknown(type, suggestion);
    }

    private static PermissionUpdate? Rules(string type, JsonElement suggestion, PermissionUpdateDestination destination)
    {
        if (ParseRules(suggestion) is not { } rules)
        {
            return null;
        }

        if (suggestion.StringOrNull("behavior") is not { Length: > 0 } rawBehavior)
        {
            return null;
        }

        var behavior = new PermissionRuleAction(rawBehavior);
        return type switch
        {
            "addRules" => new AddRulesPermissionUpdate(rules, behavior, destination),
            "replaceRules" => new ReplaceRulesPermissionUpdate(rules, behavior, destination),
            _ => new RemoveRulesPermissionUpdate(rules, behavior, destination),
        };
    }

    private static ImmutableArray<PermissionRule>? ParseRules(JsonElement suggestion)
    {
        if (!suggestion.TryProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var builder = ImmutableArray.CreateBuilder<PermissionRule>();
        foreach (var rule in rules.EnumerateArray())
        {
            if (rule.ToolNameOrNull("toolName") is not { } toolName)
            {
                return null;
            }

            builder.Add(new PermissionRule(toolName, rule.StringOrNull("ruleContent")));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<string>? ParseDirectories(JsonElement suggestion)
    {
        if (!suggestion.TryProperty("directories", out var directories)
            || directories.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var directory in directories.EnumerateArray())
        {
            if (directory.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            builder.Add(directory.GetString() ?? string.Empty);
        }

        return builder.ToImmutable();
    }
}
