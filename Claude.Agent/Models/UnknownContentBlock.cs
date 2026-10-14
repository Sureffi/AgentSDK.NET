using System.Text.Json;

namespace Claude.Agent;

/// <summary>A block of a kind this library has no type for, or one that does not fit its kind's type: its kind's name and the block as written.</summary>
public sealed class UnknownContentBlock : ContentBlock
{
    internal UnknownContentBlock(string type, JsonElement raw)
    {
        Type = type;
        Raw = raw;
    }

    /// <summary>The block's <c>type</c>.</summary>
    public string Type { get; }

    /// <summary>The block as the binary wrote it.</summary>
    public JsonElement Raw { get; }
}
