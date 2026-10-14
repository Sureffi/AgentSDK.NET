namespace Claude.Agent;

/// <summary>What the model asked the CronList tool for. The tool takes nothing: it lists every job the session holds.</summary>
public sealed class CronListToolInput : ToolInput
{
}

/// <summary>A scheduled job as CronList names it.</summary>
/// <param name="id">The job, as CronDelete takes it.</param>
/// <param name="cron">The five-field cron expression, in the machine's own time.</param>
/// <param name="humanSchedule">The schedule written out in words.</param>
/// <param name="prompt">The prompt enqueued each time it fires.</param>
/// <param name="recurring">Whether it fires at every match rather than once; null when the binary said nothing.</param>
/// <param name="durable">Whether it outlives this session; null when the binary said nothing.</param>
public sealed class CronListToolJob(
    string id,
    string cron,
    string humanSchedule,
    string prompt,
    bool? recurring = null,
    bool? durable = null)
{
    /// <summary>The job, as CronDelete takes it.</summary>
    public string Id { get; } = id;

    /// <summary>The five-field cron expression, in the machine's own time.</summary>
    public string Cron { get; } = cron;

    /// <summary>The schedule written out in words.</summary>
    public string HumanSchedule { get; } = humanSchedule;

    /// <summary>The prompt enqueued each time it fires.</summary>
    public string Prompt { get; } = prompt;

    /// <summary>Whether it fires at every match rather than once; null when the binary said nothing.</summary>
    public bool? Recurring { get; } = recurring;

    /// <summary>Whether it outlives this session; null when the binary said nothing.</summary>
    public bool? Durable { get; } = durable;
}

/// <summary>What a CronList handed back.</summary>
/// <param name="jobs">The jobs the session holds.</param>
public sealed class CronListToolOutput(IReadOnlyList<CronListToolJob> jobs) : ToolOutput
{
    /// <summary>The jobs the session holds.</summary>
    public IReadOnlyList<CronListToolJob> Jobs { get; } = jobs;
}
