using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// A permission update this library has no type for, or one whose members do not fit its type. It
/// is kept whole and written back as it came.
/// </summary>
public sealed class UnknownPermissionUpdate : PermissionUpdate
{
    internal UnknownPermissionUpdate(string type, JsonElement raw)
    {
        Type = type;
        Raw = raw;
    }

    /// <summary>The change as the binary named it; empty when it named none.</summary>
    public string Type { get; }

    /// <summary>The update as the binary wrote it.</summary>
    public JsonElement Raw { get; }
}
