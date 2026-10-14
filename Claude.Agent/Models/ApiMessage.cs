namespace Claude.Agent;

/// <summary>
/// The API's own message object, as the binary passes it through on an assistant frame and writes it
/// into a transcript: what the model answered and what the API said about the answer.
/// </summary>
public sealed class ApiMessage
{
    internal ApiMessage(IReadOnlyList<ContentBlock> content, string model)
    {
        Content = content;
        Model = model;
    }

    /// <summary>The blocks, in the order the model wrote them.</summary>
    public IReadOnlyList<ContentBlock> Content { get; }

    /// <summary>The model that answered, as a full id.</summary>
    public string Model { get; }

    /// <summary>The API's own id of the message.</summary>
    public string? Id { get; internal init; }

    /// <summary>Why the model stopped writing; null while the turn is still going.</summary>
    public StopReason? StopReason { get; internal init; }

    /// <summary>The sequence the model stopped on, when a stop sequence ended it.</summary>
    public string? StopSequence { get; internal init; }

    /// <summary>More about the stop, when the model refused.</summary>
    public StopDetails? StopDetails { get; internal init; }

    /// <summary>What the message spent. On an assistant frame it is the count at the message's start, not the turn's total; the result carries that.</summary>
    public Usage? Usage { get; internal init; }

    /// <summary>The container the model ran code in, when the turn used one.</summary>
    public MessageContainer? Container { get; internal init; }

    /// <summary>What the API cleared out of the context to make the turn fit.</summary>
    public ContextManagement? ContextManagement { get; internal init; }

    /// <summary>What the API says about how the request was served, the prompt cache included.</summary>
    public MessageDiagnostics? Diagnostics { get; internal init; }

    /// <summary>What the API changed about the request before answering it.</summary>
    public IReadOnlyList<InputTransformation> InputTransformations { get; internal init; } = [];
}

/// <summary>More about why the model stopped, when it refused to answer.</summary>
public sealed class StopDetails
{
    internal StopDetails(StopDetailsType type) => Type = type;

    /// <summary>What the details are about.</summary>
    public StopDetailsType Type { get; }

    /// <summary>What the refusal was about, when the API says.</summary>
    public RefusalCategory? Category { get; internal init; }

    /// <summary>The refusal in the API's own words, when it gives one.</summary>
    public string? Explanation { get; internal init; }

    /// <summary>A token that redeems the refused turn's cost, when the API grants one.</summary>
    public string? FallbackCreditToken { get; internal init; }

    /// <summary>Whether the credit claim carries a prefill.</summary>
    public bool? FallbackHasPrefillClaim { get; internal init; }

    /// <summary>The model the API suggests asking instead.</summary>
    public string? RecommendedModel { get; internal init; }
}

/// <summary>The container a turn's code execution ran in.</summary>
public sealed class MessageContainer
{
    internal MessageContainer(string id) => Id = id;

    /// <summary>The container's id.</summary>
    public string Id { get; }

    /// <summary>When the container stops being available, as the API wrote it.</summary>
    public string? ExpiresAt { get; internal init; }

    /// <summary>The skills loaded into the container.</summary>
    public IReadOnlyList<ContainerSkill> Skills { get; internal init; } = [];
}

/// <summary>A skill loaded into a container.</summary>
public sealed class ContainerSkill
{
    internal ContainerSkill(string skillId, ContainerSkillType type, string version)
    {
        SkillId = skillId;
        Type = type;
        Version = version;
    }

    /// <summary>The skill's id.</summary>
    public string SkillId { get; }

    /// <summary>Who wrote the skill.</summary>
    public ContainerSkillType Type { get; }

    /// <summary>The version loaded.</summary>
    public string Version { get; }
}

/// <summary>What the API cleared out of the context so the turn would fit.</summary>
public sealed class ContextManagement
{
    internal ContextManagement(IReadOnlyList<ContextEdit> appliedEdits) => AppliedEdits = appliedEdits;

    /// <summary>The edits the API made, in the order it made them.</summary>
    public IReadOnlyList<ContextEdit> AppliedEdits { get; }
}

/// <summary>One edit the API made to the context: what it cleared and how much.</summary>
public sealed class ContextEdit
{
    internal ContextEdit(ContextEditType type, long clearedInputTokens)
    {
        Type = type;
        ClearedInputTokens = clearedInputTokens;
    }

    /// <summary>What was cleared.</summary>
    public ContextEditType Type { get; }

    /// <summary>The input tokens the edit freed.</summary>
    public long ClearedInputTokens { get; }

    /// <summary>The tool uses cleared; set when the edit cleared tool uses.</summary>
    public long? ClearedToolUses { get; internal init; }

    /// <summary>The thinking turns cleared; set when the edit cleared thinking.</summary>
    public long? ClearedThinkingTurns { get; internal init; }
}

/// <summary>What the API says about how it served the request.</summary>
public sealed class MessageDiagnostics
{
    internal MessageDiagnostics(CacheMissReason? cacheMissReason) => CacheMissReason = cacheMissReason;

    /// <summary>Why the prompt cache was missed; null when it was not.</summary>
    public CacheMissReason? CacheMissReason { get; }
}

/// <summary>Why the prompt cache was missed, and what it cost.</summary>
public sealed class CacheMissReason
{
    internal CacheMissReason(CacheMissReasonType type) => Type = type;

    /// <summary>What missed the cache.</summary>
    public CacheMissReasonType Type { get; }

    /// <summary>The input tokens that had to be read again; absent for a miss the API cannot count.</summary>
    public long? CacheMissedInputTokens { get; internal init; }
}

/// <summary>One change the API made to the request before answering it.</summary>
public sealed class InputTransformation
{
    internal InputTransformation(InputTransformationType type, string path, InputTransformationReason reason)
    {
        Type = type;
        Path = path;
        Reason = reason;
    }

    /// <summary>What was changed.</summary>
    public InputTransformationType Type { get; }

    /// <summary>Where in the request it was changed, in the API's own notation.</summary>
    public string Path { get; }

    /// <summary>Why it was changed.</summary>
    public InputTransformationReason Reason { get; }
}
