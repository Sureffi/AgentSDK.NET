namespace Claude.Agent;

/// <summary>What the binary does when one named tool of a remote MCP server is called.</summary>
public sealed class McpServerToolPolicy
{
    /// <summary>A policy over one tool, by the name the server gives it.</summary>
    /// <param name="name">The tool's name, without the server's prefix.</param>
    /// <param name="permissionPolicy">What the binary does when the tool is called.</param>
    /// <param name="organizationMaxPermission">The ceiling the organization sets over the tool.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty or blank.</exception>
    public McpServerToolPolicy(
        string name,
        McpToolPermissionPolicy? permissionPolicy = null,
        McpToolOrganizationPermission? organizationMaxPermission = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        PermissionPolicy = permissionPolicy;
        OrganizationMaxPermission = organizationMaxPermission;
    }

    /// <summary>The tool's name, without the server's prefix.</summary>
    public string Name { get; }

    /// <summary>What the binary does when the tool is called; null when it decides for itself.</summary>
    public McpToolPermissionPolicy? PermissionPolicy { get; }

    /// <summary>The ceiling the organization sets over the tool, above whatever the session's own mode says; null when it sets none.</summary>
    public McpToolOrganizationPermission? OrganizationMaxPermission { get; }

    // A closed set is closed against no value at all: a policy built from a default struct names
    // nothing the binary could read.
    internal static string? Problem(IEnumerable<McpServerToolPolicy>? policies, string server)
    {
        foreach (var policy in policies ?? [])
        {
            if (policy.PermissionPolicy is { } permission && permission.ToString().Length == 0)
            {
                return $"MCP server '{server}' sets the policy of tool '{policy.Name}' to no value; set a policy or leave it null.";
            }

            if (policy.OrganizationMaxPermission is { } ceiling && ceiling.ToString().Length == 0)
            {
                return $"MCP server '{server}' sets the organization ceiling of tool '{policy.Name}' to no value; set a ceiling or leave it null.";
            }
        }

        return null;
    }
}
