namespace Claude.Agent;

/// <summary>What the model asked the Agent tool to start. The binary offers the tool under the name <c>Task</c> and calls it under the name <c>Agent</c>.</summary>
/// <param name="description">What the subagent is for, in a few words.</param>
/// <param name="prompt">What the subagent is asked to do.</param>
/// <param name="subagentType">Which declared agent to start; absent means the general one.</param>
/// <param name="model">The model the subagent runs on, by the name the account knows it under.</param>
/// <param name="runInBackground">Whether the turn goes on without waiting for the subagent.</param>
/// <param name="name">A name for the subagent.</param>
/// <param name="teamName">The team the subagent belongs to, under a key the declaration marks as going.</param>
/// <param name="mode">The permission mode the subagent runs under, under a key the declaration marks as going.</param>
/// <param name="isolation">Where the subagent runs.</param>
public sealed class AgentToolInput(
    string description,
    string prompt,
    string? subagentType = null,
    string? model = null,
    bool? runInBackground = null,
    string? name = null,
    string? teamName = null,
    PermissionMode? mode = null,
    AgentIsolation? isolation = null) : ToolInput
{
    /// <summary>What the subagent is for, in a few words.</summary>
    public string Description { get; } = description;

    /// <summary>What the subagent is asked to do.</summary>
    public string Prompt { get; } = prompt;

    /// <summary>Which declared agent to start; null when the call named none.</summary>
    public string? SubagentType { get; } = subagentType;

    /// <summary>The model the subagent runs on, by the name the account knows it under; null when the call named none.</summary>
    public string? Model { get; } = model;

    /// <summary>Whether the turn goes on without waiting for the subagent; null when the call said nothing.</summary>
    public bool? RunInBackground { get; } = runInBackground;

    /// <summary>A name for the subagent; null when the call gave none.</summary>
    public string? Name { get; } = name;

    /// <summary>The team the subagent belongs to, under a key the declaration marks as going; null when the call named none.</summary>
    public string? TeamName { get; } = teamName;

    /// <summary>The permission mode the subagent runs under, under a key the declaration marks as going; null when the call named none.</summary>
    public PermissionMode? Mode { get; } = mode;

    /// <summary>Where the subagent runs; null when the call said nothing.</summary>
    public AgentIsolation? Isolation { get; } = isolation;
}

/// <summary>What an Agent handed back. The binary answers in one of three shapes, one type each.</summary>
public abstract class AgentToolOutput : ToolOutput
{
    private protected AgentToolOutput()
    {
    }
}

/// <summary>One piece of a finished subagent's answer. Its citations are left untyped by the declaration and are read through the raw output beside this.</summary>
/// <param name="text">The text itself.</param>
public sealed class AgentToolTextBlock(string text)
{
    /// <summary>The text itself.</summary>
    public string Text { get; } = text;
}

/// <summary>The server-side tools a subagent's turn used.</summary>
/// <param name="webSearchRequests">How many web searches it ran.</param>
/// <param name="webFetchRequests">How many web fetches it ran.</param>
public sealed class AgentServerToolUse(long webSearchRequests, long webFetchRequests)
{
    /// <summary>How many web searches it ran.</summary>
    public long WebSearchRequests { get; } = webSearchRequests;

    /// <summary>How many web fetches it ran.</summary>
    public long WebFetchRequests { get; } = webFetchRequests;
}

/// <summary>How a subagent's cache writes split by how long they live.</summary>
/// <param name="ephemeralOneHourInputTokens">Tokens written to the hour-long cache.</param>
/// <param name="ephemeralFiveMinuteInputTokens">Tokens written to the five-minute cache.</param>
public sealed class AgentCacheCreation(long ephemeralOneHourInputTokens, long ephemeralFiveMinuteInputTokens)
{
    /// <summary>Tokens written to the hour-long cache.</summary>
    public long EphemeralOneHourInputTokens { get; } = ephemeralOneHourInputTokens;

    /// <summary>Tokens written to the five-minute cache.</summary>
    public long EphemeralFiveMinuteInputTokens { get; } = ephemeralFiveMinuteInputTokens;
}

/// <summary>What a subagent's output tokens were spent on.</summary>
/// <param name="thinkingTokens">Tokens spent thinking.</param>
public sealed class AgentOutputTokenDetails(long? thinkingTokens = null)
{
    /// <summary>Tokens spent thinking; null when the binary gave no count.</summary>
    public long? ThinkingTokens { get; } = thinkingTokens;
}

/// <summary>
/// What a subagent's turn cost. Its iterations are left untyped by the declaration and are read
/// through the raw output beside this.
/// </summary>
/// <param name="inputTokens">Tokens read.</param>
/// <param name="outputTokens">Tokens written.</param>
/// <param name="cacheCreationInputTokens">Tokens written to the cache.</param>
/// <param name="cacheReadInputTokens">Tokens read from the cache.</param>
/// <param name="serverToolUse">The server-side tools the turn used.</param>
/// <param name="serviceTier">The tier that served the turn.</param>
/// <param name="cacheCreation">How the cache writes split by how long they live.</param>
/// <param name="inferenceGeo">Where the turn was served.</param>
/// <param name="speed">How fast the turn was served.</param>
/// <param name="outputTokenDetails">What the output tokens were spent on.</param>
public sealed class AgentToolUsage(
    long inputTokens,
    long outputTokens,
    long? cacheCreationInputTokens = null,
    long? cacheReadInputTokens = null,
    AgentServerToolUse? serverToolUse = null,
    ServiceTier? serviceTier = null,
    AgentCacheCreation? cacheCreation = null,
    string? inferenceGeo = null,
    InferenceSpeed? speed = null,
    AgentOutputTokenDetails? outputTokenDetails = null)
{
    /// <summary>Tokens read.</summary>
    public long InputTokens { get; } = inputTokens;

    /// <summary>Tokens written.</summary>
    public long OutputTokens { get; } = outputTokens;

    /// <summary>Tokens written to the cache; null when the binary sent none.</summary>
    public long? CacheCreationInputTokens { get; } = cacheCreationInputTokens;

    /// <summary>Tokens read from the cache; null when the binary sent none.</summary>
    public long? CacheReadInputTokens { get; } = cacheReadInputTokens;

    /// <summary>The server-side tools the turn used; null when the binary sent none.</summary>
    public AgentServerToolUse? ServerToolUse { get; } = serverToolUse;

    /// <summary>The tier that served the turn; null when the binary named none.</summary>
    public ServiceTier? ServiceTier { get; } = serviceTier;

    /// <summary>How the cache writes split by how long they live; null when the binary sent none.</summary>
    public AgentCacheCreation? CacheCreation { get; } = cacheCreation;

    /// <summary>Where the turn was served; null when the binary said nothing.</summary>
    public string? InferenceGeo { get; } = inferenceGeo;

    /// <summary>How fast the turn was served; null when the binary said nothing.</summary>
    public InferenceSpeed? Speed { get; } = speed;

    /// <summary>What the output tokens were spent on, under the wire key <c>output_tokens_details</c>; null when the binary sent none.</summary>
    public AgentOutputTokenDetails? OutputTokenDetails { get; } = outputTokenDetails;
}

/// <summary>How many of each kind of tool a subagent used, and what its edits came to.</summary>
/// <param name="readCount">Files read.</param>
/// <param name="searchCount">Searches run.</param>
/// <param name="bashCount">Commands run.</param>
/// <param name="editFileCount">Files edited.</param>
/// <param name="linesAdded">Lines added.</param>
/// <param name="linesRemoved">Lines removed.</param>
/// <param name="otherToolCount">Every other tool call.</param>
/// <param name="frameCount">How many frames the subagent's turn took.</param>
public sealed class AgentToolStatistics(
    long readCount,
    long searchCount,
    long bashCount,
    long editFileCount,
    long linesAdded,
    long linesRemoved,
    long otherToolCount,
    long? frameCount = null)
{
    /// <summary>Files read.</summary>
    public long ReadCount { get; } = readCount;

    /// <summary>Searches run.</summary>
    public long SearchCount { get; } = searchCount;

    /// <summary>Commands run.</summary>
    public long BashCount { get; } = bashCount;

    /// <summary>Files edited.</summary>
    public long EditFileCount { get; } = editFileCount;

    /// <summary>Lines added.</summary>
    public long LinesAdded { get; } = linesAdded;

    /// <summary>Lines removed.</summary>
    public long LinesRemoved { get; } = linesRemoved;

    /// <summary>Every other tool call.</summary>
    public long OtherToolCount { get; } = otherToolCount;

    /// <summary>How many frames the subagent's turn took; null when the binary gave no count.</summary>
    public long? FrameCount { get; } = frameCount;
}

/// <summary>An Agent whose subagent ran to the end in this turn.</summary>
/// <param name="agentId">The subagent's id.</param>
/// <param name="content">The subagent's answer.</param>
/// <param name="totalToolUseCount">How many tools the subagent used.</param>
/// <param name="totalDuration">How long it ran; the wire carries it in milliseconds.</param>
/// <param name="totalTokens">The tokens it spent in all.</param>
/// <param name="usage">What its turn cost, broken down.</param>
/// <param name="prompt">What it was asked to do.</param>
/// <param name="agentType">Which declared agent it was.</param>
/// <param name="resolvedModel">The model it actually ran on.</param>
/// <param name="modelsUsed">Every model it ran on; empty when the binary named none.</param>
/// <param name="toolStatistics">How many of each kind of tool it used; the wire key is <c>toolStats</c>.</param>
/// <param name="worktreePath">The worktree it ran in.</param>
/// <param name="worktreeBranch">The branch that worktree is on.</param>
/// <param name="harnessNoteCount">A count the recordings carry and the declaration does not.</param>
/// <param name="harnessTailCount">A count the recordings carry and the declaration does not.</param>
/// <param name="harnessSectionHash">A hash the recordings carry and the declaration does not.</param>
public sealed class AgentCompletedToolOutput(
    string agentId,
    IReadOnlyList<AgentToolTextBlock> content,
    long totalToolUseCount,
    TimeSpan totalDuration,
    long totalTokens,
    AgentToolUsage usage,
    string prompt,
    string? agentType = null,
    string? resolvedModel = null,
    IReadOnlyList<string>? modelsUsed = null,
    AgentToolStatistics? toolStatistics = null,
    string? worktreePath = null,
    string? worktreeBranch = null,
    long? harnessNoteCount = null,
    long? harnessTailCount = null,
    string? harnessSectionHash = null) : AgentToolOutput
{
    /// <summary>The subagent's id.</summary>
    public string AgentId { get; } = agentId;

    /// <summary>The subagent's answer.</summary>
    public IReadOnlyList<AgentToolTextBlock> Content { get; } = content;

    /// <summary>How many tools the subagent used.</summary>
    public long TotalToolUseCount { get; } = totalToolUseCount;

    /// <summary>How long it ran; the wire carries it in milliseconds under <c>totalDurationMs</c>.</summary>
    public TimeSpan TotalDuration { get; } = totalDuration;

    /// <summary>The tokens it spent in all.</summary>
    public long TotalTokens { get; } = totalTokens;

    /// <summary>What its turn cost, broken down.</summary>
    public AgentToolUsage Usage { get; } = usage;

    /// <summary>What it was asked to do.</summary>
    public string Prompt { get; } = prompt;

    /// <summary>Which declared agent it was; null when the binary named none.</summary>
    public string? AgentType { get; } = agentType;

    /// <summary>The model it actually ran on; null when the binary named none.</summary>
    public string? ResolvedModel { get; } = resolvedModel;

    /// <summary>Every model it ran on; empty when the binary named none.</summary>
    public IReadOnlyList<string> ModelsUsed { get; } = modelsUsed ?? [];

    /// <summary>How many of each kind of tool it used, under the wire key <c>toolStats</c>; null when the binary sent none.</summary>
    public AgentToolStatistics? ToolStatistics { get; } = toolStatistics;

    /// <summary>The worktree it ran in; null when it ran in none of its own.</summary>
    public string? WorktreePath { get; } = worktreePath;

    /// <summary>The branch that worktree is on; null when it ran in no worktree of its own.</summary>
    public string? WorktreeBranch { get; } = worktreeBranch;

    /// <summary>A count the recordings carry and the declaration does not; null when the binary sent none.</summary>
    public long? HarnessNoteCount { get; } = harnessNoteCount;

    /// <summary>A count the recordings carry and the declaration does not; null when the binary sent none.</summary>
    public long? HarnessTailCount { get; } = harnessTailCount;

    /// <summary>A hash the recordings carry and the declaration does not; null when the binary sent none.</summary>
    public string? HarnessSectionHash { get; } = harnessSectionHash;
}

/// <summary>An Agent whose subagent was started and left running, so the turn went on without it.</summary>
/// <param name="agentId">The subagent's id.</param>
/// <param name="description">What it is for, in a few words.</param>
/// <param name="prompt">What it was asked to do.</param>
/// <param name="outputFile">The file its progress is written to.</param>
/// <param name="isAsync">Whether it was started in the background; the binary writes this true or leaves it out.</param>
/// <param name="resolvedModel">The model it runs on.</param>
/// <param name="modelsUsed">Every model it has run on; empty when the binary named none.</param>
/// <param name="canReadOutputFile">Whether this session may read that file.</param>
public sealed class AgentAsyncLaunchedToolOutput(
    string agentId,
    string description,
    string prompt,
    string outputFile,
    bool? isAsync = null,
    string? resolvedModel = null,
    IReadOnlyList<string>? modelsUsed = null,
    bool? canReadOutputFile = null) : AgentToolOutput
{
    /// <summary>The subagent's id.</summary>
    public string AgentId { get; } = agentId;

    /// <summary>What it is for, in a few words.</summary>
    public string Description { get; } = description;

    /// <summary>What it was asked to do.</summary>
    public string Prompt { get; } = prompt;

    /// <summary>The file its progress is written to.</summary>
    public string OutputFile { get; } = outputFile;

    /// <summary>Whether it was started in the background; null when the binary left the key out.</summary>
    public bool? IsAsync { get; } = isAsync;

    /// <summary>The model it runs on; null when the binary named none.</summary>
    public string? ResolvedModel { get; } = resolvedModel;

    /// <summary>Every model it has run on; empty when the binary named none.</summary>
    public IReadOnlyList<string> ModelsUsed { get; } = modelsUsed ?? [];

    /// <summary>Whether this session may read that file; null when the binary said nothing.</summary>
    public bool? CanReadOutputFile { get; } = canReadOutputFile;
}

/// <summary>An Agent whose subagent was started on a machine of its own.</summary>
/// <param name="taskId">The task the subagent runs as.</param>
/// <param name="sessionUrl">Where its session can be watched.</param>
/// <param name="description">What it is for, in a few words.</param>
/// <param name="prompt">What it was asked to do.</param>
/// <param name="outputFile">The file its progress is written to.</param>
public sealed class AgentRemoteLaunchedToolOutput(
    string taskId,
    string sessionUrl,
    string description,
    string prompt,
    string outputFile) : AgentToolOutput
{
    /// <summary>The task the subagent runs as.</summary>
    public string TaskId { get; } = taskId;

    /// <summary>Where its session can be watched, as the wire spells it.</summary>
    public string SessionUrl { get; } = sessionUrl;

    /// <summary>What it is for, in a few words.</summary>
    public string Description { get; } = description;

    /// <summary>What it was asked to do.</summary>
    public string Prompt { get; } = prompt;

    /// <summary>The file its progress is written to.</summary>
    public string OutputFile { get; } = outputFile;
}
