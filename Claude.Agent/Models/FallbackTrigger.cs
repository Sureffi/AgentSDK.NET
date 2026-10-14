namespace Claude.Agent;

/// <summary>What made a model hand its turn to the next one: a refusal, which is the one trigger the API declares.</summary>
public sealed class FallbackTrigger
{
    internal FallbackTrigger()
    {
    }

    /// <summary>The policy the request read as; null when the API named none.</summary>
    public RefusalCategory? Category { get; internal init; }
}
