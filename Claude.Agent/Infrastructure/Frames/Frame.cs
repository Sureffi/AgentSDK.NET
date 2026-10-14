namespace Claude.Agent;

internal abstract class Frame;

internal static class FrameTypes
{
    public const string User = "user";
    public const string Assistant = "assistant";
    public const string System = "system";
    public const string Result = "result";
    public const string StreamEvent = "stream_event";
    public const string RateLimitEvent = "rate_limit_event";
    public const string ConversationReset = "conversation_reset";
    public const string CommandLifecycle = "command_lifecycle";
    public const string ToolProgress = "tool_progress";
    public const string ToolUseSummary = "tool_use_summary";
    public const string AuthStatus = "auth_status";
    public const string PromptSuggestion = "prompt_suggestion";
    public const string ActiveGoal = "active_goal";
    public const string ControlRequest = "control_request";
    public const string ControlResponse = "control_response";
    public const string ControlCancelRequest = "control_cancel_request";
    public const string KeepAlive = "keep_alive";
}
