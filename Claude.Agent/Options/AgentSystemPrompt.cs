namespace Claude.Agent;

/// <summary>
/// The system prompt: text of your own, a file of your own, or the binary's own prompt with
/// something appended. A call with none, its own or the defaults', runs the binary with an empty one.
/// </summary>
public sealed class AgentSystemPrompt
{
    private AgentSystemPrompt(string? text, string? filePath, string? append, bool excludeDynamicSections, bool isPreset)
    {
        Text = text;
        FilePath = filePath;
        Append = append;
        ExcludeDynamicSections = excludeDynamicSections;
        IsPreset = isPreset;
    }

    /// <summary>The prompt, when it replaces the binary's own.</summary>
    public string? Text { get; }

    /// <summary>The file the prompt is read from, when it is.</summary>
    public string? FilePath { get; }

    /// <summary>Text appended to the binary's own prompt.</summary>
    public string? Append { get; }

    /// <summary>Leave the binary's per-session sections (date, git state, directory listing) out of its prompt.</summary>
    public bool ExcludeDynamicSections { get; }

    /// <summary>True when the binary's own prompt is the base.</summary>
    public bool IsPreset { get; }

    /// <summary>Replaces the binary's prompt with <paramref name="text"/>.</summary>
    public static AgentSystemPrompt FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new AgentSystemPrompt(text, null, null, false, false);
    }

    /// <summary>Replaces the binary's prompt with the contents of the file at <paramref name="path"/>.</summary>
    public static AgentSystemPrompt FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new AgentSystemPrompt(null, path, null, false, false);
    }

    /// <summary>Keeps the binary's own prompt, with <paramref name="append"/> after it when given.</summary>
    public static AgentSystemPrompt Preset(string? append = null, bool excludeDynamicSections = false) =>
        new(null, null, append, excludeDynamicSections, true);
}
