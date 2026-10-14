using System.Text.Json;
using Claude.Agent.Tests.Wire;

namespace Claude.Agent.Tests.Permissions;

// A permission update is one type per kind of change over a common base. It travels both ways: the
// binary suggests one on a permission request or a hook, and a caller hands one back with an allow.
// The lines here are this file's own; no recording carries a replaceRules, a removeRules or either
// directory update.
[TestClass]
public sealed class PermissionUpdateTests
{
    [TestMethod]
    public void EachKindOfUpdateTheBinarySuggestsReadsAsItsOwnTypeWithItsOwnMembers()
    {
        var added = Parse<AddRulesPermissionUpdate>(
            """{"type": "addRules", "rules": [{"toolName": "Bash", "ruleContent": "git status:*"}], "behavior": "allow", "destination": "session"}""");
        Assert.AreEqual(PermissionRuleAction.Allow, added.Behavior);
        Assert.AreEqual(PermissionUpdateDestination.Session, added.Destination);
        Assert.AreEqual("Bash", added.Rules.Single().ToolName);
        Assert.AreEqual("git status:*", added.Rules.Single().RuleContent);

        var replaced = Parse<ReplaceRulesPermissionUpdate>(
            """{"type": "replaceRules", "rules": [{"toolName": "Read"}], "behavior": "deny", "destination": "projectSettings"}""");
        Assert.AreEqual(PermissionRuleAction.Deny, replaced.Behavior);
        Assert.AreEqual(PermissionUpdateDestination.ProjectSettings, replaced.Destination);
        Assert.IsNull(replaced.Rules.Single().RuleContent);

        var removed = Parse<RemoveRulesPermissionUpdate>(
            """{"type": "removeRules", "rules": [{"toolName": "Write", "ruleContent": "/etc/**"}], "behavior": "ask", "destination": "userSettings"}""");
        Assert.AreEqual(PermissionRuleAction.Ask, removed.Behavior);
        Assert.AreEqual(PermissionUpdateDestination.UserSettings, removed.Destination);

        var mode = Parse<SetModePermissionUpdate>("""{"type": "setMode", "mode": "plan", "destination": "cliArg"}""");
        Assert.AreEqual(PermissionMode.Plan, mode.Mode);
        Assert.AreEqual(PermissionUpdateDestination.CommandLineArgument, mode.Destination);

        var reach = Parse<AddDirectoriesPermissionUpdate>(
            """{"type": "addDirectories", "directories": ["/srv/build", "/srv/vendor"], "destination": "localSettings"}""");
        CollectionAssert.AreEqual(new[] { "/srv/build", "/srv/vendor" }, reach.Directories.ToArray());
        Assert.AreEqual(PermissionUpdateDestination.LocalSettings, reach.Destination);

        var unreach = Parse<RemoveDirectoriesPermissionUpdate>(
            """{"type": "removeDirectories", "directories": ["/srv/vendor"], "destination": "userSettings"}""");
        Assert.AreEqual("/srv/vendor", unreach.Directories.Single());
        Assert.AreEqual(PermissionUpdateDestination.UserSettings, unreach.Destination);
    }

    // Degrade, never throw, never skip: a kind with no type, and an update missing a member its kind
    // needs, is kept whole as the unknown with the type as the binary wrote it.
    [TestMethod]
    public void AKindWithNoTypeAndAnUpdateMissingItsMembersAreKeptWholeAsTheUnknown()
    {
        string[] lines =
        [
            """{"type": "brewTea", "kettle": "on"}""",
            """{"type": "addRules", "behavior": "allow"}""",
            """{"type": "addRules", "rules": [{"toolName": "Read"}]}""",
            """{"type": "addRules", "rules": [{"ruleContent": "git:*"}], "behavior": "allow"}""",
            """{"type": "setMode"}""",
            """{"type": "addDirectories"}""",
            """{"type": "removeDirectories", "directories": ["/srv/build", 7]}""",
            """{"kettle": "on"}""",
        ];

        foreach (var line in lines)
        {
            var update = Parse<UnknownPermissionUpdate>(line);
            Assert.AreEqual(line, update.Raw.GetRawText(), line);
        }
    }

    [TestMethod]
    public void AnUpdateThatIsNotAnObjectAndAListThatIsNotAnArrayAreBothRead()
    {
        Assert.AreEqual(string.Empty, Parse<UnknownPermissionUpdate>("""["addRules"]""").Type);
        Assert.AreEqual(0, PermissionUpdateParser.ParseAll(Recorded.Json("""{"type": "addRules"}""")).Length);
    }

    // A caller writes a rule the way the binary writes it; the library splits it into the tool and
    // what of that tool the rule reaches.
    [TestMethod]
    public void ARuleACallerWritesIsSplitIntoItsToolAndWhatOfThatToolItReaches()
    {
        var update = PermissionUpdate.AddRules(["Read", "Bash(git status:*)"], PermissionRuleAction.Allow, PermissionUpdateDestination.Session);

        Assert.AreEqual("Read", update.Rules[0].ToolName);
        Assert.IsNull(update.Rules[0].RuleContent);
        Assert.AreEqual("Bash", update.Rules[1].ToolName);
        Assert.AreEqual("git status:*", update.Rules[1].RuleContent);
    }

    // A closed set is open to a value this library has not named and closed against no value at all:
    // a caller's factory refuses the empty one, since there would be nothing to send.
    [TestMethod]
    public void EveryFactoryRefusesAClosedSetValueThatNamesNothing()
    {
        Assert.AreEqual(
            "behavior",
            Assert.ThrowsExactly<ArgumentException>(() => PermissionUpdate.AddRules(["Read"], default, PermissionUpdateDestination.Session)).ParamName);
        Assert.AreEqual(
            "behavior",
            Assert.ThrowsExactly<ArgumentException>(() => PermissionUpdate.ReplaceRules(["Read"], default, PermissionUpdateDestination.Session)).ParamName);
        Assert.AreEqual(
            "behavior",
            Assert.ThrowsExactly<ArgumentException>(() => PermissionUpdate.RemoveRules(["Read"], default, PermissionUpdateDestination.Session)).ParamName);
        Assert.AreEqual(
            "mode",
            Assert.ThrowsExactly<ArgumentException>(() => PermissionUpdate.SetMode(default, PermissionUpdateDestination.Session)).ParamName);
        Assert.AreEqual(
            "destination",
            Assert.ThrowsExactly<ArgumentException>(
                () => PermissionUpdate.AddRules(["Read"], PermissionRuleAction.Allow, default(PermissionUpdateDestination))).ParamName);
        Assert.AreEqual(
            "destination",
            Assert.ThrowsExactly<ArgumentException>(
                () => PermissionUpdate.SetMode(PermissionMode.Plan, default(PermissionUpdateDestination))).ParamName);
        Assert.AreEqual(
            "destination",
            Assert.ThrowsExactly<ArgumentException>(
                () => PermissionUpdate.AddDirectories(["/srv/build"], default(PermissionUpdateDestination))).ParamName);
        Assert.AreEqual(
            "destination",
            Assert.ThrowsExactly<ArgumentException>(
                () => PermissionUpdate.RemoveDirectories(["/srv/build"], default(PermissionUpdateDestination))).ParamName);
    }

    [TestMethod]
    public void EveryFactoryRefusesANullList()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => PermissionUpdate.AddRules(null!, PermissionRuleAction.Allow, PermissionUpdateDestination.Session));
        Assert.ThrowsExactly<ArgumentNullException>(() => PermissionUpdate.ReplaceRules(null!, PermissionRuleAction.Allow, PermissionUpdateDestination.Session));
        Assert.ThrowsExactly<ArgumentNullException>(() => PermissionUpdate.RemoveRules(null!, PermissionRuleAction.Allow, PermissionUpdateDestination.Session));
        Assert.ThrowsExactly<ArgumentNullException>(() => PermissionUpdate.AddDirectories(null!, PermissionUpdateDestination.Session));
        Assert.ThrowsExactly<ArgumentNullException>(() => PermissionUpdate.RemoveDirectories(null!, PermissionUpdateDestination.Session));
    }

    // Reading is not calling: the same empty value that the factory refuses arrives from the binary
    // now and then, and it is kept whole rather than thrown out of the read loop.
    [TestMethod]
    public void AnEmptyDestinationModeOrBehaviorFromTheBinaryIsKeptWholeRatherThanRefused()
    {
        string[] lines =
        [
            """{"type": "setMode", "mode": ""}""",
            """{"type": "setMode", "mode": "plan", "destination": ""}""",
            """{"type": "addRules", "rules": [{"toolName": "Read"}], "behavior": ""}""",
            """{"type": "addDirectories", "directories": ["/srv/build"], "destination": ""}""",
        ];

        foreach (var line in lines)
        {
            Assert.AreEqual(line, Parse<UnknownPermissionUpdate>(line).Raw.GetRawText(), line);
        }
    }

    [TestMethod]
    public void EachKindOfUpdateGoesBackOutInTheBinarysOwnWireWords()
    {
        Assert.AreEqual(
            """{"type": "addRules", "destination": "session", "rules": [{"toolName": "Bash", "ruleContent": "git status:*"}], "behavior": "allow"}""",
            Written(PermissionUpdate.AddRules(["Bash(git status:*)"], PermissionRuleAction.Allow, PermissionUpdateDestination.Session)));

        Assert.AreEqual(
            """{"type": "replaceRules", "destination": "session", "rules": [{"toolName": "Read"}], "behavior": "deny"}""",
            Written(PermissionUpdate.ReplaceRules(["Read"], PermissionRuleAction.Deny, PermissionUpdateDestination.Session)));

        Assert.AreEqual(
            """{"type": "removeRules", "destination": "localSettings", "rules": [{"toolName": "Write", "ruleContent": "/etc/**"}], "behavior": "ask"}""",
            Written(PermissionUpdate.RemoveRules(["Write(/etc/**)"], PermissionRuleAction.Ask, PermissionUpdateDestination.LocalSettings)));

        Assert.AreEqual(
            """{"type": "setMode", "destination": "cliArg", "mode": "plan"}""",
            Written(PermissionUpdate.SetMode(PermissionMode.Plan, PermissionUpdateDestination.CommandLineArgument)));

        Assert.AreEqual(
            """{"type": "addDirectories", "destination": "userSettings", "directories": ["/srv/build", "/srv/vendor"]}""",
            Written(PermissionUpdate.AddDirectories(["/srv/build", "/srv/vendor"], PermissionUpdateDestination.UserSettings)));

        Assert.AreEqual(
            """{"type": "removeDirectories", "destination": "session", "directories": ["/srv/vendor"]}""",
            Written(PermissionUpdate.RemoveDirectories(["/srv/vendor"], PermissionUpdateDestination.Session)));
    }

    // A rule that reaches the whole tool has no content to send, and none is written: the binary
    // reads an absent ruleContent, not a null one.
    [TestMethod]
    public void ARuleThatReachesTheWholeToolGoesOutWithNoRuleContentBesideIt()
    {
        var written = Written(PermissionUpdate.AddRules(["Read"], PermissionRuleAction.Allow, PermissionUpdateDestination.Session));

        Assert.AreEqual("""{"type": "addRules", "destination": "session", "rules": [{"toolName": "Read"}], "behavior": "allow"}""", written);
        Assert.IsFalse(written.Contains("ruleContent", StringComparison.Ordinal));
    }

    // What the library could not read, it does not rewrite: the unknown goes back out byte for byte.
    [TestMethod]
    public void AnUpdateTheLibraryHasNoTypeForGoesBackOutAsItCame()
    {
        const string Line = """{"type": "brewTea", "destination": "teapotSettings", "kettle": "on"}""";

        Assert.AreEqual(Line, Written(PermissionUpdateParser.Parse(Recorded.Json(Line))));
    }

    // A set value the library has not named still goes out as the binary would read it.
    [TestMethod]
    public void AValueThisLibraryHasNotNamedGoesBackOutAsItWasRead()
    {
        const string Line = """{"type": "addRules", "destination": "teapotSettings", "rules": [{"toolName": "Kettle"}], "behavior": "brew"}""";

        var update = Parse<AddRulesPermissionUpdate>(Line);
        Assert.IsFalse(update.Behavior.IsDefined);
        Assert.IsFalse(update.Destination!.Value.IsDefined);
        Assert.AreEqual(Line, Written(update));
    }

    // Every kind of update this library has a type for is one the writer knows; only a type from
    // outside it reaches the arm that throws.
    [TestMethod]
    public void AnUpdateTypeTheWriterDoesNotKnowThrowsNamingItsRuntimeType()
    {
        var thrown = Assert.ThrowsExactly<InvalidOperationException>(() => Written(new RoguePermissionUpdate()));

        StringAssert.Contains(thrown.Message, typeof(RoguePermissionUpdate).ToString());
        StringAssert.Contains(thrown.Message, "is not a permission update this writer knows");
    }

    [TestMethod]
    public void TheWriterRefusesANullUpdate()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ReplyWriter.WritePermissionUpdate(new WireJson(), null!));
    }

    [TestMethod]
    public void EachUpdateKindHasItsBuilderOnTheModelFactory()
    {
        var rule = ClaudeAgentModelFactory.PermissionRule(toolName: "Bash", ruleContent: "git status:*");
        Assert.AreEqual("Bash", rule.ToolName);
        Assert.AreEqual("git status:*", rule.RuleContent);

        var added = ClaudeAgentModelFactory.AddRulesPermissionUpdate(
            rules: [rule],
            behavior: PermissionRuleAction.Allow,
            destination: PermissionUpdateDestination.Session);
        Assert.AreSame(rule, added.Rules.Single());
        Assert.AreEqual(PermissionRuleAction.Allow, added.Behavior);
        Assert.AreEqual(PermissionUpdateDestination.Session, added.Destination);

        var replaced = ClaudeAgentModelFactory.ReplaceRulesPermissionUpdate(rules: [rule], behavior: PermissionRuleAction.Deny, destination: PermissionUpdateDestination.UserSettings);
        Assert.AreEqual(PermissionRuleAction.Deny, replaced.Behavior);
        Assert.AreEqual(PermissionUpdateDestination.UserSettings, replaced.Destination);

        var removed = ClaudeAgentModelFactory.RemoveRulesPermissionUpdate(rules: [rule], behavior: PermissionRuleAction.Ask);
        Assert.AreEqual(PermissionRuleAction.Ask, removed.Behavior);

        var mode = ClaudeAgentModelFactory.SetModePermissionUpdate(mode: PermissionMode.BypassPermissions);
        Assert.AreEqual(PermissionMode.BypassPermissions, mode.Mode);

        var reach = ClaudeAgentModelFactory.AddDirectoriesPermissionUpdate(directories: ["/srv/build"]);
        Assert.AreEqual("/srv/build", reach.Directories.Single());

        var unreach = ClaudeAgentModelFactory.RemoveDirectoriesPermissionUpdate(
            directories: ["/srv/vendor"],
            destination: PermissionUpdateDestination.ProjectSettings);
        Assert.AreEqual("/srv/vendor", unreach.Directories.Single());
        Assert.AreEqual(PermissionUpdateDestination.ProjectSettings, unreach.Destination);

        var unknown = ClaudeAgentModelFactory.UnknownPermissionUpdate(
            type: "brewTea",
            raw: Recorded.Json("""{"type": "brewTea"}"""));
        Assert.AreEqual("brewTea", unknown.Type);
        Assert.AreEqual("brewTea", unknown.Raw.GetProperty("type").GetString());
    }

    // The builders take the model, not the rule text: a caller's own test builds a rule as the
    // library read it, and the public factories are where the binary's syntax is parsed.
    [TestMethod]
    public void TheModelFactoryBuildersTakeNoClosedSetValueTheyWouldHaveToRefuse()
    {
        var added = ClaudeAgentModelFactory.AddRulesPermissionUpdate();

        Assert.AreEqual(0, added.Rules.Count);
        Assert.AreEqual(string.Empty, added.Behavior.ToString());
        Assert.AreEqual(string.Empty, added.Destination.ToString());
    }

    // The declaration requires a destination of every kind it names, and every recorded suggestion
    // carries one. A suggestion without it names no change this library can make: it is the unknown,
    // kept whole and written back as it came.
    [TestMethod]
    public void ASuggestionThatNamesNoDestinationIsTheUnknownAndGoesBackOutAsItCame()
    {
        const string Line = """{"type": "setMode", "mode": "plan"}""";

        var update = PermissionUpdateParser.Parse(Recorded.Json(Line));

        Assert.IsInstanceOfType<UnknownPermissionUpdate>(update, out var unknown);
        Assert.AreEqual("setMode", unknown.Type);
        Assert.IsNull(unknown.Destination);
        Assert.AreEqual(Line, Written(unknown));
    }

    private static T Parse<T>(string line)
        where T : PermissionUpdate
    {
        var update = PermissionUpdateParser.Parse(Recorded.Json(line));
        Assert.IsInstanceOfType<T>(update, $"{line} read as {update.GetType().Name}");
        return (T)update;
    }

    private static string Written(PermissionUpdate update)
    {
        var writer = new WireJson();
        ReplyWriter.WritePermissionUpdate(writer, update);
        return writer.ToString();
    }

    private sealed class RoguePermissionUpdate() : PermissionUpdate(PermissionUpdateDestination.Session);
}
