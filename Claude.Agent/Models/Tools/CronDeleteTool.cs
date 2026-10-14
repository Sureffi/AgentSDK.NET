namespace Claude.Agent;

/// <summary>What the model asked the CronDelete tool to remove.</summary>
/// <param name="id">The job CronCreate handed back.</param>
public sealed class CronDeleteToolInput(string id) : ToolInput
{
    /// <summary>The job CronCreate handed back.</summary>
    public string Id { get; } = id;
}

/// <summary>What a CronDelete handed back.</summary>
/// <param name="id">The job that was removed.</param>
public sealed class CronDeleteToolOutput(string id) : ToolOutput
{
    /// <summary>The job that was removed.</summary>
    public string Id { get; } = id;
}
