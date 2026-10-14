using System.Text.Json;

namespace Claude.Agent;

internal static class ReplyWriter
{
    public static void Write(WireJson writer, ControlReply reply)
    {
        switch (reply)
        {
            case HookReply hook:
                WriteHookOutput(writer, hook.Output, hook.EventName);
                break;
            case PermissionReply permission:
                WritePermissionResult(writer, permission.Result, permission.OriginalInput);
                break;
            case McpReply mcp:
                writer.BeginObject();
                writer.Property("mcp_response", mcp.McpResponse);
                writer.EndObject();
                break;
            case EmptyReply:
                writer.BeginObject();
                writer.EndObject();
                break;
            default:
                throw new InvalidOperationException($"{reply.GetType()} is not a reply this writer knows.");
        }
    }

    public static void WriteHookOutput(WireJson writer, HookOutput output, string eventName)
    {
        writer.BeginObject();
        if (output.IsAsync)
        {
            writer.Property("async", true);
            // The wire key is milliseconds, as every *_ms field the parser reads is.
            if (output.AsyncTimeout is { } timeout)
            {
                writer.Property("asyncTimeout", (long)timeout.TotalMilliseconds);
            }

            writer.EndObject();
            return;
        }

        if (output.Continue is { } shouldContinue)
        {
            writer.Property("continue", shouldContinue);
        }

        if (output.SuppressOutput is { } suppress)
        {
            writer.Property("suppressOutput", suppress);
        }

        if (output.StopReason is { } stopReason)
        {
            writer.Property("stopReason", stopReason);
        }

        if (output.Block)
        {
            writer.Property("decision", "block");
        }

        if (output.SystemMessage is { } systemMessage)
        {
            writer.Property("systemMessage", systemMessage);
        }

        if (output.Reason is { } reason)
        {
            writer.Property("reason", reason);
        }

        if (output.HasEventSpecificOutput)
        {
            writer.Name("hookSpecificOutput");
            writer.BeginObject();
            writer.Property("hookEventName", eventName);
            if (output.Decision is { } decision)
            {
                writer.Property("permissionDecision", decision.ToString());
            }

            if (output.DecisionReason is { } decisionReason)
            {
                writer.Property("permissionDecisionReason", decisionReason);
            }

            if (Input(output.UpdatedInput, output.RawUpdatedInput) is { } updatedInput)
            {
                writer.Property("updatedInput", updatedInput);
            }

            if (output.AdditionalContext is { } additional)
            {
                writer.Property("additionalContext", additional);
            }

            if (Output(output.UpdatedToolOutput, output.RawUpdatedToolOutput) is { } toolOutput)
            {
                writer.Property("updatedToolOutput", toolOutput);
            }

            if (output.UpdatedMcpToolOutput is { } mcpOutput)
            {
                writer.Property("updatedMCPToolOutput", mcpOutput);
            }

            if (output.PermissionRequestDecision is { } permission)
            {
                writer.Name("decision");
                WritePermissionResult(writer, permission, null);
            }

            writer.EndObject();
        }

        writer.EndObject();
    }

    public static void WritePermissionUpdate(WireJson writer, PermissionUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (update is UnknownPermissionUpdate unknown)
        {
            writer.Element(unknown.Raw);
            return;
        }

        writer.BeginObject();
        writer.Property("type", Wire(update));
        if (update.Destination is { } destination)
        {
            writer.Property("destination", destination.ToString());
        }

        switch (update)
        {
            case AddRulesPermissionUpdate rules:
                WriteRules(writer, rules.Rules, rules.Behavior);
                break;
            case ReplaceRulesPermissionUpdate rules:
                WriteRules(writer, rules.Rules, rules.Behavior);
                break;
            case RemoveRulesPermissionUpdate rules:
                WriteRules(writer, rules.Rules, rules.Behavior);
                break;
            case SetModePermissionUpdate mode:
                writer.Property("mode", mode.Mode.ToString());
                break;
            case AddDirectoriesPermissionUpdate added:
                WriteDirectories(writer, added.Directories);
                break;
            case RemoveDirectoriesPermissionUpdate removed:
                WriteDirectories(writer, removed.Directories);
                break;
            default:
                throw new InvalidOperationException($"{update.GetType()} is not a permission update this writer knows.");
        }

        writer.EndObject();
    }

    // A rule that reaches the whole tool goes out with no ruleContent beside it.
    private static void WriteRules(WireJson writer, IReadOnlyList<PermissionRule> rules, PermissionRuleAction behavior)
    {
        writer.Name("rules");
        writer.BeginArray();
        foreach (var rule in rules)
        {
            writer.BeginObject();
            writer.Property("toolName", rule.ToolName);
            if (rule.RuleContent is { } content)
            {
                writer.Property("ruleContent", content);
            }

            writer.EndObject();
        }

        writer.EndArray();
        writer.Property("behavior", behavior.ToString());
    }

    private static void WriteDirectories(WireJson writer, IReadOnlyList<string> directories)
    {
        writer.Name("directories");
        writer.BeginArray();
        foreach (var directory in directories)
        {
            writer.String(directory);
        }

        writer.EndArray();
    }

    private static string Wire(PermissionUpdate update) => update switch
    {
        AddRulesPermissionUpdate => "addRules",
        ReplaceRulesPermissionUpdate => "replaceRules",
        RemoveRulesPermissionUpdate => "removeRules",
        SetModePermissionUpdate => "setMode",
        AddDirectoriesPermissionUpdate => "addDirectories",
        RemoveDirectoriesPermissionUpdate => "removeDirectories",
        _ => string.Empty,
    };

    // A typed input or output goes out as the tool's own JSON; a raw one beside it is what the
    // caller wrote, and it wins, being the bytes they chose.
    private static JsonElement? Input(ToolInput? typed, JsonElement? raw) =>
        raw ?? (typed is null ? null : ToolTypes.WriteInput(typed));

    private static JsonElement? Output(ToolOutput? typed, JsonElement? raw) =>
        raw ?? (typed is null ? null : ToolTypes.WriteOutput(typed));

    // For the can_use_tool reply, updatedInput is always written: the original when the callback
    // left it alone. For a PermissionRequest hook's decision there is no original, so it is written
    // only when set.
    private static void WritePermissionResult(WireJson writer, ToolPermissionResult result, JsonElement? originalInput)
    {
        writer.BeginObject();
        if (result.IsAllowed)
        {
            writer.Property("behavior", "allow");
            if ((Input(result.UpdatedInput, result.RawUpdatedInput) ?? originalInput) is { } input)
            {
                writer.Property("updatedInput", input);
            }

            if (result.UpdatedPermissions is { } updates)
            {
                writer.Name("updatedPermissions");
                writer.BeginArray();
                foreach (var update in updates)
                {
                    WritePermissionUpdate(writer, update);
                }

                writer.EndArray();
            }
        }
        else
        {
            writer.Property("behavior", "deny");
            writer.Property("message", result.Message);
            if (result.Interrupt)
            {
                writer.Property("interrupt", true);
            }
        }

        writer.EndObject();
    }
}
