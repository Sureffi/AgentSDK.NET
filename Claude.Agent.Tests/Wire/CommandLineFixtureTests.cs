using System.Text.Json.Nodes;
using Claude.Agent.Recorder;
using Claude.Agent.Tests.Mcp;
using Claude.Agent.Tests.Replay;
using Claude.Agent.Tests.Sessions;
namespace Claude.Agent.Tests.Wire;

[TestClass]
public sealed class CommandLineFixtureTests
{
    private const string RecordedCwd = "/tmp/claude-agent-fixtures/cwd";
    private const string ForkParentSessionId = "63bf45b5-1f73-4220-80a6-9518f49b968e";

    [TestMethod]
    public void TheTrivialFixtureArgvIsOneTurnWithTheSettingSourcesTurnedOff()
    {
        var options = Recording();
        options.Model = "haiku";
        options.MaxTurns = 1;

        AssertArgv("trivial", options);
    }

    [TestMethod]
    public void TheToolUseFixtureArgvNamesTheReadToolTwice()
    {
        var options = Recording();
        options.Model = "haiku";
        options.Tools = ["Read"];
        options.AllowedTools.Add("Read");
        options.MaxTurns = 3;
        options.Effort = Effort.Low;

        AssertArgv("tool-use", options);
    }

    [TestMethod]
    public void TheHooksFixtureArgvRoutesPermissionsOverStdioAndAsksForHookEvents()
    {
        var options = Recording();
        options.Model = "haiku";
        options.Tools = ["Read"];
        options.MaxTurns = 3;
        options.Effort = Effort.Low;
        options.IncludeHookEvents = true;

        AssertArgv("hooks", options, routePermissionsOverStdio: true);
    }

    [TestMethod]
    public void TheStreamingFixtureArgvAsksForPartialMessages()
    {
        var options = Recording();
        options.Model = "haiku";
        options.SystemPrompt = AgentSystemPrompt.FromText("Answer in one short sentence.");
        options.MaxTurns = 1;
        options.IncludePartialMessages = true;

        AssertArgv("streaming", options);
    }

    [TestMethod]
    public void TheClientFixtureArgvAppendsToThePresetSystemPrompt()
    {
        var options = Recording();
        options.Model = "haiku";
        options.SystemPrompt = AgentSystemPrompt.Preset("Be terse.");
        options.MaxTurns = 1;
        options.Effort = Effort.Low;

        AssertArgv("client", options);
    }

    [TestMethod]
    public void TheSdkMcpFixtureArgvCarriesTheInProcessServerAsJson()
    {
        var options = Recording();
        options.Model = "haiku";
        options.Tools = [];
        options.AllowedTools.Add("mcp__calc__add");
        options.MaxTurns = 3;
        options.Effort = Effort.Low;
        options.McpServers["calc"] = McpServerConfiguration.InProcess(new Calc());

        AssertArgv("sdk-mcp", options);
    }

    [TestMethod]
    public void TheCanUseToolFixtureArgvRoutesPermissionsOverStdioWithNoAllowedTools()
    {
        var options = Recording();
        options.Model = "haiku";
        options.Tools = ["Write"];
        options.MaxTurns = 3;
        options.Effort = Effort.Low;

        AssertArgv("can-use-tool", options, routePermissionsOverStdio: true);
    }

    [TestMethod]
    public void TheRewindFilesFixtureArgvReplaysUserMessages()
    {
        var options = Recording();
        options.Model = "haiku";
        options.Tools = ["Write"];
        options.AllowedTools.Add("Write");
        options.MaxTurns = 3;
        options.Effort = Effort.Low;
        options.EnableFileCheckpointing = true;
        options.ReplayUserMessages = true;

        AssertArgv("rewind-files", options);
    }

    [TestMethod]
    public void TheStopTaskFixtureArgvIsOneBareTurn()
    {
        var options = Recording();
        options.Model = "haiku";
        options.MaxTurns = 1;
        options.Effort = Effort.Low;

        AssertArgv("stop-task", options);
    }

    [TestMethod]
    public void TheMcpVerbFixturesArgvCarryTheInProcessServer()
    {
        var options = Recording();
        options.Model = "haiku";
        options.Tools = [];
        options.AllowedTools.Add("mcp__calc__add");
        options.MaxTurns = 1;
        options.Effort = Effort.Low;
        options.McpServers["calc"] = McpServerConfiguration.InProcess(new Calc());

        AssertArgv("mcp-reconnect", options);
        AssertArgv("mcp-toggle", options);
    }

    [TestMethod]
    public void TheForkFixtureArgvResumesTheParentAndForks()
    {
        var options = Recording();
        options.Model = "haiku";
        options.MaxTurns = 1;
        options.Effort = Effort.Low;

        AssertArgv("fork", options, new ClaudeAgentSessionOptions { ResumeSessionId = ForkParentSessionId, Fork = true });
    }

    [TestMethod]
    public void TheResumeFixtureArgvResumesTheParent()
    {
        var options = Recording();
        options.Model = "haiku";
        options.MaxTurns = 1;
        options.Effort = Effort.Low;

        AssertArgv("resume", options, new ClaudeAgentSessionOptions { ResumeSessionId = ForkParentSessionId });
    }

    [TestMethod]
    public void TheTaskFixturesArgvAllowTheAgentToolAndAShellOrARead()
    {
        var options = Recording();
        options.Model = "haiku";
        options.Tools = ["Bash", "Agent"];
        options.AllowedTools.Add("Bash");
        options.AllowedTools.Add("Agent");
        options.MaxTurns = 3;
        options.Effort = Effort.Low;

        AssertArgv("task", options);

        var background = Recording();
        background.Model = "haiku";
        background.Tools = ["Agent", "Read"];
        background.AllowedTools.Add("Agent");
        background.AllowedTools.Add("Read");
        background.MaxTurns = 3;
        background.Effort = Effort.Low;

        AssertArgv("task-background", background);
    }

    [TestMethod]
    public void TheHookEventsFixtureArgvCarriesTheSettingsHooksAsJsonAndAsksForHookEvents()
    {
        var options = Recording();
        options.Model = "haiku";
        options.Tools = ["Read"];
        options.AllowedTools.Add("Read");
        options.MaxTurns = 3;
        options.Effort = Effort.Low;
        options.IncludeHookEvents = true;
        options.Settings =
            """{"hooks": {"PreToolUse": [{"matcher": "Read", "hooks": [{"type": "command", "command": "exit 0"}]}], "PostToolUse": [{"matcher": "Read", "hooks": [{"type": "command", "command": "exit 0"}]}]}}""";

        AssertArgv("hook-events", options);
    }

    [TestMethod]
    public void TheHooksMoreFixtureArgvRoutesPermissionsOverStdioForTheToolNotAllowed()
    {
        var options = Recording();
        options.Model = "haiku";
        options.Tools = ["Read", "Agent", "Write"];
        options.AllowedTools.Add("Read");
        options.AllowedTools.Add("Agent");
        options.MaxTurns = 6;
        options.Effort = Effort.Low;

        AssertArgv("hooks-more", options, routePermissionsOverStdio: true);
    }

    [TestMethod]
    public void TheClientTurnFixturesArgvAreOneBareTurnEach()
    {
        var options = Recording();
        options.Model = "haiku";
        options.MaxTurns = 1;
        options.Effort = Effort.Low;

        AssertArgv("compact", options);
        AssertArgv("conversation-reset", options);
    }

    [TestMethod]
    public void TheCancelFixtureArgvRoutesPermissionsOverStdioWithNothingAllowed()
    {
        var options = Recording();
        options.Model = "haiku";
        options.Tools = ["Read", "Write"];
        options.MaxTurns = 3;
        options.Effort = Effort.Low;

        AssertArgv("cancel", options, routePermissionsOverStdio: true);
    }

    [TestMethod]
    public void TheDeniedFixtureArgvOffersWriteWithNoHandlerAndNothingAllowed()
    {
        var options = Recording();
        options.Model = "haiku";
        options.Tools = ["Write"];
        options.MaxTurns = 2;
        options.Effort = Effort.Low;

        AssertArgv("denied", options);
    }

    [TestMethod]
    public void TheDeferFixtureArgvIsTheToolUseFixturesWithAHookInTheInitialize()
    {
        var options = Recording();
        options.Model = "haiku";
        options.Tools = ["Read"];
        options.AllowedTools.Add("Read");
        options.MaxTurns = 3;
        options.Effort = Effort.Low;

        AssertArgv("defer", options);
    }

    [TestMethod]
    public void TheStructuredFixtureArgvCarriesTheThinkingBudgetAndTheSchemaInTheRecordedSpacing()
    {
        var options = Recording();
        options.Model = "haiku";
        options.MaxTurns = 1;
        options.Effort = Effort.Medium;
        options.Thinking = ThinkingOptions.WithBudget(1024);
        options.OutputSchema = Recorded.Json(
            """{"type": "object", "properties": {"word": {"type": "string"}, "letters": {"type": "integer"}}, "required": ["word", "letters"], "additionalProperties": false}""");

        AssertArgv("structured", options);
    }

    // The schema the binary answered on is the one SetOutputSchema<T> takes from the type today, byte
    // for byte, through reflection and through a source-generated contract alike.
    [TestMethod]
    public void TheSchemaTypeFixtureArgvCarriesTheSchemaTakenFromTheType()
    {
        var options = SchemaRecording();
        options.SetOutputSchema<WordReport>();

        AssertArgv("schema-type", options);

        options.SetOutputSchema(WordReportContext.Default.WordReport);

        AssertArgv("schema-type", options);
    }

    // The same schema with the root's type made ["object", "null"] and nothing else changed. Validate
    // refuses that root; the argv is built from the options as the recorder's transport was handed
    // them, past the refusal.
    [TestMethod]
    public void TheSchemaNullableFixtureArgvIsTheSchemaTypeFixturesWithANullableRoot()
    {
        var options = SchemaRecording();
        options.SetOutputSchema<WordReport>();
        var schema = JsonNode.Parse(options.OutputSchema!.Value.GetRawText())!.AsObject();
        schema["type"] = new JsonArray("object", "null");
        options.OutputSchema = Recorded.Json(schema.ToJsonString());

        AssertArgv("schema-nullable", options);
    }

    [TestMethod]
    public void TheWebSearchFixtureArgvNamesWebSearchTwice()
    {
        var options = Recording();
        options.Model = "haiku";
        options.Tools = ["WebSearch"];
        options.AllowedTools.Add("WebSearch");
        options.MaxTurns = 2;
        options.Effort = Effort.Low;

        AssertArgv("web-search", options);
    }

    [TestMethod]
    public void TheDefaultsFixtureArgvNamesNoToolsAndNoEffort()
    {
        var options = Recording();
        options.Model = "haiku";
        options.MaxTurns = 1;

        AssertArgv("defaults", options);
    }

    [TestMethod]
    public void TheToolsFixtureArgvNamesTheNineBuiltInToolsTwice()
    {
        string[] tools = ["Glob", "Grep", "Read", "Write", "Edit", "NotebookEdit", "TodoWrite", "WebFetch", "Bash"];
        var options = Recording();
        options.Model = "haiku";
        options.Tools = tools;
        foreach (var tool in tools)
        {
            options.AllowedTools.Add(tool);
        }

        options.MaxTurns = 16;
        options.Effort = Effort.Low;

        AssertArgv("tools", options);
    }

    [TestMethod]
    public void TheTaskToolsFixtureArgvNamesBashTaskOutputAndTaskStopTwice()
    {
        string[] tools = ["Bash", "TaskOutput", "TaskStop"];
        var options = Recording();
        options.Model = "haiku";
        options.Tools = tools;
        foreach (var tool in tools)
        {
            options.AllowedTools.Add(tool);
        }

        options.MaxTurns = 6;
        options.Effort = Effort.Low;

        AssertArgv("task-tools", options);
    }

    [TestMethod]
    public void ThePayloadsFixtureArgvCarriesTheInProcessAndTheStdioServerUnderStrictMcpConfig()
    {
        var options = Recording();
        options.Model = "haiku";
        options.Tools = [];
        options.MaxTurns = 1;
        options.Effort = Effort.Low;
        options.McpServers["calc"] = McpServerConfiguration.InProcess(new Calc());
        options.McpServers["echo"] = McpServerConfiguration.Stdio($"{RecordedCwd}/echo-server");
        options.OnlyConfiguredMcpServers = true;

        AssertArgv("payloads", options);
    }

    [TestMethod]
    public void TheContinuationFixtureArgvAllowsBashForFourTurns()
    {
        var options = Recording();
        options.Model = "haiku";
        options.Tools = ["Bash"];
        options.AllowedTools.Add("Bash");
        options.MaxTurns = 4;
        options.Effort = Effort.Low;

        AssertArgv("continuation", options);
    }

    [TestMethod]
    public void TheReadsFixtureArgvNamesTheReadToolTwiceForTenTurns()
    {
        var options = Recording();
        options.Model = "haiku";
        options.Tools = ["Read"];
        options.AllowedTools.Add("Read");
        options.MaxTurns = 10;
        options.Effort = Effort.Low;

        AssertArgv("reads", options);
    }

    [TestMethod]
    public void TheEditsFixtureArgvNamesTheFiveToolsTwiceForSixteenTurns()
    {
        string[] tools = ["Glob", "Grep", "Write", "Edit", "Bash"];
        var options = Recording();
        options.Model = "haiku";
        options.Tools = tools;
        foreach (var tool in tools)
        {
            options.AllowedTools.Add(tool);
        }

        options.MaxTurns = 16;
        options.Effort = Effort.Low;

        AssertArgv("edits", options);
    }

    [TestMethod]
    public void TheThinkingFixtureArgvCarriesTheBudgetAndNoDisplay()
    {
        var options = Recording();
        options.Model = "haiku";
        options.MaxTurns = 1;
        options.Thinking = ThinkingOptions.WithBudget(2048);

        AssertArgv("thinking", options);
    }

    [TestMethod]
    public void TheMcpCancelFixtureArgvCarriesTheSlowInProcessServerAndAllowsItsTool()
    {
        var options = Recording();
        options.Model = "haiku";
        options.Tools = [];
        options.AllowedTools.Add("mcp__slow__wait");
        options.MaxTurns = 3;
        options.Effort = Effort.Low;
        options.McpServers["slow"] = McpServerConfiguration.InProcess(new SlowTool());

        AssertArgv("mcp-cancel", options);
    }

    [TestMethod]
    public void TheBackgroundStopFixtureArgvOffersAndAllowsBashAlone()
    {
        var options = Recording();
        options.Model = "haiku";
        options.Tools = ["Bash"];
        options.AllowedTools.Add("Bash");
        options.MaxTurns = 3;
        options.Effort = Effort.Low;

        AssertArgv("background-stop", options);
    }

    [TestMethod]
    public void TheBackgroundInterruptFixtureArgvOffersAndAllowsBashAlone()
    {
        var options = Recording();
        options.Model = "haiku";
        options.Tools = ["Bash"];
        options.AllowedTools.Add("Bash");
        options.MaxTurns = 3;
        options.Effort = Effort.Low;

        AssertArgv("background-interrupt", options);
    }

    private static ClaudeAgentSessionOptions Recording() => new()
    {
        WorkingDirectory = RecordedCwd,
        SettingSources = [],
    };

    private static ClaudeAgentSessionOptions SchemaRecording()
    {
        var options = Recording();
        options.Model = "haiku";
        options.MaxTurns = 2;
        options.Effort = Effort.Low;
        return options;
    }

    private static void AssertArgv(
        string fixture,
        ClaudeAgentSessionOptions options,
        ClaudeAgentSessionOptions? session = null,
        bool routePermissionsOverStdio = false)
    {
        var built = CommandLine.Build(
            ClaudeAgentSessionOptions.Layer(options, session),
            new TransportNeeds(routePermissionsOverStdio),
            _ => null);

        CollectionAssert.AreEqual(Fixture.Load(fixture).Argv.ToArray(), built.ToArray(), $"{fixture}.argv.json");
    }
}
