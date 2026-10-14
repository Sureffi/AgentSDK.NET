using System.Text.Json;

namespace Claude.Agent;

internal static class ControlParser
{
    public static Result<InboundFrame, ParseError> ParseRequest(ControlRequestFrameDto frame, string line)
    {
        if (frame.RequestId is not { } requestId)
        {
            return JsonReading.Missing("request_id", line);
        }

        if (requestId.Length == 0)
        {
            return JsonReading.Invalid("request_id", "must not be empty.", line);
        }

        var id = new RequestId(requestId);
        if (frame.Request is not { ValueKind: JsonValueKind.Object } request)
        {
            return Unparsed(id, string.Empty, frame.Request ?? EmptyObject.Value, JsonReading.Missing("request", line));
        }

        if (!request.RequireString("subtype", line, "request.subtype").TryGet(out var subtype, out var subtypeError))
        {
            return Unparsed(id, string.Empty, request, subtypeError);
        }

        var parsed = ParseRequestBody(subtype, request, line);
        return parsed.TryGet(out var value, out var error)
            ? new ControlRequestFrame(id, value)
            : Unparsed(id, subtype, request, error);
    }

    public static Result<InboundFrame, ParseError> ParseResponse(ControlResponseFrameDto frame, string line)
    {
        if (frame.Response is not { } response)
        {
            return JsonReading.Missing("response", line);
        }

        if (response.RequestId is not { } requestId)
        {
            return JsonReading.Missing("response.request_id", line);
        }

        if (requestId.Length == 0)
        {
            return JsonReading.Invalid("response.request_id", "must not be empty.", line);
        }

        var id = new RequestId(requestId);
        return response.Subtype == "error"
            ? new ControlResponseFrame(new ControlFailure(id, response.Error ?? "Unknown error"))
            : new ControlResponseFrame(new ControlSuccess(id, response.Response));
    }

    private static ControlRequestFrame Unparsed(RequestId id, string subtype, JsonElement raw, ParseError reason) =>
        new ControlRequestFrame(id, new UnparsedControlRequest(subtype, raw, reason));

    private static Result<ControlRequest, ParseError> ParseRequestBody(
        string subtype,
        JsonElement request,
        string line) => subtype switch
        {
            "can_use_tool" => CanUseTool(request, line),
            "hook_callback" => HookCallback(request, line),
            "mcp_message" => McpMessage(request, line),
            _ => new UnknownControlRequest(subtype, request),
        };

    private static Result<ControlRequest, ParseError> CanUseTool(JsonElement request, string line)
    {
        if (!request.RequireToolName("tool_name", line, "request.tool_name")
                .TryGet(out var toolName, out var toolNameError))
        {
            return toolNameError;
        }

        if (!request.RequireElement("input", line, "request.input").TryGet(out var input, out var inputError))
        {
            return inputError;
        }

        var suggestions = request.TryProperty("permission_suggestions", out var raw)
            ? PermissionUpdateParser.ParseAll(raw)
            : [];

        return new CanUseToolRequest(
            toolName,
            input,
            suggestions,
            request.StringOrNull("blocked_path"),
            request.StringOrNull("decision_reason"),
            request.StringOrNull("title"),
            request.StringOrNull("display_name"),
            request.StringOrNull("description"),
            request.ToolUseIdOrNull("tool_use_id"),
            Identity.Agent(request.StringOrNull("agent_id")))
        {
            McpServer = McpServer(request),
            DecisionReasonType = request.StringOrNull("decision_reason_type") is { } reasonType
                ? new PermissionDecisionReasonType(reasonType)
                : (PermissionDecisionReasonType?)null,
            IsClassifierApprovable = request.BoolOrNull("classifier_approvable"),
            SuppressAlwaysAllowRule = request.BoolOrNull("suppress_always_allow_rule"),
            DefaultsToNo = request.BoolOrNull("default_to_no"),
            MatchedRule = MatchedRule(request),
            RequiresUserInteraction = request.BoolOrNull("requires_user_interaction"),
        };
    }

    private static McpServerOrigin? McpServer(JsonElement request) =>
        request.ElementOrNull("mcp_server") is { } server
        && server.StringOrNull("name") is { } name
        && server.StringOrNull("source") is { } source
            ? new McpServerOrigin(name, new McpServerSource(source))
            : null;

    private static MatchedPermissionRule? MatchedRule(JsonElement request)
    {
        if (request.ElementOrNull("matched_ask_rule") is not { } rule
            || rule.StringOrNull("source") is not { } source
            || rule.ToolNameOrNull("tool_name") is not { } toolName)
        {
            return null;
        }

        return new MatchedPermissionRule(new PermissionRuleSource(source), toolName, rule.StringOrNull("rule_content"));
    }

    private static Result<ControlRequest, ParseError> HookCallback(JsonElement request, string line)
    {
        if (!request.RequireString("callback_id", line, "request.callback_id")
                .TryGet(out var callbackId, out var callbackIdError))
        {
            return callbackIdError;
        }

        if (!request.RequireElement("input", line, "request.input").TryGet(out var input, out var inputError))
        {
            return inputError;
        }

        if (!HookInputParser.Parse(input, line).TryGet(out var hookInput, out var hookInputError))
        {
            return hookInputError;
        }

        return new HookCallbackRequest(callbackId, hookInput, request.ToolUseIdOrNull("tool_use_id"));
    }

    private static Result<ControlRequest, ParseError> McpMessage(JsonElement request, string line)
    {
        if (!request.RequireString("server_name", line, "request.server_name")
                .Bind(text => Identity.RequireServerName(text, "server_name", line))
                .TryGet(out var serverName, out var serverNameError))
        {
            return serverNameError;
        }

        if (!request.RequireElement("message", line, "request.message").TryGet(out var message, out var messageError))
        {
            return messageError;
        }

        return new McpMessageRequest(serverName, message);
    }
}
