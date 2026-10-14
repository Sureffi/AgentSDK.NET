namespace Claude.Agent;

internal abstract record InboundFrame
{
    private protected InboundFrame()
    {
    }
}

internal sealed record MessageFrame(AgentMessage Message) : InboundFrame;

internal sealed record ControlRequestFrame(RequestId RequestId, ControlRequest Request) : InboundFrame;

internal sealed record ControlResponseFrame(ControlResponse Response) : InboundFrame;

internal sealed record ControlCancelFrame(RequestId RequestId) : InboundFrame;

// A line the binary writes only to show it is alive; nothing reads it.
internal sealed record KeepAliveFrame : InboundFrame;
