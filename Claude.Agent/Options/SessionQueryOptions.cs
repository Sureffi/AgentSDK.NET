namespace Claude.Agent;

/// <summary>What a transcript read asks for. A limit above zero takes a page; null or zero takes everything after the offset.</summary>
public sealed class SessionQueryOptions
{
    /// <summary>The project the sessions belong to, by working directory; null searches every project.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>The size of the page. Null or zero is everything after <see cref="Offset"/>.</summary>
    public int? Limit { get; set; }

    /// <summary>How many to skip from the start.</summary>
    public int Offset { get; set; }

    /// <summary>Include the sessions of the working directory's git worktrees.</summary>
    public bool IncludeWorktrees { get; set; } = true;
}
