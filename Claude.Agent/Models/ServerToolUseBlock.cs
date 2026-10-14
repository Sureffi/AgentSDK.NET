using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// The model calling a tool the API runs itself, not the binary. Such a call is not paired into a
/// <see cref="ToolCall"/>: the binary never answers it with a tool result. The API answers it with a
/// result block of the tool's own kind, carrying this call's <see cref="Id"/>.
/// </summary>
public sealed class ServerToolUseBlock : ContentBlock
{
    internal ServerToolUseBlock(string id, ServerToolName name, JsonElement rawInput)
    {
        Id = id;
        Name = name;
        RawInput = rawInput;
    }

    /// <summary>The call's id.</summary>
    public string Id { get; }

    /// <summary>The tool the API runs.</summary>
    public ServerToolName Name { get; }

    /// <summary>The arguments as the model wrote them. The API's server tools take their own shapes, so this library has no typed view of them.</summary>
    public JsonElement RawInput { get; }

    /// <summary>What made the call; null when the wire did not say.</summary>
    public ToolCaller? Caller { get; internal init; }

    /// <summary>The cache breakpoint on this block; null when the wire carried none.</summary>
    public CacheControl? CacheControl { get; internal init; }
}
