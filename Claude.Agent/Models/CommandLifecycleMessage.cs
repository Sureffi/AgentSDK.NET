using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// Where a prompt stands with the binary. The binary writes one for each change of a prompt that
/// carries a uuid, as every prompt a session sends does; a query's prompt carries none and gets none.
/// A session's run takes the frames of its prompt's turn from the one whose <see cref="State"/> is
/// <see cref="CommandLifecycleState.Started"/> to that turn's result.
/// </summary>
public sealed class CommandLifecycleMessage : AgentMessage
{
    internal CommandLifecycleMessage(string commandUuid, CommandLifecycleState state, string? uuid, string? sessionId, JsonElement? raw)
        : base(uuid, sessionId, raw)
    {
        CommandUuid = commandUuid;
        State = state;
    }

    /// <summary>The uuid of the prompt this frame is about: the one written with it.</summary>
    public string CommandUuid { get; }

    /// <summary>What became of the prompt; a state this library does not name is kept as the binary wrote it.</summary>
    public CommandLifecycleState State { get; }
}
