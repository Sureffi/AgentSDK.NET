using System.Text.Json;

namespace Claude.Agent;

// Pairs the two halves of each tool call a session reads — the tool-use block of an assistant
// message and the tool-result block of the user message that answers it — by id, one pairing for
// the whole session, and hands the call out as snapshots beside the message that caused each: the
// start right after the assistant message, the close right after the user message. A call belongs
// to where its start went: its closing snapshot goes there too, whoever reads the message that
// answers it. A run's calls still open are closed unanswered at the run's end, just before its
// result; what no run reads has no run's end, so a call there closes when its answer comes, or
// unanswered when the session ends. Every started call gets exactly one closing snapshot: an answer
// that comes after its call was closed hands out none. Only messages fill a call; hooks and the
// permission callback never do. Its lock is taken last, under a run's and under the session's
// routing.
internal sealed class ToolCallPairing
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, OpenCall> _open = new(StringComparer.Ordinal);
    private readonly List<string> _started = [];

    // Adds to `handed`, in order, what is handed out for `message`, going to `owner`; a closing
    // snapshot of a call another owner started goes to `elsewhere`, for that owner.
    public void Read(AgentMessage message, object owner, bool endsTurn, List<AgentMessage> handed, List<Owned> elsewhere)
    {
        ArgumentNullException.ThrowIfNull(handed);
        ArgumentNullException.ThrowIfNull(elsewhere);

        lock (_gate)
        {
            switch (message)
            {
                case AssistantMessage assistant:
                    handed.Add(assistant);
                    foreach (var use in assistant.ToolUses)
                    {
                        if (_open.ContainsKey(use.Id))
                        {
                            continue;
                        }

                        var call = new ToolCall(use.Id, use.Name, use.Input, use.RawInput, ToolCallStatus.Started)
                        {
                            ParentToolUseId = assistant.ParentToolUseId,
                        };
                        _open[use.Id] = new OpenCall(call, assistant.SessionId, owner);
                        _started.Add(use.Id);
                        handed.Add(new ToolCallMessage(call, assistant.SessionId, assistant.Raw));
                    }

                    break;

                case UserMessage user:
                    ReadAnswers(user, owner, handed, elsewhere);
                    break;

                case ResultMessage when endsTurn:
                    CloseUnansweredCore(owner, handed);
                    handed.Add(message);
                    break;

                default:
                    handed.Add(message);
                    break;
            }
        }
    }

    // Every call `owner` started that is still open, closed unanswered in the order the calls started.
    public void CloseUnanswered(object owner, List<AgentMessage> handed)
    {
        lock (_gate)
        {
            CloseUnansweredCore(owner, handed);
        }
    }

    private void CloseUnansweredCore(object owner, List<AgentMessage> handed)
    {
        foreach (var id in _started.ToArray())
        {
            var open = _open[id];
            if (!ReferenceEquals(open.Owner, owner))
            {
                continue;
            }

            var call = open.Call;
            handed.Add(new ToolCallMessage(
                new ToolCall(call.Id, call.Name, call.Input, call.RawInput, ToolCallStatus.Unanswered)
                {
                    ParentToolUseId = call.ParentToolUseId,
                },
                open.SessionId,
                null));
            _open.Remove(id);
            _started.Remove(id);
        }
    }

    // The frame's tool_use_result is the output of the one call its one tool result answers; a
    // frame answering several calls at once leaves it to none of them. An answer to no open call —
    // a call already closed, or one this session never saw start — closes nothing. The message goes
    // where it was routed; each close goes to the owner that started the call it closes.
    private void ReadAnswers(UserMessage user, object owner, List<AgentMessage> handed, List<Owned> elsewhere)
    {
        var answers = user.Content.OfType<ToolResultBlock>().ToList();
        var rawOutput = answers.Count == 1 ? user.RawToolOutput : null;
        ToolOutput? output = null;
        if (rawOutput is { } raw && _open.TryGetValue(answers[0].ToolUseId, out var answered))
        {
            output = ToolTypes.ReadOutput(answered.Call.Name, raw);
            user.AdoptToolOutput(output);
        }

        handed.Add(user);
        foreach (var answer in answers)
        {
            if (!_open.Remove(answer.ToolUseId, out var open))
            {
                continue;
            }

            _started.Remove(answer.ToolUseId);
            var close = new ToolCallMessage(Close(open.Call, answer, rawOutput, output), user.SessionId, user.Raw);
            if (ReferenceEquals(open.Owner, owner))
            {
                handed.Add(close);
            }
            else
            {
                elsewhere.Add(new Owned(open.Owner, close));
            }
        }
    }

    private static ToolCall Close(ToolCall call, ToolResultBlock answer, JsonElement? rawOutput, ToolOutput? output) =>
        answer.IsError == true
            ? new ToolCall(call.Id, call.Name, call.Input, call.RawInput, ToolCallStatus.Failed)
            {
                ParentToolUseId = call.ParentToolUseId,
                Content = answer.Content,
                Error = string.Concat(answer.Content.OfType<TextBlock>().Select(block => block.Text)),
            }
            : new ToolCall(call.Id, call.Name, call.Input, call.RawInput, ToolCallStatus.Answered)
            {
                ParentToolUseId = call.ParentToolUseId,
                Content = answer.Content,
                RawOutput = rawOutput,
                Output = output,
            };

    private sealed record OpenCall(ToolCall Call, string? SessionId, object Owner);

    // A snapshot for an owner other than the one the message that caused it went to.
    public sealed record Owned(object Owner, AgentMessage Message);
}
