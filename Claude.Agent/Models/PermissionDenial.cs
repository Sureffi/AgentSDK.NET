using System.Text.Json;

namespace Claude.Agent;

/// <summary>A tool use the binary refused during a turn, as a result lists it.</summary>
public sealed class PermissionDenial
{
    internal PermissionDenial(string toolName, string toolUseId, ToolInput? input, JsonElement rawInput)
    {
        ToolName = toolName;
        ToolUseId = toolUseId;
        Input = input;
        RawInput = rawInput;
    }

    /// <summary>The tool the model asked for.</summary>
    public string ToolName { get; }

    /// <summary>The id of the refused tool use.</summary>
    public string ToolUseId { get; }

    /// <summary>The arguments as the tool's typed input; null for a tool this library has no type for, or arguments that do not fit it.</summary>
    public ToolInput? Input { get; }

    /// <summary>The arguments as the model wrote them.</summary>
    public JsonElement RawInput { get; }
}
