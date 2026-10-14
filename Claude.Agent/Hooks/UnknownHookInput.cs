using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// A hook input this library has no type for: an event it does not name, an event registered under
/// a name of the caller's own, or an input that does not fit its event's type. The event is
/// <see cref="HookInput.Event"/> and the whole input is <see cref="Raw"/>.
/// </summary>
public sealed class UnknownHookInput : HookInput
{
    internal UnknownHookInput(HookEvent hookEvent, HookInputCommon common)
        : base(hookEvent, common)
    {
    }

    /// <summary>The whole input as the binary wrote it.</summary>
    public JsonElement Raw => Json;
}
