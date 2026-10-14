namespace Claude.Agent.Tests.Domain;

[TestClass]
public sealed class ToolRuleTests
{
    [TestMethod]
    public void ARuleWithNoParenthesesNamesAWholeTool()
    {
        var rule = ToolRule.Parse("Read");

        Assert.AreEqual("Read", rule.Tool);
        Assert.IsNull(rule.Specifier);
        Assert.IsTrue(rule.IsWholeTool);
    }

    [TestMethod]
    public void ARuleWithEmptyParenthesesNamesAWholeTool() => Assert.IsTrue(ToolRule.Parse("Read()").IsWholeTool);

    [TestMethod]
    public void ARuleWithAStarSpecifierNamesAWholeTool() => Assert.IsTrue(ToolRule.Parse("Read(*)").IsWholeTool);

    [TestMethod]
    public void APrefixRuleKeepsItsSpecifier()
    {
        var rule = ToolRule.Parse("Bash(ls:*)");

        Assert.AreEqual("Bash", rule.Tool);
        Assert.AreEqual("ls:*", rule.Specifier);
        Assert.IsFalse(rule.IsWholeTool);
    }

    [TestMethod]
    public void AnExactRuleKeepsItsSpecifier()
    {
        var rule = ToolRule.Parse("Bash(git status)");

        Assert.AreEqual("git status", rule.Specifier);
        Assert.IsFalse(rule.IsWholeTool);
    }

    [TestMethod]
    public void ARuleThatOpensWithAParenthesisNamesNoTool()
    {
        var rule = ToolRule.Parse("(x");

        Assert.IsNull(rule.Tool);
        Assert.IsFalse(rule.IsWholeTool);
    }

    [TestMethod]
    public void ARuleThatOpensWithAParenthesisWritesItsRawTextBackToTheWire() =>
        Assert.AreEqual("(x", ToolRule.Parse("(x").ToWire());

    [TestMethod]
    public void AWholeToolRuleWritesJustTheToolNameBackToTheWire() =>
        Assert.AreEqual("Read", ToolRule.Parse("Read").ToWire());

    [TestMethod]
    public void ARuleWithASpecifierWritesItBackToTheWire() =>
        Assert.AreEqual("Bash(ls:*)", ToolRule.Parse("Bash(ls:*)").ToWire());

    [TestMethod]
    public void ARuleSetDropsBlankEntries() =>
        Assert.AreEqual(2, ToolRuleSet.Parse(["Read", "  ", "Bash(ls:*)", ""]).Rules.Length);

    [TestMethod]
    public void ARuleSetListsEachWholeToolOnce()
    {
        var set = ToolRuleSet.Parse(["Read", "Read()", "Bash(ls:*)", "Write"]);

        CollectionAssert.AreEqual(
            new[] { "Read", "Write" },
            set.WholeTools.ToArray());
    }

    [TestMethod]
    public void ARuleSetJoinsItsRulesWithCommasOnTheWire() =>
        Assert.AreEqual("Read,Bash(ls:*)", ToolRuleSet.Parse(["Read", "Bash(ls:*)"]).ToWire());
}
