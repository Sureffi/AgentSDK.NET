namespace Claude.Agent;

/// <summary>Where a <see cref="ToolCall"/> stands.</summary>
public enum ToolCallStatus
{
    /// <summary>The model called the tool and nothing has answered yet.</summary>
    Started,

    /// <summary>The tool answered.</summary>
    Answered,

    /// <summary>The call was answered with an error: the tool failed, or the binary refused or stopped it.</summary>
    Failed,

    /// <summary>The run's turn ended with nothing answering the call: a deferred call, a cancelled turn, a process that died.</summary>
    Unanswered,
}
