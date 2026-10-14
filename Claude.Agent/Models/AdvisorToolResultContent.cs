using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// What the API's advisor handed back: its answer, its answer kept opaque, or why it did not answer.
/// Each kind the wire names is a type of its own; a kind this library has no type for, or a part
/// that does not fit its kind's type, is an <see cref="UnknownAdvisorToolResultContent"/>.
/// </summary>
public abstract class AdvisorToolResultContent
{
    private protected AdvisorToolResultContent()
    {
    }
}

/// <summary>The advisor's answer.</summary>
public sealed class AdvisorResult : AdvisorToolResultContent
{
    internal AdvisorResult(string text)
    {
        Text = text;
    }

    /// <summary>What the advisor wrote.</summary>
    public string Text { get; }

    /// <summary>Why the advisor stopped, from the same set as a message's; null when the API named none.</summary>
    public StopReason? StopReason { get; internal init; }
}

/// <summary>The advisor's answer, kept opaque, to go back unchanged when the turn is sent again.</summary>
public sealed class AdvisorRedactedResult : AdvisorToolResultContent
{
    internal AdvisorRedactedResult(string encryptedContent)
    {
        EncryptedContent = encryptedContent;
    }

    /// <summary>The answer as the API kept it, opaque, and sent back unchanged.</summary>
    public string EncryptedContent { get; }

    /// <summary>Why the advisor stopped, from the same set as a message's; null when the API named none.</summary>
    public StopReason? StopReason { get; internal init; }
}

/// <summary>Why the advisor did not answer.</summary>
public sealed class AdvisorToolResultErrorContent : AdvisorToolResultContent
{
    internal AdvisorToolResultErrorContent(AdvisorErrorCode errorCode)
    {
        ErrorCode = errorCode;
    }

    /// <summary>What went wrong.</summary>
    public AdvisorErrorCode ErrorCode { get; }
}

/// <summary>A part of a kind this library has no type for, or one that does not fit its kind's type: its kind's name and the part as written.</summary>
public sealed class UnknownAdvisorToolResultContent : AdvisorToolResultContent
{
    internal UnknownAdvisorToolResultContent(string type, JsonElement raw)
    {
        Type = type;
        Raw = raw;
    }

    /// <summary>The part's <c>type</c>.</summary>
    public string Type { get; }

    /// <summary>The part as the binary wrote it.</summary>
    public JsonElement Raw { get; }
}
