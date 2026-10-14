using System.Collections.Immutable;
using System.Text.Json;

namespace Claude.Agent.Tests.Replay;

internal abstract record ScriptStep;

internal sealed record EmitLine(string Line) : ScriptStep;

internal sealed record AnswerControlRequest(string Line) : ScriptStep;

internal sealed record AwaitClientResponse(string RequestId) : ScriptStep;

internal sealed record AwaitPrompt(int Ordinal) : ScriptStep;

// The play waits until the client has written its control request of this ordinal.
internal sealed record AwaitClientRequest(int Ordinal) : ScriptStep;

// The play waits until the client has written this many lines.
internal sealed record AwaitWrites(int Count) : ScriptStep;

// The play waits until the client's stdin has ended, and goes on writing: a binary reads to EOF and
// still finishes what it was doing.
internal sealed record AwaitClientStdinEnd : ScriptStep;

internal sealed record HangUp : ScriptStep;

// The server stops playing here until the test resumes it, so a test can look at what the client
// holds at a point of the recording without the lines after it racing the look.
internal sealed record Pause : ScriptStep;

internal sealed record Script(ImmutableArray<ScriptStep> Steps, int ExitCode = 0, bool AwaitStdinEnd = false)
{
    public const string SuccessResponse =
        """{"type": "control_response", "response": {"subtype": "success", "request_id": "", "response": {}}}""";

    // The uuids the recording's prompts were written with, in order, null for one written without:
    // wherever a played line carries one, the server puts the uuid the client wrote for the prompt
    // at that place.
    public ImmutableArray<string?> Prompts { get; init; } = [];

    public static AnswerControlRequest AnswerInitialize => new(SuccessResponse);

    public static AnswerControlRequest AnswerWithError(string error) => new(
        "{\"type\": \"control_response\", \"response\": {\"subtype\": \"error\", \"request_id\": \"\", "
        + "\"error\": \"" + error + "\"}}");

    // A recording whose prompts carry uuids is played by what its lines name; one whose prompts do
    // not is played turn by turn. `pauseAfterResult` pauses the play right after the recording's
    // result of that ordinal, or, in a recording that names its prompts, after the frame closing
    // the prompt when that comes next; `pauseAfterLine` pauses it right after the recorded stdout
    // line of that number, in a recording that names its prompts; 0 plays it through.
    public static Script OfFixture(FixtureData fixture, bool awaitStdinEnd, bool promptFirst = true, int pauseAfterResult = 0, int pauseAfterLine = 0)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        var prompts = Fixture.PromptUuids(fixture.Stdin);
        return prompts.Any(uuid => uuid is not null)
            ? new Script(ByPromptNames(fixture, prompts, pauseAfterResult, pauseAfterLine), AwaitStdinEnd: awaitStdinEnd) { Prompts = prompts }
            : new Script(ByTurns(fixture, promptFirst, pauseAfterResult), AwaitStdinEnd: awaitStdinEnd);
    }

    // The binary names each prompt it read. The play is one sequence: a line waits until the client
    // has written every prompt named at or before it, so a line that names none after a line that
    // names one waits too, and only lines ahead of every prompt's name wait for none. Three things
    // move or hold a line past that:
    // - a control request the binary makes is moved past the answers the client waits on before
    //   it writes what the recorded stdin has ahead of its answer, is played once the client has
    //   written those lines, and what follows it waits for the answer, unless the binary went on to
    //   cancel the request;
    // - the binary's cancel of its own request, when the answer to a client's control request comes
    //   right after it, is that request's doing (the interrupt that stopped the turn): it waits
    //   until the client has written the request;
    // - a run of lines between turns that name no prompt and come right before the answer to a
    //   control request are that request's doing (the frames of a stopped task): they wait until
    //   the client has written it. A run that comes right after an answer is the answered
    //   request's doing (the status set_permission_mode announces), and waits for no later one.
    private static ImmutableArray<ScriptStep> ByPromptNames(FixtureData fixture, ImmutableArray<string?> prompts, int pauseAfterResult, int pauseAfterLine)
    {
        var steps = ImmutableArray.CreateBuilder<ScriptStep>();
        var order = InTheOrderTheClientCanMeet(fixture);
        var lines = order.Select(recorded => fixture.Stdout[recorded]).ToImmutableArray();
        var cancelled = CancelledRequests(fixture);
        var named = lines.Select(line => NamedPrompt(line, prompts)).ToArray();
        var betweenTurns = BetweenTurns(lines, named);
        var awaited = 0;
        var answers = 0;
        var results = 0;
        var pauseAfter = -1;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var type = Fixture.TypeOf(line);

            if (type == "control_response")
            {
                steps.Add(new AnswerControlRequest(line));
                answers++;
                continue;
            }

            while (awaited < named[index])
            {
                steps.Add(new AwaitPrompt(++awaited));
            }

            if (type == "control_request" && AnswerPlace(fixture, line) is { } before)
            {
                steps.Add(new AwaitWrites(before));
            }

            if (type == "control_cancel_request" && index + 1 < lines.Length && Fixture.TypeOf(lines[index + 1]) == "control_response")
            {
                steps.Add(new AwaitClientRequest(answers + 1));
            }

            if (IsRequestsDoing(index) && !IsRequestsDoing(index - 1) && (index == 0 || Fixture.TypeOf(lines[index - 1]) != "control_response"))
            {
                var answer = index;
                while (IsRequestsDoing(answer))
                {
                    answer++;
                }

                if (answer < lines.Length && Fixture.TypeOf(lines[answer]) == "control_response")
                {
                    steps.Add(new AwaitClientRequest(answers + 1));
                }
            }

            steps.Add(new EmitLine(line));

            if (type == "result" && ++results == pauseAfterResult)
            {
                pauseAfter = ClosesNext(lines, index) ? index + 1 : index;
            }

            if (index == pauseAfter || order[index] + 1 == pauseAfterLine)
            {
                steps.Add(new Pause());
            }

            if (type == "control_request" && !cancelled.Contains(Fixture.RequestIdOf(line)))
            {
                steps.Add(new AwaitClientResponse(Fixture.RequestIdOf(line)));
            }
        }

        return steps.ToImmutable();

        bool IsRequestsDoing(int at) =>
            at >= 0
            && at < lines.Length
            && betweenTurns[at]
            && named[at] == 0
            && Fixture.TypeOf(lines[at])?.StartsWith("control_", StringComparison.Ordinal) != true;
    }

    // The recording's turns in order: a turn's frames wait for its prompt. With `promptFirst`
    // false, the control requests ahead of a turn's first message (an in-process server's handshake
    // the client finishes before it prompts) are played without waiting, and the turn's messages
    // still wait: a message played before its prompt would reach no run.
    private static ImmutableArray<ScriptStep> ByTurns(FixtureData fixture, bool promptFirst, int pauseAfterResult)
    {
        var steps = ImmutableArray.CreateBuilder<ScriptStep>();
        var initialized = false;
        var prompts = fixture.Stdin.Count(line => Fixture.TypeOf(line) == "user");
        var turns = 0;
        var results = 0;
        var awaiting = true;
        var cancelled = CancelledRequests(fixture);

        foreach (var line in fixture.Stdout)
        {
            var type = Fixture.TypeOf(line);

            if (type == "control_response")
            {
                steps.Add(new AnswerControlRequest(line));
                initialized = true;
                continue;
            }

            if (initialized && awaiting && turns < prompts && BelongsToATurn(line) && (promptFirst || type?.StartsWith("control_", StringComparison.Ordinal) != true))
            {
                awaiting = false;
                steps.Add(new AwaitPrompt(++turns));
            }

            steps.Add(new EmitLine(line));

            if (type == "result")
            {
                awaiting = true;
                if (++results == pauseAfterResult)
                {
                    steps.Add(new Pause());
                }
            }

            // A request the binary went on to cancel is never answered; the script does not wait for it.
            if (type == "control_request" && !cancelled.Contains(Fixture.RequestIdOf(line)))
            {
                steps.Add(new AwaitClientResponse(Fixture.RequestIdOf(line)));
            }
        }

        return steps.ToImmutable();
    }

    // Where the recorded stdin has the client's answer to the binary's control request `line`: the
    // number of lines the client wrote before it. Null when the client never answered it.
    private static int? AnswerPlace(FixtureData fixture, string line)
    {
        var id = Fixture.RequestIdOf(line);
        for (var place = 0; place < fixture.Stdin.Length; place++)
        {
            if (Fixture.TypeOf(fixture.Stdin[place]) == "control_response" && Fixture.ResponseRequestIdOf(fixture.Stdin[place]) == id)
            {
                return place;
            }
        }

        return null;
    }

    // The recorded stdout, with each control request of the binary moved past the answers the
    // client waits on before it writes the lines the recorded stdin has ahead of its answer: a
    // client writes a control request only once the one it wrote before it is answered. The
    // binary's own order of its requests and its answers is not something the client can see.
    private static ImmutableArray<int> InTheOrderTheClientCanMeet(FixtureData fixture)
    {
        var order = Enumerable.Range(0, fixture.Stdout.Length).ToList();
        var clientRequests = fixture.Stdin
            .Where(line => Fixture.TypeOf(line) == "control_request")
            .Select(Fixture.RequestIdOf)
            .ToList();

        foreach (var request in fixture.Stdout.Where(line => Fixture.TypeOf(line) == "control_request"))
        {
            if (AnswerPlace(fixture, request) is not { } place)
            {
                continue;
            }

            var written = fixture.Stdin.Take(place)
                .Where(line => Fixture.TypeOf(line) == "control_request")
                .Select(Fixture.RequestIdOf)
                .ToList();
            var at = order.FindIndex(recorded => fixture.Stdout[recorded] == request);
            var after = at;
            foreach (var id in written.Skip(1))
            {
                var previous = clientRequests[clientRequests.IndexOf(id) - 1];
                var answer = order.FindIndex(recorded =>
                    Fixture.TypeOf(fixture.Stdout[recorded]) == "control_response" && Fixture.ResponseRequestIdOf(fixture.Stdout[recorded]) == previous);
                after = Math.Max(after, answer);
            }

            if (after > at)
            {
                var moved = order[at];
                order.RemoveAt(at);
                order.Insert(after, moved);
            }
        }

        return [.. order];
    }

    // The binary's requests it gave up on, which the client does not answer: one it cancelled with
    // control_cancel_request, and an mcp_message whose JSON-RPC id a notifications/cancelled to the
    // same server names.
    private static HashSet<string> CancelledRequests(FixtureData fixture)
    {
        var cancelled = fixture.Stdout
            .Where(line => Fixture.TypeOf(line) == "control_cancel_request")
            .Select(Fixture.RequestIdOf)
            .ToHashSet(StringComparer.Ordinal);

        var messages = fixture.Stdout
            .Where(line => Fixture.TypeOf(line) == "control_request")
            .Select(line => (Line: line, Message: McpMessageOf(line)))
            .Where(request => request.Message is not null)
            .ToList();
        var given = messages
            .Where(request => request.Message!.Value.Method == "notifications/cancelled" && request.Message.Value.CancelledId is not null)
            .Select(request => (request.Message!.Value.Server, request.Message.Value.CancelledId))
            .ToHashSet();
        foreach (var (line, message) in messages)
        {
            if (message!.Value.Id is { } id && given.Contains((message.Value.Server, id)))
            {
                cancelled.Add(Fixture.RequestIdOf(line));
            }
        }

        return cancelled;
    }

    // An mcp_message request's server, method, JSON-RPC id and, for a cancellation, the id it names,
    // each id as its JSON text.
    private static (string Server, string? Method, string? Id, string? CancelledId)? McpMessageOf(string line)
    {
        using var document = JsonDocument.Parse(line);
        if (!document.RootElement.TryGetProperty("request", out var request)
            || !request.TryGetProperty("subtype", out var subtype)
            || subtype.GetString() != "mcp_message"
            || !request.TryGetProperty("message", out var message))
        {
            return null;
        }

        return (
            request.TryGetProperty("server_name", out var server) ? server.GetString() ?? string.Empty : string.Empty,
            message.TryGetProperty("method", out var method) ? method.GetString() : null,
            message.TryGetProperty("id", out var id) ? id.GetRawText() : null,
            message.TryGetProperty("params", out var parameters) && parameters.ValueKind == JsonValueKind.Object
                && parameters.TryGetProperty("requestId", out var named)
                    ? named.GetRawText()
                    : null);
    }

    // The latest prompt, by its place among the recording's prompts from 1, whose uuid the line
    // carries; 0 for none.
    private static int NamedPrompt(string line, ImmutableArray<string?> prompts)
    {
        var latest = 0;
        for (var ordinal = 0; ordinal < prompts.Length; ordinal++)
        {
            if (prompts[ordinal] is { } uuid && line.Contains(uuid, StringComparison.Ordinal))
            {
                latest = ordinal + 1;
            }
        }

        return latest;
    }

    // Each line outside every prompt's stretch: a stretch runs from the frame that says a prompt
    // started to the next result.
    private static bool[] BetweenTurns(ImmutableArray<string> lines, int[] named)
    {
        var between = new bool[lines.Length];
        var inStretch = false;
        for (var index = 0; index < lines.Length; index++)
        {
            if (named[index] != 0 && Lifecycle(lines[index]) is (_, "started"))
            {
                inStretch = true;
            }

            between[index] = !inStretch;

            if (Fixture.TypeOf(lines[index]) == "result")
            {
                inStretch = false;
            }
        }

        return between;
    }

    // The line after the result at `index` closes the prompt the result ended.
    private static bool ClosesNext(ImmutableArray<string> lines, int index) =>
        index + 1 < lines.Length && Lifecycle(lines[index + 1]) is (_, "completed" or "cancelled");

    private static (string Prompt, string State)? Lifecycle(string line)
    {
        if (Fixture.TypeOf(line) != "command_lifecycle")
        {
            return null;
        }

        using var document = JsonDocument.Parse(line);
        return (document.RootElement.GetProperty("command_uuid").GetString() ?? string.Empty,
            document.RootElement.GetProperty("state").GetString() ?? string.Empty);
    }

    // A turn's frames follow that turn's prompt. A system frame that is not an init is the binary answering a
    // control verb between turns, and follows nothing the client has yet to send.
    private static bool BelongsToATurn(string line)
    {
        if (Fixture.TypeOf(line) != "system")
        {
            return true;
        }

        using var document = JsonDocument.Parse(line);
        return document.RootElement.TryGetProperty("subtype", out var subtype) && subtype.GetString() == "init";
    }
}
