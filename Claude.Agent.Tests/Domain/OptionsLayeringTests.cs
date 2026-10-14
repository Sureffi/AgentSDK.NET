using System.Reflection;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Domain;

using static OptionsReflection;

// What a call is asked: its own options over the client's defaults, member by member. The members
// are reflected, so one added to the options and forgotten in the layering fails here.
[TestClass]
public sealed class OptionsLayeringTests
{
    private const string Uuid = "c3644dc2-4787-4013-8c4d-0d91a1b718ff";

    private static readonly string[] Identity =
    [
        nameof(ClaudeAgentSessionOptions.SessionId),
        nameof(ClaudeAgentSessionOptions.ResumeSessionId),
        nameof(ClaudeAgentSessionOptions.ContinueLatest),
        nameof(ClaudeAgentSessionOptions.Fork),
        nameof(ClaudeAgentSessionOptions.ResumeAtMessageId),
        nameof(ClaudeAgentSessionOptions.DroppedTurnPromptId),
    ];

    // The two pairs that are one choice: a call that sets either leaves the default's other behind.
    private static readonly Dictionary<string, string> OneChoice = new(StringComparer.Ordinal)
    {
        [nameof(ClaudeAgentSessionOptions.PermissionCallback)] = nameof(ClaudeAgentSessionOptions.PermissionPromptTool),
        [nameof(ClaudeAgentSessionOptions.PermissionPromptTool)] = nameof(ClaudeAgentSessionOptions.PermissionCallback),
        [nameof(ClaudeAgentSessionOptions.Skills)] = nameof(ClaudeAgentSessionOptions.AllSkills),
        [nameof(ClaudeAgentSessionOptions.AllSkills)] = nameof(ClaudeAgentSessionOptions.Skills),
    };

    // What accumulates: the call adds to the defaults. Everything else is a scalar or a selection.
    private static readonly string[] Unioned =
    [
        nameof(ClaudeAgentSessionOptions.AllowedTools),
        nameof(ClaudeAgentSessionOptions.DisallowedTools),
        nameof(ClaudeAgentSessionOptions.AdditionalDirectories),
        nameof(ClaudeAgentSessionOptions.PluginDirectories),
        nameof(ClaudeAgentSessionOptions.Betas),
    ];

    private static readonly string[] Keyed =
    [
        nameof(ClaudeAgentSessionOptions.McpServers),
        nameof(ClaudeAgentSessionOptions.Agents),
    ];

    private static PropertyInfo[] Asked { get; } =
        [.. Properties(typeof(ClaudeAgentSessionOptions)).Where(property => !Identity.Contains(property.Name))];

    private static PropertyInfo[] Selected { get; } =
        [.. Asked.Where(property => !Unioned.Contains(property.Name) && !Keyed.Contains(property.Name))];

    [TestMethod]
    public void ACallThatSetsNothingIsAskedEveryDefault()
    {
        Assert.IsTrue(Asked.Length > 30, $"only {Asked.Length} properties were reflected");
        var defaults = Defaults();

        foreach (var layered in new[]
                 {
                     ClaudeAgentSessionOptions.Layer(defaults, null),
                     ClaudeAgentSessionOptions.Layer(defaults, new ClaudeAgentSessionOptions()),
                 })
        {
            foreach (var property in Asked)
            {
                AssertCarried(property.GetValue(defaults), property.GetValue(layered), property.Name);
            }

            CollectionAssert.AreEqual(defaults.Hooks.ToArray(), layered.Hooks.ToArray());
        }
    }

    // A scalar or a selection, one at a time: the call's value is what is asked, whole — a false over
    // a true, an empty tool list over a full one — and every other member is still the default's.
    [TestMethod]
    public void EveryScalarOrSelectionTheCallSetsWinsAndLeavesTheOthersToTheDefaults()
    {
        var defaults = Defaults();

        foreach (var set in Selected)
        {
            var call = new ClaudeAgentSessionOptions();
            Distinguish(call, set, other: true);

            var layered = ClaudeAgentSessionOptions.Layer(defaults, call);

            AssertCarried(set.GetValue(call), set.GetValue(layered), $"{set.Name}, set on the call");
            foreach (var left in Asked.Where(property => property != set))
            {
                var from = OneChoice.TryGetValue(set.Name, out var partner) && partner == left.Name ? call : defaults;
                AssertCarried(left.GetValue(from), left.GetValue(layered), $"{left.Name}, with {set.Name} set on the call");
            }
        }
    }

    // A member with no setter is a collection the caller fills. Seven of them accumulate and Skills
    // is a selection, with AllSkills; one added to the options has to be put in its place here.
    [TestMethod]
    public void EveryMemberACallerFillsIsOneOfTheSevenThatAccumulateOrSkills()
    {
        string[] filled = [.. Asked.Where(property => !property.CanWrite).Select(property => property.Name)];
        string[] placed = [.. Unioned, .. Keyed, nameof(ClaudeAgentSessionOptions.Skills)];

        CollectionAssert.AreEquivalent(placed, filled);
    }

    // What accumulates: the defaults' entries, then the call's that are not among them already.
    [TestMethod]
    public void TheFiveListsThatAccumulateAreTheDefaultsThenWhatTheCallAdds()
    {
        var defaults = Defaults();

        foreach (var name in Unioned)
        {
            var list = typeof(ClaudeAgentSessionOptions).GetProperty(name)!;
            var call = new ClaudeAgentSessionOptions();
            Distinguish(call, list, other: true);
            ((IList<string>)list.GetValue(call)!).Add($"{name}-entry");

            var layered = (IList<string>)list.GetValue(ClaudeAgentSessionOptions.Layer(defaults, call))!;

            CollectionAssert.AreEqual(new[] { $"{name}-entry", $"{name}-other" }, layered.ToArray(), name);
            Assert.IsFalse(list.CanWrite, $"{name} is settable");
        }
    }

    // Keyed: the call's entries are added, and one under a default's name replaces that entry. No
    // call drops a default's.
    [TestMethod]
    public void McpServersAndAgentsMergeByKeyAndACallsEntryReplacesTheDefaultsOfItsName()
    {
        var defaults = new ClaudeAgentSessionOptions();
        defaults.McpServers["calc"] = McpServerConfiguration.Stdio("calc-server");
        defaults.McpServers["hive"] = McpServerConfiguration.Stdio("hive-server");
        defaults.Agents["bee"] = new AgentDefinition("a bee", "buzz");
        defaults.Agents["wasp"] = new AgentDefinition("a wasp", "sting");

        var call = new ClaudeAgentSessionOptions();
        call.McpServers["hive"] = McpServerConfiguration.Stdio("another-hive");
        call.McpServers["mint"] = McpServerConfiguration.Stdio("mint-server");
        call.Agents["wasp"] = new AgentDefinition("a hornet", "sting harder");
        call.Agents["ant"] = new AgentDefinition("an ant", "carry");

        var layered = ClaudeAgentSessionOptions.Layer(defaults, call);

        CollectionAssert.AreEqual(new[] { "calc", "hive", "mint" }, layered.McpServers.Keys.ToArray());
        Assert.AreEqual("calc-server", ((McpStdioServerConfiguration)layered.McpServers["calc"]).Command);
        Assert.AreEqual("another-hive", ((McpStdioServerConfiguration)layered.McpServers["hive"]).Command);
        CollectionAssert.AreEqual(new[] { "bee", "wasp", "ant" }, layered.Agents.Keys.ToArray());
        Assert.AreEqual("a bee", layered.Agents["bee"].Description);
        Assert.AreEqual("a hornet", layered.Agents["wasp"].Description);
        layered.Validate();
    }

    [TestMethod]
    public void WhichSessionItIsComesFromTheCallAlone()
    {
        var defaults = Defaults();
        var call = new ClaudeAgentSessionOptions
        {
            SessionId = Uuid,
            ResumeSessionId = "older",
            Fork = true,
            ResumeAtMessageId = "3f2a",
            DroppedTurnPromptId = "9c1d",
        };

        var layered = ClaudeAgentSessionOptions.Layer(defaults, call);

        foreach (var name in Identity)
        {
            var property = typeof(ClaudeAgentSessionOptions).GetProperty(name)!;
            Assert.AreEqual(property.GetValue(call), property.GetValue(layered), name);
        }

        Assert.AreEqual(defaults.Model, layered.Model);
    }

    [TestMethod]
    public void ADefaultThatNamesASessionIsRefusedWhenTheClientIsMade()
    {
        foreach (var name in Identity)
        {
            var options = new ClaudeAgentClientOptions();
            Distinguish(options.Defaults, typeof(ClaudeAgentSessionOptions).GetProperty(name)!);

            var refused = Assert.ThrowsExactly<ArgumentException>(() => new ClaudeAgentClient(options), name);
            Assert.AreEqual(nameof(ClaudeAgentClientOptions.Defaults), refused.ParamName, name);
        }
    }

    [TestMethod]
    public void ADefaultTheBinaryWouldRefuseIsRefusedWhenTheClientIsMade()
    {
        var options = new ClaudeAgentClientOptions();
        options.Defaults.MaxBudgetUsd = 0m;

        var refused = Assert.ThrowsExactly<ArgumentException>(() => new ClaudeAgentClient(options));
        Assert.AreEqual(nameof(ClaudeAgentSessionOptions.MaxBudgetUsd), refused.ParamName);
    }

    [TestMethod]
    public void TheDefaultsHooksRunAndTheCallsRunTooInThatOrder()
    {
        var defaults = new ClaudeAgentSessionOptions();
        defaults.AddHook(HookEvent.Notification, null, (_, _) => Task.FromResult(new HookOutput()));
        var call = new ClaudeAgentSessionOptions();
        call.AddHook(HookEvent.PreToolUse, "Bash", (_, _) => Task.FromResult(new HookOutput()));
        call.AddHook(HookEvent.Stop, null, (_, _) => Task.FromResult(new HookOutput()));

        CollectionAssert.AreEqual(
            defaults.Hooks.Concat(call.Hooks).ToArray(),
            ClaudeAgentSessionOptions.Layer(defaults, call).Hooks.ToArray());
        CollectionAssert.AreEqual(defaults.Hooks.ToArray(), ClaudeAgentSessionOptions.Layer(defaults, new ClaudeAgentSessionOptions()).Hooks.ToArray());
    }

    // The two pairs are one selection each. Beside the call choosing the other side, below: the same
    // side replaces the default's, and a call that sets neither keeps the default's pair.
    [TestMethod]
    public void ACallThatSetsTheSameSideOfAPairReplacesItAndACallThatSetsNeitherKeepsIt()
    {
        var defaults = new ClaudeAgentSessionOptions { PermissionCallback = (_, _) => Task.FromResult(ToolPermissionResult.Deny("default")) };
        defaults.Skills.Add("blueprint");
        var call = new ClaudeAgentSessionOptions { PermissionCallback = (_, _) => Task.FromResult(ToolPermissionResult.Allow()) };
        call.Skills.Add("survey");

        var replaced = ClaudeAgentSessionOptions.Layer(defaults, call);
        Assert.AreSame(call.PermissionCallback, replaced.PermissionCallback);
        CollectionAssert.AreEqual(new[] { "survey" }, replaced.Skills.ToArray());

        var kept = ClaudeAgentSessionOptions.Layer(defaults, new ClaudeAgentSessionOptions { Model = "haiku" });
        Assert.AreSame(defaults.PermissionCallback, kept.PermissionCallback);
        CollectionAssert.AreEqual(new[] { "blueprint" }, kept.Skills.ToArray());

        var prompted = new ClaudeAgentSessionOptions { PermissionPromptTool = "mcp__calc__ask", AllSkills = true };
        var other = ClaudeAgentSessionOptions.Layer(prompted, new ClaudeAgentSessionOptions { PermissionPromptTool = "mcp__hive__ask", AllSkills = false });
        Assert.AreEqual("mcp__hive__ask", other.PermissionPromptTool);
        Assert.AreEqual(false, other.AllSkills);
        var still = ClaudeAgentSessionOptions.Layer(prompted, null);
        Assert.AreEqual("mcp__calc__ask", still.PermissionPromptTool);
        Assert.AreEqual(true, still.AllSkills);
    }

    // The defaults chose one way and the call the other. The whole is the call's choice, and
    // Validate, which refuses both at once, has nothing to refuse.
    [TestMethod]
    public void ACallThatChoosesTheOtherOfAPairIsNotRefusedForWhatTheDefaultsChose()
    {
        var prompted = new ClaudeAgentSessionOptions { PermissionPromptTool = "mcp__calc__ask", AllSkills = true };
        var call = new ClaudeAgentSessionOptions { PermissionCallback = (_, _) => Task.FromResult(ToolPermissionResult.Allow()) };
        call.Skills.Add("blueprint");

        var layered = ClaudeAgentSessionOptions.Layer(prompted, call);

        layered.Validate();
        Assert.IsNull(layered.PermissionPromptTool);
        Assert.AreSame(call.PermissionCallback, layered.PermissionCallback);
        Assert.IsNull(layered.AllSkills);
        CollectionAssert.AreEqual(new[] { "blueprint" }, layered.Skills.ToArray());

        var called = new ClaudeAgentSessionOptions { PermissionCallback = call.PermissionCallback };
        called.Skills.Add("blueprint");
        var other = ClaudeAgentSessionOptions.Layer(called, new ClaudeAgentSessionOptions { PermissionPromptTool = "mcp__calc__ask", AllSkills = true });

        other.Validate();
        Assert.IsNull(other.PermissionCallback);
        Assert.AreEqual("mcp__calc__ask", other.PermissionPromptTool);
        Assert.AreEqual(true, other.AllSkills);
        Assert.AreEqual(0, other.Skills.Count);
    }

    // An empty tool name is no choice, here as on the command line: taken for one, it would drop the
    // defaults' callback and put nothing in its place.
    [TestMethod]
    public void AnEmptyPromptToolOnTheCallLeavesTheDefaultsCallbackStanding()
    {
        var gated = new ClaudeAgentSessionOptions { PermissionCallback = (_, _) => Task.FromResult(ToolPermissionResult.Deny("no")) };

        var layered = ClaudeAgentSessionOptions.Layer(gated, new ClaudeAgentSessionOptions { PermissionPromptTool = string.Empty });

        Assert.AreSame(gated.PermissionCallback, layered.PermissionCallback);
        Assert.IsTrue(string.IsNullOrEmpty(layered.PermissionPromptTool));
        layered.Validate();
    }

    [TestMethod]
    public void WhatIsLayeredIsACopyNeitherSideReaches()
    {
        var defaults = new ClaudeAgentSessionOptions { Sandbox = new SandboxOptions { Enabled = true } };
        defaults.AllowedTools.Add("Read");
        var call = new ClaudeAgentSessionOptions { Tools = ["Read"] };
        call.Agents["bee"] = new AgentDefinition("a bee", "buzz") { Tools = ["Read"] };

        var layered = ClaudeAgentSessionOptions.Layer(defaults, call);
        defaults.Sandbox.Enabled = false;
        defaults.AllowedTools.Add("Bash");
        call.Tools.Add("Bash");
        call.Agents["bee"].Tools!.Add("Bash");
        call.AddHook(HookEvent.Stop, null, (_, _) => Task.FromResult(new HookOutput()));

        Assert.AreEqual(true, layered.Sandbox!.Enabled);
        CollectionAssert.AreEqual(new[] { "Read" }, layered.AllowedTools.ToArray());
        CollectionAssert.AreEqual(new[] { "Read" }, layered.Tools!.ToArray());
        CollectionAssert.AreEqual(new[] { "Read" }, layered.Agents["bee"].Tools!.ToArray());
        Assert.AreEqual(0, layered.Hooks.Count);
    }

    [TestMethod]
    public void ASessionResumedFromALiveOneIsAskedTheSameUnderAnotherIdentity()
    {
        var live = Defaults();
        live.SessionId = Uuid;
        live.ResumeAtMessageId = "3f2a";
        live.DroppedTurnPromptId = "9c1d";

        var forked = live.Resuming("d10f4163-5fab-46d6-948f-8fc4e19b3315", fork: true);

        foreach (var property in Asked)
        {
            AssertCarried(property.GetValue(live), property.GetValue(forked), property.Name);
        }

        CollectionAssert.AreEqual(live.Hooks.ToArray(), forked.Hooks.ToArray());
        Assert.IsNull(forked.SessionId);
        Assert.AreEqual("d10f4163-5fab-46d6-948f-8fc4e19b3315", forked.ResumeSessionId);
        Assert.IsTrue(forked.Fork);
        Assert.IsFalse(forked.ContinueLatest);
        Assert.IsNull(forked.ResumeAtMessageId);
        Assert.IsNull(forked.DroppedTurnPromptId);
        Assert.IsFalse(live.Resuming(Uuid, fork: false).Fork);
    }

    // The whole is validated as the call is made and before any process.
    [TestMethod]
    public async Task TheWholeIsValidatedWhenTheCallIsMadeAndNoProcessStarts()
    {
        using var host = TestHost.Start([], options => options.EnableFileCheckpointing = true);

        var refused = await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => host.Client.StartSessionAsync(new ClaudeAgentSessionOptions { ContinueLatest = true, ResumeSessionId = "older" }));

        Assert.AreEqual(nameof(ClaudeAgentSessionOptions.ResumeSessionId), refused.ParamName);
        Assert.AreEqual(0, host.Transports.Requests.Length);
    }

    // QueryAsync hands its run back at once, the session starting behind it, so the copy and the
    // refusal are made at the call: what is changed afterwards is not what was asked.
    [TestMethod]
    public async Task QueryAsyncCopiesAndRefusesAtTheCallNotAtTheFirstMessage()
    {
        await using var server = FixtureServer.Start("trivial");
        using var host = TestHost.Start(server);
        var asked = new ClaudeAgentSessionOptions
        {
            WorkingDirectory = "/tmp/claude-agent-fixtures/cwd",
            SettingSources = [],
            Model = "haiku",
            MaxTurns = 1,
        };

        var stream = host.Client.QueryAsync("Reply with exactly one word: pong", asked);
        asked.Model = "opus";
        asked.MaxTurns = 9;
        asked.AddHook(HookEvent.Stop, null, (_, _) => Task.FromResult(new HookOutput()));
        await foreach (var _ in stream)
        {
        }

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        CollectionAssert.AreEqual(Fixture.Load("trivial").Argv.ToArray(), host.Transports.Last!.Argv.ToArray());

        var refused = Assert.ThrowsExactly<ArgumentException>(
            () => host.Client.QueryAsync("hi", new ClaudeAgentSessionOptions { MaxBudgetUsd = 0m }));
        Assert.AreEqual(nameof(ClaudeAgentSessionOptions.MaxBudgetUsd), refused.ParamName);
        Assert.ThrowsExactly<ArgumentNullException>(() => host.Client.QueryAsync(null!, asked));
        Assert.AreEqual(1, host.Transports.Requests.Length);
    }

    // The client keeps its own copy of the defaults from the moment it is made.
    [TestMethod]
    public async Task ADefaultChangedAfterTheClientIsMadeDoesNotReachItsSessions()
    {
        var options = new ClaudeAgentClientOptions();
        options.Defaults.WorkingDirectory = "/tmp/claude-agent-fixtures/cwd";
        options.Defaults.SettingSources = [];
        options.Defaults.Model = "haiku";
        options.Defaults.MaxTurns = 1;

        await using var server = FixtureServer.Start("trivial");
        using var host = TestHost.Start(options, server);
        options.Defaults.Model = "opus";
        options.Defaults.AllowedTools.Add("Bash");
        options.Defaults.AddHook(HookEvent.Stop, null, (_, _) => Task.FromResult(new HookOutput()));

        await foreach (var _ in host.Client.QueryAsync("Reply with exactly one word: pong"))
        {
        }

        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        CollectionAssert.AreEqual(Fixture.Load("trivial").Argv.ToArray(), host.Transports.Last!.Argv.ToArray());
    }

    // Every asked member set, the hooks too, and no session named.
    private static ClaudeAgentSessionOptions Defaults()
    {
        var defaults = new ClaudeAgentSessionOptions();
        foreach (var property in Asked)
        {
            Distinguish(defaults, property);
        }

        defaults.AddHook(HookEvent.Notification, null, (_, _) => Task.FromResult(new HookOutput()));
        return defaults;
    }
}
