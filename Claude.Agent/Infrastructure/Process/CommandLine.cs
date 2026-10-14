using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace Claude.Agent;

// The argv the binary is started with, in the order the fixtures recorded it, built from what the
// call asks: the client's options are not handed in. The one thing read beside it is a settings
// file the call names, when a sandbox has to be merged into it. A value that cannot be expressed is
// an ArgumentException naming the option.
internal static class CommandLine
{
    public static ImmutableArray<string> Build(
        ClaudeAgentSessionOptions session,
        TransportNeeds needs,
        Func<string, string?> readFile)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(readFile);

        var permissionPromptTool = session.PermissionPromptTool;
        if (needs.RoutePermissionsOverStdio)
        {
            if (!string.IsNullOrEmpty(permissionPromptTool))
            {
                throw Refuse(
                    "PermissionCallback and PermissionPromptTool are one selection; set one of them.",
                    nameof(ClaudeAgentSessionOptions.PermissionPromptTool));
            }

            permissionPromptTool = "stdio";
        }

        var cmd = ImmutableArray.CreateBuilder<string>();
        cmd.Add("--output-format");
        cmd.Add("stream-json");
        cmd.Add("--verbose");

        switch (session.SystemPrompt)
        {
            case null:
                cmd.Add("--system-prompt");
                cmd.Add("");
                break;
            case { Text: { } text }:
                cmd.Add("--system-prompt");
                cmd.Add(text);
                break;
            case { FilePath: { } file }:
                cmd.Add("--system-prompt-file");
                cmd.Add(file);
                break;
            case { IsPreset: true, Append: { } append }:
                cmd.Add("--append-system-prompt");
                cmd.Add(append);
                break;
            default:
                break;
        }

        if (session.Tools is { } tools)
        {
            cmd.Add("--tools");
            cmd.Add(string.Join(",", tools));
        }

        var allowedTools = AllowRules.For(session);
        if (allowedTools.Length > 0)
        {
            cmd.Add("--allowedTools");
            cmd.Add(string.Join(",", allowedTools));
        }

        if (session.MaxTurns is { } maxTurns and not 0)
        {
            cmd.Add("--max-turns");
            cmd.Add(maxTurns.ToString(CultureInfo.InvariantCulture));
        }

        if (session.MaxBudgetUsd is { } maxBudgetUsd)
        {
            cmd.Add("--max-budget-usd");
            cmd.Add(WireJson.Float((double)maxBudgetUsd));
        }

        if (session.DisallowedTools.Count > 0)
        {
            cmd.Add("--disallowedTools");
            cmd.Add(string.Join(",", session.DisallowedTools));
        }

        if (session.TaskBudget is { } taskBudget)
        {
            cmd.Add("--task-budget");
            cmd.Add(taskBudget.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrEmpty(session.Model))
        {
            cmd.Add("--model");
            cmd.Add(session.Model);
        }

        if (!string.IsNullOrEmpty(session.FallbackModel))
        {
            cmd.Add("--fallback-model");
            cmd.Add(session.FallbackModel);
        }

        if (session.Betas.Count > 0)
        {
            cmd.Add("--betas");
            cmd.Add(string.Join(",", session.Betas));
        }

        if (!string.IsNullOrEmpty(permissionPromptTool))
        {
            cmd.Add("--permission-prompt-tool");
            cmd.Add(permissionPromptTool);
        }

        if (session.PermissionMode is { } permissionMode)
        {
            cmd.Add("--permission-mode");
            cmd.Add(permissionMode.ToString());
        }

        if (session.ContinueLatest)
        {
            cmd.Add("--continue");
        }

        if (session.ResumeSessionId is { Length: > 0 } resume)
        {
            cmd.Add($"--resume={resume}");
        }

        if (session.SessionId is { Length: > 0 } sessionId)
        {
            cmd.Add($"--session-id={sessionId}");
        }

        var settings = BuildSettingsValue(session, readFile);
        if (!string.IsNullOrEmpty(settings))
        {
            cmd.Add("--settings");
            cmd.Add(settings);
        }

        foreach (var directory in session.AdditionalDirectories)
        {
            cmd.Add("--add-dir");
            cmd.Add(directory);
        }

        if (session.McpServers.Count > 0)
        {
            cmd.Add("--mcp-config");
            cmd.Add(McpConfigValue(session.McpServers));
        }

        if (session.IncludePartialMessages is true)
        {
            cmd.Add("--include-partial-messages");
        }

        if (session.IncludeHookEvents is true)
        {
            cmd.Add("--include-hook-events");
        }

        if (session.OnlyConfiguredMcpServers is true)
        {
            cmd.Add("--strict-mcp-config");
        }

        if (session.Fork)
        {
            cmd.Add("--fork-session");
        }

        if (session.ResumeAtMessageId is { Length: > 0 } resumeAt)
        {
            cmd.Add($"--resume-session-at={resumeAt}");
        }

        if (session.DroppedTurnPromptId is { } droppedTurn)
        {
            cmd.Add($"--resume-drops-turn={droppedTurn}");
        }

        if (session.SettingSources is { } sources)
        {
            cmd.Add($"--setting-sources={string.Join(",", sources.Select(source => source.ToString()))}");
        }

        foreach (var plugin in session.PluginDirectories)
        {
            cmd.Add("--plugin-dir");
            cmd.Add(plugin);
        }

        if (session.ReplayUserMessages is true)
        {
            cmd.Add("--replay-user-messages");
        }

        switch (session.Thinking)
        {
            case null:
                break;
            case { IsDisabled: true }:
                cmd.Add("--thinking");
                cmd.Add("disabled");
                break;
            case { BudgetTokens: { } budget } budgeted:
                cmd.Add("--max-thinking-tokens");
                cmd.Add(budget.ToString(CultureInfo.InvariantCulture));
                AddThinkingDisplay(cmd, budgeted.OmitThinking);
                break;
            case var adaptive:
                cmd.Add("--thinking");
                cmd.Add("adaptive");
                AddThinkingDisplay(cmd, adaptive.OmitThinking);
                break;
        }

        if (session.Effort is { } effort)
        {
            cmd.Add("--effort");
            cmd.Add(effort.ToString());
        }

        if (session.OutputSchema is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } schema)
        {
            cmd.Add("--json-schema");
            cmd.Add(WireJson.Serialize(schema));
        }

        cmd.Add("--input-format");
        cmd.Add("stream-json");

        return cmd.ToImmutable();
    }

    // Only the omitted display is asked for. Without the flag the binary names no display on a
    // stream-json run, and the API's default applies: the thinking fixture's budgeted haiku run
    // came back with an empty thinking text and a signature.
    private static void AddThinkingDisplay(ImmutableArray<string>.Builder cmd, bool omitThinking)
    {
        if (omitThinking)
        {
            cmd.Add("--thinking-display");
            cmd.Add("omitted");
        }
    }

    private static ArgumentException Refuse(string message, string member) => new(message, member);

    private static string McpConfigValue(IDictionary<string, McpServerConfiguration> servers)
    {
        var writer = new WireJson();
        writer.BeginObject();
        writer.Name("mcpServers");
        writer.BeginObject();
        foreach (var (name, server) in servers)
        {
            writer.Name(name);
            WriteServer(writer, name, server);
        }

        writer.EndObject();
        writer.EndObject();
        return writer.ToString();
    }

    // The bytes of one server, in the order the declaration lists its keys: the recordings pin the
    // first of them, and what the recordings never carried is written after.
    private static void WriteServer(WireJson writer, string name, McpServerConfiguration server)
    {
        writer.BeginObject();
        switch (server)
        {
            case McpStdioServerConfiguration stdio:
                writer.Property("type", "stdio");
                writer.Property("command", stdio.Command);
                if (stdio.Arguments is { } args)
                {
                    writer.Name("args");
                    writer.Strings(args);
                }

                if (stdio.EnvironmentVariables is { } env)
                {
                    writer.Name("env");
                    writer.Map(env);
                }

                WriteTimeout(writer, stdio.Timeout);
                WriteAlwaysLoad(writer, stdio.AlwaysLoad);
                break;

            case McpSseServerConfiguration sse:
                writer.Property("type", "sse");
                writer.Property("url", sse.Url.OriginalString);
                WriteHeaders(writer, sse.Headers);
                WriteToolPolicies(writer, sse.Tools);
                WriteTimeout(writer, sse.Timeout);
                WriteAlwaysLoad(writer, sse.AlwaysLoad);
                break;

            case McpHttpServerConfiguration http:
                writer.Property("type", "http");
                writer.Property("url", http.Url.OriginalString);
                WriteHeaders(writer, http.Headers);
                WriteToolPolicies(writer, http.Tools);
                WriteTimeout(writer, http.Timeout);
                WriteAlwaysLoad(writer, http.AlwaysLoad);
                break;

            case McpSdkServerConfiguration sdk:
                writer.Property("type", "sdk");
                writer.Property("name", name);
                WriteTimeout(writer, sdk.Timeout);
                break;

            default:
                break;
        }

        writer.EndObject();
    }

    private static void WriteHeaders(WireJson writer, IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is { } members)
        {
            writer.Name("headers");
            writer.Map(members);
        }
    }

    private static void WriteToolPolicies(WireJson writer, IReadOnlyList<McpServerToolPolicy>? policies)
    {
        if (policies is not { } tools)
        {
            return;
        }

        writer.Name("tools");
        writer.BeginArray();
        foreach (var tool in tools)
        {
            writer.BeginObject();
            writer.Property("name", tool.Name);
            if (tool.PermissionPolicy is { } policy)
            {
                writer.Property("permission_policy", policy.ToString());
            }

            if (tool.OrganizationMaxPermission is { } ceiling)
            {
                writer.Property("org_max_permission", ceiling.ToString());
            }

            writer.EndObject();
        }

        writer.EndArray();
    }

    private static void WriteTimeout(WireJson writer, TimeSpan? timeout)
    {
        if (timeout is { } value)
        {
            writer.Property("timeout", (long)value.TotalMilliseconds);
        }
    }

    private static void WriteAlwaysLoad(WireJson writer, bool? alwaysLoad)
    {
        if (alwaysLoad is { } value)
        {
            writer.Property("alwaysLoad", value);
        }
    }

    private static string? BuildSettingsValue(ClaudeAgentSessionOptions options, Func<string, string?> readFile)
    {
        var hasSettings = options.Settings is not null;
        if (options.Sandbox is not { } sandbox)
        {
            return hasSettings ? options.Settings : null;
        }

        var members = new List<KeyValuePair<string, JsonElement>>();
        if (hasSettings)
        {
            using var document = LoadSettings(options.Settings!.Trim(), readFile);
            if (document is { RootElement.ValueKind: JsonValueKind.Object })
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    members.Add(new KeyValuePair<string, JsonElement>(property.Name, property.Value.Clone()));
                }
            }
        }

        var writer = new WireJson();
        writer.BeginObject();
        var merged = false;
        foreach (var (key, value) in members)
        {
            writer.Name(key);
            if (string.Equals(key, "sandbox", StringComparison.Ordinal))
            {
                WriteSandbox(writer, sandbox);
                merged = true;
            }
            else
            {
                writer.Element(value);
            }
        }

        if (!merged)
        {
            writer.Name("sandbox");
            WriteSandbox(writer, sandbox);
        }

        writer.EndObject();
        return writer.ToString();
    }

    private static JsonDocument? LoadSettings(string settings, Func<string, string?> readFile)
    {
        if (settings.StartsWith('{') && settings.EndsWith('}'))
        {
            try
            {
                return JsonDocument.Parse(settings);
            }
            catch (JsonException)
            {
                return ReadSettingsFile(settings, readFile);
            }
        }

        return ReadSettingsFile(settings, readFile);
    }

    private static JsonDocument? ReadSettingsFile(string path, Func<string, string?> readFile)
    {
        if (readFile(path) is not { } text)
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void WriteSandbox(WireJson writer, SandboxOptions sandbox)
    {
        writer.BeginObject();
        WriteBool(writer, "enabled", sandbox.Enabled);
        WriteBool(writer, "autoAllowBashIfSandboxed", sandbox.AutoAllowBashIfSandboxed);
        WriteStrings(writer, "excludedCommands", sandbox.ExcludedCommands);
        WriteBool(writer, "allowUnsandboxedCommands", sandbox.AllowUnsandboxedCommands);
        if (sandbox.HasNetwork)
        {
            writer.Name("network");
            writer.BeginObject();
            WriteStrings(writer, "allowedDomains", sandbox.AllowedDomains);
            WriteStrings(writer, "deniedDomains", sandbox.DeniedDomains);
            WriteBool(writer, "allowManagedDomainsOnly", sandbox.AllowManagedDomainsOnly);
            WriteStrings(writer, "allowUnixSockets", sandbox.AllowUnixSockets);
            WriteBool(writer, "allowAllUnixSockets", sandbox.AllowAllUnixSockets);
            WriteBool(writer, "allowLocalBinding", sandbox.AllowLocalBinding);
            WriteStrings(writer, "allowMachLookup", sandbox.AllowMachLookup);
            WriteInt(writer, "httpProxyPort", sandbox.HttpProxyPort);
            WriteInt(writer, "socksProxyPort", sandbox.SocksProxyPort);
            writer.EndObject();
        }

        if (sandbox.HasIgnoredViolations)
        {
            writer.Name("ignoreViolations");
            writer.BeginObject();
            WriteStrings(writer, "file", sandbox.IgnoredFileViolations);
            WriteStrings(writer, "network", sandbox.IgnoredNetworkViolations);
            writer.EndObject();
        }

        WriteBool(writer, "enableWeakerNestedSandbox", sandbox.EnableWeakerNestedSandbox);
        writer.EndObject();
    }

    private static void WriteBool(WireJson writer, string name, bool? value)
    {
        if (value is { } flag)
        {
            writer.Name(name);
            writer.Bool(flag);
        }
    }

    private static void WriteInt(WireJson writer, string name, int? value)
    {
        if (value is { } number)
        {
            writer.Name(name);
            writer.Number(number);
        }
    }

    private static void WriteStrings(WireJson writer, string name, IList<string>? value)
    {
        if (value is { } values)
        {
            writer.Name(name);
            writer.Strings(values);
        }
    }
}
