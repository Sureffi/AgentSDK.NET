using System.Text.Json;

namespace Claude.Agent;

internal interface IToolServer
{
    string Name { get; }

    string Version { get; }

    IReadOnlyList<ToolDefinition> Tools { get; }

    ValueTask<ToolResult> CallAsync(string name, JsonElement arguments, CancellationToken cancellationToken);
}
