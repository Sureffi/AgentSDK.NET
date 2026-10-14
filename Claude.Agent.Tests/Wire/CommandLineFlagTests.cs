using Claude.Agent.Tests.Mcp;

namespace Claude.Agent.Tests.Wire;

[TestClass]
public sealed class CommandLineFlagTests
{
    [TestMethod]
    public void ANullSystemPromptStillSendsTheFlagWithAnEmptyValue()
    {
        CollectionAssert.AreEqual(Expected("--system-prompt", ""), Build(new ClaudeAgentSessionOptions()));
    }

    [TestMethod]
    public void ATextSystemPromptGoesOnTheCommandLine()
    {
        var options = new ClaudeAgentSessionOptions { SystemPrompt = AgentSystemPrompt.FromText("Answer briefly.") };

        CollectionAssert.AreEqual(Expected("--system-prompt", "Answer briefly."), Build(options));
    }

    [TestMethod]
    public void AFileSystemPromptIsPassedAsAPath()
    {
        var options = new ClaudeAgentSessionOptions { SystemPrompt = AgentSystemPrompt.FromFile("/tmp/prompt.md") };

        CollectionAssert.AreEqual(Expected("--system-prompt-file", "/tmp/prompt.md"), Build(options));
    }

    [TestMethod]
    public void APresetSystemPromptWithAnAppendUsesTheAppendFlag()
    {
        var options = new ClaudeAgentSessionOptions { SystemPrompt = AgentSystemPrompt.Preset("Be terse.") };

        CollectionAssert.AreEqual(Expected("--append-system-prompt", "Be terse."), Build(options));
    }

    [TestMethod]
    public void APresetSystemPromptWithoutAnAppendSendsNoPromptFlagAtAll()
    {
        var options = new ClaudeAgentSessionOptions { SystemPrompt = AgentSystemPrompt.Preset() };

        CollectionAssert.AreEqual(Expected(), Build(options));
    }

    [TestMethod]
    public void AnEmptyToolListSendsTheToolsFlagWithAnEmptyValue()
    {
        var options = new ClaudeAgentSessionOptions { Tools = [] };

        CollectionAssert.AreEqual(Expected("--system-prompt", "", "--tools", ""), Build(options));
    }

    [TestMethod]
    public void AToolListIsCommaJoined()
    {
        var options = new ClaudeAgentSessionOptions { Tools = ["Read", "Write"] };

        CollectionAssert.AreEqual(Expected("--system-prompt", "", "--tools", "Read,Write"), Build(options));
    }

    [TestMethod]
    public void AllowedToolsAreCommaJoinedRuleStrings()
    {
        var options = new ClaudeAgentSessionOptions();
        options.AllowedTools.Add("Read");
        options.AllowedTools.Add("Bash(ls:*)");

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--allowedTools", "Read,Bash(ls:*)"),
            Build(options));
    }

    [TestMethod]
    public void MaxTurnsOfZeroIsOmitted()
    {
        var options = new ClaudeAgentSessionOptions { MaxTurns = 0 };

        CollectionAssert.AreEqual(Expected("--system-prompt", ""), Build(options));
    }

    [TestMethod]
    public void MaxTurnsIsSentWhenItIsSet()
    {
        var options = new ClaudeAgentSessionOptions { MaxTurns = 4 };

        CollectionAssert.AreEqual(Expected("--system-prompt", "", "--max-turns", "4"), Build(options));
    }

    [TestMethod]
    public void MaxBudgetUsdIsWrittenAsAShortestRoundTripDecimal()
    {
        var options = new ClaudeAgentSessionOptions { MaxBudgetUsd = 1.5m };

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--max-budget-usd", "1.5"),
            Build(options));
    }

    [TestMethod]
    public void DisallowedToolsAreCommaJoined()
    {
        var options = new ClaudeAgentSessionOptions();
        options.DisallowedTools.Add("Bash");
        options.DisallowedTools.Add("Write");

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--disallowedTools", "Bash,Write"),
            Build(options));
    }

    [TestMethod]
    public void ATaskBudgetOfZeroIsStillSent()
    {
        var options = new ClaudeAgentSessionOptions { TaskBudget = 0 };

        CollectionAssert.AreEqual(Expected("--system-prompt", "", "--task-budget", "0"), Build(options));
    }

    [TestMethod]
    public void TheModelGoesOnTheCommandLine()
    {
        var options = new ClaudeAgentSessionOptions { Model = "sonnet" };

        CollectionAssert.AreEqual(Expected("--system-prompt", "", "--model", "sonnet"), Build(options));
    }

    [TestMethod]
    public void TheFallbackModelGoesOnTheCommandLine()
    {
        var options = new ClaudeAgentSessionOptions { FallbackModel = "haiku" };

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--fallback-model", "haiku"),
            Build(options));
    }

    [TestMethod]
    public void BetasAreCommaJoined()
    {
        var options = new ClaudeAgentSessionOptions();
        options.Betas.Add("context-1m-2025-08-07");
        options.Betas.Add("other-beta");

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--betas", "context-1m-2025-08-07,other-beta"),
            Build(options));
    }

    [TestMethod]
    public void APermissionPromptToolGoesOnTheCommandLine()
    {
        var options = new ClaudeAgentSessionOptions { PermissionPromptTool = "mcp__auth__ask" };

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--permission-prompt-tool", "mcp__auth__ask"),
            Build(options));
    }

    [TestMethod]
    public void APermissionCallbackRoutesThePermissionPromptToolOverStdio()
    {
        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--permission-prompt-tool", "stdio"),
            Build(new ClaudeAgentSessionOptions(), routePermissionsOverStdio: true));
    }

    [TestMethod]
    public void APermissionCallbackCannotBeCombinedWithAPermissionPromptTool()
    {
        var options = new ClaudeAgentSessionOptions { PermissionPromptTool = "mcp__auth__ask" };

        var refused = Assert.ThrowsExactly<ArgumentException>(
            () => CommandLine.Build(options, new TransportNeeds(RoutePermissionsOverStdio: true), _ => null));

        Assert.AreEqual(nameof(ClaudeAgentSessionOptions.PermissionPromptTool), refused.ParamName);
    }

    [TestMethod]
    public void ThePermissionModeIsSentInItsWireForm()
    {
        var options = new ClaudeAgentSessionOptions { PermissionMode = PermissionMode.BypassPermissions };

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--permission-mode", "bypassPermissions"),
            Build(options));
    }

    // The binary decides what it accepts, not this library: a mode, an effort or a settings source
    // the library does not name reaches argv as the caller wrote it.
    [TestMethod]
    public void APermissionModeTheLibraryDoesNotNameReachesArgvUnchanged()
    {
        var options = new ClaudeAgentSessionOptions { PermissionMode = new PermissionMode("ponderMode") };

        Assert.IsFalse(options.PermissionMode!.Value.IsDefined);
        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--permission-mode", "ponderMode"),
            Build(options));
    }

    [TestMethod]
    public void AnEffortTheLibraryDoesNotNameReachesArgvUnchanged()
    {
        var options = new ClaudeAgentSessionOptions { Effort = "colossal" };

        Assert.IsFalse(options.Effort!.Value.IsDefined);
        CollectionAssert.AreEqual(Expected("--system-prompt", "", "--effort", "colossal"), Build(options));
    }

    [TestMethod]
    public void ASettingSourceTheLibraryDoesNotNameReachesArgvUnchanged()
    {
        var options = new ClaudeAgentSessionOptions { SettingSources = [SettingSource.User, "enterprise"] };

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--setting-sources=user,enterprise"),
            Build(options));
    }

    [TestMethod]
    public void ContinuingTheLatestSessionIsABareFlag()
    {
        var session = new ClaudeAgentSessionOptions { ContinueLatest = true };

        CollectionAssert.AreEqual(Expected("--system-prompt", "", "--continue"), Build(new ClaudeAgentSessionOptions(), session));
    }

    [TestMethod]
    public void ResumeUsesTheEqualsFormSoADashLeadingValueCannotBecomeAFlag()
    {
        var session = new ClaudeAgentSessionOptions { ResumeSessionId = "-rf" };

        CollectionAssert.AreEqual(Expected("--system-prompt", "", "--resume=-rf"), Build(new ClaudeAgentSessionOptions(), session));
    }

    [TestMethod]
    public void TheSessionIdUsesTheEqualsForm()
    {
        var session = new ClaudeAgentSessionOptions { SessionId = "0f4c2a3e-1d5b-4a2f-9c8e-7b6a5d4c3b2a" };

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--session-id=0f4c2a3e-1d5b-4a2f-9c8e-7b6a5d4c3b2a"),
            Build(new ClaudeAgentSessionOptions(), session));
    }

    [TestMethod]
    public void SettingsAlonePassThroughUnchanged()
    {
        var options = new ClaudeAgentSessionOptions { Settings = """{"model": "sonnet"}""" };

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--settings", """{"model": "sonnet"}"""),
            Build(options));
    }

    [TestMethod]
    public void ASandboxMergesIntoInlineSettingsJson()
    {
        var options = new ClaudeAgentSessionOptions
        {
            Settings = """{"model": "sonnet"}""",
            Sandbox = new SandboxOptions { Enabled = true },
        };

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--settings", """{"model": "sonnet", "sandbox": {"enabled": true}}"""),
            Build(options));
    }

    [TestMethod]
    public void ASandboxMergesIntoTheSettingsFileTheInjectedReaderReturns()
    {
        var options = new ClaudeAgentSessionOptions
        {
            Settings = "/tmp/settings.json",
            Sandbox = new SandboxOptions { Enabled = false, ExcludedCommands = ["curl"] },
        };

        var argv = Build(
            options,
            readFile: path => path == "/tmp/settings.json" ? """{"env": {"A": "1"}}""" : null);

        CollectionAssert.AreEqual(
            Expected(
                "--system-prompt",
                "",
                "--settings",
                """{"env": {"A": "1"}, "sandbox": {"enabled": false, "excludedCommands": ["curl"]}}"""),
            argv);
    }

    [TestMethod]
    public void EachAdditionalDirectoryGetsItsOwnFlag()
    {
        var options = new ClaudeAgentSessionOptions();
        options.AdditionalDirectories.Add("/one");
        options.AdditionalDirectories.Add("/two");

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--add-dir", "/one", "--add-dir", "/two"),
            Build(options));
    }

    [TestMethod]
    public void McpServersAreSerialisedWithTheRecordedSeparators()
    {
        var options = new ClaudeAgentSessionOptions();
        options.McpServers["one"] = McpServerConfiguration.Stdio("run", ["--flag"], new Dictionary<string, string> { ["K"] = "V" });
        options.McpServers["two"] = McpServerConfiguration.Sse(new Uri("https://sse.example"), new Dictionary<string, string> { ["H"] = "1" });
        options.McpServers["three"] = McpServerConfiguration.Http(new Uri("https://http.example"));
        options.McpServers["four"] = McpServerConfiguration.InProcess(new Calc());

        CollectionAssert.AreEqual(
            Expected(
                "--system-prompt",
                "",
                "--mcp-config",
                """{"mcpServers": {"one": {"type": "stdio", "command": "run", "args": ["--flag"], "env": {"K": "V"}}, "two": {"type": "sse", "url": "https://sse.example", "headers": {"H": "1"}}, "three": {"type": "http", "url": "https://http.example"}, "four": {"type": "sdk", "name": "four"}}}"""),
            Build(options));
    }

    // What the recordings never carried is written after what they pin: a timeout in milliseconds,
    // the always-load flag, and one entry per tool the caller sets a policy over.
    [TestMethod]
    public void AnMcpServersTimeoutAlwaysLoadAndToolPoliciesFollowTheKeysTheRecordingsPin()
    {
        var options = new ClaudeAgentSessionOptions();
        options.McpServers["one"] = McpServerConfiguration.Stdio(
            "run",
            ["--flag"],
            new Dictionary<string, string> { ["K"] = "V" },
            TimeSpan.FromSeconds(30),
            alwaysLoad: true);
        options.McpServers["two"] = McpServerConfiguration.Http(
            new Uri("https://http.example"),
            tools:
            [
                new McpServerToolPolicy("search", McpToolPermissionPolicy.AlwaysAllow, McpToolOrganizationPermission.Ask),
                new McpServerToolPolicy("write"),
            ],
            alwaysLoad: false);
        options.McpServers["three"] = McpServerConfiguration.InProcess(new Calc(), timeout: TimeSpan.FromMilliseconds(1500));

        CollectionAssert.AreEqual(
            Expected(
                "--system-prompt",
                "",
                "--mcp-config",
                """{"mcpServers": {"one": {"type": "stdio", "command": "run", "args": ["--flag"], "env": {"K": "V"}, "timeout": 30000, "alwaysLoad": true}, "two": {"type": "http", "url": "https://http.example", "tools": [{"name": "search", "permission_policy": "always_allow", "org_max_permission": "ask"}, {"name": "write"}], "alwaysLoad": false}, "three": {"type": "sdk", "name": "three", "timeout": 1500}}}"""),
            Build(options));
    }

    [TestMethod]
    public void PartialMessagesAreABareFlag()
    {
        var options = new ClaudeAgentSessionOptions { IncludePartialMessages = true };

        CollectionAssert.AreEqual(Expected("--system-prompt", "", "--include-partial-messages"), Build(options));
    }

    [TestMethod]
    public void HookEventsAreABareFlag()
    {
        var options = new ClaudeAgentSessionOptions { IncludeHookEvents = true };

        CollectionAssert.AreEqual(Expected("--system-prompt", "", "--include-hook-events"), Build(options));
    }

    [TestMethod]
    public void OnlyConfiguredMcpServersIsABareFlag()
    {
        var options = new ClaudeAgentSessionOptions { OnlyConfiguredMcpServers = true };

        CollectionAssert.AreEqual(Expected("--system-prompt", "", "--strict-mcp-config"), Build(options));
    }

    [TestMethod]
    public void ForkingTheSessionIsABareFlag()
    {
        var session = new ClaudeAgentSessionOptions { Fork = true };

        CollectionAssert.AreEqual(Expected("--system-prompt", "", "--fork-session"), Build(new ClaudeAgentSessionOptions(), session));
    }

    [TestMethod]
    public void ResumeAtMessageIdUsesTheEqualsForm()
    {
        var session = new ClaudeAgentSessionOptions { ResumeAtMessageId = "3f2a" };

        CollectionAssert.AreEqual(Expected("--system-prompt", "", "--resume-session-at=3f2a"), Build(new ClaudeAgentSessionOptions(), session));
    }

    [TestMethod]
    public void AnEmptyDroppedTurnPromptIdIsStillForwarded()
    {
        var session = new ClaudeAgentSessionOptions { DroppedTurnPromptId = string.Empty };

        CollectionAssert.AreEqual(Expected("--system-prompt", "", "--resume-drops-turn="), Build(new ClaudeAgentSessionOptions(), session));
    }

    [TestMethod]
    public void EmptySettingSourcesSendTheFlagWithNoValue()
    {
        var options = new ClaudeAgentSessionOptions { SettingSources = [] };

        CollectionAssert.AreEqual(Expected("--system-prompt", "", "--setting-sources="), Build(options));
    }

    [TestMethod]
    public void SettingSourcesAreCommaJoinedInTheirWireForm()
    {
        var options = new ClaudeAgentSessionOptions { SettingSources = [SettingSource.User, SettingSource.Project] };

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--setting-sources=user,project"),
            Build(options));
    }

    [TestMethod]
    public void EachPluginDirectoryGetsItsOwnFlag()
    {
        var options = new ClaudeAgentSessionOptions();
        options.PluginDirectories.Add("/plugins/one");

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--plugin-dir", "/plugins/one"),
            Build(options));
    }

    [TestMethod]
    public void ReplayingUserMessagesIsABareFlag()
    {
        var options = new ClaudeAgentSessionOptions { ReplayUserMessages = true };

        CollectionAssert.AreEqual(Expected("--system-prompt", "", "--replay-user-messages"), Build(options));
    }

    [TestMethod]
    public void AdaptiveThinkingSendsTheThinkingFlagAndNoDisplay()
    {
        var options = new ClaudeAgentSessionOptions { Thinking = ThinkingOptions.Adaptive() };

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--thinking", "adaptive"),
            Build(options));
    }

    [TestMethod]
    public void ABudgetedThinkingSendsTheTokenBudget()
    {
        var options = new ClaudeAgentSessionOptions { Thinking = ThinkingOptions.WithBudget(5000, omitThinking: true) };

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--max-thinking-tokens", "5000", "--thinking-display", "omitted"),
            Build(options));
    }

    [TestMethod]
    public void DisabledThinkingSendsTheThinkingFlag()
    {
        var options = new ClaudeAgentSessionOptions { Thinking = ThinkingOptions.Disabled() };

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--thinking", "disabled"),
            Build(options));
    }

    [TestMethod]
    public void TheEffortIsSentInItsWireForm()
    {
        var options = new ClaudeAgentSessionOptions { Effort = Effort.XHigh };

        CollectionAssert.AreEqual(Expected("--system-prompt", "", "--effort", "xhigh"), Build(options));
    }

    [TestMethod]
    public void TheOutputSchemaIsSerialisedAsJson()
    {
        var options = new ClaudeAgentSessionOptions { OutputSchema = Recorded.Json("""{"type": "object"}""") };

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--json-schema", """{"type": "object"}"""),
            Build(options));
    }

    [TestMethod]
    public void EverySkillAllowsTheWholeSkillToolAndNamesNoSettingSource()
    {
        var options = new ClaudeAgentSessionOptions { AllSkills = true };

        CollectionAssert.AreEqual(Expected("--system-prompt", "", "--allowedTools", "Skill"), Build(options));
    }

    [TestMethod]
    public void NamedSkillsAppendOneRulePerSkillAfterTheAllowedToolsAndNameNoSettingSource()
    {
        var options = new ClaudeAgentSessionOptions();
        options.Skills.Add("pdf");
        options.Skills.Add("xlsx");
        options.AllowedTools.Add("Read");

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--allowedTools", "Read,Skill(pdf),Skill(xlsx)"),
            Build(options));
    }

    [TestMethod]
    public void ASkillNameGoesIntoItsRuleWithBackslashesDoubledAndParenthesesEscaped()
    {
        var options = new ClaudeAgentSessionOptions();
        options.Skills.Add(@"blue\(print)\");

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--allowedTools", @"Skill(blue\\\(print\)\\)"),
            Build(options));
    }

    // The Skill rules follow the options' own as they are, one a name: the Skill tool gathers its rules
    // by their content, and a rule the options already hold grants nothing more.
    [TestMethod]
    public void TheSkillRulesFollowTheAllowedToolsAsTheyAre()
    {
        var options = new ClaudeAgentSessionOptions();
        options.AllowedTools.Add("Skill(pdf)");
        options.Skills.Add("pdf");

        CollectionAssert.AreEqual(Expected("--system-prompt", "", "--allowedTools", "Skill(pdf),Skill(pdf)"), Build(options));
    }

    [TestMethod]
    public void TheSettingSourcesTheCallerChoseGoOutAsTheyAreWithSkills()
    {
        var options = new ClaudeAgentSessionOptions { AllSkills = true, SettingSources = [SettingSource.Local] };

        CollectionAssert.AreEqual(
            Expected("--system-prompt", "", "--allowedTools", "Skill", "--setting-sources=local"),
            Build(options));
    }

    [TestMethod]
    public void EveryFlagIsEmittedInTheOrderTheReferenceBuildsThem()
    {
        var options = new ClaudeAgentSessionOptions
        {
            SystemPrompt = AgentSystemPrompt.FromText("sys"),
            Tools = ["Read"],
            MaxTurns = 2,
            MaxBudgetUsd = 1.5m,
            TaskBudget = 7,
            Model = "sonnet",
            FallbackModel = "haiku",
            PermissionPromptTool = "ask",
            PermissionMode = PermissionMode.Plan,
            Settings = """{"a": 1}""",
            IncludePartialMessages = true,
            IncludeHookEvents = true,
            OnlyConfiguredMcpServers = true,
            SettingSources = [SettingSource.User],
            ReplayUserMessages = true,
            Thinking = ThinkingOptions.Adaptive(),
            Effort = Effort.High,
            OutputSchema = Recorded.Json("""{"type": "object"}"""),
        };
        options.AllowedTools.Add("Read");
        options.DisallowedTools.Add("Bash");
        options.Betas.Add("beta-a");
        options.AdditionalDirectories.Add("/one");
        options.McpServers["calc"] = McpServerConfiguration.InProcess(new Calc());
        options.PluginDirectories.Add("/plug");

        var session = new ClaudeAgentSessionOptions
        {
            ResumeSessionId = "abc",
            Fork = true,
            ResumeAtMessageId = "uuid",
            DroppedTurnPromptId = string.Empty,
        };

        CollectionAssert.AreEqual(
            Expected(
                "--system-prompt", "sys",
                "--tools", "Read",
                "--allowedTools", "Read",
                "--max-turns", "2",
                "--max-budget-usd", "1.5",
                "--disallowedTools", "Bash",
                "--task-budget", "7",
                "--model", "sonnet",
                "--fallback-model", "haiku",
                "--betas", "beta-a",
                "--permission-prompt-tool", "ask",
                "--permission-mode", "plan",
                "--resume=abc",
                "--settings", """{"a": 1}""",
                "--add-dir", "/one",
                "--mcp-config", """{"mcpServers": {"calc": {"type": "sdk", "name": "calc"}}}""",
                "--include-partial-messages",
                "--include-hook-events",
                "--strict-mcp-config",
                "--fork-session",
                "--resume-session-at=uuid",
                "--resume-drops-turn=",
                "--setting-sources=user",
                "--plugin-dir", "/plug",
                "--replay-user-messages",
                "--thinking", "adaptive",
                "--effort", "high",
                "--json-schema", """{"type": "object"}"""),
            Build(options, session));
    }

    private static string[] Expected(params string[] flags) =>
        ["--output-format", "stream-json", "--verbose", .. flags, "--input-format", "stream-json"];

    private static string[] Build(
        ClaudeAgentSessionOptions options,
        ClaudeAgentSessionOptions? session = null,
        bool routePermissionsOverStdio = false,
        Func<string, string?>? readFile = null) =>
        [.. CommandLine.Build(
            ClaudeAgentSessionOptions.Layer(options, session),
            new TransportNeeds(routePermissionsOverStdio),
            readFile ?? (_ => null))];
}
