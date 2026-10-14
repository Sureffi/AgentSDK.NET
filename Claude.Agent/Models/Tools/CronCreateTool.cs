namespace Claude.Agent;

/// <summary>What the model asked the CronCreate tool to schedule.</summary>
/// <param name="cron">The five-field cron expression, in the machine's own time.</param>
/// <param name="prompt">The prompt to enqueue each time it fires.</param>
/// <param name="recurring">Whether it fires at every match rather than once; null when the call said nothing.</param>
/// <param name="durable">Whether it outlives this session; null when the call said nothing.</param>
public sealed class CronCreateToolInput(
    string cron,
    string prompt,
    bool? recurring = null,
    bool? durable = null) : ToolInput
{
    /// <summary>The five-field cron expression, in the machine's own time.</summary>
    public string Cron { get; } = cron;

    /// <summary>The prompt to enqueue each time it fires.</summary>
    public string Prompt { get; } = prompt;

    /// <summary>Whether it fires at every match rather than once; null when the call said nothing.</summary>
    public bool? Recurring { get; } = recurring;

    /// <summary>Whether it outlives this session; null when the call said nothing.</summary>
    public bool? Durable { get; } = durable;
}

/// <summary>What a CronCreate handed back.</summary>
/// <param name="id">The job, as CronDelete takes it.</param>
/// <param name="humanSchedule">The schedule written out in words.</param>
/// <param name="recurring">Whether it fires at every match rather than once.</param>
/// <param name="durable">Whether it outlives this session; null when the binary said nothing.</param>
public sealed class CronCreateToolOutput(
    string id,
    string humanSchedule,
    bool recurring,
    bool? durable = null) : ToolOutput
{
    /// <summary>The job, as CronDelete takes it.</summary>
    public string Id { get; } = id;

    /// <summary>The schedule written out in words.</summary>
    public string HumanSchedule { get; } = humanSchedule;

    /// <summary>Whether it fires at every match rather than once.</summary>
    public bool Recurring { get; } = recurring;

    /// <summary>Whether it outlives this session; null when the binary said nothing.</summary>
    public bool? Durable { get; } = durable;
}
