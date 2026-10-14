using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Claude.Agent;

/// <summary>
/// What a run completes with: every message it handed out, in order, the tool-call snapshots among
/// them and the result last, and the views read from them.
/// </summary>
public sealed class AgentTrace
{
    // What the binary calls the schema's answer on the result frame. Recorded in
    // Claude.Agent.Tests/fixtures/structured.stdout.jsonl, the result line.
    private const string StructuredOutputKey = "structured_output";

    internal AgentTrace(IReadOnlyList<AgentMessage> messages, ResultMessage result, Type? evidenceType, Type? answerType)
    {
        Messages = messages;
        Result = result;
        EvidenceType = evidenceType;
        AnswerType = answerType;

        var calls = new List<ToolCall>();
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        var text = new List<string>();
        foreach (var message in messages)
        {
            switch (message)
            {
                case ToolCallMessage { ToolCall: var call }:
                    if (positions.TryGetValue(call.Id, out var position))
                    {
                        calls[position] = call;
                    }
                    else
                    {
                        positions[call.Id] = calls.Count;
                        calls.Add(call);
                    }

                    break;

                case AssistantMessage { ParentToolUseId: null } assistant when assistant.Content.OfType<TextBlock>().Any():
                    text.Add(assistant.Text);
                    break;

                default:
                    break;
            }
        }

        ToolCalls = calls;
        Text = string.Join("\n", text);
    }

    /// <summary>Every message of the run, in the order it was handed out; the last is <see cref="Result"/>.</summary>
    public IReadOnlyList<AgentMessage> Messages { get; }

    /// <summary>The result that ended the run.</summary>
    public ResultMessage Result { get; }

    /// <summary>Each tool call of the run as it closed, in the order the calls started.</summary>
    public IReadOnlyList<ToolCall> ToolCalls { get; }

    /// <summary>The type of the evidence the run was asked over, <see cref="ClaudeAgentClient.AskAsync{TEvidence, TAnswer}(string, TEvidence, ClaudeAgentSessionOptions, JsonSerializerOptions, System.Text.Json.Schema.JsonSchemaExporterOptions, CancellationToken)"/>'s first type argument; null for a run that took none.</summary>
    public Type? EvidenceType { get; }

    /// <summary>The type the run's output schema was taken from: an ask's answer type, or the type <see cref="ClaudeAgentSessionOptions.SetOutputSchema{T}(JsonSerializerOptions, System.Text.Json.Schema.JsonSchemaExporterOptions)"/> was given. Null when the run asked for no schema, or for one handed in as <see cref="ClaudeAgentSessionOptions.OutputSchema"/>.</summary>
    public Type? AnswerType { get; }

    /// <summary>The text of the main thread's assistant messages: each message's text blocks joined, and the messages joined by a line feed. A subagent's messages are not in it.</summary>
    public string Text { get; }

    /// <summary>
    /// The structured output of the run read as <typeparamref name="T"/>: the answer the binary put
    /// on the result for the schema <see cref="ClaudeAgentSessionOptions.OutputSchema"/> asked for.
    /// The default of <typeparamref name="T"/> — null for a class — when the run carried none: no
    /// schema was asked for, or the turn ended without an answer matching it. Reading is the
    /// platform's, through <paramref name="serializerOptions"/>, or
    /// <see cref="JsonSerializerOptions.Default"/> when that is null, and what it throws comes out as
    /// it threw it: <see cref="JsonException"/> for an answer that is there and does not fit
    /// <typeparamref name="T"/>, since that is not the same as none,
    /// <see cref="NotSupportedException"/> for a type it does not read, and
    /// <see cref="InvalidOperationException"/> for a contract it refuses. <see cref="JsonElement"/> as <typeparamref name="T"/> reads any answer whole,
    /// and tells none from one.
    /// </summary>
    public T? GetStructuredOutput<T>(JsonSerializerOptions? serializerOptions = null) =>
        StructuredOutput is { } output ? output.Deserialize<T>(serializerOptions) : default;

    /// <summary>
    /// The structured output of the run read as <typeparamref name="T"/> through
    /// <paramref name="jsonTypeInfo"/>, the contract a source generator wrote for it, so the read
    /// needs no reflection. The default of <typeparamref name="T"/> when the run carried none, and
    /// what System.Text.Json throws when the answer is there, as the other overload; a null contract
    /// is an <see cref="ArgumentNullException"/>, as it is to the platform.
    /// </summary>
    public T? GetStructuredOutput<T>(JsonTypeInfo<T> jsonTypeInfo)
    {
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);
        return StructuredOutput is { } output ? output.Deserialize(jsonTypeInfo) : default;
    }

    // The answer as the binary wrote it, off the result's own line: null when the result was
    // assembled without one, when the frame has no such key, and when the key is there as null.
    private JsonElement? StructuredOutput =>
        Result.Raw is { ValueKind: JsonValueKind.Object } raw
        && raw.TryGetProperty(StructuredOutputKey, out var output)
        && output.ValueKind is not JsonValueKind.Null
            ? output
            : null;
}
