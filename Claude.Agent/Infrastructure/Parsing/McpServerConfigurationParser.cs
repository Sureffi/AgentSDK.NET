using System.Text.Json;

namespace Claude.Agent;

// The configuration the binary holds for one MCP server, as it reports it on a status. A kind this
// library has no type for, or one whose members do not fit its kind, is the unknown of the set.
internal static class McpServerConfigurationParser
{
    public static McpServerConfiguration Read(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return new UnknownMcpServerConfiguration(string.Empty, element);
        }

        var type = element.StringOrNull("type");
        var timeout = Milliseconds(element.Int64OrNull("timeout"));
        var alwaysLoad = element.BoolOrNull("alwaysLoad");

        // A stdio configuration is the one whose `type` the binary may leave out: a command and no
        // type is one.
        if (type is "stdio" || (type is null && element.StringOrNull("command") is not null))
        {
            return element.StringOrNull("command") is { } command
                ? new McpStdioServerConfiguration(command)
                {
                    Arguments = Strings(element.ElementOrNull("args")),
                    EnvironmentVariables = Map(element.ElementOrNull("env")),
                    Timeout = timeout,
                    AlwaysLoad = alwaysLoad,
                }
                : new UnknownMcpServerConfiguration(type ?? string.Empty, element);
        }

        switch (type)
        {
            case "sse":
                return Url(element) is { } sse
                    ? new McpSseServerConfiguration(sse)
                    {
                        Headers = Map(element.ElementOrNull("headers")),
                        Tools = Policies(element.ElementOrNull("tools")),
                        Timeout = timeout,
                        AlwaysLoad = alwaysLoad,
                    }
                    : new UnknownMcpServerConfiguration(type, element);

            case "http":
                return Url(element) is { } http
                    ? new McpHttpServerConfiguration(http)
                    {
                        Headers = Map(element.ElementOrNull("headers")),
                        Tools = Policies(element.ElementOrNull("tools")),
                        Timeout = timeout,
                        AlwaysLoad = alwaysLoad,
                    }
                    : new UnknownMcpServerConfiguration(type, element);

            case "sdk":
                return new McpSdkServerConfiguration
                {
                    Name = Identity.ServerName(element.StringOrNull("name")),
                    Timeout = timeout,
                };

            case "claudeai-proxy":
                return new McpClaudeAIProxyServerConfiguration
                {
                    Url = Url(element),
                    Id = element.StringOrNull("id"),
                    Timeout = timeout,
                };

            default:
                return new UnknownMcpServerConfiguration(type ?? string.Empty, element);
        }
    }

    private const long MaxMilliseconds = long.MaxValue / TimeSpan.TicksPerMillisecond;

    private static Uri? Url(JsonElement element) =>
        element.StringOrNull("url") is { } text && Uri.TryCreate(text, UriKind.Absolute, out var url) ? url : null;

    // A count of milliseconds as a duration, null when no TimeSpan holds it.
    private static TimeSpan? Milliseconds(long? milliseconds) =>
        milliseconds is { } value and >= -MaxMilliseconds and <= MaxMilliseconds
            ? TimeSpan.FromTicks(value * TimeSpan.TicksPerMillisecond)
            : null;

    private static string[]? Strings(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Array } items)
        {
            return null;
        }

        var values = new List<string>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                values.Add(item.GetString() ?? string.Empty);
            }
        }

        return [.. values];
    }

    private static OrderedDictionary<string, string>? Map(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Object } members)
        {
            return null;
        }

        var map = new OrderedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var member in members.EnumerateObject())
        {
            if (member.Value.ValueKind == JsonValueKind.String)
            {
                map[member.Name] = member.Value.GetString() ?? string.Empty;
            }
        }

        return map;
    }

    private static McpServerToolPolicy[]? Policies(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Array } items)
        {
            return null;
        }

        var policies = new List<McpServerToolPolicy>();
        foreach (var item in items.EnumerateArray())
        {
            if (Identity.Tool(item.StringOrNull("name")) is not { } name)
            {
                continue;
            }

            policies.Add(new McpServerToolPolicy(
                name,
                item.StringOrNull("permission_policy") is { } policy ? new McpToolPermissionPolicy(policy) : (McpToolPermissionPolicy?)null,
                item.StringOrNull("org_max_permission") is { } ceiling ? new McpToolOrganizationPermission(ceiling) : (McpToolOrganizationPermission?)null));
        }

        return [.. policies];
    }
}
