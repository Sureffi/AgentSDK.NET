using System.Text.Json;

namespace Claude.Agent;

// The one way in and out of the typed tool views: a tool's JSON read as its typed input or output
// by the tool's name, and a typed input or output written back as the wire's JSON (a hook's or the
// permission callback's updated input or output going in). A tool this library has no type for
// reads as null; so does JSON that does not fit the tool's type, and the raw beside the view is
// then the whole of what the caller has. Nothing here throws on what it reads.
//
// The names are the constants of ToolNames and not literals, so the general founding scan over the
// parsers does not reach them: a tool name is founded by a recorded init list or a recorded call
// alone, never by the declared list, and Tools/ToolNameFoundingTests.cs is what holds that.
internal static class ToolTypes
{
    // Every name this library answers to, for the test that founds them on the recordings.
    public static IReadOnlyList<string> Names { get; } =
    [
        ToolNames.Agent,
        ToolNames.Task,
        ToolNames.Bash,
        ToolNames.TaskOutput,
        ToolNames.TaskStop,
        ToolNames.Edit,
        ToolNames.Read,
        ToolNames.Write,
        ToolNames.Glob,
        ToolNames.Grep,
        ToolNames.NotebookEdit,
        ToolNames.WebFetch,
        ToolNames.WebSearch,
        ToolNames.TaskCreate,
        ToolNames.TaskGet,
        ToolNames.TaskUpdate,
        ToolNames.TaskList,
        ToolNames.CronCreate,
        ToolNames.CronDelete,
        ToolNames.CronList,
        ToolNames.ScheduleWakeup,
        ToolNames.Monitor,
        ToolNames.Workflow,
        ToolNames.EnterWorktree,
        ToolNames.ExitWorktree,
        ToolNames.PushNotification,
        ToolNames.ReportFindings,
        ToolNames.Artifact,
        ToolNames.RemoteTrigger,
    ];

    public static ToolInput? ReadInput(string name, JsonElement raw)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (raw.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return name switch
        {
            ToolNames.Agent or ToolNames.Task => AgentToolParser.ReadInput(raw),
            ToolNames.Bash => BashToolParser.ReadInput(raw),
            ToolNames.TaskOutput => TaskOutputToolParser.ReadInput(raw),
            ToolNames.TaskStop => TaskStopToolParser.ReadInput(raw),
            ToolNames.Edit => EditToolParser.ReadInput(raw),
            ToolNames.Read => ReadToolParser.ReadInput(raw),
            ToolNames.Write => WriteToolParser.ReadInput(raw),
            ToolNames.Glob => GlobToolParser.ReadInput(raw),
            ToolNames.Grep => GrepToolParser.ReadInput(raw),
            ToolNames.NotebookEdit => NotebookEditToolParser.ReadInput(raw),
            ToolNames.WebFetch => WebFetchToolParser.ReadInput(raw),
            ToolNames.WebSearch => WebSearchToolParser.ReadInput(raw),
            ToolNames.TaskCreate => TaskCreateToolParser.ReadInput(raw),
            ToolNames.TaskGet => TaskGetToolParser.ReadInput(raw),
            ToolNames.TaskUpdate => TaskUpdateToolParser.ReadInput(raw),
            ToolNames.TaskList => TaskListToolParser.ReadInput(raw),
            ToolNames.CronCreate => CronCreateToolParser.ReadInput(raw),
            ToolNames.CronDelete => CronDeleteToolParser.ReadInput(raw),
            ToolNames.CronList => CronListToolParser.ReadInput(raw),
            ToolNames.ScheduleWakeup => ScheduleWakeupToolParser.ReadInput(raw),
            ToolNames.Monitor => MonitorToolParser.ReadInput(raw),
            ToolNames.Workflow => WorkflowToolParser.ReadInput(raw),
            ToolNames.EnterWorktree => EnterWorktreeToolParser.ReadInput(raw),
            ToolNames.ExitWorktree => ExitWorktreeToolParser.ReadInput(raw),
            ToolNames.PushNotification => PushNotificationToolParser.ReadInput(raw),
            ToolNames.ReportFindings => ReportFindingsToolParser.ReadInput(raw),
            ToolNames.Artifact => ArtifactToolParser.ReadInput(raw),
            ToolNames.RemoteTrigger => RemoteTriggerToolParser.ReadInput(raw),
            _ => null,
        };
    }

    public static ToolOutput? ReadOutput(string name, JsonElement raw)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (raw.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return name switch
        {
            ToolNames.Agent or ToolNames.Task => AgentToolParser.ReadOutput(raw),
            ToolNames.Bash => BashToolParser.ReadOutput(raw),
            ToolNames.TaskOutput => TaskOutputToolParser.ReadOutput(raw),
            ToolNames.TaskStop => TaskStopToolParser.ReadOutput(raw),
            ToolNames.Edit => EditToolParser.ReadOutput(raw),
            ToolNames.Read => ReadToolParser.ReadOutput(raw),
            ToolNames.Write => WriteToolParser.ReadOutput(raw),
            ToolNames.Glob => GlobToolParser.ReadOutput(raw),
            ToolNames.Grep => GrepToolParser.ReadOutput(raw),
            ToolNames.NotebookEdit => NotebookEditToolParser.ReadOutput(raw),
            ToolNames.WebFetch => WebFetchToolParser.ReadOutput(raw),
            ToolNames.WebSearch => WebSearchToolParser.ReadOutput(raw),
            ToolNames.TaskCreate => TaskCreateToolParser.ReadOutput(raw),
            ToolNames.TaskGet => TaskGetToolParser.ReadOutput(raw),
            ToolNames.TaskUpdate => TaskUpdateToolParser.ReadOutput(raw),
            ToolNames.TaskList => TaskListToolParser.ReadOutput(raw),
            ToolNames.CronCreate => CronCreateToolParser.ReadOutput(raw),
            ToolNames.CronDelete => CronDeleteToolParser.ReadOutput(raw),
            ToolNames.CronList => CronListToolParser.ReadOutput(raw),
            ToolNames.ScheduleWakeup => ScheduleWakeupToolParser.ReadOutput(raw),
            ToolNames.Monitor => MonitorToolParser.ReadOutput(raw),
            ToolNames.Workflow => WorkflowToolParser.ReadOutput(raw),
            ToolNames.EnterWorktree => EnterWorktreeToolParser.ReadOutput(raw),
            ToolNames.ExitWorktree => ExitWorktreeToolParser.ReadOutput(raw),
            ToolNames.PushNotification => PushNotificationToolParser.ReadOutput(raw),
            ToolNames.ReportFindings => ReportFindingsToolParser.ReadOutput(raw),
            ToolNames.Artifact => ArtifactToolParser.ReadOutput(raw),
            ToolNames.RemoteTrigger => RemoteTriggerToolParser.ReadOutput(raw),
            _ => null,
        };
    }

    public static JsonElement WriteInput(ToolInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return ToolWriting.Document(writer =>
        {
            switch (input)
            {
                case AgentToolInput agent:
                    AgentToolParser.WriteInput(writer, agent);
                    break;
                case BashToolInput bash:
                    BashToolParser.WriteInput(writer, bash);
                    break;
                case TaskOutputToolInput taskOutput:
                    TaskOutputToolParser.WriteInput(writer, taskOutput);
                    break;
                case TaskStopToolInput taskStop:
                    TaskStopToolParser.WriteInput(writer, taskStop);
                    break;
                case EditToolInput edit:
                    EditToolParser.WriteInput(writer, edit);
                    break;
                case ReadToolInput read:
                    ReadToolParser.WriteInput(writer, read);
                    break;
                case WriteToolInput write:
                    WriteToolParser.WriteInput(writer, write);
                    break;
                case GlobToolInput glob:
                    GlobToolParser.WriteInput(writer, glob);
                    break;
                case GrepToolInput grep:
                    GrepToolParser.WriteInput(writer, grep);
                    break;
                case NotebookEditToolInput notebook:
                    NotebookEditToolParser.WriteInput(writer, notebook);
                    break;
                case WebFetchToolInput fetch:
                    WebFetchToolParser.WriteInput(writer, fetch);
                    break;
                case WebSearchToolInput search:
                    WebSearchToolParser.WriteInput(writer, search);
                    break;
                case TaskCreateToolInput taskCreate:
                    TaskCreateToolParser.WriteInput(writer, taskCreate);
                    break;
                case TaskGetToolInput taskGet:
                    TaskGetToolParser.WriteInput(writer, taskGet);
                    break;
                case TaskUpdateToolInput taskUpdate:
                    TaskUpdateToolParser.WriteInput(writer, taskUpdate);
                    break;
                case TaskListToolInput taskList:
                    TaskListToolParser.WriteInput(writer, taskList);
                    break;
                case CronCreateToolInput cronCreate:
                    CronCreateToolParser.WriteInput(writer, cronCreate);
                    break;
                case CronDeleteToolInput cronDelete:
                    CronDeleteToolParser.WriteInput(writer, cronDelete);
                    break;
                case CronListToolInput cronList:
                    CronListToolParser.WriteInput(writer, cronList);
                    break;
                case ScheduleWakeupToolInput scheduleWakeup:
                    ScheduleWakeupToolParser.WriteInput(writer, scheduleWakeup);
                    break;
                case MonitorToolInput monitor:
                    MonitorToolParser.WriteInput(writer, monitor);
                    break;
                case WorkflowToolInput workflow:
                    WorkflowToolParser.WriteInput(writer, workflow);
                    break;
                case EnterWorktreeToolInput enterWorktree:
                    EnterWorktreeToolParser.WriteInput(writer, enterWorktree);
                    break;
                case ExitWorktreeToolInput exitWorktree:
                    ExitWorktreeToolParser.WriteInput(writer, exitWorktree);
                    break;
                case PushNotificationToolInput pushNotification:
                    PushNotificationToolParser.WriteInput(writer, pushNotification);
                    break;
                case ReportFindingsToolInput reportFindings:
                    ReportFindingsToolParser.WriteInput(writer, reportFindings);
                    break;
                case ArtifactToolInput artifact:
                    ArtifactToolParser.WriteInput(writer, artifact);
                    break;
                case RemoteTriggerToolInput remoteTrigger:
                    RemoteTriggerToolParser.WriteInput(writer, remoteTrigger);
                    break;
                default:
                    throw new NotSupportedException($"No tool input of type {input.GetType().Name} can be written.");
            }
        });
    }

    public static JsonElement WriteOutput(ToolOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return ToolWriting.Document(writer =>
        {
            switch (output)
            {
                case AgentToolOutput agent:
                    AgentToolParser.WriteOutput(writer, agent);
                    break;
                case BashToolOutput bash:
                    BashToolParser.WriteOutput(writer, bash);
                    break;
                case TaskOutputToolOutput taskOutput:
                    TaskOutputToolParser.WriteOutput(writer, taskOutput);
                    break;
                case TaskStopToolOutput taskStop:
                    TaskStopToolParser.WriteOutput(writer, taskStop);
                    break;
                case EditToolOutput edit:
                    EditToolParser.WriteOutput(writer, edit);
                    break;
                case ReadToolOutput read:
                    ReadToolParser.WriteOutput(writer, read);
                    break;
                case WriteToolOutput write:
                    WriteToolParser.WriteOutput(writer, write);
                    break;
                case GlobToolOutput glob:
                    GlobToolParser.WriteOutput(writer, glob);
                    break;
                case GrepToolOutput grep:
                    GrepToolParser.WriteOutput(writer, grep);
                    break;
                case NotebookEditToolOutput notebook:
                    NotebookEditToolParser.WriteOutput(writer, notebook);
                    break;
                case WebFetchToolOutput fetch:
                    WebFetchToolParser.WriteOutput(writer, fetch);
                    break;
                case WebSearchToolOutput search:
                    WebSearchToolParser.WriteOutput(writer, search);
                    break;
                case TaskCreateToolOutput taskCreate:
                    TaskCreateToolParser.WriteOutput(writer, taskCreate);
                    break;
                case TaskGetToolOutput taskGet:
                    TaskGetToolParser.WriteOutput(writer, taskGet);
                    break;
                case TaskUpdateToolOutput taskUpdate:
                    TaskUpdateToolParser.WriteOutput(writer, taskUpdate);
                    break;
                case TaskListToolOutput taskList:
                    TaskListToolParser.WriteOutput(writer, taskList);
                    break;
                case CronCreateToolOutput cronCreate:
                    CronCreateToolParser.WriteOutput(writer, cronCreate);
                    break;
                case CronDeleteToolOutput cronDelete:
                    CronDeleteToolParser.WriteOutput(writer, cronDelete);
                    break;
                case CronListToolOutput cronList:
                    CronListToolParser.WriteOutput(writer, cronList);
                    break;
                case ScheduleWakeupToolOutput scheduleWakeup:
                    ScheduleWakeupToolParser.WriteOutput(writer, scheduleWakeup);
                    break;
                case MonitorToolOutput monitor:
                    MonitorToolParser.WriteOutput(writer, monitor);
                    break;
                case WorkflowToolOutput workflow:
                    WorkflowToolParser.WriteOutput(writer, workflow);
                    break;
                case EnterWorktreeToolOutput enterWorktree:
                    EnterWorktreeToolParser.WriteOutput(writer, enterWorktree);
                    break;
                case ExitWorktreeToolOutput exitWorktree:
                    ExitWorktreeToolParser.WriteOutput(writer, exitWorktree);
                    break;
                case PushNotificationToolOutput pushNotification:
                    PushNotificationToolParser.WriteOutput(writer, pushNotification);
                    break;
                case ReportFindingsToolOutput reportFindings:
                    ReportFindingsToolParser.WriteOutput(writer, reportFindings);
                    break;
                case ArtifactToolOutput artifact:
                    ArtifactToolParser.WriteOutput(writer, artifact);
                    break;
                case RemoteTriggerToolOutput remoteTrigger:
                    RemoteTriggerToolParser.WriteOutput(writer, remoteTrigger);
                    break;
                default:
                    throw new NotSupportedException($"No tool output of type {output.GetType().Name} can be written.");
            }
        });
    }
}
