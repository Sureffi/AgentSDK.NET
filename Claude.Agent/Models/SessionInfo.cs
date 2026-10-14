namespace Claude.Agent;

/// <summary>One session as a listing shows it.</summary>
public sealed class SessionInfo
{
    internal SessionInfo(string sessionId, string summary, DateTimeOffset lastModified)
    {
        SessionId = sessionId;
        Summary = summary;
        LastModified = lastModified;
    }

    /// <summary>The session's id, a UUID.</summary>
    public string SessionId { get; }

    /// <summary>The custom title, else the last prompt, else the binary's summary, else the first prompt.</summary>
    public string Summary { get; }

    /// <summary>When the transcript was last written.</summary>
    public DateTimeOffset LastModified { get; }

    /// <summary>The transcript's size on disk, in bytes; null when the transcript could not be read.</summary>
    public long? SizeBytes { get; internal init; }

    /// <summary>
    /// The title given by <see cref="ClaudeSessionClient.RenameSessionAsync"/> or the binary's
    /// <c>/rename</c>, else the title the binary generated for the session; null when it has neither.
    /// </summary>
    public string? CustomTitle { get; internal init; }

    // The title a rename gave, without the generated title in its place.
    internal string? RenamedTitle { get; init; }

    /// <summary>The first prompt of the session.</summary>
    public string? FirstPrompt { get; internal init; }

    /// <summary>The git branch the session was on when it last said.</summary>
    public string? GitBranch { get; internal init; }

    /// <summary>The directory the session ran in.</summary>
    public string? WorkingDirectory { get; internal init; }

    /// <summary>The tag given by <see cref="ClaudeSessionClient.TagSessionAsync"/>, when one was.</summary>
    public string? Tag { get; internal init; }

    /// <summary>When the transcript's first entry was written.</summary>
    public DateTimeOffset? CreatedAt { get; internal init; }
}
