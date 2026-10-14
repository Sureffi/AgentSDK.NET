using System.Text.Json;

namespace Claude.Agent.Tests.Models;

// The builders a caller writes a test against: each parameter is the property of its name, and a
// builder called with nothing hands back a model whose lists are empty rather than null.
[TestClass]
public sealed class PayloadFactoryTests
{
    [TestMethod]
    public void TheUsageBuildersCarryEveryValueTheyAreGiven()
    {
        var cache = ClaudeAgentModelFactory.CacheCreationTokens(11, 12);
        var details = ClaudeAgentModelFactory.OutputTokensDetails(13);
        var serverTools = ClaudeAgentModelFactory.ServerToolUsage(14, 15);
        var credit = ClaudeAgentModelFactory.FallbackCredit(ClaudeAgentModelFactory.RedeemedFallbackCreditStatus());
        var iteration = ClaudeAgentModelFactory.MessageUsageIteration("claude-haiku-4-5");

        Assert.AreEqual(11, cache.Ephemeral1HourInputTokens);
        Assert.AreEqual(12, cache.Ephemeral5MinuteInputTokens);
        Assert.AreEqual(13, details.ThinkingTokens);
        Assert.AreEqual(14, serverTools.WebFetchRequests);
        Assert.AreEqual(15, serverTools.WebSearchRequests);

        var usage = ClaudeAgentModelFactory.Usage(
            1,
            2,
            3,
            4,
            cache,
            details,
            serverTools,
            ServiceTier.Priority,
            InferenceSpeed.Fast,
            "europe",
            [iteration],
            credit);

        Assert.AreEqual(1, usage.InputTokens);
        Assert.AreEqual(2, usage.OutputTokens);
        Assert.AreEqual(3, usage.CacheCreationInputTokens);
        Assert.AreEqual(4, usage.CacheReadInputTokens);
        Assert.AreSame(cache, usage.CacheCreation);
        Assert.AreSame(details, usage.OutputTokensDetails);
        Assert.AreSame(serverTools, usage.ServerToolUse);
        Assert.AreEqual(ServiceTier.Priority, usage.ServiceTier);
        Assert.AreEqual(InferenceSpeed.Fast, usage.Speed);
        Assert.AreEqual("europe", usage.InferenceRegion);
        Assert.AreSame(iteration, usage.Iterations.Single());
        Assert.AreSame(credit, usage.FallbackCredit);
    }

    [TestMethod]
    public void TheModelUsageBuilderCarriesEveryValueItIsGiven()
    {
        var usage = ClaudeAgentModelFactory.ModelUsage(
            1,
            2,
            3,
            4,
            5,
            6,
            7.5m,
            ModelUsageCostBasis.Managed,
            8,
            9,
            "claude-haiku-4-5",
            "bedrock");

        Assert.AreEqual(1, usage.InputTokens);
        Assert.AreEqual(2, usage.OutputTokens);
        Assert.AreEqual(3, usage.ThinkingTokens);
        Assert.AreEqual(4, usage.CacheReadInputTokens);
        Assert.AreEqual(5, usage.CacheCreationInputTokens);
        Assert.AreEqual(6, usage.WebSearchRequests);
        Assert.AreEqual(7.5m, usage.CostUsd);
        Assert.AreEqual(ModelUsageCostBasis.Managed, usage.CostBasis);
        Assert.AreEqual(8, usage.ContextWindow);
        Assert.AreEqual(9, usage.MaxOutputTokens);
        Assert.AreEqual("claude-haiku-4-5", usage.CanonicalModel);
        Assert.AreEqual("bedrock", usage.Provider);
    }

    [TestMethod]
    public void EachUsageIterationBuilderCarriesEveryValueItIsGiven()
    {
        var cache = ClaudeAgentModelFactory.CacheCreationTokens(5, 6);

        var message = ClaudeAgentModelFactory.MessageUsageIteration("claude-haiku-4-5", 1, 2, 3, 4, cache);
        Assert.AreEqual("claude-haiku-4-5", message.Model);
        AssertCounts(message, cache);

        var compaction = ClaudeAgentModelFactory.CompactionUsageIteration(1, 2, 3, 4, cache);
        AssertCounts(compaction, cache);

        var advisor = ClaudeAgentModelFactory.AdvisorMessageUsageIteration("claude-haiku-4-5", 1, 2, 3, 4, cache);
        Assert.AreEqual("claude-haiku-4-5", advisor.Model);
        AssertCounts(advisor, cache);

        var fallback = ClaudeAgentModelFactory.FallbackMessageUsageIteration("claude-opus-4-5", 1, 2, 3, 4, cache);
        Assert.AreEqual("claude-opus-4-5", fallback.Model);
        AssertCounts(fallback, cache);

        var unknown = ClaudeAgentModelFactory.UnknownUsageIteration("rehearsal", Json("""{"type": "rehearsal"}"""), 1, 2, 3, 4, cache);
        Assert.AreEqual("rehearsal", unknown.Type);
        Assert.AreEqual("rehearsal", unknown.Raw.GetProperty("type").GetString());
        AssertCounts(unknown, cache);
    }

    [TestMethod]
    public void EachFallbackCreditBuilderCarriesEveryValueItIsGiven()
    {
        var notApplied = ClaudeAgentModelFactory.NotAppliedFallbackCreditStatus(
            FallbackCreditNotAppliedReason.Expired,
            ["tools"]);
        Assert.AreEqual(FallbackCreditNotAppliedReason.Expired, notApplied.Reason);
        CollectionAssert.AreEqual(new[] { "tools" }, notApplied.FieldsToRemove.ToArray());

        var unknown = ClaudeAgentModelFactory.UnknownFallbackCreditStatus("deferred", Json("""{"until": "later"}"""));
        Assert.AreEqual("deferred", unknown.Type);
        Assert.AreEqual("later", unknown.Raw.GetProperty("until").GetString());

        Assert.AreSame(notApplied, ClaudeAgentModelFactory.FallbackCredit(notApplied).Status);
    }

    [TestMethod]
    public void TheServerInfoBuildersCarryEveryValueTheyAreGiven()
    {
        var command = ClaudeAgentModelFactory.SlashCommandInfo("design", "makes a design", "[what]", ["draw"], true);
        Assert.AreEqual("design", command.Name);
        Assert.AreEqual("makes a design", command.Description);
        Assert.AreEqual("[what]", command.ArgumentHint);
        CollectionAssert.AreEqual(new[] { "draw" }, command.Aliases.ToArray());
        Assert.AreEqual(true, command.IsBuiltIn);
        Assert.IsNull(ClaudeAgentModelFactory.SlashCommandInfo("mill").IsBuiltIn);

        var agent = ClaudeAgentModelFactory.AgentInfo("Explore", "finds code", "haiku");
        Assert.AreEqual("Explore", agent.Name);
        Assert.AreEqual("finds code", agent.Description);
        Assert.AreEqual("haiku", agent.Model);

        var model = ClaudeAgentModelFactory.ModelInfo(
            "sonnet",
            "claude-sonnet-4-5",
            "Sonnet",
            "the everyday one",
            true,
            [Effort.Low, Effort.Max],
            false,
            true,
            false);
        Assert.AreEqual("sonnet", model.Value);
        Assert.AreEqual("claude-sonnet-4-5", model.ResolvedModel);
        Assert.AreEqual("Sonnet", model.DisplayName);
        Assert.AreEqual("the everyday one", model.Description);
        Assert.AreEqual(true, model.SupportsEffort);
        CollectionAssert.AreEqual(new[] { Effort.Low, Effort.Max }, model.SupportedEffortLevels.ToArray());
        Assert.AreEqual(false, model.SupportsAdaptiveThinking);
        Assert.AreEqual(true, model.SupportsFastMode);
        Assert.AreEqual(false, model.SupportsAutoMode);

        var account = ClaudeAgentModelFactory.AccountInfo(
            "bee@mint.example",
            "Mint",
            "Claude Max",
            "keychain",
            ApiKeySource.Project,
            ApiProvider.Vertex);
        Assert.AreEqual("bee@mint.example", account.Email);
        Assert.AreEqual("Mint", account.Organization);
        Assert.AreEqual("Claude Max", account.SubscriptionType);
        Assert.AreEqual("keychain", account.TokenSource);
        Assert.AreEqual(ApiKeySource.Project, account.ApiKeySource);
        Assert.AreEqual(ApiProvider.Vertex, account.ApiProvider);

        var info = ClaudeAgentModelFactory.ServerInfo(
            [command],
            [agent],
            "Concise",
            ["Concise"],
            "/home/bee/.claude/output-styles",
            [model],
            account,
            true,
            false,
            FastModeState.Cooldown,
            FastModeDisabledReason.Pending,
            4242,
            PermissionMode.Plan,
            SessionState.Idle,
            true,
            false,
            true,
            false,
            true,
            false,
            ["ui_surface_v1"],
            ClaudeAgentModelFactory.FeedbackMode(FeedbackModeKind.Post));

        Assert.AreSame(command, info.Commands.Single());
        Assert.AreSame(agent, info.Agents.Single());
        Assert.AreEqual("Concise", info.OutputStyle);
        CollectionAssert.AreEqual(new[] { "Concise" }, info.AvailableOutputStyles.ToArray());
        Assert.AreEqual("/home/bee/.claude/output-styles", info.UserOutputStylesDirectory);
        Assert.AreSame(model, info.Models.Single());
        Assert.AreSame(account, info.Account);
        Assert.AreEqual(true, info.HooksApplied);
        Assert.AreEqual(false, info.PluginsApplied);
        Assert.AreEqual(FastModeState.Cooldown, info.FastModeState);
        Assert.AreEqual(FastModeDisabledReason.Pending, info.FastModeDisabledReason);
        Assert.AreEqual(4242, info.ProcessId);
        Assert.AreEqual(PermissionMode.Plan, info.PermissionMode);
        Assert.AreEqual(SessionState.Idle, info.SessionState);
        Assert.AreEqual(true, info.AnalyticsDisabled);
        Assert.AreEqual(false, info.RemoteControlAvailable);
        Assert.AreEqual(true, info.RemoteControlAutoEnable);
        Assert.AreEqual(false, info.RemoteControlAutoOnByDefault);
        Assert.AreEqual(true, info.RemoteControlAutoConnectDefault);
        Assert.AreEqual(false, info.IdeRemoteControlAutoEnableGate);
        CollectionAssert.AreEqual(new[] { "ui_surface_v1" }, info.Capabilities.ToArray());
        Assert.AreEqual(FeedbackModeKind.Post, info.FeedbackMode?.Kind);
    }

    [TestMethod]
    public void TheInitBuilderCarriesTheScratchpadThePerTurnEffortAndTheView()
    {
        var init = ClaudeAgentModelFactory.SystemInitMessage(
            scratchpadDirectory: "/tmp/claude-1000/s-1/scratchpad",
            isPerTurnEffortActive: true,
            viewMode: ViewMode.Default,
            sessionId: "s-1");

        Assert.AreEqual("/tmp/claude-1000/s-1/scratchpad", init.ScratchpadDirectory);
        Assert.AreEqual(true, init.IsPerTurnEffortActive);
        Assert.AreEqual(ViewMode.Default, init.ViewMode);
        Assert.AreEqual("s-1", init.SessionId);

        var bare = ClaudeAgentModelFactory.SystemInitMessage();
        Assert.IsNull(bare.ScratchpadDirectory);
        Assert.IsNull(bare.IsPerTurnEffortActive);
        Assert.IsNull(bare.ViewMode);
    }

    [TestMethod]
    public void TheContextUsageBuildersCarryEveryValueTheyAreGiven()
    {
        var category = ClaudeAgentModelFactory.ContextUsageCategory("Messages", 10, "purple", true, ContextSpace.Buffer);
        Assert.AreEqual("Messages", category.Name);
        Assert.AreEqual(10, category.Tokens);
        Assert.AreEqual("purple", category.Color);
        Assert.AreEqual(true, category.IsDeferred);
        Assert.AreEqual(ContextSpace.Buffer, category.Kind);

        var square = ClaudeAgentModelFactory.ContextUsageGridSquare("purple", true, "Messages", 10, 0.5, 0.25);
        Assert.AreEqual("purple", square.Color);
        Assert.AreEqual(true, square.IsFilled);
        Assert.AreEqual("Messages", square.CategoryName);
        Assert.AreEqual(10, square.Tokens);
        Assert.AreEqual(0.5, square.Percentage);
        Assert.AreEqual(0.25, square.SquareFullness);

        var memory = ClaudeAgentModelFactory.ContextUsageMemoryFile("/tmp/mint/CLAUDE.md", "project", 11);
        Assert.AreEqual("/tmp/mint/CLAUDE.md", memory.Path);
        Assert.AreEqual("project", memory.Type);
        Assert.AreEqual(11, memory.Tokens);

        var mcpTool = ClaudeAgentModelFactory.ContextUsageMcpTool("mcp__calc__add", "calc", 12, true);
        Assert.AreEqual("mcp__calc__add", mcpTool.Name);
        Assert.AreEqual("calc", mcpTool.ServerName);
        Assert.AreEqual(12, mcpTool.Tokens);
        Assert.AreEqual(true, mcpTool.IsLoaded);

        var deferred = ClaudeAgentModelFactory.ContextUsageDeferredTool("NotebookEdit", 13, false);
        Assert.AreEqual("NotebookEdit", deferred.Name);
        Assert.AreEqual(13, deferred.Tokens);
        Assert.AreEqual(false, deferred.IsLoaded);

        var systemTool = ClaudeAgentModelFactory.ContextUsageSystemTool("Bash", 14);
        Assert.AreEqual("Bash", systemTool.Name);
        Assert.AreEqual(14, systemTool.Tokens);

        var section = ClaudeAgentModelFactory.ContextUsageSystemPromptSection("identity", 15);
        Assert.AreEqual("identity", section.Name);
        Assert.AreEqual(15, section.Tokens);

        var contextAgent = ClaudeAgentModelFactory.ContextUsageAgent("Explore", "built-in", 16);
        Assert.AreEqual("Explore", contextAgent.AgentType);
        Assert.AreEqual("built-in", contextAgent.Source);
        Assert.AreEqual(16, contextAgent.Tokens);

        var commands = ClaudeAgentModelFactory.ContextUsageSlashCommands(17, 18, 19);
        Assert.AreEqual(17, commands.TotalCommands);
        Assert.AreEqual(18, commands.IncludedCommands);
        Assert.AreEqual(19, commands.Tokens);

        var skill = ClaudeAgentModelFactory.ContextUsageSkill("dataviz", "built-in", 20);
        Assert.AreEqual("dataviz", skill.Name);
        Assert.AreEqual("built-in", skill.Source);
        Assert.AreEqual(20, skill.Tokens);

        var skills = ClaudeAgentModelFactory.ContextUsageSkills(21, 22, 23, [skill]);
        Assert.AreEqual(21, skills.TotalSkills);
        Assert.AreEqual(22, skills.IncludedSkills);
        Assert.AreEqual(23, skills.Tokens);
        Assert.AreSame(skill, skills.SkillFrontmatter.Single());

        var call = ClaudeAgentModelFactory.ContextUsageToolCallTokens("Read", 24, 25);
        Assert.AreEqual("Read", call.Name);
        Assert.AreEqual(24, call.CallTokens);
        Assert.AreEqual(25, call.ResultTokens);

        var attachment = ClaudeAgentModelFactory.ContextUsageAttachmentTokens("environment", 26);
        Assert.AreEqual("environment", attachment.Name);
        Assert.AreEqual(26, attachment.Tokens);

        var breakdown = ClaudeAgentModelFactory.ContextUsageMessageBreakdown(27, 28, 29, 30, 31, 32, 33, [call], [attachment]);
        Assert.AreEqual(27, breakdown.ToolCallTokens);
        Assert.AreEqual(28, breakdown.ToolResultTokens);
        Assert.AreEqual(29, breakdown.AttachmentTokens);
        Assert.AreEqual(30, breakdown.AssistantMessageTokens);
        Assert.AreEqual(31, breakdown.UserMessageTokens);
        Assert.AreEqual(32, breakdown.RedirectedContextTokens);
        Assert.AreEqual(33, breakdown.UnattributedTokens);
        Assert.AreSame(call, breakdown.ToolCallsByType.Single());
        Assert.AreSame(attachment, breakdown.AttachmentsByType.Single());

        var apiTokens = ClaudeAgentModelFactory.ContextUsageApiTokens(34, 35, 36, 37);
        Assert.AreEqual(34, apiTokens.InputTokens);
        Assert.AreEqual(35, apiTokens.OutputTokens);
        Assert.AreEqual(36, apiTokens.CacheCreationInputTokens);
        Assert.AreEqual(37, apiTokens.CacheReadInputTokens);

        var usage = ClaudeAgentModelFactory.ContextUsage(
            "claude-haiku-4-5",
            [category],
            38,
            39,
            40,
            41.5,
            [[square]],
            [memory],
            [mcpTool],
            [deferred],
            [systemTool],
            [section],
            [contextAgent],
            commands,
            skills,
            42,
            true,
            "settings",
            breakdown,
            apiTokens);

        Assert.AreEqual("claude-haiku-4-5", usage.Model);
        Assert.AreSame(category, usage.Categories.Single());
        Assert.AreEqual(38, usage.TotalTokens);
        Assert.AreEqual(39, usage.MaxTokens);
        Assert.AreEqual(40, usage.RawMaxTokens);
        Assert.AreEqual(41.5, usage.Percentage);
        Assert.AreSame(square, usage.GridRows.Single().Single());
        Assert.AreSame(memory, usage.MemoryFiles.Single());
        Assert.AreSame(mcpTool, usage.McpTools.Single());
        Assert.AreSame(deferred, usage.DeferredBuiltInTools?.Single());
        Assert.AreSame(systemTool, usage.SystemTools?.Single());
        Assert.AreSame(section, usage.SystemPromptSections?.Single());
        Assert.AreSame(contextAgent, usage.Agents.Single());
        Assert.AreSame(commands, usage.SlashCommands);
        Assert.AreSame(skills, usage.Skills);
        Assert.AreEqual(42, usage.AutoCompactThreshold);
        Assert.IsTrue(usage.IsAutoCompactEnabled);
        Assert.AreEqual("settings", usage.AutoCompactSource);
        Assert.AreSame(breakdown, usage.MessageBreakdown);
        Assert.AreSame(apiTokens, usage.ApiUsage);
    }

    [TestMethod]
    public void TheMcpStatusBuildersCarryEveryValueTheyAreGiven()
    {
        var annotations = ClaudeAgentModelFactory.McpToolAnnotations(true, false, true);
        Assert.AreEqual(true, annotations.IsReadOnly);
        Assert.AreEqual(false, annotations.IsDestructive);
        Assert.AreEqual(true, annotations.IsOpenWorld);

        var tool = ClaudeAgentModelFactory.McpServerTool("add", "adds two numbers", annotations);
        Assert.AreEqual("add", tool.Name);
        Assert.AreEqual("adds two numbers", tool.Description);
        Assert.AreSame(annotations, tool.Annotations);

        var serverInfo = ClaudeAgentModelFactory.McpServerInfo("calc", "1.0.0");
        Assert.AreEqual("calc", serverInfo.Name);
        Assert.AreEqual("1.0.0", serverInfo.Version);

        var proxy = ClaudeAgentModelFactory.McpClaudeAIProxyServerConfiguration(
            new Uri("https://api.example/mcp"),
            "mcpsrv_1",
            TimeSpan.FromSeconds(3));
        Assert.AreEqual(new Uri("https://api.example/mcp"), proxy.Url);
        Assert.AreEqual("mcpsrv_1", proxy.Id);
        Assert.AreEqual(TimeSpan.FromSeconds(3), proxy.Timeout);

        var unknown = ClaudeAgentModelFactory.UnknownMcpServerConfiguration(
            "grpc",
            Json("""{"target": "hive:9000"}"""),
            TimeSpan.FromSeconds(4));
        Assert.AreEqual("grpc", unknown.Type);
        Assert.AreEqual("hive:9000", unknown.Raw.GetProperty("target").GetString());
        Assert.AreEqual(TimeSpan.FromSeconds(4), unknown.Timeout);

        var status = ClaudeAgentModelFactory.McpServerStatus(
            "calc",
            McpServerConnectionStatus.Failed,
            serverInfo,
            "no transport",
            proxy,
            McpServerSource.ClaudeAI,
            McpServerSource.Sdk,
            [tool]);

        Assert.AreEqual("calc", status.Name);
        Assert.AreEqual(McpServerConnectionStatus.Failed, status.Status);
        Assert.AreSame(serverInfo, status.ServerInfo);
        Assert.AreEqual("no transport", status.Error);
        Assert.AreSame(proxy, status.Config);
        Assert.AreEqual(McpServerSource.ClaudeAI, status.Scope);
        Assert.AreEqual(McpServerSource.Sdk, status.Source);
        Assert.AreSame(tool, status.Tools.Single());
    }

    // A builder called with nothing: every list is empty rather than null, and the three lists whose
    // absence the binary means something by stay null.
    [TestMethod]
    public void ABuilderCalledWithNothingHandsBackEmptyListsRatherThanNull()
    {
        var info = ClaudeAgentModelFactory.ServerInfo();
        Assert.AreEqual(0, info.Commands.Count);
        Assert.AreEqual(0, info.Agents.Count);
        Assert.AreEqual(0, info.Models.Count);
        Assert.AreEqual(0, info.AvailableOutputStyles.Count);

        var usage = ClaudeAgentModelFactory.ContextUsage();
        Assert.AreEqual(string.Empty, usage.Model);
        Assert.AreEqual(0, usage.Categories.Count);
        Assert.AreEqual(0, usage.GridRows.Count);
        Assert.AreEqual(0, usage.MemoryFiles.Count);
        Assert.AreEqual(0, usage.McpTools.Count);
        Assert.AreEqual(0, usage.Agents.Count);
        Assert.IsNull(usage.DeferredBuiltInTools);
        Assert.IsNull(usage.SystemTools);
        Assert.IsNull(usage.SystemPromptSections);

        Assert.AreEqual(0, ClaudeAgentModelFactory.Usage().Iterations.Count);
        Assert.AreEqual(0, ClaudeAgentModelFactory.McpServerStatus().Tools.Count);
        Assert.AreEqual(0, ClaudeAgentModelFactory.NotAppliedFallbackCreditStatus().FieldsToRemove.Count);
        Assert.AreEqual(0, ClaudeAgentModelFactory.ContextUsageSkills().SkillFrontmatter.Count);
        Assert.AreEqual(JsonValueKind.Object, ClaudeAgentModelFactory.UnknownUsageIteration().Raw.ValueKind);
        Assert.AreEqual(JsonValueKind.Object, ClaudeAgentModelFactory.UnknownMcpServerConfiguration().Raw.ValueKind);
    }

    private static void AssertCounts(UsageIteration iteration, CacheCreationTokens cache)
    {
        Assert.AreEqual(1, iteration.InputTokens);
        Assert.AreEqual(2, iteration.OutputTokens);
        Assert.AreEqual(3, iteration.CacheCreationInputTokens);
        Assert.AreEqual(4, iteration.CacheReadInputTokens);
        Assert.AreSame(cache, iteration.CacheCreation);
    }

    private static JsonElement Json(string text)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }
}
