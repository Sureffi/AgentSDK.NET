namespace Claude.Agent;

/// <summary>A batch of tool calls has finished, before the model is asked again. The hook may add to what the model reads.</summary>
public sealed class PostToolBatchHookInput : HookInput
{
    internal PostToolBatchHookInput(HookInputCommon common,
        IReadOnlyList<ToolCall> toolCalls)
        : base(HookEvent.PostToolBatch, common)
    {
        ToolCalls = toolCalls;
    }

    /// <summary>The calls of the batch: answered where the binary reported an output, started where it did not.</summary>
    public IReadOnlyList<ToolCall> ToolCalls { get; }
}
