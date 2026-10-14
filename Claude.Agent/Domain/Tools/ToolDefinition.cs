using System.Text.Json;

namespace Claude.Agent;

internal sealed record ToolAnnotations(
    string? Title = null,
    bool? ReadOnlyHint = null,
    bool? DestructiveHint = null,
    bool? IdempotentHint = null,
    bool? OpenWorldHint = null);

internal sealed record ToolDefinition(
    string Name,
    string Description,
    JsonElement InputSchema,
    ToolAnnotations? Annotations = null,
    int? MaxResultSizeChars = null);
