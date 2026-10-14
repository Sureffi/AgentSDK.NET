namespace Claude.Agent;

/// <summary>A piece of the model's answer is about to be shown. The hook may show something else in its place, without changing what is kept.</summary>
public sealed class MessageDisplayHookInput : HookInput
{
    internal MessageDisplayHookInput(HookInputCommon common,
        string turnId,
        string messageId,
        long index,
        bool isFinal,
        string delta)
        : base(HookEvent.MessageDisplay, common)
    {
        TurnId = turnId;
        MessageId = messageId;
        Index = index;
        IsFinal = isFinal;
        Delta = delta;
    }

    /// <summary>The turn the message belongs to.</summary>
    public string TurnId { get; }

    /// <summary>The message, by the identifier the binary gives it while it is being shown. Not the identifier the API gives the message.</summary>
    public string MessageId { get; }

    /// <summary>Which piece of the message this is, counted from zero.</summary>
    public long Index { get; }

    /// <summary>Whether this is the message's last piece.</summary>
    public bool IsFinal { get; }

    /// <summary>What has been finished since the piece before; empty on a last piece that ends on a newline.</summary>
    public string Delta { get; }
}
