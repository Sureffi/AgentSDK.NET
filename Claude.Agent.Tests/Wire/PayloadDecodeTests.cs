namespace Claude.Agent.Tests.Wire;

// The four control replies that carry a payload, read as models rather than as raw JSON. What the
// recordings hold is replayed; what only the declaration holds is a hand-written line.
[TestClass]
public sealed class PayloadDecodeTests
{
    private static readonly PayloadParser Parser = new();

    [TestMethod]
    public void TheInitializeReplyCarriesWhatTheBinaryLoadedAndWhoItIsLoggedInAs()
    {
        var info = Info("payloads", "req_1_257a2c4f");

        Assert.AreEqual(56, info.Commands.Count);
        Assert.AreEqual("deep-research", info.Commands[0].Name);
        Assert.AreEqual(string.Empty, info.Commands[0].ArgumentHint);
        Assert.AreEqual("[what to design]", info.Commands[1].ArgumentHint);
        Assert.AreEqual(0, info.Commands[0].Aliases.Count);
        Assert.AreEqual("default", info.OutputStyle);
        CollectionAssert.AreEqual(
            new[] { "default", "Proactive", "Concise", "Explanatory", "Learning" },
            info.AvailableOutputStyles.ToArray());
        Assert.AreEqual("/home/user/.claude/output-styles", info.UserOutputStylesDirectory);
        Assert.AreEqual("user@example.com", info.Account?.Email);
        Assert.AreEqual("user@example.com's Organization", info.Account?.Organization);
        Assert.AreEqual("Claude Max", info.Account?.SubscriptionType);
        Assert.AreEqual(ApiProvider.FirstParty, info.Account?.ApiProvider);
        Assert.IsNull(info.Account?.TokenSource);
        Assert.IsNull(info.Account?.ApiKeySource);
    }

    // 2.1.283 marks the binary's own commands, and every command of the recorded cwd is one; 2.1.276
    // marks none.
    [TestMethod]
    public void TheInitializeReplyMarksTheBinarysOwnCommandsWhereTheBinaryWritesTheMark()
    {
        var info = Info("schema-type", "req_1_b48cc312");

        Assert.AreEqual(57, info.Commands.Count);
        Assert.IsTrue(info.Commands.All(command => command.IsBuiltIn == true));

        Assert.IsTrue(Info("payloads", "req_1_257a2c4f").Commands.All(command => command.IsBuiltIn is null));
    }

    [TestMethod]
    public void TheInitializeReplyCarriesTheAgentsAndTheModelsWithTheirEffortLevels()
    {
        var info = Info("payloads", "req_1_257a2c4f");

        Assert.AreEqual(6, info.Agents.Count);
        Assert.AreEqual("claude", info.Agents[0].Name);
        Assert.IsNull(info.Agents[0].Model);
        Assert.AreEqual("haiku", info.Agents[1].Model);
        Assert.AreEqual("inherit", info.Agents[2].Model);

        Assert.AreEqual(5, info.Models.Count);
        var model = info.Models[0];
        Assert.AreEqual("default", model.Value);
        Assert.AreEqual("claude-opus-5[1m]", model.ResolvedModel);
        Assert.AreEqual("Default (recommended)", model.DisplayName);
        Assert.AreEqual(true, model.SupportsEffort);
        CollectionAssert.AreEqual(
            new[] { Effort.Low, Effort.Medium, Effort.High, Effort.XHigh, Effort.Max },
            model.SupportedEffortLevels.ToArray());
        Assert.AreEqual(true, model.SupportsAdaptiveThinking);
        Assert.AreEqual(true, model.SupportsFastMode);
        Assert.AreEqual(true, model.SupportsAutoMode);
    }

    [TestMethod]
    public void TheInitializeReplyCarriesHowTheBinaryIsRunning()
    {
        var info = Info("payloads", "req_1_257a2c4f");

        Assert.AreEqual(1476860, info.ProcessId);
        Assert.AreEqual(PermissionMode.Default, info.PermissionMode);
        Assert.AreEqual(SessionState.Idle, info.SessionState);
        Assert.AreEqual(FastModeState.Off, info.FastModeState);
        Assert.AreEqual(FastModeDisabledReason.SdkOptInRequired, info.FastModeDisabledReason);
        Assert.AreEqual(false, info.AnalyticsDisabled);
        Assert.AreEqual(true, info.RemoteControlAvailable);
        Assert.AreEqual(true, info.RemoteControlAutoEnable);
        Assert.AreEqual(false, info.RemoteControlAutoOnByDefault);
        Assert.AreEqual(false, info.RemoteControlAutoConnectDefault);
        Assert.AreEqual(true, info.IdeRemoteControlAutoEnableGate);
        Assert.IsNull(info.HooksApplied);
        Assert.IsNull(info.PluginsApplied);
    }

    // 2.1.295 names its capabilities and its feedback mode; a reply from before it carries neither.
    [TestMethod]
    public void TheInitializeReplyCarriesTheCapabilitiesAndTheFeedbackMode()
    {
        var info = Info("ask", "req_1_f62788f2");
        var earlier = Info("payloads", "req_1_257a2c4f");

        CollectionAssert.AreEqual(new[] { "ui_surface_v1" }, info.Capabilities.ToArray());
        Assert.IsNotNull(info.FeedbackMode);
        Assert.AreEqual(FeedbackModeKind.Post, info.FeedbackMode.Kind);
        Assert.IsTrue(info.FeedbackMode.Kind!.Value.IsDefined);
        Assert.AreEqual(0, earlier.Capabilities.Count);
        Assert.IsNull(earlier.FeedbackMode);
    }

    // The initialize reply is the one payload with no key of its own to miss: a session that is
    // answered with nothing still opens, on a server info that says nothing.
    [TestMethod]
    public void AnInitializeReplyWithNoBodyIsAServerInfoThatSaysNothing()
    {
        var parsed = Parser.ServerInfo(null);

        Assert.IsTrue(parsed.IsOk, Recorded.Reason(parsed));
        Assert.AreEqual(0, parsed.Value.Commands.Count);
        Assert.AreEqual(0, parsed.Value.Models.Count);
        Assert.IsNull(parsed.Value.Account);
        Assert.IsNull(parsed.Value.ProcessId);
    }

    [TestMethod]
    public void TheContextUsageReplyCarriesTheTotalsTheGridAndTheRowsOfTheBreakdown()
    {
        var usage = Context("payloads", "req_2_a56c492a");

        Assert.AreEqual("claude-haiku-4-5-20251001", usage.Model);
        Assert.AreEqual(1263, usage.TotalTokens);
        Assert.AreEqual(200000, usage.MaxTokens);
        Assert.AreEqual(200000, usage.RawMaxTokens);
        Assert.AreEqual(1d, usage.Percentage);
        Assert.AreEqual(167000, usage.AutoCompactThreshold);
        Assert.IsTrue(usage.IsAutoCompactEnabled);
        Assert.AreEqual("auto", usage.AutoCompactSource);

        Assert.AreEqual(3, usage.Categories.Count);
        Assert.AreEqual("MCP tools", usage.Categories[0].Name);
        Assert.AreEqual(126, usage.Categories[0].Tokens);
        Assert.AreEqual("cyan_FOR_SUBAGENTS_ONLY", usage.Categories[0].Color);
        Assert.AreEqual(ContextSpace.Used, usage.Categories[0].Kind);
        Assert.AreEqual(ContextSpace.Free, usage.Categories[2].Kind);
        Assert.IsNull(usage.Categories[0].IsDeferred);

        Assert.AreEqual(10, usage.GridRows.Count);
        Assert.AreEqual(10, usage.GridRows[0].Count);
        var square = usage.GridRows[0][0];
        Assert.AreEqual("cyan_FOR_SUBAGENTS_ONLY", square.Color);
        Assert.AreEqual(true, square.IsFilled);
        Assert.AreEqual("MCP tools", square.CategoryName);
        Assert.AreEqual(126, square.Tokens);
        Assert.AreEqual(0d, square.Percentage);
        Assert.AreEqual(0.063d, square.SquareFullness);
    }

    [TestMethod]
    public void TheContextUsageReplyCarriesTheMcpToolsTheMessageBreakdownAndTheLastApiCall()
    {
        var usage = Context("payloads", "req_2_a56c492a");

        Assert.AreEqual(2, usage.McpTools.Count);
        Assert.AreEqual("mcp__calc__add", usage.McpTools[0].Name);
        Assert.AreEqual("calc", usage.McpTools[0].ServerName);
        Assert.AreEqual(66, usage.McpTools[0].Tokens);
        Assert.AreEqual(false, usage.McpTools[0].IsLoaded);
        Assert.AreEqual(0, usage.MemoryFiles.Count);
        Assert.AreEqual(0, usage.Agents.Count);

        var breakdown = usage.MessageBreakdown;
        Assert.AreEqual(0, breakdown?.ToolCallTokens);
        Assert.AreEqual(0, breakdown?.ToolResultTokens);
        Assert.AreEqual(452, breakdown?.AttachmentTokens);
        Assert.AreEqual(157, breakdown?.AssistantMessageTokens);
        Assert.AreEqual(8, breakdown?.UserMessageTokens);
        Assert.AreEqual(0, breakdown?.RedirectedContextTokens);
        Assert.AreEqual(521, breakdown?.UnattributedTokens);
        Assert.AreEqual(0, breakdown?.ToolCallsByType.Count);
        Assert.AreEqual(6, breakdown?.AttachmentsByType.Count);
        Assert.AreEqual("prompt_snapshot", breakdown?.AttachmentsByType[0].Name);
        Assert.AreEqual(184, breakdown?.AttachmentsByType[0].Tokens);

        Assert.AreEqual(1263, usage.ApiUsage?.InputTokens);
        Assert.AreEqual(43, usage.ApiUsage?.OutputTokens);
        Assert.AreEqual(0, usage.ApiUsage?.CacheCreationInputTokens);
        Assert.AreEqual(0, usage.ApiUsage?.CacheReadInputTokens);
    }

    [TestMethod]
    public void TheContextUsageReplyCarriesWhatTheSlashCommandsAndTheSkillsCost()
    {
        var usage = Context("client", "req_4_7f5fa2d8");

        Assert.AreEqual(16, usage.SlashCommands?.TotalCommands);
        Assert.AreEqual(16, usage.SlashCommands?.IncludedCommands);
        Assert.AreEqual(971, usage.SlashCommands?.Tokens);

        Assert.AreEqual(16, usage.Skills?.TotalSkills);
        Assert.AreEqual(16, usage.Skills?.IncludedSkills);
        Assert.AreEqual(1821, usage.Skills?.Tokens);
        Assert.AreEqual("dataviz", usage.Skills?.SkillFrontmatter[0].Name);
        Assert.AreEqual("built-in", usage.Skills?.SkillFrontmatter[0].Source);
        Assert.AreEqual(362, usage.Skills?.SkillFrontmatter[0].Tokens);
    }

    // The three lists the binary leaves out on every recording: a key that is absent means something
    // other than an empty list, so they are null until the binary sends one.
    [TestMethod]
    public void TheToolAndPromptSectionListsAreNullUntilTheBinarySendsThem()
    {
        var recorded = Context("payloads", "req_2_a56c492a");

        Assert.IsNull(recorded.DeferredBuiltInTools);
        Assert.IsNull(recorded.SystemTools);
        Assert.IsNull(recorded.SystemPromptSections);

        var sent = Read(
            """
            {
              "model": "claude-haiku-4-5",
              "deferredBuiltinTools": [{"name": "NotebookEdit", "tokens": 320, "isLoaded": false}],
              "systemTools": [{"name": "Bash", "tokens": 1200}],
              "systemPromptSections": [{"name": "identity", "tokens": 44}],
              "agents": [{"agentType": "Explore", "source": "built-in", "tokens": 310}],
              "memoryFiles": [{"path": "/tmp/mint/CLAUDE.md", "type": "project", "tokens": 88}]
            }
            """);

        Assert.AreEqual("NotebookEdit", sent.DeferredBuiltInTools?.Single().Name);
        Assert.AreEqual(320, sent.DeferredBuiltInTools?.Single().Tokens);
        Assert.AreEqual(false, sent.DeferredBuiltInTools?.Single().IsLoaded);
        Assert.AreEqual("Bash", sent.SystemTools?.Single().Name);
        Assert.AreEqual(1200, sent.SystemTools?.Single().Tokens);
        Assert.AreEqual("identity", sent.SystemPromptSections?.Single().Name);
        Assert.AreEqual(44, sent.SystemPromptSections?.Single().Tokens);
        Assert.AreEqual("Explore", sent.Agents.Single().AgentType);
        Assert.AreEqual("built-in", sent.Agents.Single().Source);
        Assert.AreEqual(310, sent.Agents.Single().Tokens);
        Assert.AreEqual("/tmp/mint/CLAUDE.md", sent.MemoryFiles.Single().Path);
        Assert.AreEqual("project", sent.MemoryFiles.Single().Type);
        Assert.AreEqual(88, sent.MemoryFiles.Single().Tokens);
    }

    [TestMethod]
    public void AContextUsageWithNoModelIsRefusedNamingTheKeyItWants()
    {
        var parsed = Parser.ContextUsage(Recorded.Json("""{"totalTokens": 12}"""));

        Assert.IsFalse(parsed.IsOk);
        StringAssert.Contains(Recorded.Reason(parsed), "model");
    }

    [TestMethod]
    public void TheMcpStatusReplyCarriesEachServerWithItsConfigurationAndItsTools()
    {
        var servers = Servers("payloads", "req_3_5b95214d");

        Assert.AreEqual(2, servers.Count);
        var spawned = servers[0];
        Assert.AreEqual("echo", spawned.Name);
        Assert.AreEqual(McpServerConnectionStatus.Connected, spawned.Status);
        Assert.AreEqual("echo", spawned.ServerInfo?.Name);
        Assert.AreEqual("1.0.0", spawned.ServerInfo?.Version);
        Assert.AreEqual(McpServerSource.Dynamic, spawned.Scope);
        Assert.AreEqual(McpServerSource.Dynamic, spawned.Source);
        Assert.IsNull(spawned.Error);
        Assert.AreEqual("echo", spawned.Tools.Single().Name);
        Assert.IsNull(spawned.Tools.Single().Description);
        Assert.IsNull(spawned.Tools.Single().Annotations?.IsReadOnly);

        var stdio = Is<McpStdioServerConfiguration>(spawned.Config);
        Assert.AreEqual("/tmp/claude-agent-fixtures/cwd/echo-server", stdio.Command);
        Assert.AreEqual(0, stdio.Arguments?.Count);
        Assert.IsNull(stdio.EnvironmentVariables);
        Assert.IsNull(stdio.Timeout);

        // The in-process server the binary reports back has no configuration of its own.
        var inProcess = servers[1];
        Assert.AreEqual("calc", inProcess.Name);
        Assert.AreEqual(McpServerSource.Sdk, inProcess.Source);
        Assert.IsNull(inProcess.Config);
    }

    [TestMethod]
    public void AServerReachedThroughTheProxyReadsAsItsOwnKindWithItsToolAnnotations()
    {
        var server = Servers("client", "req_5_1135df8a").Single();

        Assert.AreEqual("claude.ai Claude Docs", server.Name);
        Assert.AreEqual("Claude Docs", server.ServerInfo?.Name);
        Assert.AreEqual(McpServerSource.ClaudeAI, server.Scope);
        Assert.AreEqual(McpServerSource.ClaudeAI, server.Source);

        var proxy = Is<McpClaudeAIProxyServerConfiguration>(server.Config);
        Assert.AreEqual(new Uri("https://api.anthropic.com/v1/pages/mcp"), proxy.Url);
        Assert.AreEqual("mcpsrv_014Fj7srWvLMyCuRCS8J9W3d", proxy.Id);

        Assert.AreEqual(8, server.Tools.Count);
        Assert.AreEqual("read", server.Tools[1].Name);
        Assert.AreEqual(true, server.Tools[1].Annotations?.IsReadOnly);
        Assert.AreEqual(true, server.Tools[3].Annotations?.IsDestructive);
        Assert.IsNull(server.Tools[3].Annotations?.IsOpenWorld);
    }

    [TestMethod]
    public void EachConfigurationKindTheBinaryReportsReadsAsItsOwnType()
    {
        var servers = Servers(
            """
            {"mcpServers": [
              {"name": "bare", "status": "connected", "config": {"command": "run", "args": ["--once"], "env": {"K": "V"}}},
              {"name": "feed", "status": "connected", "config": {"type": "sse", "url": "https://mint.example/sse", "headers": {"Authorization": "Bearer token"}, "timeout": 30000, "alwaysLoad": true}},
              {"name": "web", "status": "connected", "config": {"type": "http", "url": "https://mint.example/mcp", "tools": [{"name": "search", "permission_policy": "always_allow", "org_max_permission": "ask"}]}},
              {"name": "calc", "status": "connected", "config": {"type": "sdk", "name": "calc", "timeout": 1000}},
              {"name": "hive", "status": "connected", "config": {"type": "grpc", "target": "hive:9000"}},
              {"name": "odd", "status": "failed", "error": "no transport", "config": 7}
            ]}
            """);

        var bare = Is<McpStdioServerConfiguration>(servers[0].Config);
        Assert.AreEqual("run", bare.Command);
        CollectionAssert.AreEqual(new[] { "--once" }, bare.Arguments?.ToArray());
        Assert.AreEqual("V", bare.EnvironmentVariables?["K"]);

        var sse = Is<McpSseServerConfiguration>(servers[1].Config);
        Assert.AreEqual(new Uri("https://mint.example/sse"), sse.Url);
        Assert.AreEqual("Bearer token", sse.Headers?["Authorization"]);
        Assert.AreEqual(TimeSpan.FromSeconds(30), sse.Timeout);
        Assert.AreEqual(true, sse.AlwaysLoad);
        Assert.IsNull(sse.Tools);

        var http = Is<McpHttpServerConfiguration>(servers[2].Config);
        var policy = http.Tools?.Single();
        Assert.AreEqual("search", policy?.Name);
        Assert.AreEqual(McpToolPermissionPolicy.AlwaysAllow, policy?.PermissionPolicy);
        Assert.AreEqual(McpToolOrganizationPermission.Ask, policy?.OrganizationMaxPermission);
        Assert.IsNull(http.AlwaysLoad);

        var sdk = Is<McpSdkServerConfiguration>(servers[3].Config);
        Assert.AreEqual("calc", sdk.Name);
        Assert.AreEqual(TimeSpan.FromSeconds(1), sdk.Timeout);
        Assert.IsNull(sdk.Tools);
        Assert.IsNull(sdk.Version);

        var unnamed = Is<UnknownMcpServerConfiguration>(servers[4].Config);
        Assert.AreEqual("grpc", unnamed.Type);
        Assert.AreEqual("hive:9000", unnamed.Raw.GetProperty("target").GetString());

        var broken = Is<UnknownMcpServerConfiguration>(servers[5].Config);
        Assert.AreEqual(string.Empty, broken.Type);
        Assert.AreEqual("no transport", servers[5].Error);
        Assert.AreEqual(McpServerConnectionStatus.Failed, servers[5].Status);
    }

    // A configuration that names a kind but not what the kind needs is the unknown of the set: the
    // members are kept as written rather than being invented.
    [TestMethod]
    public void AConfigurationMissingWhatItsKindNeedsIsTheUnknownOfTheSet()
    {
        var servers = Servers(
            """
            {"mcpServers": [
              {"name": "one", "status": "connected", "config": {"type": "stdio"}},
              {"name": "two", "status": "connected", "config": {"type": "sse", "url": "not a url"}},
              {"name": "three", "status": "connected", "config": {"type": "http"}}
            ]}
            """);

        foreach (var server in servers)
        {
            var configuration = Is<UnknownMcpServerConfiguration>(server.Config);
            Assert.AreNotEqual(string.Empty, configuration.Type);
        }
    }

    [TestMethod]
    public void AnMcpStatusWithNoServerListIsRefusedNamingTheKeyItWants()
    {
        var parsed = Parser.McpStatus(Recorded.Json("{}"));

        Assert.IsFalse(parsed.IsOk);
        StringAssert.Contains(Recorded.Reason(parsed), "mcpServers");
    }

    [TestMethod]
    public void TheSetPermissionModeReplyCarriesTheModeTheBinaryConfirmed()
    {
        var parsed = Parser.PermissionMode(Recorded.SuccessPayload("payloads", "req_4_29cbead3"));

        Assert.IsTrue(parsed.IsOk, Recorded.Reason(parsed));
        Assert.AreEqual(PermissionMode.AcceptEdits, parsed.Value);
        Assert.IsTrue(parsed.Value.IsDefined);
    }

    [TestMethod]
    public void APermissionModeReplyWithNoModeIsRefusedNamingTheKeyItWants()
    {
        var parsed = Parser.PermissionMode(Recorded.Json("{}"));

        Assert.IsFalse(parsed.IsOk);
        StringAssert.Contains(Recorded.Reason(parsed), "mode");
    }

    [TestMethod]
    public void TheRewindReplyCarriesWhetherTheRewindCanRunAndWhatItWouldTouch()
    {
        var parsed = Parser.Rewind(Recorded.SuccessPayload("rewind-files", "req_2_c6dd069a"));

        Assert.IsTrue(parsed.IsOk, Recorded.Reason(parsed));
        Assert.IsTrue(parsed.Value.CanRewind);
        Assert.AreEqual(0, parsed.Value.SkippedLinks);
        Assert.AreEqual(0, parsed.Value.FilesChanged.Count);
        Assert.IsNull(parsed.Value.Insertions);
        Assert.IsNull(parsed.Value.Deletions);
        Assert.IsNull(parsed.Value.Error);

        var counted = Parser.Rewind(Recorded.Json(
            """
            {"canRewind": false, "filesChanged": ["a.cs", "b.cs"], "insertions": 12, "deletions": 3, "skippedLinks": 1, "error": "a file moved"}
            """));

        Assert.IsTrue(counted.IsOk, Recorded.Reason(counted));
        Assert.IsFalse(counted.Value.CanRewind);
        CollectionAssert.AreEqual(new[] { "a.cs", "b.cs" }, counted.Value.FilesChanged.ToArray());
        Assert.AreEqual(12, counted.Value.Insertions);
        Assert.AreEqual(3, counted.Value.Deletions);
        Assert.AreEqual(1, counted.Value.SkippedLinks);
        Assert.AreEqual("a file moved", counted.Value.Error);
    }

    // The recorded receipt lists nothing still queued and has no cancelled list, since no interrupt
    // this library writes asks the binary to cancel its queue. The declaration's cancelled list, and
    // the empty body an older binary answers with, are written here.
    [TestMethod]
    public void TheInterruptReplyCarriesWhatTheBinaryStillHoldsAndWhatItCancelled()
    {
        var recorded = Body(Parser.Interrupt(Recorded.SuccessPayload("cancel", "req_2_7ec71678")));
        Assert.AreEqual(0, recorded.StillQueued.Count);
        Assert.IsNull(recorded.Cancelled);

        var written = Body(Parser.Interrupt(Recorded.Json("""{"still_queued": ["7c1e0000-0000-4000-8000-000000000001"], "cancelled": ["7c1e0000-0000-4000-8000-000000000002"]}""")));
        CollectionAssert.AreEqual(new[] { "7c1e0000-0000-4000-8000-000000000001" }, written.StillQueued.ToArray());
        CollectionAssert.AreEqual(new[] { "7c1e0000-0000-4000-8000-000000000002" }, written.Cancelled!.ToArray());

        var older = Body(Parser.Interrupt(null));
        Assert.AreEqual(0, older.StillQueued.Count);
        Assert.IsNull(older.Cancelled);
    }

    [TestMethod]
    public void AnInterruptReplyThatIsNotAReceiptIsRefused()
    {
        Assert.IsFalse(Parser.Interrupt(Recorded.Json("""{"still_queued": "7c1e0000"}""")).IsOk);
        Assert.IsFalse(Parser.Interrupt(Recorded.Json("""{"still_queued": [], "cancelled": 3}""")).IsOk);
        Assert.IsFalse(Parser.Interrupt(Recorded.Json("[]")).IsOk);
    }

    // MSTest's own assertion does not hand the value back, and every kind test needs it typed.
    private static TValue Is<TValue>(object? value)
    {
        Assert.IsInstanceOfType<TValue>(value);
        return (TValue)value!;
    }

    private static ServerInfo Info(string fixture, string requestId)
    {
        var parsed = Parser.ServerInfo(Recorded.SuccessPayload(fixture, requestId));
        Assert.IsTrue(parsed.IsOk, Recorded.Reason(parsed));
        return parsed.Value;
    }

    private static ContextUsage Context(string fixture, string requestId)
    {
        var parsed = Parser.ContextUsage(Recorded.SuccessPayload(fixture, requestId));
        Assert.IsTrue(parsed.IsOk, Recorded.Reason(parsed));
        return parsed.Value;
    }

    private static ContextUsage Read(string json) => Body(Parser.ContextUsage(Recorded.Json(json)));

    private static IReadOnlyList<McpServerStatus> Servers(string fixture, string requestId) =>
        Body(Parser.McpStatus(Recorded.SuccessPayload(fixture, requestId)));

    private static IReadOnlyList<McpServerStatus> Servers(string json) =>
        Body(Parser.McpStatus(Recorded.Json(json)));

    private static TValue Body<TValue>(Result<TValue, ParseError> parsed)
    {
        Assert.IsTrue(parsed.IsOk, Recorded.Reason(parsed));
        return parsed.Value;
    }
}
