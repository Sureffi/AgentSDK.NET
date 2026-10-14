namespace Claude.Agent;

/// <summary>What the model asked the WebFetch tool to fetch and read.</summary>
/// <param name="url">The address to fetch.</param>
/// <param name="prompt">What to look for in what comes back.</param>
public sealed class WebFetchToolInput(string url, string prompt) : ToolInput
{
    /// <summary>The address to fetch, as the wire spells it.</summary>
    public string Url { get; } = url;

    /// <summary>What to look for in what comes back.</summary>
    public string Prompt { get; } = prompt;
}

/// <summary>An artifact version a WebFetch marked as seen.</summary>
/// <param name="slug">The artifact's name.</param>
/// <param name="version">The version of it that now counts as read.</param>
/// <param name="seeded">Whether the artifact was already in the session's context at start.</param>
public sealed class WebFetchArtifactReference(string slug, string? version = null, bool? seeded = null)
{
    /// <summary>The artifact's name.</summary>
    public string Slug { get; } = slug;

    /// <summary>The version of it that now counts as read; null when the binary named none.</summary>
    public string? Version { get; } = version;

    /// <summary>Whether the artifact was already in the session's context at start; null when the binary said nothing.</summary>
    public bool? Seeded { get; } = seeded;
}

/// <summary>What a WebFetch handed back.</summary>
/// <param name="bytes">How many bytes came back.</param>
/// <param name="code">The status code the host answered with.</param>
/// <param name="codeText">The status code's own words.</param>
/// <param name="result">What the model was told about the page.</param>
/// <param name="duration">How long the fetch took; the wire carries it in milliseconds.</param>
/// <param name="url">The address that was fetched, after any redirect the binary followed.</param>
/// <param name="artifactRead">The artifact version this fetch marked as seen.</param>
public sealed class WebFetchToolOutput(
    long bytes,
    long code,
    string codeText,
    string result,
    TimeSpan duration,
    string url,
    WebFetchArtifactReference? artifactRead = null) : ToolOutput
{
    /// <summary>How many bytes came back.</summary>
    public long Bytes { get; } = bytes;

    /// <summary>The status code the host answered with.</summary>
    public long Code { get; } = code;

    /// <summary>The status code's own words.</summary>
    public string CodeText { get; } = codeText;

    /// <summary>What the model was told about the page.</summary>
    public string Result { get; } = result;

    /// <summary>How long the fetch took; the wire carries it in milliseconds under <c>durationMs</c>.</summary>
    public TimeSpan Duration { get; } = duration;

    /// <summary>The address that was fetched, as the wire spells it.</summary>
    public string Url { get; } = url;

    /// <summary>The artifact version this fetch marked as seen; null when it marked none.</summary>
    public WebFetchArtifactReference? ArtifactRead { get; } = artifactRead;
}
