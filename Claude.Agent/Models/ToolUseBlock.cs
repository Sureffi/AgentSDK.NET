using System.Text.Json;

namespace Claude.Agent;

/// <summary>The model calling a tool the binary runs. The run pairs it with the result that answers it as a <see cref="ToolCall"/>.</summary>
public sealed class ToolUseBlock : ContentBlock
{
    internal ToolUseBlock(string id, string name, ToolInput? input, JsonElement rawInput)
    {
        Id = id;
        Name = name;
        Input = input;
        RawInput = rawInput;
    }

    /// <summary>The call's id; the <see cref="ToolResultBlock"/> that answers it carries it as <see cref="ToolResultBlock.ToolUseId"/>.</summary>
    public string Id { get; }

    /// <summary>The tool called, by the name the model used.</summary>
    public string Name { get; }

    /// <summary>The arguments as the tool's typed input; null for a tool this library has no type for, or arguments that do not fit it.</summary>
    public ToolInput? Input { get; }

    /// <summary>The arguments as the model wrote them.</summary>
    public JsonElement RawInput { get; }

    /// <summary>What made the call; null when the wire did not say. Every recorded call was made by the model itself.</summary>
    public ToolCaller? Caller { get; internal init; }

    /// <summary>The family of tools this one belongs to; null when it belongs to none.</summary>
    public string? ToolsetName { get; internal init; }

    /// <summary>The cache breakpoint on this block; null when the wire carried none.</summary>
    public CacheControl? CacheControl { get; internal init; }
}
