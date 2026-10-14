namespace Claude.Agent;

/// <summary>
/// A tool's arguments read as that tool's own type. Each tool this library types has one derived
/// type; a tool without one is read through the raw JSON beside where this would be.
/// </summary>
public abstract class ToolInput
{
    private protected ToolInput()
    {
    }
}
