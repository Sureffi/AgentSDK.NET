namespace Claude.Agent;

/// <summary>Marks a method as a tool of an in-process MCP server. Its parameters become the tool's input schema.</summary>
/// <param name="name">The tool's name; the model sees it as mcp__server__name.</param>
/// <param name="description">What the tool does; the model reads this to choose.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class McpToolAttribute(string name, string description) : Attribute
{
    private bool? _readOnly;
    private bool? _destructive;
    private bool? _idempotent;
    private bool? _openWorld;

    /// <summary>The tool's name.</summary>
    public string Name { get; } = name;

    /// <summary>What the tool does.</summary>
    public string Description { get; } = description;

    /// <summary>The MCP <c>readOnlyHint</c>: the tool changes nothing.</summary>
    public bool ReadOnly
    {
        get => _readOnly ?? false;
        set => _readOnly = value;
    }

    /// <summary>The MCP <c>destructiveHint</c>: the tool may destroy something.</summary>
    public bool Destructive
    {
        get => _destructive ?? false;
        set => _destructive = value;
    }

    /// <summary>The MCP <c>idempotentHint</c>: calling it again with the same input changes nothing more.</summary>
    public bool Idempotent
    {
        get => _idempotent ?? false;
        set => _idempotent = value;
    }

    /// <summary>The MCP <c>openWorldHint</c>: the tool reaches outside this process's own world.</summary>
    public bool OpenWorld
    {
        get => _openWorld ?? false;
        set => _openWorld = value;
    }

    /// <summary>Zero leaves the limit unset.</summary>
    public int MaxResultSizeChars { get; set; }

    internal ToolAnnotations? Annotations =>
        _readOnly is null && _destructive is null && _idempotent is null && _openWorld is null
            ? null
            : new ToolAnnotations(null, _readOnly, _destructive, _idempotent, _openWorld);
}
