using System.Text.Json;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Tools;

// Every name ToolTypes answers to is founded on a recording, and on a recording only. The general
// founding scan (Architecture/TreeTests.cs) cannot hold this: it matches a name by its value
// anywhere in a stdout, so it misses a name that only ever appears as an item of an init frame's
// tools list, and it would pass a tool name it found in the declared list, which founds no tool name
// (`Bash` is on that list as the value of another field entirely). This is the channel the page's
// tools row means: a recorded init list, or a recorded call. The row's other two conditions are
// held here too: which tools the reference declares, and which declarations are too thin to type.
[TestClass]
public sealed class ToolNameFoundingTests
{
    [TestMethod]
    public void EveryNameTheTypedToolViewsAnswerToIsInARecordedInitListOrARecordedCall()
    {
        var offered = new HashSet<string>(StringComparer.Ordinal);
        var called = new HashSet<string>(StringComparer.Ordinal);
        Recordings(offered, called);

        Assert.IsTrue(offered.Count >= 40, $"{offered.Count} tools offered across the recordings: the scan has lost the init frames");
        Assert.IsTrue(called.Count >= 10, $"{called.Count} tools called across the recordings: the scan has lost the calls");
        Assert.AreNotEqual(0, ToolTypes.Names.Count);

        var unfounded = ToolTypes.Names
            .Where(name => !offered.Contains(name) && !called.Contains(name))
            .ToArray();
        Assert.AreEqual(
            0,
            unfounded.Length,
            $"no recorded init list and no recorded call carries: {string.Join(", ", unfounded)}");
    }

    // The other direction of the same claim: a name no recording carries is not typed, so a typed
    // view cannot be founded on the declaration alone. Nothing this library types answers to a name
    // the reference declares and no recording ever offered or called.
    [TestMethod]
    public void ATypedViewIsNullForADeclaredToolNoRecordingOffersOrCalls()
    {
        var offered = new HashSet<string>(StringComparer.Ordinal);
        var called = new HashSet<string>(StringComparer.Ordinal);
        Recordings(offered, called);

        // Declared by the reference, offered by no recorded init frame and called in no recording.
        string[] unfoundedNames = ["TodoWrite", "ExitPlanMode", "EnterPlanMode", "AskUserQuestion", "ListMcpResourcesTool"];
        using var document = JsonDocument.Parse("""{"file_path": "/notes.txt", "todos": [], "pattern": "*"}""");
        var arguments = document.RootElement;
        foreach (var name in unfoundedNames)
        {
            Assert.IsFalse(offered.Contains(name) || called.Contains(name), $"{name} is founded after all");
            Assert.IsNull(ToolTypes.ReadInput(name, arguments), $"{name} has a typed input and no recording founds it");
            Assert.IsNull(ToolTypes.ReadOutput(name, arguments), $"{name} has a typed output and no recording founds it");
        }
    }

    // A name this library types, said by which recorded channel found it, so the report can name them.
    [TestMethod]
    public void TheAgentToolIsOfferedUnderOneNameAndCalledUnderAnother()
    {
        var offered = new HashSet<string>(StringComparer.Ordinal);
        var called = new HashSet<string>(StringComparer.Ordinal);
        Recordings(offered, called);

        Assert.IsTrue(offered.Contains("Task"), "no recorded init frame offers the Agent tool under its list name");
        Assert.IsFalse(called.Contains("Task"), "a recording calls the Agent tool under its list name after all");
        Assert.IsTrue(called.Contains("Agent"), "no recording calls the Agent tool");
        Assert.IsFalse(offered.Contains("Agent"), "a recorded init frame offers the Agent tool under its call name after all");
    }

    // Sixteen of the names this library answers to are founded by the recorded init lists alone: the
    // binary offers each of them and no recording calls one. Their typed views therefore rest on the
    // declaration and on the hand-written decode tests, and nothing in the recordings holds them;
    // this says so where a reader of the suite will find it.
    [TestMethod]
    public void SixteenToolsAreOfferedByARecordedInitListAndCalledByNoRecording()
    {
        var offered = new HashSet<string>(StringComparer.Ordinal);
        var called = new HashSet<string>(StringComparer.Ordinal);
        Recordings(offered, called);

        string[] names =
        [
            "TaskCreate", "TaskGet", "TaskUpdate", "TaskList",
            "CronCreate", "CronDelete", "CronList", "ScheduleWakeup", "Monitor", "Workflow",
            "EnterWorktree", "ExitWorktree", "PushNotification", "ReportFindings", "Artifact", "RemoteTrigger",
        ];

        // One object carrying the required key of every one of them, so each reads as its own input.
        // The action is which type three of them are, and ExitWorktree has no list among its actions,
        // so it reads an object of its own.
        using var document = JsonDocument.Parse("""
            {"taskId": "task-1", "id": "job-1", "subject": "the task", "description": "the build",
             "cron": "30 14 * * 1", "prompt": "read the mail", "action": "list",
             "message": "the build is green", "status": "proactive", "findings": [], "timeout_ms": 1000}
            """);
        using var exit = JsonDocument.Parse("""{"action": "keep"}""");
        foreach (var name in names)
        {
            Assert.IsTrue(offered.Contains(name), $"no recorded init list offers {name}");
            Assert.IsFalse(called.Contains(name), $"a recording calls {name} after all, so its typed view has a recording to stand on");
            var input = name == "ExitWorktree" ? exit.RootElement : document.RootElement;
            Assert.IsNotNull(ToolTypes.ReadInput(name, input), $"{name} has no typed input");
        }

        CollectionAssert.IsSubsetOf(names, ToolTypes.Names.ToArray());
    }

    // Every tool the reference declares an input for, at the version the fixtures' README pins the
    // list to, under the name the binary gives it where a recording founds that name and under the
    // declaration's own otherwise, each with the declaration it is read against. The Agent
    // declaration is offered as Task and called as Agent. Mcp, the declaration of every MCP server's
    // tool, is not here: the binary names each such tool mcp__<server>__<tool>, and DeclarationOf
    // reads every name of that form against it.
    private static readonly Dictionary<string, string> Declared = new(StringComparer.Ordinal)
    {
        ["Agent"] = "Agent", ["Task"] = "Agent", ["Bash"] = "Bash", ["TaskOutput"] = "TaskOutput",
        ["ExitPlanMode"] = "ExitPlanMode", ["Edit"] = "FileEdit", ["Read"] = "FileRead", ["Write"] = "FileWrite",
        ["Glob"] = "Glob", ["Grep"] = "Grep", ["TaskStop"] = "TaskStop", ["ListMcpResources"] = "ListMcpResources",
        ["RefreshMcpTools"] = "RefreshMcpTools", ["NotebookEdit"] = "NotebookEdit",
        ["ReadMcpResourceDir"] = "ReadMcpResourceDir", ["ReadMcpResource"] = "ReadMcpResource",
        ["ReportFindings"] = "ReportFindings", ["TodoWrite"] = "TodoWrite", ["WebFetch"] = "WebFetch",
        ["WebSearch"] = "WebSearch", ["AskUserQuestion"] = "AskUserQuestion", ["SendFeedback"] = "SendFeedback",
        ["ClaudeDesign"] = "ClaudeDesign", ["Projects"] = "Projects", ["EnterPlanMode"] = "EnterPlanMode",
        ["TaskCreate"] = "TaskCreate", ["TaskGet"] = "TaskGet", ["TaskUpdate"] = "TaskUpdate", ["TaskList"] = "TaskList",
        ["REPL"] = "REPL", ["Workflow"] = "Workflow", ["CronCreate"] = "CronCreate", ["CronDelete"] = "CronDelete",
        ["CronList"] = "CronList", ["ScheduleWakeup"] = "ScheduleWakeup", ["RemoteTrigger"] = "RemoteTrigger",
        ["ShowOnboardingRolePicker"] = "ShowOnboardingRolePicker", ["ReadNotifications"] = "ReadNotifications",
        ["Monitor"] = "Monitor", ["ProposeSkills"] = "ProposeSkills", ["ProposeGoal"] = "ProposeGoal",
        ["Artifact"] = "Artifact", ["PushNotification"] = "PushNotification", ["EnterWorktree"] = "EnterWorktree",
        ["ExitWorktree"] = "ExitWorktree",
    };

    private const string Mcp = "Mcp";

    private const string McpToolPrefix = "mcp__";

    private static string? DeclarationOf(string name) =>
        name.StartsWith(McpToolPrefix, StringComparison.Ordinal) ? Mcp : Declared.GetValueOrDefault(name);

    // What the rule below reads of each declaration, read off it by hand. Open JSON is JSON the
    // declaration gives no keys of: an object of `[k: string]: unknown`, the input being one itself,
    // or `unknown`. A string is a string, whatever it holds: RemoteTrigger's `json`, the service's
    // answer as JSON text, is one of the three scalars its output is, and so RemoteTrigger hands back
    // no open JSON.
    private static readonly string[] SendsOpenJson =
        ["ExitPlanMode", "Mcp", "ClaudeDesign", "TaskCreate", "TaskUpdate", "Workflow", "RemoteTrigger", "Artifact"];

    private static readonly string[] HandsBackOpenJson =
        ["Agent", "FileRead", "Mcp", "Artifact", "Projects", "Bash", "REPL", "ClaudeDesign"];

    // Of the declarations open both ways, those that name the keys of an object inside the input or
    // the output (Artifact's stored page and its listing).
    private static readonly string[] ShapesAnObjectInside = ["Artifact"];

    // The rule: a declaration is too thin to give its body a shape when what the call sends and what
    // it hands back are both open JSON and no object inside either has keys the declaration names —
    // around the open JSON it names scalars and nothing else. Such a tool is read raw.
    private static HashSet<string> TooThin() =>
        [.. SendsOpenJson.Intersect(HandsBackOpenJson, StringComparer.Ordinal).Except(ShapesAnObjectInside, StringComparer.Ordinal)];

    // The rule applied to every declaration. Workflow sends open JSON and hands back a shaped answer;
    // TaskCreate, TaskUpdate and RemoteTrigger the same, RemoteTrigger's answer three scalars; Artifact
    // is open both ways and shapes the objects inside.
    [TestMethod]
    public void TheDeclarationsTooThinToGiveTheirBodyAShapeAreClaudeDesignAndMcp()
    {
        var declarations = Declared.Values.Append(Mcp).ToHashSet(StringComparer.Ordinal);
        Assert.AreEqual(45, declarations.Count, "the reference declares forty-five tool inputs");
        CollectionAssert.IsSubsetOf(SendsOpenJson, declarations.ToArray());
        CollectionAssert.IsSubsetOf(HandsBackOpenJson, declarations.ToArray());
        CollectionAssert.IsSubsetOf(ShapesAnObjectInside, SendsOpenJson.Intersect(HandsBackOpenJson, StringComparer.Ordinal).ToArray());

        CollectionAssert.AreEquivalent(new[] { "ClaudeDesign", "Mcp" }, TooThin().ToArray());
    }

    // The tools row whole: a declared tool under a name a recording founds has a type unless its
    // declaration is too thin, a too-thin one has none, and no type answers to a name the reference
    // does not declare. That no type answers to a name no recording founds is the first test above.
    // The founded tools the rule reads raw are every MCP server's tool the recordings offer or call,
    // the fixtures' own servers' among them, since the Mcp declaration is too thin, and no other.
    // ClaudeDesign, the other too-thin declaration, is founded by no recording.
    [TestMethod]
    public void EveryDeclaredToolUnderAFoundedNameHasATypeUnlessItsDeclarationIsTooThin()
    {
        var offered = new HashSet<string>(StringComparer.Ordinal);
        var called = new HashSet<string>(StringComparer.Ordinal);
        Recordings(offered, called);
        var thin = TooThin();

        var foundedAndThin = new List<string>();
        foreach (var name in Declared.Keys.Union(offered).Union(called).Order(StringComparer.Ordinal))
        {
            if (DeclarationOf(name) is not { } declaration)
            {
                continue;
            }

            var founded = offered.Contains(name) || called.Contains(name);
            var typed = ToolTypes.Names.Contains(name);
            if (thin.Contains(declaration))
            {
                Assert.IsFalse(typed, $"{name}'s declaration is too thin to give its body a shape and it has a type");
                if (founded)
                {
                    foundedAndThin.Add(name);
                }
            }
            else if (founded)
            {
                Assert.IsTrue(typed, $"{name} is declared, a recording founds it, and it has no type");
            }
        }

        CollectionAssert.IsSubsetOf(ToolTypes.Names.ToArray(), Declared.Keys.ToArray(), "a type answers to a name the reference does not declare");

        var mcpTools = foundedAndThin.Where(name => DeclarationOf(name) == Mcp).ToArray();
        CollectionAssert.AreEqual(Array.Empty<string>(), foundedAndThin.Except(mcpTools).ToArray());
        CollectionAssert.IsSubsetOf(new[] { "mcp__calc__add", "mcp__echo__echo", "mcp__slow__wait" }, mcpTools);
    }

    // RemoteTrigger is declared by the reference and offered by the recorded init lists, and no
    // recording calls it. The body it sends a service is JSON the declaration gives no shape, and the
    // service's answer comes back as a string, which is a string whatever it holds: the output is three
    // scalars, so the declaration is not too thin and the tool has a type, its answer read as the text
    // it is and never read inside.
    [TestMethod]
    public void RemoteTriggerIsOfferedByARecordedInitListAndTypedItsAnswerAString()
    {
        var offered = new HashSet<string>(StringComparer.Ordinal);
        var called = new HashSet<string>(StringComparer.Ordinal);
        Recordings(offered, called);

        Assert.IsTrue(offered.Contains("RemoteTrigger"), "no recorded init list offers RemoteTrigger");
        Assert.IsFalse(called.Contains("RemoteTrigger"), "a recording calls RemoteTrigger after all");
        CollectionAssert.Contains(ToolTypes.Names.ToArray(), "RemoteTrigger");

        using var input = JsonDocument.Parse("""{"action": "get", "trigger_id": "trigger-1"}""");
        using var output = JsonDocument.Parse("""{"status": 200, "json": "{\"triggers\":[]}"}""");
        Assert.AreEqual("trigger-1", ((RemoteTriggerGetToolInput)ToolTypes.ReadInput("RemoteTrigger", input.RootElement)!).TriggerId);
        var answer = (RemoteTriggerToolOutput)ToolTypes.ReadOutput("RemoteTrigger", output.RootElement)!;
        Assert.AreEqual(200L, answer.Status);
        Assert.AreEqual("""{"triggers":[]}""", answer.Json);
    }

    // The tool names of every recorded stdout: what an init frame offered, and what a call named —
    // a tool_use block, a hook's tool_name, or a permission request's.
    private static void Recordings(HashSet<string> offered, HashSet<string> called)
    {
        foreach (var file in Directory.EnumerateFiles(Fixture.Root, "*.stdout.jsonl"))
        {
            foreach (var line in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(line);
                var frame = document.RootElement;
                Offered(frame, offered);
                Called(frame, called);
            }
        }
    }

    private static void Offered(JsonElement frame, HashSet<string> offered)
    {
        if (frame.TryGetProperty("type", out var type)
            && type.ValueKind == JsonValueKind.String
            && type.GetString() == "system"
            && frame.TryGetProperty("subtype", out var subtype)
            && subtype.ValueKind == JsonValueKind.String
            && subtype.GetString() == "init"
            && frame.TryGetProperty("tools", out var tools)
            && tools.ValueKind == JsonValueKind.Array)
        {
            foreach (var tool in tools.EnumerateArray().Where(tool => tool.ValueKind == JsonValueKind.String))
            {
                offered.Add(tool.GetString()!);
            }
        }
    }

    private static void Called(JsonElement frame, HashSet<string> called)
    {
        if (frame.TryGetProperty("message", out var message)
            && message.ValueKind == JsonValueKind.Object
            && message.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                if (block.ValueKind == JsonValueKind.Object
                    && block.TryGetProperty("type", out var kind)
                    && kind.ValueKind == JsonValueKind.String
                    && kind.GetString() == "tool_use"
                    && block.TryGetProperty("name", out var name)
                    && name.ValueKind == JsonValueKind.String)
                {
                    called.Add(name.GetString()!);
                }
            }
        }

        if (!frame.TryGetProperty("request", out var request) || request.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        Named(request, called);
        if (request.TryGetProperty("input", out var input) && input.ValueKind == JsonValueKind.Object)
        {
            Named(input, called);
        }
    }

    private static void Named(JsonElement element, HashSet<string> called)
    {
        if (element.TryGetProperty("tool_name", out var name) && name.ValueKind == JsonValueKind.String)
        {
            called.Add(name.GetString()!);
        }
    }
}
