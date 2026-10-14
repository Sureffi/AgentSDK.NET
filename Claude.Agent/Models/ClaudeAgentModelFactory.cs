using System.Text.Json;

namespace Claude.Agent;

/// <summary>Builds the models this library otherwise builds from the wire, for tests of code that consumes them.</summary>
public static partial class ClaudeAgentModelFactory
{
    /// <summary>Builds an <see cref="Claude.Agent.AgentTrace"/> for a test from <paramref name="messages"/>, whose last must be the <see cref="Claude.Agent.ResultMessage"/>; the tool calls and the text are read from them as a run reads them.</summary>
    public static AgentTrace AgentTrace(IEnumerable<AgentMessage> messages) => AgentTrace(messages, null, null);

    /// <summary>Builds an <see cref="Claude.Agent.AgentTrace"/> for a test from <paramref name="messages"/>, whose last must be the <see cref="Claude.Agent.ResultMessage"/>, naming <paramref name="evidenceType"/> and <paramref name="answerType"/> as a run's trace names its evidence and answer types; the tool calls and the text are read from the messages as a run reads them.</summary>
    public static AgentTrace AgentTrace(IEnumerable<AgentMessage> messages, Type? evidenceType, Type? answerType)
    {
        ArgumentNullException.ThrowIfNull(messages);

        IReadOnlyList<AgentMessage> list = [.. messages];
        if (list.Count == 0 || list[^1] is not Claude.Agent.ResultMessage result)
        {
            throw new ArgumentException("A trace ends with its result.", nameof(messages));
        }

        return new(list, result, evidenceType, answerType);
    }

    /// <summary>Builds a <see cref="Claude.Agent.ToolCall"/> for a test. Each parameter is the property of its name. <c>rawInput</c> defaults to an empty object.</summary>
    public static ToolCall ToolCall(
        string? id = null,
        string? name = null,
        ToolInput? input = null,
        JsonElement rawInput = default,
        ToolCallStatus status = ToolCallStatus.Started,
        ToolOutput? output = null,
        JsonElement? rawOutput = null,
        IEnumerable<ContentBlock>? content = null,
        string? error = null,
        string? parentToolUseId = null) =>
        new(id ?? string.Empty, name ?? string.Empty, input, Object(rawInput), status)
        {
            Output = output,
            RawOutput = rawOutput,
            Content = [.. content ?? []],
            Error = error,
            ParentToolUseId = parentToolUseId,
        };

    /// <summary>Builds a <see cref="Claude.Agent.ToolCallMessage"/> for a test. Each parameter is the property of its name; <c>toolCall</c> defaults to a started call with nothing set.</summary>
    public static ToolCallMessage ToolCallMessage(
        ToolCall? toolCall = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(toolCall ?? ToolCall(), sessionId, raw);

    /// <summary>Builds an <see cref="Claude.Agent.UnknownMessage"/> for a test. Each parameter is the property of its name.</summary>
    public static UnknownMessage UnknownMessage(
        string? type = null,
        string? subtype = null,
        string? uuid = null,
        string? sessionId = null,
        JsonElement? raw = null) =>
        new(type ?? string.Empty, subtype, uuid, sessionId, raw);

    /// <summary>Builds a <see cref="Claude.Agent.SessionInfo"/> for a test. Each parameter is the property of its name.</summary>
    public static SessionInfo SessionInfo(
        string? sessionId = null,
        string? summary = null,
        DateTimeOffset lastModified = default,
        long? sizeBytes = null,
        string? customTitle = null,
        string? firstPrompt = null,
        string? gitBranch = null,
        string? workingDirectory = null,
        string? tag = null,
        DateTimeOffset? createdAt = null) =>
        new(sessionId ?? string.Empty, summary ?? string.Empty, lastModified)
        {
            SizeBytes = sizeBytes,
            CustomTitle = customTitle,
            FirstPrompt = firstPrompt,
            GitBranch = gitBranch,
            WorkingDirectory = workingDirectory,
            Tag = tag,
            CreatedAt = createdAt,
        };

    private static JsonElement Object(JsonElement element) =>
        element.ValueKind == JsonValueKind.Undefined ? EmptyObject.Value : element;
}
