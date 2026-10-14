namespace Claude.Agent;

/// <summary>A tool the API runs on its own side, by the name it calls it. A name this library does not name is read as it is.</summary>
public readonly struct ServerToolName : IEquatable<ServerToolName>
{
    private readonly string? _value;

    /// <summary>Takes the name the binary gives the value; one this library does not name is kept as it is.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public ServerToolName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    /// <summary><c>advisor</c>: a second model the API asks before answering.</summary>
    public static ServerToolName Advisor { get; } = new("advisor");

    /// <summary><c>web_search</c>: a search the API runs.</summary>
    public static ServerToolName WebSearch { get; } = new("web_search");

    /// <summary><c>web_fetch</c>: a page the API fetches.</summary>
    public static ServerToolName WebFetch { get; } = new("web_fetch");

    /// <summary><c>code_execution</c>: code the API runs in its container.</summary>
    public static ServerToolName CodeExecution { get; } = new("code_execution");

    /// <summary><c>bash_code_execution</c>: a shell command the API runs in its container.</summary>
    public static ServerToolName BashCodeExecution { get; } = new("bash_code_execution");

    /// <summary><c>text_editor_code_execution</c>: a file the API views or edits in its container.</summary>
    public static ServerToolName TextEditorCodeExecution { get; } = new("text_editor_code_execution");

    /// <summary><c>tool_search_tool_regex</c>: a search over the tools on offer, by pattern.</summary>
    public static ServerToolName ToolSearchToolRegex { get; } = new("tool_search_tool_regex");

    /// <summary><c>tool_search_tool_bm25</c>: a search over the tools on offer, by relevance.</summary>
    public static ServerToolName ToolSearchToolBm25 { get; } = new("tool_search_tool_bm25");

    /// <summary>Whether the value is one this library names.</summary>
    public bool IsDefined =>
        _value is "advisor" or "web_search" or "web_fetch" or "code_execution" or "bash_code_execution"
            or "text_editor_code_execution" or "tool_search_tool_regex" or "tool_search_tool_bm25";

    /// <summary>Whether two values are the same name.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when both carry the same name.</returns>
    public static bool operator ==(ServerToolName left, ServerToolName right) => left.Equals(right);

    /// <summary>Whether two values are different names.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>True when the names differ.</returns>
    public static bool operator !=(ServerToolName left, ServerToolName right) => !left.Equals(right);

    /// <summary>Takes a name the binary would write, named by this library or not.</summary>
    /// <param name="value">The name as it travels on the wire.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static implicit operator ServerToolName(string value) => new(value);

    /// <summary>Whether this is the same name as <paramref name="other"/>, compared ordinally.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns>True when both carry the same name.</returns>
    public bool Equals(ServerToolName other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="obj"/> is a <see cref="ServerToolName"/> of the same name.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True when it is one and both carry the same name.</returns>
    public override bool Equals(object? obj) => obj is ServerToolName other && Equals(other);

    /// <summary>A hash of the name, ordinal.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>The name as it travels on the wire; empty for a value never given one.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => _value ?? string.Empty;
}
