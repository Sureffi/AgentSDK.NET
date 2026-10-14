namespace Claude.Agent;

/// <summary>A tool named to the model, so it can call one a search over the tools on offer found.</summary>
public sealed class ToolReferenceBlock : ContentBlock
{
    internal ToolReferenceBlock(string toolName)
    {
        ToolName = toolName;
    }

    /// <summary>The tool, by the name the model would call it.</summary>
    public string ToolName { get; }

    /// <summary>The cache breakpoint on this block; null when the wire carried none.</summary>
    public CacheControl? CacheControl { get; internal init; }
}
