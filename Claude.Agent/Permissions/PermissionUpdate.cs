using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// A change to the binary's permissions: one type per change, over this base. A change this library
/// has no type for arrives as <see cref="UnknownPermissionUpdate"/> and goes back as it came.
/// </summary>
public abstract class PermissionUpdate
{
    private protected PermissionUpdate(PermissionUpdateDestination destination)
    {
        Destination = destination;
    }

    // The unknown is the one update with no destination: it has no typed members at all, and goes
    // back out as the raw it came in as.
    private protected PermissionUpdate()
    {
    }

    /// <summary>Where the binary keeps the change; the declaration requires one of every kind it names, and every recorded suggestion carries one. Null only on <see cref="UnknownPermissionUpdate"/>.</summary>
    public PermissionUpdateDestination? Destination { get; }

    /// <summary>Adds <paramref name="rules"/> with <paramref name="behavior"/>. A rule is written as the binary writes it: "Read", "Bash(git:*)".</summary>
    /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
    /// <exception cref="ArgumentException">A closed set's value names nothing.</exception>
    public static AddRulesPermissionUpdate AddRules(IEnumerable<string> rules, PermissionRuleAction behavior, PermissionUpdateDestination destination) =>
        new(Parsed(rules), Named(behavior), Kept(destination));

    /// <summary>Replaces the rules of <paramref name="behavior"/> with <paramref name="rules"/>. A rule is written as the binary writes it: "Read", "Bash(git:*)".</summary>
    /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
    /// <exception cref="ArgumentException">A closed set's value names nothing.</exception>
    public static ReplaceRulesPermissionUpdate ReplaceRules(IEnumerable<string> rules, PermissionRuleAction behavior, PermissionUpdateDestination destination) =>
        new(Parsed(rules), Named(behavior), Kept(destination));

    /// <summary>Removes <paramref name="rules"/> from those of <paramref name="behavior"/>. A rule is written as the binary writes it: "Read", "Bash(git:*)".</summary>
    /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
    /// <exception cref="ArgumentException">A closed set's value names nothing.</exception>
    public static RemoveRulesPermissionUpdate RemoveRules(IEnumerable<string> rules, PermissionRuleAction behavior, PermissionUpdateDestination destination) =>
        new(Parsed(rules), Named(behavior), Kept(destination));

    /// <summary>Sets the permission mode.</summary>
    /// <exception cref="ArgumentException">A closed set's value names nothing.</exception>
    public static SetModePermissionUpdate SetMode(PermissionMode mode, PermissionUpdateDestination destination)
    {
        Names(mode.ToString(), nameof(mode), "a permission mode");
        return new SetModePermissionUpdate(mode, Kept(destination));
    }

    /// <summary>Lets the tools reach <paramref name="directories"/> as well.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="directories"/> is null.</exception>
    /// <exception cref="ArgumentException">A closed set's value names nothing.</exception>
    public static AddDirectoriesPermissionUpdate AddDirectories(IEnumerable<string> directories, PermissionUpdateDestination destination)
    {
        ArgumentNullException.ThrowIfNull(directories);
        return new AddDirectoriesPermissionUpdate([.. directories], Kept(destination));
    }

    /// <summary>Takes <paramref name="directories"/> back out of reach.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="directories"/> is null.</exception>
    /// <exception cref="ArgumentException">A closed set's value names nothing.</exception>
    public static RemoveDirectoriesPermissionUpdate RemoveDirectories(IEnumerable<string> directories, PermissionUpdateDestination destination)
    {
        ArgumentNullException.ThrowIfNull(directories);
        return new RemoveDirectoriesPermissionUpdate([.. directories], Kept(destination));
    }

    internal static UnknownPermissionUpdate Unknown(string type, JsonElement raw) => new(type, raw);

    private static IReadOnlyList<PermissionRule> Parsed(IEnumerable<string> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return [.. rules.Select(PermissionRule.Parse)];
    }

    private static PermissionRuleAction Named(PermissionRuleAction behavior)
    {
        Names(behavior.ToString(), nameof(behavior), "what the rules do");
        return behavior;
    }

    // A closed set is open to a value this library has not named and closed against no value at
    // all: the default names nothing, so there would be nothing to send.
    private static PermissionUpdateDestination Kept(PermissionUpdateDestination destination)
    {
        Names(destination.ToString(), nameof(destination), "where the binary keeps the change");
        return destination;
    }

    private static void Names(string value, string member, string what)
    {
        if (value.Length == 0)
        {
            throw new ArgumentException($"An update names {what}; the value is empty.", member);
        }
    }
}
