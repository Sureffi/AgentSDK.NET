using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// What the API's shell execution handed back: what the command wrote, or why it did not run. Each
/// kind the wire names is a type of its own; a kind this library has no type for, or a part that
/// does not fit its kind's type, is an <see cref="UnknownBashCodeExecutionToolResultContent"/>.
/// </summary>
public abstract class BashCodeExecutionToolResultContent
{
    private protected BashCodeExecutionToolResultContent()
    {
    }
}

/// <summary>What the command wrote and what it left behind.</summary>
public sealed class BashCodeExecutionResult : BashCodeExecutionToolResultContent
{
    internal BashCodeExecutionResult(IReadOnlyList<BashCodeExecutionOutput> content, long returnCode, string standardError, string standardOutput)
    {
        Content = content;
        ReturnCode = returnCode;
        StandardError = standardError;
        StandardOutput = standardOutput;
    }

    /// <summary>The files the command left in the container.</summary>
    public IReadOnlyList<BashCodeExecutionOutput> Content { get; }

    /// <summary>What the command exited with.</summary>
    public long ReturnCode { get; }

    /// <summary>What the command wrote to standard error.</summary>
    public string StandardError { get; }

    /// <summary>What the command wrote to standard output.</summary>
    public string StandardOutput { get; }
}

/// <summary>Why the command did not run.</summary>
public sealed class BashCodeExecutionToolResultErrorContent : BashCodeExecutionToolResultContent
{
    internal BashCodeExecutionToolResultErrorContent(BashCodeExecutionErrorCode errorCode)
    {
        ErrorCode = errorCode;
    }

    /// <summary>What went wrong.</summary>
    public BashCodeExecutionErrorCode ErrorCode { get; }
}

/// <summary>A part of a kind this library has no type for, or one that does not fit its kind's type: its kind's name and the part as written.</summary>
public sealed class UnknownBashCodeExecutionToolResultContent : BashCodeExecutionToolResultContent
{
    internal UnknownBashCodeExecutionToolResultContent(string type, JsonElement raw)
    {
        Type = type;
        Raw = raw;
    }

    /// <summary>The part's <c>type</c>.</summary>
    public string Type { get; }

    /// <summary>The part as the binary wrote it.</summary>
    public JsonElement Raw { get; }
}
