using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace Claude.Agent;

/// <summary>
/// What one call asks of the binary: which session it is, and everything that session runs with,
/// layered over <see cref="ClaudeAgentClientOptions.Defaults"/>. A scalar or a selection: the call's
/// value wins, and null takes the default. Anything that accumulates: the call adds to the defaults,
/// and a default always holds. Three accumulate: the hooks; <see cref="McpServers"/> and
/// <see cref="Agents"/>, by key; and <see cref="AllowedTools"/>, <see cref="DisallowedTools"/>,
/// <see cref="AdditionalDirectories"/>, <see cref="PluginDirectories"/> and <see cref="Betas"/>.
/// <see cref="Tools"/> and <see cref="SettingSources"/> are selections: an empty list is a value,
/// none. What a member's summary says of null is said of null after the defaults.
/// </summary>
public sealed class ClaudeAgentSessionOptions
{
    private JsonElement? _outputSchema;
    private Type? _answerType;
    private readonly List<HookRegistration> _hooks = [];

    /// <summary>The id the new session is given; a UUID. Null lets the binary choose.</summary>
    public string? SessionId { get; set; }

    /// <summary>The session to resume: its id, or a title the binary knows.</summary>
    public string? ResumeSessionId { get; set; }

    /// <summary>Resume the most recent session in the working directory.</summary>
    public bool ContinueLatest { get; set; }

    /// <summary>Resume into a new session id instead of the one resumed.</summary>
    public bool Fork { get; set; }

    /// <summary>Resume at this message, discarding what follows it.</summary>
    public string? ResumeAtMessageId { get; set; }

    /// <summary>The uuid of the user prompt that opened the turn a truncating resume discards; the binary verifies it.</summary>
    public string? DroppedTurnPromptId { get; set; }

    /// <summary>The directory the binary runs in; must be absolute. Null is the process's own.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>The system prompt: replaced, read from a file, or the binary's own with text appended. Null takes the default, and with no default the binary runs with an empty one; <see cref="AgentSystemPrompt.Preset"/> keeps its own.</summary>
    public AgentSystemPrompt? SystemPrompt { get; set; }

    /// <summary>The model, by alias ("haiku", "sonnet", "opus") or full id. Null is the binary's default.</summary>
    public string? Model { get; set; }

    /// <summary>The model the binary falls back to when <see cref="Model"/> is overloaded.</summary>
    public string? FallbackModel { get; set; }

    /// <summary>How much effort the model spends on a turn. Null is the binary's default.</summary>
    public Effort? Effort { get; set; }

    /// <summary>Extended thinking: adaptive, a fixed token budget, or off. Null is the binary's default.</summary>
    public ThinkingOptions? Thinking { get; set; }

    /// <summary>Beta feature names, e.g. "context-1m-2025-08-07". A call's are added to the defaults'.</summary>
    public IList<string> Betas { get; } = [];

    /// <summary>The most turns one prompt may take before the result is an error. Zero is no limit.</summary>
    public int? MaxTurns { get; set; }

    /// <summary>The most one session may cost, in US dollars, before the binary stops it; must be greater than zero.</summary>
    public decimal? MaxBudgetUsd { get; set; }

    /// <summary>The token budget passed as --task-budget; must not be negative.</summary>
    public int? TaskBudget { get; set; }

    /// <summary>The tools the model has. An empty list is no tools at all; null takes the default, and with no default the binary's own set.</summary>
    public IList<string>? Tools { get; set; }

    /// <summary>Permission rules that allow without asking, in the binary's rule syntax: "Read", "Bash(git:*)". A call's are added to the defaults'.</summary>
    public IList<string> AllowedTools { get; } = [];

    /// <summary>Permission rules that deny without asking, in the same syntax as <see cref="AllowedTools"/>. A call's are added to the defaults'.</summary>
    public IList<string> DisallowedTools { get; } = [];

    /// <summary>How the binary treats a tool use no rule decides. Null is the binary's default.</summary>
    public PermissionMode? PermissionMode { get; set; }

    /// <summary>An MCP tool the binary asks for permission instead of this process. One selection with <see cref="PermissionCallback"/>: a call that sets either replaces the default's pair.</summary>
    public string? PermissionPromptTool { get; set; }

    /// <summary>Decides each tool use the binary would otherwise ask a person about.</summary>
    public Func<ToolPermissionRequest, CancellationToken, Task<ToolPermissionResult>>? PermissionCallback { get; set; }

    /// <summary>The MCP servers the session starts with, by name: external ones the binary launches or connects to, and in-process ones this library serves. A call's are added to the defaults', and one under a default's name replaces it.</summary>
    public IDictionary<string, McpServerConfiguration> McpServers { get; } = new OrderedDictionary<string, McpServerConfiguration>(StringComparer.Ordinal);

    /// <summary>Use only the servers in <see cref="McpServers"/>, ignoring every MCP configuration on disk.</summary>
    public bool? OnlyConfiguredMcpServers { get; set; }

    /// <summary>Subagents the model may start through the Agent tool, by the name it starts them by. A call's are added to the defaults', and one under a default's name replaces it.</summary>
    public IDictionary<string, AgentDefinition> Agents { get; } = new OrderedDictionary<string, AgentDefinition>(StringComparer.Ordinal);

    /// <summary>Skills enabled by name. One selection with <see cref="AllSkills"/>: a call that sets either replaces the default's pair.</summary>
    public IList<string> Skills { get; } = [];

    /// <summary>Enable every skill the binary finds.</summary>
    public bool? AllSkills { get; set; }

    /// <summary>Directories the binary loads plugins from. A call's are added to the defaults'.</summary>
    public IList<string> PluginDirectories { get; } = [];

    /// <summary>Settings for the binary: a JSON object or the path of a settings file.</summary>
    public string? Settings { get; set; }

    /// <summary>Which settings files the binary reads. An empty list is none; null takes the default, and with no default the binary's own choice.</summary>
    public IList<SettingSource>? SettingSources { get; set; }

    /// <summary>The sandbox the binary runs shell commands in. Null leaves sandboxing to the settings files.</summary>
    public SandboxOptions? Sandbox { get; set; }

    /// <summary>Directories beyond the working directory the tools may reach. A call's are added to the defaults'.</summary>
    public IList<string> AdditionalDirectories { get; } = [];

    /// <summary>Yield a <see cref="StreamEventMessage"/> for each partial-message event as the model streams, ahead of the finished <see cref="AssistantMessage"/>.</summary>
    public bool? IncludePartialMessages { get; set; }

    /// <summary>Have the binary report hook lifecycle events as <see cref="SystemMessage"/> frames: "hook_started" and "hook_response".</summary>
    public bool? IncludeHookEvents { get; set; }

    /// <summary>Ask the binary to forward a subagent's text as messages carrying <see cref="AssistantMessage.ParentToolUseId"/>.</summary>
    public bool? ForwardSubagentText { get; set; }

    /// <summary>Echo each prompt back as a <see cref="UserMessage"/> with its id, the checkpoint <see cref="ClaudeAgentSession.RewindFilesAsync"/> takes.</summary>
    public bool? ReplayUserMessages { get; set; }

    /// <summary>A JSON schema the final answer must satisfy, handed to the binary as it stands; the answer is <see cref="AgentTrace.GetStructuredOutput{T}(JsonSerializerOptions)"/>'s. <see cref="SetOutputSchema{T}(JsonSerializerOptions, JsonSchemaExporterOptions)"/> takes one from a type. Its root is held to the rule a schema taken from a type is: a root whose type is not <c>"object"</c>, a nullable <c>["object", "null"]</c> among them, or that has <c>anyOf</c>, <c>oneOf</c> or <c>allOf</c> at it, is an <see cref="ArgumentException"/> before any process starts: when the client is made, on <see cref="ClaudeAgentClientOptions.Defaults"/>, and as the call is made. The root is read off the element as it is held, never parsed again, so an element read with comments or trailing commas allowed, or nested deeper than a reader's default, is judged by its root like any other, and a root that repeats a key by its last <c>"type"</c>, as the binary reads it. JSON null is no schema. A schema set here names no type: the trace's <see cref="AgentTrace.AnswerType"/> is null for it.</summary>
    public JsonElement? OutputSchema
    {
        get => _outputSchema;
        set
        {
            _outputSchema = value?.Clone();
            _answerType = null;
        }
    }

    // The type the schema was taken from, which the trace names: set by SetOutputSchema<T>, and none
    // for a schema handed in.
    internal Type? AnswerType => _answerType;

    /// <summary>Have the binary checkpoint the files it edits, so <see cref="ClaudeAgentSession.RewindFilesAsync"/> can restore them.</summary>
    public bool? EnableFileCheckpointing { get; set; }

    /// <summary>
    /// Sets <see cref="OutputSchema"/> to the schema of <typeparamref name="T"/>, written by the
    /// platform's own exporter off the contract <paramref name="serializerOptions"/> holds for it,
    /// or <see cref="JsonSerializerOptions.Default"/> when that is null, in a form the API accepts
    /// and saying the type whole. The root is <c>"object"</c> and never nullable, for a class as for
    /// a struct, and where the exporter wrote it nullable, each reference back to it,
    /// <c>{"$ref": "#"}</c>, becomes <c>{"anyOf": [{"$ref": "#"}, {"type": "null"}]}</c>, so it
    /// still admits the null the type reads there. A member's
    /// <see cref="System.ComponentModel.DescriptionAttribute"/>, on the property
    /// or on the constructor parameter that sets it, is its <c>description</c>, and a type's is the
    /// description of every node of that type whose member has none. A member's
    /// <see cref="System.ComponentModel.DataAnnotations.MaxLengthAttribute"/>,
    /// <see cref="System.ComponentModel.DataAnnotations.StringLengthAttribute"/> and
    /// <see cref="System.ComponentModel.DataAnnotations.LengthAttribute"/> set <c>maxLength</c> on a
    /// string and <c>maxItems</c> on an array, the least of them when there are several; on a byte
    /// array, which the contract writes as base64, they count bytes, and <c>maxLength</c> is the
    /// base64 length of that many. An attribute on a member an override overrides is the
    /// override's. Every
    /// object the contract reads member by member, and which keeps no extension data, has
    /// <c>additionalProperties</c> false. The exporter itself writes an enum's names where the
    /// contract writes that enum as strings, and <c>required</c>: the required members, those
    /// marked <see cref="System.Text.Json.Serialization.JsonRequiredAttribute"/>, and the
    /// constructor parameters that have no default. <paramref name="exporterOptions"/>'s settings
    /// apply, and its <see cref="JsonSchemaExporterOptions.TransformSchemaNode"/> runs over each
    /// node after the library's work. The same serializer options read the answer back in
    /// <see cref="AgentTrace.GetStructuredOutput{T}(JsonSerializerOptions)"/>, so the schema asked
    /// for and the type read are one, and the trace's <see cref="AgentTrace.AnswerType"/> names
    /// <typeparamref name="T"/>. Everything is thrown before any process starts: what the
    /// exporter throws comes out where it stands, <see cref="NotSupportedException"/> for options it
    /// does not export through and <see cref="InvalidOperationException"/> for a contract it
    /// refuses, and a schema whose root is not an object, as a primitive's or a collection's is, or
    /// has <c>anyOf</c>, <c>oneOf</c> or <c>allOf</c> at its root, as a polymorphic type's has,
    /// which the API does not accept, is an <see cref="ArgumentException"/>.
    /// </summary>
    public void SetOutputSchema<T>(JsonSerializerOptions? serializerOptions = null, JsonSchemaExporterOptions? exporterOptions = null)
    {
        var contract = serializerOptions ?? JsonSerializerOptions.Default;

        // What JsonSerializer does on its first read through these options: give them the reflecting
        // resolver when they carry none. The exporter alone refuses them instead.
        contract.MakeReadOnly(populateMissingResolver: true);
        OutputSchema = SchemaExporter.Export(contract, typeof(T), exporterOptions);
        _answerType = typeof(T);
    }

    /// <summary>
    /// Sets <see cref="OutputSchema"/> to the schema of <typeparamref name="T"/> written off
    /// <paramref name="jsonTypeInfo"/>, the contract a source generator wrote for it, so the export
    /// needs no reflection. The schema is the one <see cref="SetOutputSchema{T}(JsonSerializerOptions, JsonSchemaExporterOptions)"/>
    /// writes, <paramref name="exporterOptions"/> applies as it does there, and the trace's
    /// <see cref="AgentTrace.AnswerType"/> names <typeparamref name="T"/>. What the exporter
    /// throws comes out as it threw it; a root that is not an object, or has <c>anyOf</c>,
    /// <c>oneOf</c> or <c>allOf</c> at it, is an
    /// <see cref="ArgumentException"/>, and a null contract an <see cref="ArgumentNullException"/>,
    /// as it is to the platform.
    /// </summary>
    public void SetOutputSchema<T>(JsonTypeInfo<T> jsonTypeInfo, JsonSchemaExporterOptions? exporterOptions = null)
    {
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);
        OutputSchema = SchemaExporter.Export(jsonTypeInfo, exporterOptions, nameof(jsonTypeInfo));
        _answerType = typeof(T);
    }

    internal IReadOnlyList<HookRegistration> Hooks => _hooks;

    /// <summary>Runs <paramref name="callback"/> when the binary fires <paramref name="hookEvent"/> for a tool matching <paramref name="matcher"/> (null: every tool). The defaults' hooks run and a call's run too, the defaults' first. <paramref name="timeout"/> reaches the binary once for an event and a matcher: hooks that share both share the longest of theirs, the defaults' and a call's alike.</summary>
    public void AddHook(
        HookEvent hookEvent,
        string? matcher,
        Func<HookInput, CancellationToken, Task<HookOutput>> callback,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (hookEvent.ToString().Length == 0)
        {
            throw new ArgumentException("A hook names the event it runs on; the value is empty.", nameof(hookEvent));
        }

        if (timeout is { } bound && bound <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "A hook timeout must be positive.");
        }

        _hooks.Add(new HookRegistration(hookEvent, matcher, callback, timeout));
    }

    // The call over the defaults, member by member, as a copy neither side can reach afterwards. A
    // scalar or a selection is the call's when it has one; what accumulates is the defaults' and then
    // what the call adds, so a default always holds. Which session it is comes from the call alone.
    // The two pairs are one selection each, so a call that chooses is not refused for what the
    // defaults chose.
    internal static ClaudeAgentSessionOptions Layer(ClaudeAgentSessionOptions defaults, ClaudeAgentSessionOptions? call)
    {
        call ??= new ClaudeAgentSessionOptions();

        var choosesPermission = call.PermissionCallback is not null || !string.IsNullOrEmpty(call.PermissionPromptTool);
        var choosesSkills = call.AllSkills is not null || call.Skills.Count > 0;

        var layered = new ClaudeAgentSessionOptions
        {
            SessionId = call.SessionId,
            ResumeSessionId = call.ResumeSessionId,
            ContinueLatest = call.ContinueLatest,
            Fork = call.Fork,
            ResumeAtMessageId = call.ResumeAtMessageId,
            DroppedTurnPromptId = call.DroppedTurnPromptId,
            WorkingDirectory = call.WorkingDirectory ?? defaults.WorkingDirectory,
            SystemPrompt = call.SystemPrompt ?? defaults.SystemPrompt,
            Model = call.Model ?? defaults.Model,
            FallbackModel = call.FallbackModel ?? defaults.FallbackModel,
            Effort = call.Effort ?? defaults.Effort,
            Thinking = call.Thinking ?? defaults.Thinking,
            MaxTurns = call.MaxTurns ?? defaults.MaxTurns,
            MaxBudgetUsd = call.MaxBudgetUsd ?? defaults.MaxBudgetUsd,
            TaskBudget = call.TaskBudget ?? defaults.TaskBudget,
            Tools = (call.Tools ?? defaults.Tools) is { } tools ? [.. tools] : null,
            PermissionMode = call.PermissionMode ?? defaults.PermissionMode,
            PermissionPromptTool = choosesPermission ? call.PermissionPromptTool : defaults.PermissionPromptTool,
            PermissionCallback = choosesPermission ? call.PermissionCallback : defaults.PermissionCallback,
            OnlyConfiguredMcpServers = call.OnlyConfiguredMcpServers ?? defaults.OnlyConfiguredMcpServers,
            AllSkills = choosesSkills ? call.AllSkills : defaults.AllSkills,
            Settings = call.Settings ?? defaults.Settings,
            SettingSources = (call.SettingSources ?? defaults.SettingSources) is { } sources ? [.. sources] : null,
            Sandbox = (call.Sandbox ?? defaults.Sandbox)?.Clone(),
            IncludePartialMessages = call.IncludePartialMessages ?? defaults.IncludePartialMessages,
            IncludeHookEvents = call.IncludeHookEvents ?? defaults.IncludeHookEvents,
            ForwardSubagentText = call.ForwardSubagentText ?? defaults.ForwardSubagentText,
            ReplayUserMessages = call.ReplayUserMessages ?? defaults.ReplayUserMessages,
            OutputSchema = call.OutputSchema ?? defaults.OutputSchema,
            EnableFileCheckpointing = call.EnableFileCheckpointing ?? defaults.EnableFileCheckpointing,
        };

        Union(defaults.Betas, call.Betas, layered.Betas);
        Union(defaults.AllowedTools, call.AllowedTools, layered.AllowedTools);
        Union(defaults.DisallowedTools, call.DisallowedTools, layered.DisallowedTools);
        Union(defaults.PluginDirectories, call.PluginDirectories, layered.PluginDirectories);
        Union(defaults.AdditionalDirectories, call.AdditionalDirectories, layered.AdditionalDirectories);
        Union(choosesSkills ? call.Skills : defaults.Skills, [], layered.Skills);

        // By key, the default's place kept: a call's entry under a default's name replaces it there.
        foreach (var (name, server) in defaults.McpServers.Concat(call.McpServers))
        {
            layered.McpServers[name] = server;
        }

        foreach (var (name, agent) in defaults.Agents.Concat(call.Agents))
        {
            layered.Agents[name] = agent.Clone();
        }

        layered._answerType = call.OutputSchema is not null ? call._answerType : defaults._answerType;
        layered._hooks.AddRange(defaults._hooks);
        layered._hooks.AddRange(call._hooks);
        return layered;
    }

    internal ClaudeAgentSessionOptions Clone() => Layer(new ClaudeAgentSessionOptions(), this);

    // What a session forked or resumed from a live one is asked: the same, as another session.
    internal ClaudeAgentSessionOptions Resuming(string sessionId, bool fork)
    {
        var next = Clone();
        next.SessionId = null;
        next.ResumeSessionId = sessionId;
        next.ContinueLatest = false;
        next.Fork = fork;
        next.ResumeAtMessageId = null;
        next.DroppedTurnPromptId = null;
        return next;
    }

    // The defaults name no session: a default resume would make every call the same session.
    internal void ValidateAsDefaults()
    {
        if (SessionId is not null || ResumeSessionId is not null || ContinueLatest || Fork
            || ResumeAtMessageId is not null || DroppedTurnPromptId is not null)
        {
            throw Problem(
                "The defaults name no session: SessionId, ResumeSessionId, ContinueLatest, Fork, ResumeAtMessageId and DroppedTurnPromptId are set per call.",
                nameof(ClaudeAgentClientOptions.Defaults));
        }

        Validate();
    }

    // The first problem, as an ArgumentException naming the member. Every refusal here is one the
    // binary would make later or one that cannot be expressed on the command line at all.
    internal void Validate()
    {
        if (SessionId is { } id && !Ids.IsUuid(id))
        {
            throw Problem($"Session id '{id}' is not a valid UUID.", nameof(SessionId));
        }

        if (SessionId is not null && (ContinueLatest || ResumeSessionId is not null) && !Fork)
        {
            throw Problem(
                "A session id cannot be used with ContinueLatest or ResumeSessionId unless Fork is set.",
                nameof(SessionId));
        }

        if (ContinueLatest && ResumeSessionId is not null)
        {
            throw Problem("ContinueLatest and ResumeSessionId are mutually exclusive.", nameof(ResumeSessionId));
        }

        if (WorkingDirectory is not null && (string.IsNullOrWhiteSpace(WorkingDirectory) || !Path.IsPathRooted(WorkingDirectory)))
        {
            throw Problem($"A working directory must be an absolute path, got '{WorkingDirectory}'.", nameof(WorkingDirectory));
        }

        if (Effort is { } effort && effort.ToString().Length == 0)
        {
            throw Problem("Effort is set to no value; set an effort level or leave it null.", nameof(Effort));
        }

        if (PermissionMode is { } permissionMode && permissionMode.ToString().Length == 0)
        {
            throw Problem("PermissionMode is set to no value; set a mode or leave it null.", nameof(PermissionMode));
        }

        foreach (var source in SettingSources ?? [])
        {
            if (source.ToString().Length == 0)
            {
                throw Problem("A setting source is set to no value; name a settings file.", nameof(SettingSources));
            }
        }

        if (MaxTurns is < 0)
        {
            throw Problem("MaxTurns must not be negative.", nameof(MaxTurns));
        }

        if (MaxBudgetUsd is <= 0)
        {
            throw Problem("A budget must be greater than zero.", nameof(MaxBudgetUsd));
        }

        if (TaskBudget is < 0)
        {
            throw Problem("TaskBudget must not be negative.", nameof(TaskBudget));
        }

        if (Thinking is { BudgetTokens: < 0 })
        {
            throw Problem("A thinking budget must not be negative.", nameof(Thinking));
        }

        // JSON null is no schema: the command line sends none for it. The root is judged on the element
        // as it is held, never parsed again.
        if (OutputSchema is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } schema
            && OutputSchemaRoot.Problem(schema, "The output schema") is { } schemaProblem)
        {
            throw Problem(schemaProblem, nameof(OutputSchema));
        }

        if (PermissionCallback is not null && !string.IsNullOrEmpty(PermissionPromptTool))
        {
            throw Problem("PermissionCallback and PermissionPromptTool are one selection; set one of them.", nameof(PermissionPromptTool));
        }

        if (AllSkills is true && Skills.Count > 0)
        {
            throw Problem("AllSkills and Skills are one choice; set one or the other.", nameof(Skills));
        }

        foreach (var skill in Skills)
        {
            if (SkillNames.Problem(skill) is { } problem)
            {
                throw Problem(problem, nameof(Skills));
            }
        }

        foreach (var directory in AdditionalDirectories)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw Problem("An additional directory must not be blank.", nameof(AdditionalDirectories));
            }
        }

        foreach (var plugin in PluginDirectories)
        {
            if (string.IsNullOrWhiteSpace(plugin))
            {
                throw Problem("A plugin directory must not be blank.", nameof(PluginDirectories));
            }
        }

        foreach (var (name, server) in McpServers)
        {
            if (!Ids.IsKeyPart(name))
            {
                throw Problem($"'{name}' is not an MCP server name.", nameof(McpServers));
            }

            if (server.Problem(name) is { } problem)
            {
                throw Problem(problem, nameof(McpServers));
            }
        }

        foreach (var (name, agent) in Agents)
        {
            if (agent.Problem(name) is { } problem)
            {
                throw Problem(problem, nameof(Agents));
            }
        }
    }

    private static ArgumentException Problem(string message, string member) => new(message, member);

    // The defaults' entries, then the call's that are not among them already.
    private static void Union(IList<string> defaults, IList<string> call, IList<string> target)
    {
        foreach (var item in defaults.Concat(call))
        {
            if (!target.Contains(item))
            {
                target.Add(item);
            }
        }
    }
}

internal sealed record HookRegistration(
    HookEvent Event,
    string? Matcher,
    Func<HookInput, CancellationToken, Task<HookOutput>> Callback,
    TimeSpan? Timeout);
