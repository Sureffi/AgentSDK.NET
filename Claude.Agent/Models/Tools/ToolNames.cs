namespace Claude.Agent;

// The names the binary puts in a tool_use block for the tools this library types. They are constants
// here, and not literals in the dispatch, because a tool name is founded one way only: a recorded
// init list or a recorded call. The declared list founds no tool name — it holds `Bash` as the value
// of another field entirely — so the general founding scan over the parsers
// (Architecture/TreeTests.cs) is the wrong channel for these, and would pass a name no recording
// carries. Tools/ToolNameFoundingTests.cs is the right one: it walks the names ToolTypes answers to
// and fails on one no recorded init list and no recorded call carries.
internal static class ToolNames
{
    // The Agent tool answers to two names: every recorded init frame that offers it lists `Task`,
    // every recorded tool_use block names it `Agent`.
    public const string Agent = "Agent";
    public const string Task = "Task";
    public const string Bash = "Bash";
    public const string TaskOutput = "TaskOutput";
    public const string TaskStop = "TaskStop";
    public const string Edit = "Edit";
    public const string Read = "Read";
    public const string Write = "Write";
    public const string Glob = "Glob";
    public const string Grep = "Grep";
    public const string NotebookEdit = "NotebookEdit";
    public const string WebFetch = "WebFetch";
    public const string WebSearch = "WebSearch";

    // Sixteen more the recorded init lists offer and no recording calls.
    public const string TaskCreate = "TaskCreate";
    public const string TaskGet = "TaskGet";
    public const string TaskUpdate = "TaskUpdate";
    public const string TaskList = "TaskList";
    public const string CronCreate = "CronCreate";
    public const string CronDelete = "CronDelete";
    public const string CronList = "CronList";
    public const string ScheduleWakeup = "ScheduleWakeup";
    public const string Monitor = "Monitor";
    public const string Workflow = "Workflow";
    public const string EnterWorktree = "EnterWorktree";
    public const string ExitWorktree = "ExitWorktree";
    public const string PushNotification = "PushNotification";
    public const string ReportFindings = "ReportFindings";
    public const string Artifact = "Artifact";
    public const string RemoteTrigger = "RemoteTrigger";
}
