using System.Collections;
using Claude.Agent.Tests.Architecture;
using Claude.Agent.Tests.Replay;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Claude.Agent.Tests.DependencyInjection;

[TestClass]
public sealed class ConfigurationBindingTests
{
    private const string Schema =
        """{"type":"object","properties":{"answer":{"type":"string"}},"required":["answer"]}""";

    // Options the fixture's ClaudeAgent section cannot carry. Each line says why.
    private static readonly HashSet<string> Absent =
    [
        "StandardError",       // a TextWriter, code-only: no configuration shape can carry it
        "PermissionCallback",  // a delegate, code-only
        "AllSkills",           // mutually exclusive with Skills, which the section carries; the alternate section binds it
        "SessionId",           // which session a call is: a default names none, and so do the five below
        "ResumeSessionId",
        "ContinueLatest",
        "Fork",
        "ResumeAtMessageId",
        "DroppedTurnPromptId",
    ];

    [TestMethod]
    public void TheFixtureBindsToTheOptionsWrittenInCode()
    {
        var bound = Bind("ClaudeAgent");
        var written = InCode();

        foreach (var property in Properties())
        {
            Same(property.GetValue(written), property.GetValue(bound), property.Name);
        }
    }

    // A bool has two values, so one binding cannot give every option a value of its own: two options
    // bound to each other's key read the same, and the fixture above gives six of them true. Each
    // pass reads one bit of an option's position instead, and two positions differ in at least one
    // bit, so a binder that crosses any two fails in some pass. The tool factory's parameters are
    // held the same way (Tools/ToolFactoryTests.cs).
    [TestMethod]
    public void NoTwoBooleanOptionsAreBoundToEachOthersKey()
    {
        var session = Asked().Where(property => property.PropertyType == typeof(bool?)).ToArray();
        var sandbox = typeof(SandboxOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(bool?))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.IsTrue(session.Length >= 7, $"{session.Length} boolean options of a call were found");
        Assert.IsTrue(sandbox.Length >= 7, $"{sandbox.Length} boolean options of the sandbox were found");

        for (var pass = 0; (1 << pass) <= Math.Max(session.Length, sandbox.Length); pass++)
        {
            var asked = new[] { ("Defaults", session), ("Defaults:Sandbox", sandbox) };
            var values = asked
                .SelectMany(group => group.Item2.Select((property, index) =>
                    new KeyValuePair<string, string?>($"{group.Item1}:{property.Name}", Bit(index, pass) ? "true" : "false")))
                .ToArray();

            // Through the binder alone: the options as a whole are refused for pairs the binary would
            // refuse together, and what is under test here is only which key reaches which option.
            var dto = new ConfigurationBuilder().AddInMemoryCollection(values).Build()
                .Get<ClaudeAgentClientOptionsDto>() ?? new ClaudeAgentClientOptionsDto();
            var options = new ClaudeAgentClientOptions();
            OptionsFromConfiguration.Apply(dto, options, "ClaudeAgent");

            foreach (var (name, properties) in asked)
            {
                object holder = name == "Defaults" ? options.Defaults : options.Defaults.Sandbox!;
                for (var index = 0; index < properties.Length; index++)
                {
                    Assert.AreEqual(
                        Bit(index, pass),
                        properties[index].GetValue(holder),
                        $"pass {pass}: {name}:{properties[index].Name} did not take its own key's value");
                }
            }
        }
    }

    private static bool Bit(int index, int pass) => ((index >> pass) & 1) == 0;

    [TestMethod]
    public void TheFixtureLeavesNoOptionAtItsDefault()
    {
        var bound = Bind("ClaudeAgent");
        var fresh = new ClaudeAgentClientOptions();
        var reach = Properties().Where(property => property.Name != nameof(ClaudeAgentClientOptions.Defaults)).ToArray();
        var asked = Asked();
        Assert.AreEqual(6, reach.Length, string.Join(", ", reach.Select(property => property.Name)));
        Assert.IsTrue(asked.Length > 30, $"only {asked.Length} properties were reflected");

        foreach (var property in reach.Where(property => !Absent.Contains(property.Name)))
        {
            AssertSet(property.Name, property.GetValue(fresh), property.GetValue(bound));
        }

        foreach (var property in asked.Where(property => !Absent.Contains(property.Name)))
        {
            AssertSet($"Defaults.{property.Name}", property.GetValue(fresh.Defaults), property.GetValue(bound.Defaults));
        }
    }

    [TestMethod]
    public void TheOtherFormOfEveryKindThatHasOneBinds()
    {
        var bound = Bind("ClaudeAgentAlternate").Defaults;

        Assert.IsNotNull(bound.SystemPrompt);
        Assert.AreEqual("You are Mint.", bound.SystemPrompt.Text);
        Assert.IsFalse(bound.SystemPrompt.IsPreset);
        Assert.AreEqual(true, bound.AllSkills);
        Assert.IsNotNull(bound.Thinking);
        Assert.IsNull(bound.Thinking.BudgetTokens);
        Assert.IsFalse(bound.Thinking.IsDisabled);
        Assert.IsTrue(bound.Thinking.OmitThinking);
    }

    // The two fixtures between them bind two of the three forms of the system prompt and two of the
    // three thinking modes. The third of each is bound here, from the keys alone.
    [TestMethod]
    public void TheThirdFormOfTheSystemPromptAndTheThirdThinkingModeBind()
    {
        var fromFile = Inline(("ClaudeAgent:Defaults:SystemPrompt:File", "/tmp/mint/prompt.md")).Defaults.SystemPrompt;
        Assert.IsNotNull(fromFile);
        Assert.AreEqual("/tmp/mint/prompt.md", fromFile.FilePath);
        Assert.IsNull(fromFile.Text);
        Assert.IsFalse(fromFile.IsPreset);

        var disabled = Inline(("ClaudeAgent:Defaults:Thinking:Mode", "disabled")).Defaults.Thinking;
        Assert.IsNotNull(disabled);
        Assert.IsTrue(disabled.IsDisabled);
        Assert.IsNull(disabled.BudgetTokens);
    }

    // An agent's effort is either a level the binary names or a count of tokens, and the wire takes
    // the count as a number. A configuration carries both as text, so which one a value is comes from
    // reading it: the fixture's agent names a level, and this one names a count.
    [TestMethod]
    public void AnAgentsEffortAsATokenCountBindsAsACountAndNotAsALevel()
    {
        var bound = Inline(
            ("ClaudeAgent:Defaults:Agents:bee:Description", "a bee"),
            ("ClaudeAgent:Defaults:Agents:bee:Prompt", "buzz"),
            ("ClaudeAgent:Defaults:Agents:bee:Effort", "2000")).Defaults;

        var agent = bound.Agents["bee"];
        Assert.AreEqual(2000, agent.EffortTokens);
        Assert.IsNull(agent.Effort);
    }

    [TestMethod]
    public void AThinkingModeSpeltTheWrongWayNamesItsKey()
    {
        var refused = Assert.ThrowsExactly<OptionsValidationException>(
            () => Inline(("ClaudeAgent:Defaults:Thinking:Mode", "enabled")));

        StringAssert.Contains(refused.Message, "ClaudeAgent:Defaults:Thinking:Mode");
        StringAssert.Contains(refused.Message, "adaptive, budget or disabled");
    }

    [TestMethod]
    public void AnEmptySectionLeavesTheOptionsAtTheirDefaults()
    {
        var bound = Bind("ClaudeAgentEmpty");
        var fresh = new ClaudeAgentClientOptions();

        foreach (var property in Properties())
        {
            Same(property.GetValue(fresh), property.GetValue(bound), property.Name);
        }

        bound.Validate(TestHostEnvironments.Unmade);
    }

    [TestMethod]
    public async Task AnMcpServerKindTheTreeDoesNotKnowIsReportedAtStart()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>("ClaudeAgent:Defaults:McpServers:hive:Type", "grpc"),
        ]);
        builder.Services.AddSingleton(TestHostEnvironments.Unmade);
        builder.Services.AddClaudeAgentClient(builder.Configuration.GetSection("ClaudeAgent"));

        using var host = builder.Build();

        var refused = await Assert.ThrowsExactlyAsync<OptionsValidationException>(() => host.StartAsync());
        StringAssert.Contains(refused.Message, "ClaudeAgent:Defaults:McpServers:hive:Type");
        StringAssert.Contains(refused.Message, "grpc");
    }

    [TestMethod]
    public void AnInProcessServerInConfigurationIsRefusedNamingTheRegistrationThatTakesIt()
    {
        var refused = Assert.ThrowsExactly<OptionsValidationException>(
            () => Inline(("ClaudeAgent:Defaults:McpServers:calc:Type", "sdk")));

        StringAssert.Contains(refused.Message, "ClaudeAgent:Defaults:McpServers:calc:Type");
        StringAssert.Contains(refused.Message, "AddClaudeTools");
    }

    [TestMethod]
    public void AnMcpServerTimeoutIsMillisecondsAndANegativeOneNamesItsKey()
    {
        var bound = Inline(
            ("ClaudeAgent:Defaults:McpServers:hive:Type", "stdio"),
            ("ClaudeAgent:Defaults:McpServers:hive:Command", "hive-server"),
            ("ClaudeAgent:Defaults:McpServers:hive:Timeout", "45000"));

        Assert.AreEqual(TimeSpan.FromSeconds(45), bound.Defaults.McpServers["hive"].Timeout);

        var refused = Assert.ThrowsExactly<OptionsValidationException>(() => Inline(
            ("ClaudeAgent:Defaults:McpServers:hive:Type", "stdio"),
            ("ClaudeAgent:Defaults:McpServers:hive:Command", "hive-server"),
            ("ClaudeAgent:Defaults:McpServers:hive:Timeout", "-1")));

        StringAssert.Contains(refused.Message, "ClaudeAgent:Defaults:McpServers:hive:Timeout");
        StringAssert.Contains(refused.Message, "negative");
    }

    [TestMethod]
    public void AnEmptyToolPolicyFailsAtHostStartNamingItsKey()
    {
        var refused = Assert.ThrowsExactly<OptionsValidationException>(() => Inline(
            ("ClaudeAgent:Defaults:McpServers:web:Type", "http"),
            ("ClaudeAgent:Defaults:McpServers:web:Url", "https://mint.example/mcp"),
            ("ClaudeAgent:Defaults:McpServers:web:Tools:0:Name", "search"),
            ("ClaudeAgent:Defaults:McpServers:web:Tools:0:PermissionPolicy", string.Empty)));

        StringAssert.Contains(refused.Message, "ClaudeAgent:Defaults:McpServers:web:Tools:0:PermissionPolicy");
        StringAssert.Contains(refused.Message, "a tool policy");
    }

    // A closed set binds as it stands: a value this library names and one it does not both reach
    // the options, since the binary decides what it accepts.
    [TestMethod]
    public void APermissionModeBindsWhetherTheLibraryNamesItOrNot()
    {
        var named = Inline(("ClaudeAgent:Defaults:PermissionMode", "acceptEdits")).Defaults.PermissionMode;
        Assert.AreEqual(PermissionMode.AcceptEdits, named);
        Assert.IsTrue(named!.Value.IsDefined);

        var unnamed = Inline(("ClaudeAgent:Defaults:PermissionMode", "accept-edits")).Defaults.PermissionMode;
        Assert.AreEqual("accept-edits", unnamed.ToString());
        Assert.IsFalse(unnamed!.Value.IsDefined);
    }

    [TestMethod]
    public void AnEffortBindsWhetherTheLibraryNamesItOrNot()
    {
        var named = Inline(("ClaudeAgent:Defaults:Effort", "xhigh")).Defaults.Effort;
        Assert.AreEqual(Effort.XHigh, named);
        Assert.IsTrue(named!.Value.IsDefined);

        var unnamed = Inline(("ClaudeAgent:Defaults:Effort", "colossal")).Defaults.Effort;
        Assert.AreEqual("colossal", unnamed.ToString());
        Assert.IsFalse(unnamed!.Value.IsDefined);
    }

    // The one value a closed set cannot bind: an empty string names nothing to send.
    [TestMethod]
    public void AnEmptyPermissionModeFailsAtHostStartNamingItsKey()
    {
        var refused = Assert.ThrowsExactly<OptionsValidationException>(
            () => Inline(("ClaudeAgent:Defaults:PermissionMode", string.Empty)));

        StringAssert.Contains(refused.Message, "ClaudeAgent:Defaults:PermissionMode");
    }

    [TestMethod]
    public void AnEmptySettingSourceFailsAtHostStartNamingItsKey()
    {
        var refused = Assert.ThrowsExactly<OptionsValidationException>(
            () => Inline(("ClaudeAgent:Defaults:SettingSources:0", string.Empty)));

        StringAssert.Contains(refused.Message, "ClaudeAgent:Defaults:SettingSources:0");
    }

    [TestMethod]
    public void AnOptionValidateRefusesIsReportedAsAnOptionsFailure()
    {
        var refused = Assert.ThrowsExactly<OptionsValidationException>(
            () => Inline(("ClaudeAgent:Defaults:MaxTurns", "-1")));

        StringAssert.Contains(refused.Message, "MaxTurns");
    }

    private static ClaudeAgentClientOptions Bind(string section)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(Fixture.Root, "appsettings.json"))
            .Build();

        return Read(configuration.GetSection(section));
    }

    private static ClaudeAgentClientOptions Inline(params (string Key, string? Value)[] values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();

        return Read(configuration.GetSection("ClaudeAgent"));
    }

    private static ClaudeAgentClientOptions Read(IConfiguration section)
    {
        var services = new ServiceCollection();
        services.AddSingleton(TestHostEnvironments.Unmade);
        services.AddClaudeAgentClient(section);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ClaudeAgentClientOptions>>().Value;
    }

    private static ClaudeAgentClientOptions InCode()
    {
        var options = new ClaudeAgentClientOptions
        {
            ExecutablePath = "/usr/local/bin/claude",
            ProcessGracePeriod = TimeSpan.FromSeconds(3),
            MaxMessageSize = 2097152,
            ConfigurationDirectory = "/tmp/mint/claude-agent",
        };

        var asked = options.Defaults;
        asked.WorkingDirectory = "/tmp/mint";
        asked.SystemPrompt = AgentSystemPrompt.Preset("Answer in one short paragraph.", excludeDynamicSections: true);
        asked.Model = "sonnet";
        asked.FallbackModel = "haiku";
        asked.Effort = Effort.High;
        asked.Thinking = ThinkingOptions.WithBudget(4096);
        asked.MaxTurns = 8;
        asked.MaxBudgetUsd = 12.50m;
        asked.TaskBudget = 3;
        asked.Tools = ["Read", "Write"];
        asked.PermissionMode = PermissionMode.AcceptEdits;
        asked.PermissionPromptTool = "mcp__calc__ask";
        asked.OnlyConfiguredMcpServers = true;
        asked.Settings = "/tmp/mint/settings.json";
        asked.SettingSources = [SettingSource.Project, SettingSource.Local];
        asked.Sandbox = new SandboxOptions
        {
            Enabled = true,
            AutoAllowBashIfSandboxed = true,
            ExcludedCommands = ["git push"],
            AllowUnsandboxedCommands = false,
            EnableWeakerNestedSandbox = true,
            AllowedDomains = ["api.anthropic.com"],
            DeniedDomains = ["telemetry.example"],
            AllowManagedDomainsOnly = false,
            AllowUnixSockets = ["/run/mint.sock"],
            AllowAllUnixSockets = false,
            AllowLocalBinding = true,
            AllowMachLookup = ["com.apple.dnssd"],
            HttpProxyPort = 8080,
            SocksProxyPort = 1080,
            IgnoredFileViolations = ["/tmp/mint/cache"],
            IgnoredNetworkViolations = ["logs.example"],
        };
        asked.IncludePartialMessages = true;
        asked.IncludeHookEvents = true;
        asked.ForwardSubagentText = true;
        asked.ReplayUserMessages = true;
        asked.OutputSchema = Element(Schema);
        asked.EnableFileCheckpointing = true;

        options.EnvironmentVariables["MINT_MODE"] = "fixture";
        asked.Betas.Add("context-1m-2025-08-07");
        asked.AllowedTools.Add("Read");
        asked.AllowedTools.Add("mcp__calc__add");
        asked.DisallowedTools.Add("Bash");
        asked.McpServers["hive"] = McpServerConfiguration.Stdio(
            "hive-server",
            ["--stdio"],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["HIVE_TOKEN"] = "token" },
            TimeSpan.FromSeconds(30),
            alwaysLoad: true);
        asked.McpServers["feed"] = McpServerConfiguration.Sse(
            new Uri("https://mint.example/sse"),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = "Bearer token" },
            [new McpServerToolPolicy("watch", McpToolPermissionPolicy.AlwaysAsk)],
            TimeSpan.FromSeconds(15),
            alwaysLoad: false);
        asked.McpServers["web"] = McpServerConfiguration.Http(
            new Uri("https://mint.example/mcp"),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = "Bearer token" },
            [new McpServerToolPolicy("search", McpToolPermissionPolicy.AlwaysAllow, McpToolOrganizationPermission.Ask)]);
        asked.Agents["bee"] = new AgentDefinition("reads the hive", "You are a bee.")
        {
            Tools = ["Read"],
            DisallowedTools = ["Bash"],
            Model = "haiku",
            Skills = ["blueprint"],
            Memory = "/tmp/mint/bee.md",
            McpServers = ["hive"],
            InitialPrompt = "buzz",
            MaxTurns = 2,
            Background = true,
            Effort = Effort.Low,
            PermissionMode = PermissionMode.Plan,
        };
        asked.Skills.Add("blueprint");
        asked.PluginDirectories.Add("/tmp/mint/plugins/hive");
        asked.AdditionalDirectories.Add("/tmp/mint/docs");
        return options;
    }

    private static JsonElement Element(string text)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static PropertyInfo[] Properties() =>
        [.. typeof(ClaudeAgentClientOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(property => property.Name, StringComparer.Ordinal)];

    private static PropertyInfo[] Asked() =>
        [.. typeof(ClaudeAgentSessionOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(property => property.Name, StringComparer.Ordinal)];

    private static void AssertSet(string name, object? fresh, object? bound)
    {
        var message = $"{name} is still at its default; the fixture forgot it.";

        switch (fresh)
        {
            case null:
                Assert.IsNotNull(bound, message);
                break;

            case ICollection collection:
                Assert.IsTrue(collection.Count == 0 && ((ICollection)bound!).Count > 0, message);
                break;

            default:
                Assert.AreNotEqual(fresh, bound, message);
                break;
        }
    }

    private static void Same(object? expected, object? actual, string at)
    {
        if (expected is null || actual is null)
        {
            Assert.AreEqual(expected, actual, at);
            return;
        }

        if (expected is JsonElement element)
        {
            Assert.AreEqual(element.GetRawText(), ((JsonElement)actual).GetRawText(), at);
            return;
        }

        if (expected is Uri url)
        {
            Assert.AreEqual(url.OriginalString, ((Uri)actual).OriginalString, at);
            return;
        }

        if (expected is IDictionary map)
        {
            var other = (IDictionary)actual;
            CollectionAssert.AreEqual(Sorted(map), Sorted(other), $"{at} keys");
            foreach (var key in map.Keys)
            {
                Same(map[key], other[key!], $"{at}:{key}");
            }

            return;
        }

        if (expected is IEnumerable and not string)
        {
            var left = ((IEnumerable)expected).Cast<object?>().ToArray();
            var right = ((IEnumerable)actual).Cast<object?>().ToArray();
            Assert.AreEqual(left.Length, right.Length, $"{at} length");
            for (var index = 0; index < left.Length; index++)
            {
                Same(left[index], right[index], $"{at}:{index}");
            }

            return;
        }

        var type = expected.GetType();

        // A closed set is compared whole, by its value. Walking its properties instead would compare
        // IsDefined alone, since the values it names are static: two different modes would both read
        // as defined and pass.
        if (type.IsPrimitive || type.IsEnum || expected is string or decimal or TimeSpan || ClosedSets.Is(type))
        {
            Assert.AreEqual(expected, actual, at);
            return;
        }

        Assert.AreEqual(type, actual.GetType(), at);
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(property => property.GetIndexParameters().Length == 0))
        {
            Same(property.GetValue(expected), property.GetValue(actual), $"{at}.{property.Name}");
        }
    }

    private static string[] Sorted(IDictionary map) =>
        [.. map.Keys.Cast<object>().Select(key => key.ToString() ?? string.Empty).Order(StringComparer.Ordinal)];
}
