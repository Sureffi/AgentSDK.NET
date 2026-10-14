namespace Claude.Agent;

/// <summary>Where one model stopped and another took the turn over: the boundary between what each wrote.</summary>
public sealed class FallbackBlock : ContentBlock
{
    internal FallbackBlock(FallbackModel from, FallbackModel to, FallbackTrigger trigger)
    {
        From = from;
        To = to;
        Trigger = trigger;
    }

    /// <summary>The model whose output ends here.</summary>
    public FallbackModel From { get; }

    /// <summary>The model that wrote what follows.</summary>
    public FallbackModel To { get; }

    /// <summary>What made the handover.</summary>
    public FallbackTrigger Trigger { get; }
}
