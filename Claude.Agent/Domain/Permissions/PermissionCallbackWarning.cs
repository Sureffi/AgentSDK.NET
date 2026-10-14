namespace Claude.Agent;

// What a session's options leave to its permission callback, by the binary's permission flow: a tool
// call is decided by the deny rules, then the ask rules and the tool's own check, then the mode and
// the allow rules, and a call none of them decided is the one the binary asks the callback about.
// Under dontAsk the binary denies such a call instead, so the callback is never asked. Under
// bypassPermissions the mode allows every call no rule or safety check has stopped first, and an allow
// rule that names a whole tool allows that tool's calls the same way, so the callback is asked about
// them only when an ask rule or a safety check holds one back.
internal static class PermissionCallbackWarning
{
    public static string? For(PermissionMode? mode, IEnumerable<string> allowedTools)
    {
        ArgumentNullException.ThrowIfNull(allowedTools);

        if (mode == PermissionMode.DontAsk)
        {
            return "PermissionMode is dontAsk: the binary denies every tool call it would ask about, so the permission callback is never called.";
        }

        if (mode == PermissionMode.BypassPermissions)
        {
            return "PermissionMode is bypassPermissions: the binary allows every tool call no rule or safety check stops first, "
                + "so the permission callback is called only for a call an ask rule or a safety check holds back.";
        }

        var whole = ToolRuleSet.Parse(allowedTools).WholeTools.ToArray();
        if (whole.Length == 0)
        {
            return null;
        }

        return $"AllowedTools allows {string.Join(", ", whole)} whole: the binary allows those calls without asking, "
            + "so the permission callback is called for them only when an ask rule or a safety check holds one back.";
    }
}
