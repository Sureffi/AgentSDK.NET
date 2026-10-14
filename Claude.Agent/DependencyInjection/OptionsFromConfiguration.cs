using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Claude.Agent;

// Copies a bound section onto the options. A value that is not one of its kind fails with the key
// it sits under, as an OptionsValidationException the host reports at start.
internal static class OptionsFromConfiguration
{
    public static void Apply(ClaudeAgentClientOptionsDto dto, ClaudeAgentClientOptions options, string path)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(options);

        options.ExecutablePath = dto.ExecutablePath ?? options.ExecutablePath;

        if (dto.EnvironmentVariables is { } env)
        {
            foreach (var (name, value) in env)
            {
                options.EnvironmentVariables[name] = value;
            }
        }

        options.ProcessGracePeriod = dto.ProcessGracePeriod ?? options.ProcessGracePeriod;
        options.MaxMessageSize = dto.MaxMessageSize ?? options.MaxMessageSize;
        options.ConfigurationDirectory = dto.ConfigurationDirectory ?? options.ConfigurationDirectory;

        if (dto.Defaults is { } defaults)
        {
            Apply(defaults, options.Defaults, Key(path, "Defaults"));
        }
    }

    private static void Apply(ClaudeAgentSessionOptionsDto dto, ClaudeAgentSessionOptions options, string path)
    {
        options.WorkingDirectory = dto.WorkingDirectory ?? options.WorkingDirectory;

        if (dto.SystemPrompt is { } systemPrompt)
        {
            options.SystemPrompt = Prompt(systemPrompt, Key(path, "SystemPrompt"));
        }

        options.Model = dto.Model ?? options.Model;
        options.FallbackModel = dto.FallbackModel ?? options.FallbackModel;

        if (dto.Effort is { } effort)
        {
            options.Effort = new Effort(Named(effort, Key(path, "Effort"), "an effort level"));
        }

        if (dto.Thinking is { } thinking)
        {
            options.Thinking = Thinking(thinking, Key(path, "Thinking"));
        }

        AddAll(dto.Betas, options.Betas);
        options.MaxTurns = dto.MaxTurns ?? options.MaxTurns;
        options.MaxBudgetUsd = dto.MaxBudgetUsd ?? options.MaxBudgetUsd;
        options.TaskBudget = dto.TaskBudget ?? options.TaskBudget;

        if (dto.Tools is { } tools)
        {
            options.Tools = [.. tools];
        }

        AddAll(dto.AllowedTools, options.AllowedTools);
        AddAll(dto.DisallowedTools, options.DisallowedTools);

        if (dto.PermissionMode is { } permissionMode)
        {
            options.PermissionMode = PermissionMode(permissionMode, Key(path, "PermissionMode"));
        }

        if (dto.PermissionPromptTool is { } permissionPromptTool)
        {
            options.PermissionPromptTool = Ids.IsName(permissionPromptTool)
                ? permissionPromptTool
                : throw Failure($"{Key(path, "PermissionPromptTool")}: a tool name must not be empty.");
        }

        if (dto.McpServers is { } servers)
        {
            foreach (var (name, server) in servers)
            {
                var key = Key(path, "McpServers", name);
                options.McpServers[Ids.IsKeyPart(name)
                    ? name
                    : throw Failure($"{key}: '{name}' is not an MCP server name.")] = McpServer(server, key);
            }
        }

        options.OnlyConfiguredMcpServers = dto.OnlyConfiguredMcpServers ?? options.OnlyConfiguredMcpServers;

        if (dto.Agents is { } agents)
        {
            foreach (var (name, agent) in agents)
            {
                options.Agents[name] = Agent(agent, Key(path, "Agents", name));
            }
        }

        AddAll(dto.Skills, options.Skills);
        options.AllSkills = dto.AllSkills ?? options.AllSkills;
        AddAll(dto.PluginDirectories, options.PluginDirectories);
        options.Settings = dto.Settings ?? options.Settings;

        if (dto.SettingSources is { } settingSources)
        {
            options.SettingSources =
            [
                .. settingSources.Select((source, index) => new SettingSource(
                    Named(source, Key(path, "SettingSources", Index(index)), "a settings file"))),
            ];
        }

        if (dto.Sandbox is { } sandbox)
        {
            options.Sandbox = Sandbox(sandbox);
        }

        AddAll(dto.AdditionalDirectories, options.AdditionalDirectories);
        options.IncludePartialMessages = dto.IncludePartialMessages ?? options.IncludePartialMessages;
        options.IncludeHookEvents = dto.IncludeHookEvents ?? options.IncludeHookEvents;
        options.ForwardSubagentText = dto.ForwardSubagentText ?? options.ForwardSubagentText;
        options.ReplayUserMessages = dto.ReplayUserMessages ?? options.ReplayUserMessages;

        if (dto.OutputSchema is { } outputSchema)
        {
            options.OutputSchema = Element(outputSchema, Key(path, "OutputSchema"));
        }

        options.EnableFileCheckpointing = dto.EnableFileCheckpointing ?? options.EnableFileCheckpointing;
    }

    private static AgentSystemPrompt Prompt(SystemPromptDto dto, string key)
    {
        var forms = (dto.Text is null ? 0 : 1) + (dto.File is null ? 0 : 1) + (dto.Preset == true ? 1 : 0);
        if (forms > 1)
        {
            throw Failure($"{key} sets more than one of Text, File and Preset; a system prompt is one of the three.");
        }

        return dto switch
        {
            { Text: { } text } => AgentSystemPrompt.FromText(text),
            { File: { } file } => AgentSystemPrompt.FromFile(file),
            { Preset: true } => AgentSystemPrompt.Preset(dto.Append, dto.ExcludeDynamicSections ?? false),
            _ => throw Failure($"{key} names no system prompt; set Text, File, or Preset with an Append."),
        };
    }

    private static ThinkingOptions Thinking(ThinkingDto dto, string key)
    {
        var omit = dto.OmitThinking ?? false;

        return dto.Mode switch
        {
            "adaptive" => ThinkingOptions.Adaptive(omit),
            "budget" => ThinkingOptions.WithBudget(
                dto.BudgetTokens ?? throw Failure($"{key}:BudgetTokens is required when Mode is budget."),
                omit),
            "disabled" when omit => throw Failure($"{key}:OmitThinking cannot be set when Mode is disabled."),
            "disabled" => ThinkingOptions.Disabled(),
            null => throw Failure($"{key}:Mode is required; use adaptive, budget or disabled."),
            _ => throw Failure($"{key}:Mode: '{dto.Mode}' is not a thinking mode. Use adaptive, budget or disabled."),
        };
    }

    private static SandboxOptions Sandbox(SandboxDto dto) => new()
    {
        Enabled = dto.Enabled,
        AutoAllowBashIfSandboxed = dto.AutoAllowBashIfSandboxed,
        ExcludedCommands = dto.ExcludedCommands,
        AllowUnsandboxedCommands = dto.AllowUnsandboxedCommands,
        EnableWeakerNestedSandbox = dto.EnableWeakerNestedSandbox,
        AllowedDomains = dto.AllowedDomains,
        DeniedDomains = dto.DeniedDomains,
        AllowManagedDomainsOnly = dto.AllowManagedDomainsOnly,
        AllowUnixSockets = dto.AllowUnixSockets,
        AllowAllUnixSockets = dto.AllowAllUnixSockets,
        AllowLocalBinding = dto.AllowLocalBinding,
        AllowMachLookup = dto.AllowMachLookup,
        HttpProxyPort = dto.HttpProxyPort,
        SocksProxyPort = dto.SocksProxyPort,
        IgnoredFileViolations = dto.IgnoredFileViolations,
        IgnoredNetworkViolations = dto.IgnoredNetworkViolations,
    };

    private static McpServerConfiguration McpServer(McpServerDto dto, string key) => dto.Type switch
    {
        "stdio" => McpServerConfiguration.Stdio(
            Required(dto.Command, $"{key}:Command"),
            dto.Args,
            dto.Env,
            Milliseconds(dto.Timeout, $"{key}:Timeout"),
            dto.AlwaysLoad),
        "sse" => McpServerConfiguration.Sse(
            Url(Required(dto.Url, $"{key}:Url"), $"{key}:Url"),
            dto.Headers,
            ToolPolicies(dto.Tools, key),
            Milliseconds(dto.Timeout, $"{key}:Timeout"),
            dto.AlwaysLoad),
        "http" => McpServerConfiguration.Http(
            Url(Required(dto.Url, $"{key}:Url"), $"{key}:Url"),
            dto.Headers,
            ToolPolicies(dto.Tools, key),
            Milliseconds(dto.Timeout, $"{key}:Timeout"),
            dto.AlwaysLoad),
        "sdk" => throw Failure($"{key}:Type: an in-process server is code; register it with AddClaudeTools."),
        null => throw Failure($"{key}:Type is required; use stdio, sse or http."),
        _ => throw Failure($"{key}:Type: '{dto.Type}' is not an MCP server kind. Use stdio, sse or http."),
    };

    private static List<McpServerToolPolicy>? ToolPolicies(List<McpToolPolicyDto>? tools, string key)
    {
        if (tools is null)
        {
            return null;
        }

        var policies = new List<McpServerToolPolicy>(tools.Count);
        for (var index = 0; index < tools.Count; index++)
        {
            var tool = tools[index];
            var at = $"{key}:Tools:{Index(index)}";
            policies.Add(new McpServerToolPolicy(
                Required(tool.Name, $"{at}:Name"),
                tool.PermissionPolicy is { } policy
                    ? new McpToolPermissionPolicy(Named(policy, $"{at}:PermissionPolicy", "a tool policy"))
                    : (McpToolPermissionPolicy?)null,
                tool.OrganizationMaxPermission is { } ceiling
                    ? new McpToolOrganizationPermission(Named(ceiling, $"{at}:OrganizationMaxPermission", "an organization ceiling"))
                    : (McpToolOrganizationPermission?)null));
        }

        return policies;
    }

    private static TimeSpan? Milliseconds(int? milliseconds, string key) =>
        milliseconds is { } value
            ? value >= 0 ? TimeSpan.FromMilliseconds(value) : throw Failure($"{key}: '{value}' is negative.")
            : null;

    private static AgentDefinition Agent(AgentDto dto, string key)
    {
        var agent = new AgentDefinition(Required(dto.Description, $"{key}:Description"), Required(dto.Prompt, $"{key}:Prompt"))
        {
            Tools = dto.Tools,
            DisallowedTools = dto.DisallowedTools,
            Model = dto.Model,
            Skills = dto.Skills,
            Memory = dto.Memory is { } memory ? new AgentMemory(Named(memory, $"{key}:Memory", "a memory scope")) : (AgentMemory?)null,
            McpServers = dto.McpServers,
            InitialPrompt = dto.InitialPrompt,
            MaxTurns = dto.MaxTurns,
            Background = dto.Background,
            PermissionMode = dto.PermissionMode is { } mode ? PermissionMode(mode, $"{key}:PermissionMode") : (PermissionMode?)null,
        };

        if (dto.Effort is { } effort)
        {
            if (int.TryParse(effort, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tokens))
            {
                agent.EffortTokens = tokens;
            }
            else
            {
                agent.Effort = new Effort(Named(effort, $"{key}:Effort", "an effort level or a token count"));
            }
        }

        return agent;
    }

    private static PermissionMode PermissionMode(string value, string key) =>
        new(Named(value, key, "a permission mode"));

    // A closed set binds as it stands, named by this library or not; only an empty value is refused,
    // since the binary is given a name and there is none.
    private static string Named(string value, string key, string what) =>
        value.Length == 0 ? throw Failure($"{key} is empty; it names {what}.") : value;

    private static Uri Url(string value, string key) =>
        Uri.TryCreate(value, UriKind.Absolute, out var url) ? url : throw Failure($"{key}: '{value}' is not an absolute url.");

    private static JsonElement Element(string text, string key)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        catch (JsonException error)
        {
            throw Failure($"{key}: the value is not JSON. {error.Message}");
        }
    }

    private static string Required(string? value, string key) =>
        string.IsNullOrEmpty(value) ? throw Failure($"{key} is required.") : value;

    private static void AddAll(List<string>? source, IList<string> target)
    {
        if (source is null)
        {
            return;
        }

        foreach (var value in source)
        {
            target.Add(value);
        }
    }

    private static string Index(int index) => index.ToString(CultureInfo.InvariantCulture);

    private static string Key(string path, params string[] parts) =>
        string.Join(':', parts.Prepend(path).Where(part => part.Length > 0));

    private static OptionsValidationException Failure(string message) =>
        new(string.Empty, typeof(ClaudeAgentClientOptions), [message]);
}
