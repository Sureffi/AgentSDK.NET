namespace Claude.Agent.Tests.Mcp;

// One MCP server as a caller configures it. Which kind it is, is the type: the four factories each
// return their own, and the two kinds only the binary holds are refused before a process starts.
[TestClass]
public sealed class McpServerConfigurationTests
{
    [TestMethod]
    public void EachFactoryReturnsTheKindItNames()
    {
        var stdio = McpServerConfiguration.Stdio("run", "--once");
        Assert.AreEqual("run", stdio.Command);
        CollectionAssert.AreEqual(new[] { "--once" }, stdio.Arguments?.ToArray());
        Assert.IsNull(stdio.EnvironmentVariables);
        Assert.IsNull(stdio.Timeout);
        Assert.IsNull(stdio.AlwaysLoad);

        var full = McpServerConfiguration.Stdio(
            "run",
            ["--once"],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["K"] = "V" },
            TimeSpan.FromSeconds(30),
            alwaysLoad: true);
        Assert.AreEqual("V", full.EnvironmentVariables?["K"]);
        Assert.AreEqual(TimeSpan.FromSeconds(30), full.Timeout);
        Assert.AreEqual(true, full.AlwaysLoad);

        var sse = McpServerConfiguration.Sse(
            new Uri("https://mint.example/sse"),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = "Bearer token" },
            [new McpServerToolPolicy("search", McpToolPermissionPolicy.AlwaysAsk)],
            TimeSpan.FromSeconds(5),
            alwaysLoad: false);
        Assert.AreEqual(new Uri("https://mint.example/sse"), sse.Url);
        Assert.AreEqual("Bearer token", sse.Headers?["Authorization"]);
        Assert.AreEqual(McpToolPermissionPolicy.AlwaysAsk, sse.Tools?.Single().PermissionPolicy);
        Assert.AreEqual(TimeSpan.FromSeconds(5), sse.Timeout);
        Assert.AreEqual(false, sse.AlwaysLoad);

        var http = McpServerConfiguration.Http(new Uri("https://mint.example/mcp"));
        Assert.AreEqual(new Uri("https://mint.example/mcp"), http.Url);
        Assert.IsNull(http.Headers);
        Assert.IsNull(http.Tools);

        var inProcess = McpServerConfiguration.InProcess(new Calc(), "2.0.0", TimeSpan.FromSeconds(2));
        Assert.IsInstanceOfType<Calc>(inProcess.Tools);
        Assert.AreEqual("2.0.0", inProcess.Version);
        Assert.AreEqual(TimeSpan.FromSeconds(2), inProcess.Timeout);
        Assert.IsNull(inProcess.Name);
    }

    // A caller's copy of the environment and the headers: the dictionary a caller passes is theirs
    // to change afterwards, and the configuration does not change with it.
    [TestMethod]
    public void TheEnvironmentAndTheHeadersAreCopiedFromTheCallersDictionary()
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["K"] = "V" };
        var headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["H"] = "1" };
        var stdio = McpServerConfiguration.Stdio("run", null, environment);
        var http = McpServerConfiguration.Http(new Uri("https://mint.example/mcp"), headers);

        environment["K"] = "changed";
        headers["H"] = "2";

        Assert.AreEqual("V", stdio.EnvironmentVariables?["K"]);
        Assert.AreEqual("1", http.Headers?["H"]);
        Assert.IsNull(stdio.Arguments);
    }

    [TestMethod]
    public void AServerWithNothingToReachItByIsRefusedWhereItIsBuilt()
    {
        Assert.ThrowsExactly<ArgumentException>(() => McpServerConfiguration.Stdio(" "));
        Assert.ThrowsExactly<ArgumentNullException>(() => McpServerConfiguration.Sse(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => McpServerConfiguration.Http(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => McpServerConfiguration.InProcess(null!));
        Assert.ThrowsExactly<ArgumentException>(() => McpServerConfiguration.InProcess(new Calc(), " "));
        Assert.ThrowsExactly<ArgumentException>(() => new McpServerToolPolicy(" "));
    }

    [TestMethod]
    public void ANegativeTimeoutIsRefusedBeforeAProcessStarts()
    {
        var refused = Refused(McpServerConfiguration.Stdio("run", null, null, TimeSpan.FromSeconds(-1)));

        StringAssert.Contains(refused.Message, "hive");
        StringAssert.Contains(refused.Message, "negative timeout");
    }

    // A closed set is closed against no value at all: a policy built from a default struct names
    // nothing the binary could read, and saying so at the options is cheaper than a failed start.
    [TestMethod]
    public void AToolPolicySetToNoValueIsRefusedBeforeAProcessStarts()
    {
        var refused = Refused(McpServerConfiguration.Http(
            new Uri("https://mint.example/mcp"),
            tools: [new McpServerToolPolicy("search", default(McpToolPermissionPolicy))]));

        StringAssert.Contains(refused.Message, "tool 'search'");
        StringAssert.Contains(refused.Message, "set a policy or leave it null");
    }

    [TestMethod]
    public void AnOrganizationCeilingSetToNoValueIsRefusedBeforeAProcessStarts()
    {
        var refused = Refused(McpServerConfiguration.Sse(
            new Uri("https://mint.example/sse"),
            tools: [new McpServerToolPolicy("search", null, default(McpToolOrganizationPermission))]));

        StringAssert.Contains(refused.Message, "organization ceiling");
        StringAssert.Contains(refused.Message, "set a ceiling or leave it null");
    }

    // The two kinds the binary reports and this library cannot send back.
    [TestMethod]
    public void AConfigurationOnlyTheBinaryHoldsIsRefusedBeforeAProcessStarts()
    {
        foreach (var configuration in new McpServerConfiguration[]
        {
            ClaudeAgentModelFactory.McpClaudeAIProxyServerConfiguration(new Uri("https://api.example/mcp"), "mcpsrv_1"),
            ClaudeAgentModelFactory.UnknownMcpServerConfiguration("grpc"),
        })
        {
            StringAssert.Contains(Refused(configuration).Message, "cannot send");
        }
    }

    [TestMethod]
    public void EveryKindACallerCanBuildValidates()
    {
        var options = new ClaudeAgentSessionOptions();
        options.McpServers["hive"] = McpServerConfiguration.Stdio("run", "--once");
        options.McpServers["feed"] = McpServerConfiguration.Sse(
            new Uri("https://mint.example/sse"),
            tools: [new McpServerToolPolicy("search", McpToolPermissionPolicy.AlwaysDeny, McpToolOrganizationPermission.Blocked)]);
        options.McpServers["web"] = McpServerConfiguration.Http(new Uri("https://mint.example/mcp"), timeout: TimeSpan.Zero);
        options.McpServers["calc"] = McpServerConfiguration.InProcess(new Calc());

        options.Validate();
    }

    private static ArgumentException Refused(McpServerConfiguration configuration)
    {
        var options = new ClaudeAgentSessionOptions();
        options.McpServers["hive"] = configuration;

        var refused = Assert.ThrowsExactly<ArgumentException>(options.Validate);
        Assert.AreEqual(nameof(ClaudeAgentSessionOptions.McpServers), refused.ParamName);
        return refused;
    }
}
