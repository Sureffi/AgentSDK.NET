namespace Claude.Agent;

/// <summary>
/// Where a turn came from, on a user frame and on the result: the person at the terminal, a peer, a
/// schedule the binary keeps. One type per kind the binary names; a kind this library has no type
/// for is an <see cref="UnknownMessageOrigin"/>.
/// </summary>
public abstract class MessageOrigin
{
    private protected MessageOrigin()
    {
    }
}

/// <summary>The turn came from the person driving the session.</summary>
public sealed class HumanOrigin : MessageOrigin
{
    internal HumanOrigin()
    {
    }
}

/// <summary>The turn came in over a channel the binary listens on.</summary>
public sealed class ChannelOrigin : MessageOrigin
{
    internal ChannelOrigin(string server) => Server = server;

    /// <summary>The server the channel runs on.</summary>
    public string Server { get; }
}

/// <summary>The turn came from another session's agent.</summary>
public sealed class PeerOrigin : MessageOrigin
{
    internal PeerOrigin(string from) => From = from;

    /// <summary>The peer that sent it.</summary>
    public string From { get; }

    /// <summary>How the peer asked for it to be delivered.</summary>
    public PeerOriginMode? FromMode { get; internal init; }

    /// <summary>What the peer calls itself.</summary>
    public string? Name { get; internal init; }

    /// <summary>The peer's session.</summary>
    public string? FromSession { get; internal init; }

    /// <summary>The peer's task, when a task sent it.</summary>
    public string? SenderTaskId { get; internal init; }

    /// <summary>What the peer sent.</summary>
    public string? Body { get; internal init; }

    /// <summary>The process id the binary verified the peer by.</summary>
    public long? VerifiedPeerProcessId { get; internal init; }
}

/// <summary>The binary opened the turn itself, after a background task reported in.</summary>
public sealed class TaskNotificationOrigin : MessageOrigin
{
    internal TaskNotificationOrigin()
    {
    }

    /// <summary>What sent it, when the binary says more than the kind.</summary>
    public TaskNotificationSource? Subkind { get; internal init; }
}

/// <summary>The turn came from the coordinator of a group of sessions.</summary>
public sealed class CoordinatorOrigin : MessageOrigin
{
    internal CoordinatorOrigin()
    {
    }
}

/// <summary>The binary does not say where the turn came from.</summary>
public sealed class UnclassifiedOrigin : MessageOrigin
{
    internal UnclassifiedOrigin()
    {
    }
}

/// <summary>The turn came from a session watching this one.</summary>
public sealed class ObserverOrigin : MessageOrigin
{
    internal ObserverOrigin(string from, string senderTaskId)
    {
        From = from;
        SenderTaskId = senderTaskId;
    }

    /// <summary>The observer that sent it.</summary>
    public string From { get; }

    /// <summary>The observer's task.</summary>
    public string SenderTaskId { get; }
}

/// <summary>The binary carried on by itself, without being asked.</summary>
public sealed class AutoContinuationOrigin : MessageOrigin
{
    internal AutoContinuationOrigin()
    {
    }
}

/// <summary>The turn is what a watching session did, reported back.</summary>
public sealed class ObserverActivityOrigin : MessageOrigin
{
    internal ObserverActivityOrigin()
    {
    }
}

/// <summary>An origin of a kind this library has no type for: the kind's name and the JSON it came in.</summary>
public sealed class UnknownMessageOrigin : MessageOrigin
{
    internal UnknownMessageOrigin(string type, System.Text.Json.JsonElement raw)
    {
        Type = type;
        Raw = raw;
    }

    /// <summary>The kind, as the binary named it.</summary>
    public string Type { get; }

    /// <summary>The origin as the binary wrote it.</summary>
    public System.Text.Json.JsonElement Raw { get; }
}
