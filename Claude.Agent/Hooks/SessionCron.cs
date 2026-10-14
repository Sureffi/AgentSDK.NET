namespace Claude.Agent;

/// <summary>A prompt the session is set to run on a schedule, as a stop hook is told of it.</summary>
public sealed class SessionCron
{
    internal SessionCron(string id, string schedule, bool isRecurring, string prompt)
    {
        Id = id;
        Schedule = schedule;
        IsRecurring = isRecurring;
        Prompt = prompt;
    }

    /// <summary>The schedule's entry.</summary>
    public string Id { get; }

    /// <summary>When it runs, in the binary's own syntax.</summary>
    public string Schedule { get; }

    /// <summary>Whether it runs again after this time.</summary>
    public bool IsRecurring { get; }

    /// <summary>The prompt it submits.</summary>
    public string Prompt { get; }
}
