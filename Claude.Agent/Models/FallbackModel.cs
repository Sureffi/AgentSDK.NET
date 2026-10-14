namespace Claude.Agent;

/// <summary>One end of a fallback hop: the model the turn handed over from, or to.</summary>
public sealed class FallbackModel
{
    internal FallbackModel(string model)
    {
        Model = model;
    }

    /// <summary>The model, by the name the API answered with: what the caller asked for on the declining side, the canonical id on the other.</summary>
    public string Model { get; }
}
