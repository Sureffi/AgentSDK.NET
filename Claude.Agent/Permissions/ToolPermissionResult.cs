using System.Text.Json;

namespace Claude.Agent;

/// <summary>Allow the tool, optionally with a rewritten input and permission updates, or deny it with a message.</summary>
public sealed class ToolPermissionResult
{
    private ToolPermissionResult(bool isAllowed)
    {
        IsAllowed = isAllowed;
    }

    /// <summary>Whether the tool runs.</summary>
    public bool IsAllowed { get; }

    /// <summary>The input the tool runs with instead of the one requested, as the tool's own type; null when none was given, or when <see cref="RawUpdatedInput"/> was given instead.</summary>
    public ToolInput? UpdatedInput { get; private init; }

    /// <summary>The input the tool runs with instead of the one requested, as JSON. Set it for a tool this library has no type for, and for an MCP server's tool, whose shape is someone else's. Set beside <see cref="UpdatedInput"/> it is the one that goes out.</summary>
    public JsonElement? RawUpdatedInput { get; private init; }

    /// <summary>Permission changes the binary applies with the allow; a request's <see cref="ToolPermissionRequest.Suggestions"/> handed back is "always allow".</summary>
    public IReadOnlyList<PermissionUpdate>? UpdatedPermissions { get; private init; }

    /// <summary>The denial the model reads.</summary>
    public string Message { get; private init; } = string.Empty;

    /// <summary>Deny and end the turn.</summary>
    public bool Interrupt { get; private init; }

    /// <summary>Runs the tool, with an input in place of the requested one when one is given. <paramref name="rawUpdatedInput"/> wins over <paramref name="updatedInput"/> when both are.</summary>
    public static ToolPermissionResult Allow(
        ToolInput? updatedInput = null,
        JsonElement? rawUpdatedInput = null,
        IEnumerable<PermissionUpdate>? updatedPermissions = null) =>
        new(true)
        {
            UpdatedInput = updatedInput,
            RawUpdatedInput = rawUpdatedInput?.Clone(),
            UpdatedPermissions = updatedPermissions is null ? null : [.. updatedPermissions],
        };

    /// <summary>Does not run the tool. The model reads <paramref name="message"/>; <paramref name="interrupt"/> ends the turn as well.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
    public static ToolPermissionResult Deny(string message = "", bool interrupt = false)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new ToolPermissionResult(false) { Message = message, Interrupt = interrupt };
    }
}
