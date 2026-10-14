using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// What the API's code execution handed back: what the code wrote, the same with its output kept
/// opaque, or why it did not run. Each kind the wire names is a type of its own; a kind this library
/// has no type for, or a part that does not fit its kind's type, is an
/// <see cref="UnknownCodeExecutionToolResultContent"/>.
/// </summary>
public abstract class CodeExecutionToolResultContent
{
    private protected CodeExecutionToolResultContent()
    {
    }
}

/// <summary>What the code wrote and what it left behind.</summary>
public sealed class CodeExecutionResult : CodeExecutionToolResultContent
{
    internal CodeExecutionResult(IReadOnlyList<CodeExecutionOutput> content, long returnCode, string standardError, string standardOutput)
    {
        Content = content;
        ReturnCode = returnCode;
        StandardError = standardError;
        StandardOutput = standardOutput;
    }

    /// <summary>The files the code left in the container.</summary>
    public IReadOnlyList<CodeExecutionOutput> Content { get; }

    /// <summary>What the code exited with.</summary>
    public long ReturnCode { get; }

    /// <summary>What the code wrote to standard error.</summary>
    public string StandardError { get; }

    /// <summary>What the code wrote to standard output.</summary>
    public string StandardOutput { get; }
}

/// <summary>What the code wrote, with standard output kept opaque, to go back unchanged when the turn is sent again.</summary>
public sealed class EncryptedCodeExecutionResult : CodeExecutionToolResultContent
{
    internal EncryptedCodeExecutionResult(IReadOnlyList<CodeExecutionOutput> content, string encryptedStandardOutput, long returnCode, string standardError)
    {
        Content = content;
        EncryptedStandardOutput = encryptedStandardOutput;
        ReturnCode = returnCode;
        StandardError = standardError;
    }

    /// <summary>The files the code left in the container.</summary>
    public IReadOnlyList<CodeExecutionOutput> Content { get; }

    /// <summary>What the code wrote to standard output, as the API kept it, opaque.</summary>
    public string EncryptedStandardOutput { get; }

    /// <summary>What the code exited with.</summary>
    public long ReturnCode { get; }

    /// <summary>What the code wrote to standard error.</summary>
    public string StandardError { get; }
}

/// <summary>Why the code did not run.</summary>
public sealed class CodeExecutionToolResultErrorContent : CodeExecutionToolResultContent
{
    internal CodeExecutionToolResultErrorContent(CodeExecutionErrorCode errorCode)
    {
        ErrorCode = errorCode;
    }

    /// <summary>What went wrong.</summary>
    public CodeExecutionErrorCode ErrorCode { get; }
}

/// <summary>A part of a kind this library has no type for, or one that does not fit its kind's type: its kind's name and the part as written.</summary>
public sealed class UnknownCodeExecutionToolResultContent : CodeExecutionToolResultContent
{
    internal UnknownCodeExecutionToolResultContent(string type, JsonElement raw)
    {
        Type = type;
        Raw = raw;
    }

    /// <summary>The part's <c>type</c>.</summary>
    public string Type { get; }

    /// <summary>The part as the binary wrote it.</summary>
    public JsonElement Raw { get; }
}
