namespace Claude.Agent;

/// <summary>
/// One block of a message's content. Each kind the wire names is a type of its own; a kind this
/// library has no type for, or a block that does not fit its kind's type, is an
/// <see cref="UnknownContentBlock"/>.
/// </summary>
public abstract class ContentBlock
{
    private protected ContentBlock()
    {
    }
}
