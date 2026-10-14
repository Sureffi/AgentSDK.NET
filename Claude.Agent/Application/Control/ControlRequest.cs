using System.Collections.Immutable;
using System.Text.Json;

namespace Claude.Agent;

internal abstract record ControlRequest
{
    private protected ControlRequest(string subtype) => Subtype = subtype;

    public string Subtype { get; init; }
}

internal sealed record CanUseToolRequest(
    string ToolName,
    JsonElement Input,
    ImmutableArray<PermissionUpdate> PermissionSuggestions,
    string? BlockedPath,
    string? DecisionReason,
    string? Title,
    string? DisplayName,
    string? Description,
    string? ToolUseId,
    string? AgentId) : ControlRequest("can_use_tool")
{
    public ImmutableArray<PermissionUpdate> PermissionSuggestions { get; } =
        PermissionSuggestions.IsDefault ? [] : PermissionSuggestions;

    public McpServerOrigin? McpServer { get; init; }

    public PermissionDecisionReasonType? DecisionReasonType { get; init; }

    public bool? IsClassifierApprovable { get; init; }

    public bool? SuppressAlwaysAllowRule { get; init; }

    public bool? DefaultsToNo { get; init; }

    public MatchedPermissionRule? MatchedRule { get; init; }

    public bool? RequiresUserInteraction { get; init; }

    // The ask as a caller reads it: the tool as a started call, and everything the binary said about
    // why it is asking. Built here so the handler and a test of the recordings build the same one.
    public ToolPermissionRequest ToPermissionRequest() =>
        new(
            new ToolCall(ToolUseId ?? string.Empty, ToolName, ToolTypes.ReadInput(ToolName, Input), Input, ToolCallStatus.Started),
            PermissionSuggestions)
        {
            McpServer = McpServer,
            AgentId = AgentId,
            BlockedPath = BlockedPath,
            DecisionReason = DecisionReason,
            DecisionReasonType = DecisionReasonType,
            IsClassifierApprovable = IsClassifierApprovable,
            SuppressAlwaysAllowRule = SuppressAlwaysAllowRule,
            DefaultsToNo = DefaultsToNo,
            MatchedRule = MatchedRule,
            Title = Title,
            DisplayName = DisplayName,
            Description = Description,
            RequiresUserInteraction = RequiresUserInteraction,
        };
}

internal sealed record HookCallbackRequest(
    string CallbackId,
    HookInput Input,
    string? ToolUseId) : ControlRequest("hook_callback");

internal sealed record McpMessageRequest(string ServerName, JsonElement Message) : ControlRequest("mcp_message");

internal sealed record InitializeRequest(
    IReadOnlyDictionary<HookEvent, ImmutableArray<HookMatcherConfig>>? Hooks,
    IReadOnlyDictionary<string, AgentDefinition>? Agents,
    bool? ExcludeDynamicSections,
    ImmutableArray<string>? Skills,
    bool ForwardSubagentText) : ControlRequest("initialize");

internal sealed record HookMatcherConfig(
    string? Matcher,
    ImmutableArray<string> HookCallbackIds,
    double? Timeout)
{
    public ImmutableArray<string> HookCallbackIds { get; } =
        HookCallbackIds.IsDefault ? [] : HookCallbackIds;
}

internal sealed record InterruptRequest() : ControlRequest("interrupt");

internal sealed record SetPermissionModeRequest(PermissionMode Mode) : ControlRequest("set_permission_mode");

internal sealed record SetModelRequest(string? Model) : ControlRequest("set_model");

internal sealed record RewindFilesRequest(string UserMessageId) : ControlRequest("rewind_files");

internal sealed record McpReconnectRequest(string ServerName) : ControlRequest("mcp_reconnect");

internal sealed record McpToggleRequest(string ServerName, bool Enabled) : ControlRequest("mcp_toggle");

internal sealed record StopTaskRequest(string TaskId) : ControlRequest("stop_task");

internal sealed record McpStatusRequest() : ControlRequest("mcp_status");

internal sealed record GetContextUsageRequest() : ControlRequest("get_context_usage");

internal sealed record UnknownControlRequest(string Subtype, JsonElement Raw) : ControlRequest(Subtype);

internal sealed record UnparsedControlRequest(string Subtype, JsonElement Raw, ParseError Reason)
    : ControlRequest(Subtype);
