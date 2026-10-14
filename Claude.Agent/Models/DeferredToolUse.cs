using System.Text.Json;

namespace Claude.Agent;

/// <summary>A tool use the binary handed back instead of running it, because a hook deferred it; the turn ends with it.</summary>
public sealed class DeferredToolUse
{
    internal DeferredToolUse(string id, string name, ToolInput? input, JsonElement rawInput)
    {
        Id = id;
        Name = name;
        Input = input;
        RawInput = rawInput;
    }

    /// <summary>The tool use's id.</summary>
    public string Id { get; }

    /// <summary>The tool the model called.</summary>
    public string Name { get; }

    /// <summary>The arguments as the tool's typed input; null for a tool this library has no type for, or arguments that do not fit it.</summary>
    public ToolInput? Input { get; }

    /// <summary>The arguments as the model wrote them.</summary>
    public JsonElement RawInput { get; }
}
