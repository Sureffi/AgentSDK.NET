namespace Claude.Agent.Tests.Sessions;

// The ids the initialize request registers are what the binary sends back in hook_callback, so
// the table is the one place a callback id turns into a caller's delegate.
[TestClass]
public sealed class HookTableTests
{
    [TestMethod]
    public void EveryRegistrationGetsItsOwnIdInRegistrationOrder()
    {
        var options = new ClaudeAgentSessionOptions();
        options.AddHook(HookEvent.PreToolUse, "Read", Empty);
        options.AddHook(HookEvent.PreToolUse, "Read", Empty);
        options.AddHook(HookEvent.Stop, null, Empty);

        var table = new HookTable(options.Hooks);

        Assert.IsTrue(table.Any);
        Assert.AreSame(options.Hooks[0], table.Find("hook_0"));
        Assert.AreSame(options.Hooks[1], table.Find("hook_1"));
        Assert.AreSame(options.Hooks[2], table.Find("hook_2"));
    }

    [TestMethod]
    public void RegistrationsOnOneEventAndMatcherShareOneMatcherEntry()
    {
        var options = new ClaudeAgentSessionOptions();
        options.AddHook(HookEvent.PreToolUse, "Read", Empty);
        options.AddHook(HookEvent.Stop, null, Empty);
        options.AddHook(HookEvent.PreToolUse, "Read", Empty);
        options.AddHook(HookEvent.PreToolUse, "Write", Empty);

        var config = new HookTable(options.Hooks).ToInitializeConfig();

        Assert.IsNotNull(config);
        CollectionAssert.AreEqual(new[] { HookEvent.PreToolUse, HookEvent.Stop }, config.Keys.ToArray());

        var pre = config[HookEvent.PreToolUse];
        Assert.AreEqual(2, pre.Length);
        Assert.AreEqual("Read", pre[0].Matcher);
        CollectionAssert.AreEqual(new[] { "hook_0", "hook_2" }, pre[0].HookCallbackIds.ToArray());
        Assert.AreEqual("Write", pre[1].Matcher);
        CollectionAssert.AreEqual(new[] { "hook_3" }, pre[1].HookCallbackIds.ToArray());

        var stop = config[HookEvent.Stop].Single();
        Assert.IsNull(stop.Matcher);
        CollectionAssert.AreEqual(new[] { "hook_1" }, stop.HookCallbackIds.ToArray());
    }

    [TestMethod]
    public void TheGroupsTimeoutIsTheLongestOfItsRegistrationsInSeconds()
    {
        var options = new ClaudeAgentSessionOptions();
        options.AddHook(HookEvent.PreToolUse, "Read", Empty, TimeSpan.FromSeconds(3));
        options.AddHook(HookEvent.PreToolUse, "Read", Empty, TimeSpan.FromSeconds(12.5));
        options.AddHook(HookEvent.Stop, null, Empty);

        var config = new HookTable(options.Hooks).ToInitializeConfig();

        Assert.IsNotNull(config);
        Assert.AreEqual(12.5, config[HookEvent.PreToolUse].Single().Timeout);
        Assert.IsNull(config[HookEvent.Stop].Single().Timeout);
    }

    [TestMethod]
    public void AnIdNobodyRegisteredFindsNothing()
    {
        var options = new ClaudeAgentSessionOptions();
        options.AddHook(HookEvent.Stop, null, Empty);

        Assert.IsNull(new HookTable(options.Hooks).Find("hook_9"));
    }

    [TestMethod]
    public void NoRegistrationsIsNoConfigAtAll()
    {
        var table = new HookTable(new ClaudeAgentSessionOptions().Hooks);

        Assert.IsFalse(table.Any);
        Assert.IsNull(table.ToInitializeConfig());
    }

    private static Task<HookOutput> Empty(HookInput input, CancellationToken ct) => Task.FromResult(new HookOutput());
}
