using System.Text.Json;

namespace Claude.Agent;

/// <summary>Something the binary wants shown to the person driving the session.</summary>
public sealed class NotificationMessage : SystemMessage
{
    internal NotificationMessage(string key, string text, NotificationPriority priority, string? uuid, string? sessionId, JsonElement? raw)
        : base("notification", uuid, sessionId, raw)
    {
        Key = key;
        Text = text;
        Priority = priority;
    }

    /// <summary>What the notice is about, as the binary names it.</summary>
    public string Key { get; }

    /// <summary>The notice itself.</summary>
    public string Text { get; }

    /// <summary>How loudly it asks to be shown.</summary>
    public NotificationPriority Priority { get; }

    /// <summary>The colour the binary would show it in, in its own words.</summary>
    public string? Color { get; internal init; }

    /// <summary>How long it would be shown for.</summary>
    public TimeSpan? Timeout { get; internal init; }
}

/// <summary>Something the binary has to say about the session, weightier than a notice.</summary>
public sealed class InformationalMessage : SystemMessage
{
    internal InformationalMessage(string content, InformationalLevel level, string? uuid, string? sessionId, JsonElement? raw)
        : base("informational", uuid, sessionId, raw)
    {
        Content = content;
        Level = level;
    }

    /// <summary>What it says.</summary>
    public string Content { get; }

    /// <summary>How weighty it is.</summary>
    public InformationalLevel Level { get; }

    /// <summary>The tool use it is about, when it is about one.</summary>
    public string? ToolUseId { get; internal init; }

    /// <summary>Whether the turn should stop here.</summary>
    public bool? PreventContinuation { get; internal init; }
}

/// <summary>What a local command the prompt named wrote.</summary>
public sealed class LocalCommandOutputMessage : SystemMessage
{
    internal LocalCommandOutputMessage(string content, string? uuid, string? sessionId, JsonElement? raw)
        : base("local_command_output", uuid, sessionId, raw)
    {
        Content = content;
    }

    /// <summary>What the command wrote.</summary>
    public string Content { get; }
}

/// <summary>An MCP server's elicitation has been answered.</summary>
public sealed class ElicitationCompleteMessage : SystemMessage
{
    internal ElicitationCompleteMessage(string mcpServerName, string elicitationId, string? uuid, string? sessionId, JsonElement? raw)
        : base("elicitation_complete", uuid, sessionId, raw)
    {
        McpServerName = mcpServerName;
        ElicitationId = elicitationId;
    }

    /// <summary>The server that asked.</summary>
    public string McpServerName { get; }

    /// <summary>The asking, by the id the binary gave it.</summary>
    public string ElicitationId { get; }
}

/// <summary>The binary brought memories into the turn, and which.</summary>
public sealed class MemoryRecallMessage : SystemMessage
{
    internal MemoryRecallMessage(MemoryRecallMode mode, IReadOnlyList<RecalledMemory> memories, string? uuid, string? sessionId, JsonElement? raw)
        : base("memory_recall", uuid, sessionId, raw)
    {
        Mode = mode;
        Memories = memories;
    }

    /// <summary>How they were brought in.</summary>
    public MemoryRecallMode Mode { get; }

    /// <summary>The memories.</summary>
    public IReadOnlyList<RecalledMemory> Memories { get; }
}

/// <summary>One memory the binary brought into the turn.</summary>
public sealed class RecalledMemory
{
    internal RecalledMemory(string path, MemoryScope scope)
    {
        Path = path;
        Scope = scope;
    }

    /// <summary>Where it is kept.</summary>
    public string Path { get; }

    /// <summary>Who it belongs to.</summary>
    public MemoryScope Scope { get; }

    /// <summary>What it says, when the notice carries it.</summary>
    public string? Content { get; internal init; }
}

/// <summary>Files the turn produced have been kept, and which could not be.</summary>
public sealed class FilesPersistedMessage : SystemMessage
{
    internal FilesPersistedMessage(
        IReadOnlyList<PersistedFile> files,
        IReadOnlyList<FilePersistenceFailure> failed,
        string processedAt,
        string? uuid,
        string? sessionId,
        JsonElement? raw)
        : base("files_persisted", uuid, sessionId, raw)
    {
        Files = files;
        Failed = failed;
        ProcessedAt = processedAt;
    }

    /// <summary>The files kept.</summary>
    public IReadOnlyList<PersistedFile> Files { get; }

    /// <summary>The files that could not be kept.</summary>
    public IReadOnlyList<FilePersistenceFailure> Failed { get; }

    /// <summary>When the binary did the work, as it wrote it.</summary>
    public string ProcessedAt { get; }
}

/// <summary>A file the binary kept, and the id it goes under.</summary>
public sealed class PersistedFile
{
    internal PersistedFile(string fileName, string fileId)
    {
        FileName = fileName;
        FileId = fileId;
    }

    /// <summary>The file's name.</summary>
    public string FileName { get; }

    /// <summary>The id it is kept under.</summary>
    public string FileId { get; }
}

/// <summary>A file the binary could not keep, and why.</summary>
public sealed class FilePersistenceFailure
{
    internal FilePersistenceFailure(string fileName, string error)
    {
        FileName = fileName;
        Error = error;
    }

    /// <summary>The file's name.</summary>
    public string FileName { get; }

    /// <summary>Why it could not be kept.</summary>
    public string Error { get; }
}

/// <summary>The slash commands a prompt may start with have changed.</summary>
public sealed class CommandsChangedMessage : SystemMessage
{
    internal CommandsChangedMessage(IReadOnlyList<SlashCommandInfo> commands, string? uuid, string? sessionId, JsonElement? raw)
        : base("commands_changed", uuid, sessionId, raw)
    {
        Commands = commands;
    }

    /// <summary>The commands as they now stand.</summary>
    public IReadOnlyList<SlashCommandInfo> Commands { get; }
}

/// <summary>A command a prompt may start with.</summary>
public sealed class SlashCommandInfo
{
    internal SlashCommandInfo(string name, string description, string argumentHint)
    {
        Name = name;
        Description = description;
        ArgumentHint = argumentHint;
    }

    /// <summary>The command, without its slash.</summary>
    public string Name { get; }

    /// <summary>What it does.</summary>
    public string Description { get; }

    /// <summary>What it takes after the name.</summary>
    public string ArgumentHint { get; }

    /// <summary>Other names it answers to.</summary>
    public IReadOnlyList<string> Aliases { get; internal init; } = [];

    /// <summary>True when the command is the binary's own; where two commands share a name, the one so marked is the one the name runs. Null for one a user, a project, a plugin or an MCP server defined, and on a binary that writes no mark, as 2.1.276 writes none.</summary>
    public bool? IsBuiltIn { get; internal init; }
}

/// <summary>Where installing a plugin stands.</summary>
public sealed class PluginInstallMessage : SystemMessage
{
    internal PluginInstallMessage(PluginInstallStatus status, string? uuid, string? sessionId, JsonElement? raw)
        : base("plugin_install", uuid, sessionId, raw)
    {
        Status = status;
    }

    /// <summary>Where it stands.</summary>
    public PluginInstallStatus Status { get; }

    /// <summary>The plugin, by name.</summary>
    public string? Name { get; internal init; }

    /// <summary>Why it failed, when it did.</summary>
    public string? Error { get; internal init; }
}

/// <summary>The model refused, and the binary is falling back to another one.</summary>
public sealed class ModelRefusalFallbackMessage : SystemMessage
{
    internal ModelRefusalFallbackMessage(
        RefusalFallbackTrigger trigger,
        RefusalFallbackDirection direction,
        string originalModel,
        string fallbackModel,
        string content,
        string? uuid,
        string? sessionId,
        JsonElement? raw)
        : base("model_refusal_fallback", uuid, sessionId, raw)
    {
        Trigger = trigger;
        Direction = direction;
        OriginalModel = originalModel;
        FallbackModel = fallbackModel;
        Content = content;
    }

    /// <summary>What made the binary fall back.</summary>
    public RefusalFallbackTrigger Trigger { get; }

    /// <summary>What it did about it.</summary>
    public RefusalFallbackDirection Direction { get; }

    /// <summary>How long that holds.</summary>
    public RefusalFallbackScope? Scope { get; internal init; }

    /// <summary>The model that refused.</summary>
    public string OriginalModel { get; }

    /// <summary>The model asked instead.</summary>
    public string FallbackModel { get; }

    /// <summary>What the binary has to say about it.</summary>
    public string Content { get; }

    /// <summary>The API request that was refused.</summary>
    public string? RequestId { get; internal init; }

    /// <summary>What the refusal was about, in the API's own words.</summary>
    public string? ApiRefusalCategory { get; internal init; }

    /// <summary>The refusal in the API's own words.</summary>
    public string? ApiRefusalExplanation { get; internal init; }

    /// <summary>The messages taken back because of the refusal.</summary>
    public IReadOnlyList<string> RetractedMessageUuids { get; internal init; } = [];

    /// <summary>The prompt that was refused.</summary>
    public string? RefusedUserMessageUuid { get; internal init; }
}

/// <summary>The model refused and there is no other model to ask.</summary>
public sealed class ModelRefusalNoFallbackMessage : SystemMessage
{
    internal ModelRefusalNoFallbackMessage(string originalModel, string content, string? uuid, string? sessionId, JsonElement? raw)
        : base("model_refusal_no_fallback", uuid, sessionId, raw)
    {
        OriginalModel = originalModel;
        Content = content;
    }

    /// <summary>The model that refused.</summary>
    public string OriginalModel { get; }

    /// <summary>What the binary has to say about it.</summary>
    public string Content { get; }

    /// <summary>The API request that was refused.</summary>
    public string? RequestId { get; internal init; }

    /// <summary>What the refusal was about, in the API's own words.</summary>
    public string? ApiRefusalCategory { get; internal init; }

    /// <summary>The refusal in the API's own words.</summary>
    public string? ApiRefusalExplanation { get; internal init; }

    /// <summary>The prompt that was refused.</summary>
    public string? RefusedUserMessageUuid { get; internal init; }
}

/// <summary>The background tasks the session is keeping, as they now stand.</summary>
public sealed class BackgroundTasksChangedMessage : SystemMessage
{
    internal BackgroundTasksChangedMessage(IReadOnlyList<BackgroundTask> tasks, string? uuid, string? sessionId, JsonElement? raw)
        : base("background_tasks_changed", uuid, sessionId, raw)
    {
        Tasks = tasks;
    }

    /// <summary>The tasks, empty when the last one has gone.</summary>
    public IReadOnlyList<BackgroundTask> Tasks { get; }
}

/// <summary>One background task the session is keeping.</summary>
public sealed class BackgroundTask
{
    internal BackgroundTask(string taskId, TaskType taskType, string description)
    {
        TaskId = taskId;
        TaskType = taskType;
        Description = description;
    }

    /// <summary>The task's id.</summary>
    public string TaskId { get; }

    /// <summary>What kind of work it is.</summary>
    public TaskType TaskType { get; }

    /// <summary>What it is for.</summary>
    public string Description { get; }

    /// <summary>Whether it runs beside the conversation rather than for it.</summary>
    public bool? Ambient { get; internal init; }
}
