using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// What made a tool call: the model itself, or code the API ran. Each kind the wire names is a type
/// of its own; a kind this library has no type for, or a caller that does not fit its kind's type,
/// is an <see cref="UnknownToolCaller"/>.
/// </summary>
public abstract class ToolCaller
{
    private protected ToolCaller()
    {
    }
}

/// <summary>The model called the tool itself.</summary>
public sealed class DirectToolCaller : ToolCaller
{
    internal DirectToolCaller()
    {
    }
}

/// <summary>Code the API's 2025-08-25 code execution tool ran called the tool.</summary>
public sealed class CodeExecutionToolCaller20250825 : ToolCaller
{
    internal CodeExecutionToolCaller20250825(string toolId)
    {
        ToolId = toolId;
    }

    /// <summary>The code execution call the calling code was running under.</summary>
    public string ToolId { get; }
}

/// <summary>Code the API's 2026-01-20 code execution tool ran called the tool.</summary>
public sealed class CodeExecutionToolCaller20260120 : ToolCaller
{
    internal CodeExecutionToolCaller20260120(string toolId)
    {
        ToolId = toolId;
    }

    /// <summary>The code execution call the calling code was running under.</summary>
    public string ToolId { get; }
}

/// <summary>A caller of a kind this library has no type for, or one that does not fit its kind's type: its kind's name and the caller as written.</summary>
public sealed class UnknownToolCaller : ToolCaller
{
    internal UnknownToolCaller(string type, JsonElement raw)
    {
        Type = type;
        Raw = raw;
    }

    /// <summary>The caller's <c>type</c>.</summary>
    public string Type { get; }

    /// <summary>The caller as the binary wrote it.</summary>
    public JsonElement Raw { get; }
}
