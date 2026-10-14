namespace Claude.Agent.Tests.Domain;

// The warning a session with a permission callback logs at start, by the binary's permission flow:
// dontAsk denies what the callback would be asked, bypassPermissions and an allow rule for a whole
// tool allow what it would be asked, and anything narrower leaves the callback its calls.
[TestClass]
public sealed class PermissionCallbackWarningTests
{
    [TestMethod]
    public void DontAskLeavesTheCallbackNothingToBeAsked()
    {
        var warning = PermissionCallbackWarning.For(PermissionMode.DontAsk, []);

        Assert.IsNotNull(warning);
        StringAssert.Contains(warning, "dontAsk");
        StringAssert.Contains(warning, "never called");
    }

    [TestMethod]
    public void BypassPermissionsLeavesTheCallbackOnlyWhatARuleOrASafetyCheckHoldsBack()
    {
        var warning = PermissionCallbackWarning.For(PermissionMode.BypassPermissions, []);

        Assert.IsNotNull(warning);
        StringAssert.Contains(warning, "bypassPermissions");
        StringAssert.Contains(warning, "ask rule or a safety check");
    }

    [TestMethod]
    public void EachWholeToolTheAllowRulesNameIsListedOnceInItsOrder()
    {
        var warning = PermissionCallbackWarning.For(PermissionMode.Default, ["Read", "Bash(ls:*)", "Write", "Read"]);

        Assert.IsNotNull(warning);
        StringAssert.Contains(warning, "AllowedTools allows Read, Write whole");
    }

    [TestMethod]
    public void RulesThatEachNameAContentLeaveTheCallbackItsCalls() =>
        Assert.IsNull(PermissionCallbackWarning.For(PermissionMode.Default, ["Bash(ls:*)", "Read(/tmp/**)"]));

    [TestMethod]
    public void AModeThatAsksWithNoRuleWarnsOfNothing()
    {
        Assert.IsNull(PermissionCallbackWarning.For(null, []));
        Assert.IsNull(PermissionCallbackWarning.For(PermissionMode.AcceptEdits, []));
    }

    [TestMethod]
    public void EverySkillEnabledIsTheWholeSkillToolAsTheRulesGoOut()
    {
        var options = new ClaudeAgentSessionOptions { AllSkills = true };
        options.AllowedTools.Add("Skill");

        var warning = PermissionCallbackWarning.For(options.PermissionMode, AllowRules.For(options));

        Assert.IsNotNull(warning);
        StringAssert.Contains(warning, "AllowedTools allows Skill whole");
    }

    [TestMethod]
    public void NamedSkillsGoOutAsRulesWithAContentAndWarnOfNothing()
    {
        var options = new ClaudeAgentSessionOptions();
        options.Skills.Add("blueprint");

        Assert.IsNull(PermissionCallbackWarning.For(options.PermissionMode, AllowRules.For(options)));
    }
}
