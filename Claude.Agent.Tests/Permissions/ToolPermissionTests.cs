using System.Text.Json;
using Claude.Agent.Tests.Wire;

namespace Claude.Agent.Tests.Permissions;

// The permission callback's two halves: the request the binary asks with, and the result the caller
// answers with. Seven of the request's members are declared and never recorded, so the line below is
// this file's own; decision_reason_type is recorded, and is held on the recording instead.
[TestClass]
public sealed class ToolPermissionTests
{
    [TestMethod]
    public void APermissionRequestCarriesTheCallTheServerTheMatchedRuleAndWhatTheBinaryAlreadyDecided()
    {
        var request = Asked(
            """
            {"type":"control_request","request_id":"req_permission_1","request":{"subtype":"can_use_tool","tool_name":"Bash","input":{"command":"git status"},"tool_use_id":"toolu_permission_1","agent_id":"agent-9","blocked_path":"/etc/hosts","decision_reason":"no rule reaches it","decision_reason_type":"workingDir","title":"Run git status?","display_name":"Bash","description":"git status","mcp_server":{"name":"notes","source":"project"},"classifier_approvable":true,"suppress_always_allow_rule":false,"default_to_no":true,"requires_user_interaction":false,"matched_ask_rule":{"source":"projectSettings","tool_name":"Bash","rule_content":"git:*"},"permission_suggestions":[{"type":"addRules","rules":[{"toolName":"Bash","ruleContent":"git status:*"}],"behavior":"allow","destination":"session"}]}}
            """);

        Assert.AreEqual("Bash", request.ToolCall.Name);
        Assert.AreEqual("toolu_permission_1", request.ToolCall.Id);
        Assert.AreEqual(ToolCallStatus.Started, request.ToolCall.Status);
        Assert.AreEqual("git status", request.ToolCall.RawInput.GetProperty("command").GetString());

        Assert.AreEqual("agent-9", request.AgentId);
        Assert.AreEqual("/etc/hosts", request.BlockedPath);
        Assert.AreEqual("no rule reaches it", request.DecisionReason);
        Assert.AreEqual(PermissionDecisionReasonType.WorkingDirectory, request.DecisionReasonType);
        Assert.AreEqual("Run git status?", request.Title);
        Assert.AreEqual("Bash", request.DisplayName);
        Assert.AreEqual("git status", request.Description);
        Assert.AreEqual("notes", request.McpServer!.Name);
        Assert.AreEqual(new McpServerSource("project"), request.McpServer.Source);
        Assert.IsFalse(request.McpServer.Source.IsDefined, "project is declared and never recorded, so the library does not name it");
        Assert.IsTrue(request.IsClassifierApprovable);
        Assert.IsFalse(request.SuppressAlwaysAllowRule);
        Assert.IsTrue(request.DefaultsToNo);
        Assert.IsFalse(request.RequiresUserInteraction);

        Assert.AreEqual(PermissionRuleSource.ProjectSettings, request.MatchedRule!.Source);
        Assert.IsTrue(request.MatchedRule.Source.IsDefined, "projectSettings is one of the eleven sources the reference declares a rule to come from");
        Assert.AreEqual("Bash", request.MatchedRule.ToolName);
        Assert.AreEqual("git:*", request.MatchedRule.RuleContent);

        var suggestion = (AddRulesPermissionUpdate)request.Suggestions.Single();
        Assert.AreEqual(PermissionRuleAction.Allow, suggestion.Behavior);
        Assert.AreEqual("git status:*", suggestion.Rules.Single().RuleContent);
    }

    // What the binary did not send is null, not empty, and a matched rule missing its source or its
    // tool is no rule at all.
    [TestMethod]
    public void TheRequestMembersTheBinaryDidNotSendAreNullAndAHalfMatchedRuleIsNone()
    {
        var bare = Asked(
            """
            {"type":"control_request","request_id":"req_permission_2","request":{"subtype":"can_use_tool","tool_name":"Read","input":{"file_path":"/srv/build/log.txt"}}}
            """);

        Assert.AreEqual(string.Empty, bare.ToolCall.Id);
        Assert.AreEqual(0, bare.Suggestions.Count);
        Assert.IsNull(bare.McpServer);
        Assert.IsNull(bare.DecisionReasonType);
        Assert.IsNull(bare.IsClassifierApprovable);
        Assert.IsNull(bare.SuppressAlwaysAllowRule);
        Assert.IsNull(bare.DefaultsToNo);
        Assert.IsNull(bare.RequiresUserInteraction);
        Assert.IsNull(bare.MatchedRule);

        var halves = Asked(
            """
            {"type":"control_request","request_id":"req_permission_3","request":{"subtype":"can_use_tool","tool_name":"Read","input":{},"matched_ask_rule":{"tool_name":"Read"},"mcp_server":{"name":"notes"}}}
            """);

        Assert.IsNull(halves.MatchedRule);
        Assert.IsNull(halves.McpServer);
    }

    // Founded on the recording: suggestions.stdout.jsonl carries decision_reason_type on its
    // can_use_tool requests — "other" on line 8, "workingDir" on lines 12 and 16.
    [TestMethod]
    public void TheRecordedPermissionRequestsCarryTheDecisionReasonTypeTheyWereRefusedBy()
    {
        var types = Recorded.Requests<CanUseToolRequest>("suggestions")
            .Select(request => request.ToPermissionRequest().DecisionReasonType)
            .ToArray();

        CollectionAssert.AreEqual(
            new PermissionDecisionReasonType?[]
            {
                PermissionDecisionReasonType.Other,
                PermissionDecisionReasonType.WorkingDirectory,
                PermissionDecisionReasonType.WorkingDirectory,
            },
            types);
        Assert.IsTrue(types.All(type => type!.Value.IsDefined));
    }

    // The allow goes out with the input the binary asked about when the callback left it alone, and
    // with the caller's bytes when it did not.
    [TestMethod]
    public void AnAllowWritesTheOriginalInputUntilTheCallerGivesOneOfItsOwn()
    {
        var original = Recorded.Json("""{"command": "git status"}""");

        Assert.AreEqual(
            """{"behavior": "allow", "updatedInput": {"command": "git status"}}""",
            Written(ToolPermissionResult.Allow(), original));

        Assert.AreEqual(
            """{"behavior": "allow", "updatedInput": {"command": "git status --short"}}""",
            Written(
                ToolPermissionResult.Allow(rawUpdatedInput: Recorded.Json("""{"command": "git status --short"}""")),
                original));
    }

    // A typed input read off the wire goes back as the call the binary takes: a RemoteTrigger create
    // carries the body the binary refuses it without, through the callback's allow and a hook's updated
    // input alike.
    [TestMethod]
    public void ATypedRemoteTriggerInputHandedBackCarriesItsBody()
    {
        var read = (RemoteTriggerCreateToolInput)ToolTypes.ReadInput(
            "RemoteTrigger",
            Recorded.Json("""{"action": "create", "body": {"name": "nightly", "cron_expression": "0 3 * * *"}}"""))!;

        Assert.AreEqual(
            """{"behavior": "allow", "updatedInput": {"action": "create", "body": {"name": "nightly", "cron_expression": "0 3 * * *"}}}""",
            Written(ToolPermissionResult.Allow(updatedInput: read), Recorded.Json("{}")));
        Assert.AreEqual(
            """{"hookSpecificOutput": {"hookEventName": "PreToolUse", "updatedInput": {"action": "create", "body": {"name": "nightly", "cron_expression": "0 3 * * *"}}}}""",
            WrittenOutput(new HookOutput { UpdatedInput = read }, "PreToolUse"));
    }

    [TestMethod]
    public void AnAllowWritesThePermissionUpdatesItWasGiven()
    {
        Assert.AreEqual(
            """{"behavior": "allow", "updatedInput": {}, "updatedPermissions": [{"type": "addRules", "destination": "session", "rules": [{"toolName": "Bash", "ruleContent": "git status:*"}], "behavior": "allow"}]}""",
            Written(
                ToolPermissionResult.Allow(
                    updatedPermissions: [PermissionUpdate.AddRules(["Bash(git status:*)"], PermissionRuleAction.Allow, PermissionUpdateDestination.Session)]),
                Recorded.Json("{}")));
    }

    [TestMethod]
    public void ADenialWritesItsMessageAndTheInterruptOnlyWhenItEndsTheTurn()
    {
        Assert.AreEqual(
            """{"behavior": "deny", "message": "Not that path."}""",
            Written(ToolPermissionResult.Deny("Not that path."), Recorded.Json("{}")));

        Assert.AreEqual(
            """{"behavior": "deny", "message": "Stop.", "interrupt": true}""",
            Written(ToolPermissionResult.Deny("Stop.", interrupt: true), Recorded.Json("{}")));
    }

    // A raw input the caller wrote is the bytes they chose, so it is what goes out; the typed one is
    // the library's rendering of the same thing, and yields to it.
    [TestMethod]
    public void TheRawInputACallerWroteIsKeptWholeOnTheResultAndOnTheHookOutput()
    {
        var raw = Recorded.Json("""{"command": "git status --short"}""");

        var result = ToolPermissionResult.Allow(rawUpdatedInput: raw);
        Assert.IsNull(result.UpdatedInput);
        Assert.AreEqual("""{"command": "git status --short"}""", result.RawUpdatedInput!.Value.GetRawText());

        var output = new HookOutput
        {
            RawUpdatedInput = raw,
            RawUpdatedToolOutput = Recorded.Json("""{"stdout": "clean"}"""),
            UpdatedMcpToolOutput = Recorded.Json("""{"content": []}"""),
        };

        Assert.IsNull(output.UpdatedInput);
        Assert.IsNull(output.UpdatedToolOutput);
        Assert.AreEqual(
            """{"hookSpecificOutput": {"hookEventName": "PostToolUse", "updatedInput": {"command": "git status --short"}, "updatedToolOutput": {"stdout": "clean"}, "updatedMCPToolOutput": {"content": []}}}""",
            WrittenOutput(output, "PostToolUse"));
    }

    // A hook may answer a permission request with the same result the callback would have given; it
    // goes out under the decision, and with no original input to fall back on it writes none.
    [TestMethod]
    public void AHooksPermissionDecisionGoesOutUnderTheHookSpecificOutputWithNoOriginalInput()
    {
        var output = new HookOutput { PermissionRequestDecision = ToolPermissionResult.Deny("Not that path.") };

        Assert.AreEqual(
            """{"hookSpecificOutput": {"hookEventName": "PermissionRequest", "decision": {"behavior": "deny", "message": "Not that path."}}}""",
            WrittenOutput(output, "PermissionRequest"));

        var allowed = new HookOutput { PermissionRequestDecision = ToolPermissionResult.Allow() };

        Assert.AreEqual(
            """{"hookSpecificOutput": {"hookEventName": "PermissionRequest", "decision": {"behavior": "allow"}}}""",
            WrittenOutput(allowed, "PermissionRequest"));
    }

    [TestMethod]
    public void ADenialRefusesANullMessage()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ToolPermissionResult.Deny(null!));
    }

    [TestMethod]
    public void ThePermissionRequestAndTheMatchedRuleHaveTheirBuildersOnTheModelFactory()
    {
        var call = ClaudeAgentModelFactory.ToolCall(id: "toolu_permission_1", name: "Bash");
        var matched = ClaudeAgentModelFactory.MatchedPermissionRule(
            source: PermissionRuleSource.ProjectSettings,
            toolName: "Bash",
            ruleContent: "git:*");
        var suggestion = PermissionUpdate.SetMode(PermissionMode.AcceptEdits, PermissionUpdateDestination.Session);
        var server = ClaudeAgentModelFactory.McpServerOrigin("notes", "project");

        var request = ClaudeAgentModelFactory.ToolPermissionRequest(
            toolCall: call,
            suggestions: [suggestion],
            mcpServer: server,
            agentId: "agent-9",
            blockedPath: "/etc/hosts",
            decisionReason: "no rule reaches it",
            decisionReasonType: PermissionDecisionReasonType.AsynchronousAgent,
            isClassifierApprovable: true,
            suppressAlwaysAllowRule: false,
            defaultsToNo: true,
            matchedRule: matched,
            title: "Run git status?",
            displayName: "Bash",
            description: "git status",
            requiresUserInteraction: false);

        Assert.AreSame(call, request.ToolCall);
        Assert.AreSame(suggestion, request.Suggestions.Single());
        Assert.AreSame(server, request.McpServer);
        Assert.AreEqual("agent-9", request.AgentId);
        Assert.AreEqual("/etc/hosts", request.BlockedPath);
        Assert.AreEqual("no rule reaches it", request.DecisionReason);
        Assert.AreEqual(PermissionDecisionReasonType.AsynchronousAgent, request.DecisionReasonType);
        Assert.IsTrue(request.IsClassifierApprovable);
        Assert.IsFalse(request.SuppressAlwaysAllowRule);
        Assert.IsTrue(request.DefaultsToNo);
        Assert.AreSame(matched, request.MatchedRule);
        Assert.AreEqual("Run git status?", request.Title);
        Assert.AreEqual("Bash", request.DisplayName);
        Assert.AreEqual("git status", request.Description);
        Assert.IsFalse(request.RequiresUserInteraction);
    }

    private static ToolPermissionRequest Asked(string line)
    {
        var decoded = new FrameCodec().Decode(line);
        Assert.IsTrue(decoded.IsOk, Recorded.Reason(decoded));

        var frame = (ControlRequestFrame)decoded.Value;
        return ((CanUseToolRequest)frame.Request).ToPermissionRequest();
    }

    private static string Written(ToolPermissionResult result, JsonElement originalInput)
    {
        var writer = new WireJson();
        ReplyWriter.Write(writer, new PermissionReply(result, originalInput));
        return writer.ToString();
    }

    private static string WrittenOutput(HookOutput output, string eventName)
    {
        var writer = new WireJson();
        ReplyWriter.WriteHookOutput(writer, output, eventName);
        return writer.ToString();
    }
}
