using System.Text.Json;

namespace Claude.Agent;

/// <summary>What the model asked the RemoteTrigger tool to do with the account's scheduled cloud routines. The action is which type this is, one type a value, each with the keys that action takes. The body a call sends the service is the service's shape, kept raw as <c>Body</c>: create, update and create_webhook_trigger, which the binary refuses without one, always carry it, and run carries it when the call sends one.</summary>
public abstract class RemoteTriggerToolInput : ToolInput
{
    private protected RemoteTriggerToolInput()
    {
    }

    // A body as a copy of its own, which outlives the document it was read from. The binary takes a
    // JSON object there and nothing else.
    private protected static JsonElement CopyOfBody(JsonElement body, string paramName) =>
        body.ValueKind == JsonValueKind.Object
            ? body.Clone()
            : throw new ArgumentException($"A RemoteTrigger body is a JSON object, not {body.ValueKind}.", paramName);
}

/// <summary>A RemoteTrigger that lists the routines. It takes nothing else.</summary>
public sealed class RemoteTriggerListToolInput : RemoteTriggerToolInput
{
}

/// <summary>A RemoteTrigger that reads one routine.</summary>
/// <param name="triggerId">The routine to read.</param>
public sealed class RemoteTriggerGetToolInput(string triggerId) : RemoteTriggerToolInput
{
    /// <summary>The routine to read.</summary>
    public string TriggerId { get; } = triggerId;
}

/// <summary>A RemoteTrigger that makes a routine of the body the call sends, which the binary requires.</summary>
/// <param name="body">The routine as the service takes it: a JSON object, copied. Anything else is an <see cref="ArgumentException"/>.</param>
public sealed class RemoteTriggerCreateToolInput(JsonElement body) : RemoteTriggerToolInput
{
    /// <summary>The routine as the service takes it, a JSON object the binary sends as it stands. The service's shape is someone else's, so it is kept raw, as a copy that outlives the document it was read from.</summary>
    public JsonElement Body { get; } = CopyOfBody(body, nameof(body));
}

/// <summary>A RemoteTrigger that changes a routine by the body the call sends, which the binary requires.</summary>
/// <param name="triggerId">The routine to change.</param>
/// <param name="body">What to change of the routine, as the service takes it: a JSON object, copied. Anything else is an <see cref="ArgumentException"/>.</param>
public sealed class RemoteTriggerUpdateToolInput(string triggerId, JsonElement body) : RemoteTriggerToolInput
{
    /// <summary>The routine to change.</summary>
    public string TriggerId { get; } = triggerId;

    /// <summary>What to change of the routine, as the service takes it, a JSON object the binary sends as it stands. The service's shape is someone else's, so it is kept raw, as a copy that outlives the document it was read from.</summary>
    public JsonElement Body { get; } = CopyOfBody(body, nameof(body));
}

/// <summary>A RemoteTrigger that fires a routine now, sending the service the body the call sends, when it sends one.</summary>
/// <param name="triggerId">The routine to fire.</param>
/// <param name="body">What to send the service with the run, as the service takes it: a JSON object, copied, or null to send none. Anything else is an <see cref="ArgumentException"/>.</param>
public sealed class RemoteTriggerRunToolInput(string triggerId, JsonElement? body = null) : RemoteTriggerToolInput
{
    /// <summary>The routine to fire.</summary>
    public string TriggerId { get; } = triggerId;

    /// <summary>What the call sends the service with the run, a JSON object the binary sends less any <c>trigger_id</c> in it, or null when the call sends none. The service's shape is someone else's, so it is kept raw, as a copy that outlives the document it was read from.</summary>
    public JsonElement? Body { get; } = body is { } sent ? CopyOfBody(sent, nameof(body)) : null;
}

/// <summary>A RemoteTrigger that attaches a source of events to a routine, the routine and the events named in the body the call sends, which the binary requires.</summary>
/// <param name="body">The routine and the events, as the service takes them: a JSON object, copied. Anything else is an <see cref="ArgumentException"/>.</param>
public sealed class RemoteTriggerCreateWebhookTriggerToolInput(JsonElement body) : RemoteTriggerToolInput
{
    /// <summary>The routine and the events, as the service takes them, a JSON object the binary sends as it stands. The service's shape is someone else's, so it is kept raw, as a copy that outlives the document it was read from.</summary>
    public JsonElement Body { get; } = CopyOfBody(body, nameof(body));
}

/// <summary>A RemoteTrigger that lists a routine's runs, a page at a time.</summary>
/// <param name="triggerId">The routine whose runs to list.</param>
/// <param name="cursor">Where an earlier page of the listing said to go on from; null for the first page.</param>
public sealed class RemoteTriggerListRunsToolInput(string triggerId, string? cursor = null) : RemoteTriggerToolInput
{
    /// <summary>The routine whose runs to list.</summary>
    public string TriggerId { get; } = triggerId;

    /// <summary>Where an earlier page of the listing said to go on from; null for the first page.</summary>
    public string? Cursor { get; } = cursor;
}

/// <summary>A RemoteTrigger that reads the log of one run, a page at a time.</summary>
/// <param name="sessionId">The session the run ran as, as a listing of runs names it.</param>
/// <param name="cursor">Where an earlier page of the log said to go on from; null for the first page.</param>
public sealed class RemoteTriggerGetRunLogToolInput(string sessionId, string? cursor = null) : RemoteTriggerToolInput
{
    /// <summary>The session the run ran as, as a listing of runs names it.</summary>
    public string SessionId { get; } = sessionId;

    /// <summary>Where an earlier page of the log said to go on from; null for the first page.</summary>
    public string? Cursor { get; } = cursor;
}

/// <summary>What a RemoteTrigger handed back: the service's answer as the binary passes it on.</summary>
/// <param name="status">The HTTP status the service answered with.</param>
/// <param name="json">The service's answer as JSON text, or, for a listing of runs and a run's log, JSON text the binary writes of it.</param>
/// <param name="summary">What the binary adds to the answer in words, such as when a routine next runs; null when it adds nothing.</param>
public sealed class RemoteTriggerToolOutput(long status, string json, string? summary = null) : ToolOutput
{
    /// <summary>The HTTP status the service answered with.</summary>
    public long Status { get; } = status;

    /// <summary>The service's answer as JSON text, or, for a listing of runs and a run's log, JSON text the binary writes of it. It is a string as the binary hands it back, whatever it holds: this library does not read inside it.</summary>
    public string Json { get; } = json;

    /// <summary>What the binary adds to the answer in words, such as when a routine next runs; null when it adds nothing.</summary>
    public string? Summary { get; } = summary;
}
