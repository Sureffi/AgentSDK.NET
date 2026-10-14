using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// A call of a tool the binary runs, paired from the two halves a run reads: the tool-use block of an
/// assistant message and the tool-result block that answers it, matched by id. A run hands each call
/// over twice, as a <see cref="ToolCallMessage"/> when it starts and again when it closes. Calls of
/// the tools the API runs itself (<see cref="ServerToolUseBlock"/>) are not paired.
/// </summary>
public sealed class ToolCall
{
    internal ToolCall(string id, string name, ToolInput? input, JsonElement rawInput, ToolCallStatus status)
    {
        Id = id;
        Name = name;
        Input = input;
        RawInput = rawInput;
        Status = status;
    }

    /// <summary>The call's id, the <see cref="ToolUseBlock.Id"/> of its tool use.</summary>
    public string Id { get; }

    /// <summary>The tool called.</summary>
    public string Name { get; }

    /// <summary>The arguments as the tool's typed input; null for a tool this library has no type for, or arguments that do not fit it.</summary>
    public ToolInput? Input { get; }

    /// <summary>The arguments as the model wrote them.</summary>
    public JsonElement RawInput { get; }

    /// <summary>Where the call stands.</summary>
    public ToolCallStatus Status { get; }

    /// <summary>What the tool reported, as its typed output; null until the call is answered, and for a tool this library has no type for or an output that does not fit it.</summary>
    public ToolOutput? Output { get; internal init; }

    /// <summary>What the tool reported, as the binary wrote it beside the result; null until the call is answered, and when the binary wrote none.</summary>
    public JsonElement? RawOutput { get; internal init; }

    /// <summary>What the model was fed as the answer, the <see cref="ToolResultBlock.Content"/>; empty until the call closes, and when it closes unanswered.</summary>
    public IReadOnlyList<ContentBlock> Content { get; internal init; } = [];

    /// <summary>The error text a <see cref="ToolCallStatus.Failed"/> call was answered with: the text blocks of its tool result, joined as the binary wrote them; null otherwise.</summary>
    public string? Error { get; internal init; }

    /// <summary>The Agent tool use that started the subagent that made this call; null on the main thread.</summary>
    public string? ParentToolUseId { get; internal init; }
}
