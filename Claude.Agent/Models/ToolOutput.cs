namespace Claude.Agent;

/// <summary>
/// What a tool reported, read as that tool's own type. Each tool this library types has one derived
/// type; a tool without one is read through the raw JSON beside where this would be.
/// </summary>
public abstract class ToolOutput
{
    private protected ToolOutput()
    {
    }
}
