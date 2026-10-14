using System.Text.Json;

namespace Claude.Agent;

internal static class RequestWriter
{
    public static void Write(WireJson writer, ControlRequest request)
    {
        writer.BeginObject();
        writer.Property("subtype", request.Subtype);
        switch (request)
        {
            case InitializeRequest initialize:
                WriteInitialize(writer, initialize);
                break;
            case SetPermissionModeRequest mode:
                writer.Property("mode", mode.Mode.ToString());
                break;
            case SetModelRequest model:
                writer.Name("model");
                if (model.Model is { } value)
                {
                    writer.String(value);
                }
                else
                {
                    writer.Null();
                }

                break;
            case RewindFilesRequest rewind:
                writer.Property("user_message_id", rewind.UserMessageId);
                break;
            case McpReconnectRequest reconnect:
                writer.Property("serverName", reconnect.ServerName);
                break;
            case McpToggleRequest toggle:
                writer.Property("serverName", toggle.ServerName);
                writer.Property("enabled", toggle.Enabled);
                break;
            case StopTaskRequest stop:
                writer.Property("task_id", stop.TaskId);
                break;
            case CanUseToolRequest canUseTool:
                WriteCanUseTool(writer, canUseTool);
                break;
            case HookCallbackRequest hook:
                writer.Property("callback_id", hook.CallbackId);
                writer.Property("input", hook.Input.Json);
                writer.Name("tool_use_id");
                if (hook.ToolUseId is { } hookToolUseId)
                {
                    writer.String(hookToolUseId);
                }
                else
                {
                    writer.Null();
                }

                break;
            case McpMessageRequest mcp:
                writer.Property("server_name", mcp.ServerName);
                writer.Property("message", mcp.Message);
                break;
            case UnknownControlRequest unknown:
                WriteBody(writer, unknown.Raw);
                break;
            case UnparsedControlRequest unparsed:
                WriteBody(writer, unparsed.Raw);
                break;
            case InterruptRequest:
            case McpStatusRequest:
            case GetContextUsageRequest:
            default:
                break;
        }

        writer.EndObject();
    }

    private static void WriteInitialize(WireJson writer, InitializeRequest initialize)
    {
        writer.Name("hooks");
        if (initialize.Hooks is { Count: > 0 } hooks)
        {
            writer.BeginObject();
            foreach (var (hookEvent, matchers) in hooks)
            {
                writer.Name(hookEvent.ToString());
                writer.BeginArray();
                foreach (var matcher in matchers)
                {
                    writer.BeginObject();
                    writer.Name("matcher");
                    if (matcher.Matcher is { } value)
                    {
                        writer.String(value);
                    }
                    else
                    {
                        writer.Null();
                    }

                    writer.Name("hookCallbackIds");
                    writer.BeginArray();
                    foreach (var id in matcher.HookCallbackIds)
                    {
                        writer.String(id);
                    }

                    writer.EndArray();
                    if (matcher.Timeout is { } timeout)
                    {
                        writer.Name("timeout");
                        writer.Number(timeout);
                    }

                    writer.EndObject();
                }

                writer.EndArray();
            }

            writer.EndObject();
        }
        else
        {
            writer.Null();
        }

        if (initialize.Agents is { Count: > 0 } agents)
        {
            writer.Name("agents");
            writer.BeginObject();
            foreach (var (name, definition) in agents)
            {
                writer.Name(name);
                WriteAgent(writer, definition);
            }

            writer.EndObject();
        }

        if (initialize.ExcludeDynamicSections is { } excludeDynamicSections)
        {
            writer.Property("excludeDynamicSections", excludeDynamicSections);
        }

        if (initialize.Skills is { } skills)
        {
            writer.Name("skills");
            writer.BeginArray();
            foreach (var skill in skills)
            {
                writer.String(skill);
            }

            writer.EndArray();
        }

        if (initialize.ForwardSubagentText)
        {
            writer.Property("forwardSubagentText", true);
        }
    }

    private static void WriteAgent(WireJson writer, AgentDefinition definition)
    {
        writer.BeginObject();
        writer.Property("description", definition.Description);
        writer.Property("prompt", definition.Prompt);
        WriteStrings(writer, "tools", definition.Tools);
        WriteStrings(writer, "disallowedTools", definition.DisallowedTools);
        if (definition.Model is { } model)
        {
            writer.Property("model", model);
        }

        WriteStrings(writer, "skills", definition.Skills);
        if (definition.Memory is { } memory)
        {
            writer.Property("memory", memory.ToString());
        }

        WriteStrings(writer, "mcpServers", definition.McpServers);

        if (definition.InitialPrompt is { } initialPrompt)
        {
            writer.Property("initialPrompt", initialPrompt);
        }

        if (definition.MaxTurns is { } maxTurns)
        {
            writer.Property("maxTurns", maxTurns);
        }

        if (definition.Background is { } background)
        {
            writer.Property("background", background);
        }

        if (definition.Effort is { } effort)
        {
            writer.Property("effort", effort.ToString());
        }
        else if (definition.EffortTokens is { } effortTokens)
        {
            writer.Property("effort", effortTokens);
        }

        if (definition.PermissionMode is { } permissionMode)
        {
            writer.Property("permissionMode", permissionMode.ToString());
        }

        writer.EndObject();
    }

    private static void WriteStrings(WireJson writer, string name, IEnumerable<string>? values)
    {
        if (values is not { } items)
        {
            return;
        }

        writer.Name(name);
        writer.BeginArray();
        foreach (var item in items)
        {
            writer.String(item);
        }

        writer.EndArray();
    }

    private static void WriteCanUseTool(WireJson writer, CanUseToolRequest request)
    {
        writer.Property("tool_name", request.ToolName);
        writer.Property("input", request.Input);
        writer.Name("permission_suggestions");
        writer.BeginArray();
        foreach (var suggestion in request.PermissionSuggestions)
        {
            ReplyWriter.WritePermissionUpdate(writer, suggestion);
        }

        writer.EndArray();
        writer.Name("blocked_path");
        if (request.BlockedPath is { } blockedPath)
        {
            writer.String(blockedPath);
        }
        else
        {
            writer.Null();
        }

        WriteOptional(writer, "decision_reason", request.DecisionReason);
        WriteOptional(writer, "title", request.Title);
        WriteOptional(writer, "display_name", request.DisplayName);
        WriteOptional(writer, "description", request.Description);
        writer.Name("tool_use_id");
        if (request.ToolUseId is { } toolUseId)
        {
            writer.String(toolUseId);
        }
        else
        {
            writer.Null();
        }

        WriteOptional(writer, "agent_id", request.AgentId);
    }

    private static void WriteOptional(WireJson writer, string name, string? value)
    {
        if (value is { } text)
        {
            writer.Property(name, text);
        }
    }

    private static void WriteBody(WireJson writer, JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in raw.EnumerateObject())
        {
            if (property.NameEquals("subtype"))
            {
                continue;
            }

            writer.Property(property.Name, property.Value);
        }
    }
}
