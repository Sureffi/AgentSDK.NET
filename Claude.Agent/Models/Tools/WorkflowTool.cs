namespace Claude.Agent;

/// <summary>What the model asked the Workflow tool to run. Every member is optional and the binary takes a script path over a script over a name; the arguments a script is handed are arbitrary JSON, so they are read through the raw beside this view and are no member here.</summary>
/// <param name="script">The whole script to run; null when the call named one instead.</param>
/// <param name="name">The name of a script the binary already has; null when the call sent one.</param>
/// <param name="description">What the run is for. The binary ignores it: a script says this itself.</param>
/// <param name="title">What the run is called. The binary ignores it: a script says this itself.</param>
/// <param name="scriptPath">A script on disk, which the binary takes over both of the others; null when the call sent neither.</param>
/// <param name="resumeFromRunId">An earlier run of this session to carry results over from; null when the call starts fresh.</param>
public sealed class WorkflowToolInput(
    string? script = null,
    string? name = null,
    string? description = null,
    string? title = null,
    string? scriptPath = null,
    string? resumeFromRunId = null) : ToolInput
{
    /// <summary>The whole script to run; null when the call named one instead.</summary>
    public string? Script { get; } = script;

    /// <summary>The name of a script the binary already has; null when the call sent one.</summary>
    public string? Name { get; } = name;

    /// <summary>What the run is for. The binary ignores it: a script says this itself.</summary>
    public string? Description { get; } = description;

    /// <summary>What the run is called. The binary ignores it: a script says this itself.</summary>
    public string? Title { get; } = title;

    /// <summary>A script on disk, which the binary takes over both of the others; null when the call sent neither.</summary>
    public string? ScriptPath { get; } = scriptPath;

    /// <summary>An earlier run of this session to carry results over from; null when the call starts fresh.</summary>
    public string? ResumeFromRunId { get; } = resumeFromRunId;
}

/// <summary>What a Workflow handed back. The binary answers before the workflow ends: this says where it went, not what it found. The status is which type this is: run here or run elsewhere, one type each, over the members both carry.</summary>
public abstract class WorkflowToolOutput : ToolOutput
{
    private protected WorkflowToolOutput(string taskId, WorkflowTaskType? taskType, string? workflowName, string? summary)
    {
        TaskId = taskId;
        TaskType = taskType;
        WorkflowName = workflowName;
        Summary = summary;
    }

    /// <summary>The background task the workflow runs as.</summary>
    public string TaskId { get; }

    /// <summary>What kind of task that is; null on a transcript written before the binary wrote it.</summary>
    public WorkflowTaskType? TaskType { get; }

    /// <summary>The name the script gives itself; null on a transcript written before the binary wrote it.</summary>
    public string? WorkflowName { get; }

    /// <summary>The run said shortly; null when the binary gave none.</summary>
    public string? Summary { get; }
}

/// <summary>A Workflow that runs here, in the background of this session. The binary builds only this one; where the script would not compile, it is this one again with the error set.</summary>
/// <param name="taskId">The background task the workflow runs as.</param>
/// <param name="taskType">What kind of task that is; null on a transcript written before the binary wrote it.</param>
/// <param name="workflowName">The name the script gives itself; null on a transcript written before the binary wrote it.</param>
/// <param name="summary">The run said shortly; null when the binary gave none.</param>
/// <param name="runId">What a later call resumes from; null on a transcript written before the binary wrote it.</param>
/// <param name="transcriptDirectory">Where the subagents' transcripts are written; null when the binary named none.</param>
/// <param name="scriptPath">Where the script was kept, to edit and run again; null when the binary named none.</param>
/// <param name="error">Why the script would not run; null when it ran.</param>
public sealed class WorkflowAsyncLaunchedToolOutput(
    string taskId,
    WorkflowTaskType? taskType = null,
    string? workflowName = null,
    string? summary = null,
    string? runId = null,
    string? transcriptDirectory = null,
    string? scriptPath = null,
    string? error = null) : WorkflowToolOutput(taskId, taskType, workflowName, summary)
{
    /// <summary>What a later call resumes from; null on a transcript written before the binary wrote it.</summary>
    public string? RunId { get; } = runId;

    /// <summary>Where the subagents' transcripts are written; null when the binary named none.</summary>
    public string? TranscriptDirectory { get; } = transcriptDirectory;

    /// <summary>Where the script was kept, to edit and run again; null when the binary named none.</summary>
    public string? ScriptPath { get; } = scriptPath;

    /// <summary>Why the script would not run; null when it ran.</summary>
    public string? Error { get; } = error;
}

/// <summary>A Workflow that runs elsewhere, in a cloud session. The declaration names it; the binary this was read against builds none.</summary>
/// <param name="taskId">The background task the workflow runs as.</param>
/// <param name="sessionUrl">The session to watch.</param>
/// <param name="taskType">What kind of task that is; null when the binary named none.</param>
/// <param name="workflowName">The name the script gives itself; null when the binary named none.</param>
/// <param name="summary">The run said shortly; null when the binary gave none.</param>
/// <param name="warning">Something worth knowing that did not stop the run, such as the local branch differing from the one the cloud session clones; null when there was none.</param>
public sealed class WorkflowRemoteLaunchedToolOutput(
    string taskId,
    string sessionUrl,
    WorkflowTaskType? taskType = null,
    string? workflowName = null,
    string? summary = null,
    string? warning = null) : WorkflowToolOutput(taskId, taskType, workflowName, summary)
{
    /// <summary>The session to watch.</summary>
    public string SessionUrl { get; } = sessionUrl;

    /// <summary>Something worth knowing that did not stop the run, such as the local branch differing from the one the cloud session clones; null when there was none.</summary>
    public string? Warning { get; } = warning;
}
