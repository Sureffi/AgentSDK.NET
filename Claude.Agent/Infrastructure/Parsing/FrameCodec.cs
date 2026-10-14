using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Claude.Agent;

internal sealed class FrameCodec
{
    private readonly ILogger<FrameCodec> _logger;

    public FrameCodec(ILogger<FrameCodec>? logger = null) =>
        _logger = logger ?? NullLogger<FrameCodec>.Instance;

    public Result<InboundFrame, ParseError> Decode(string line)
    {
        var decoded = DecodeCore(line);
        if (!_logger.IsEnabled(LogLevel.Debug))
        {
            return decoded;
        }

        if (!decoded.IsOk)
        {
            _logger.LogDebug(
                "Rejected frame ({Kind}): {Message}",
                decoded.Error.Kind,
                decoded.Error.Message);
        }
        else if (decoded.Value is MessageFrame { Message: UnknownMessage unknown })
        {
            _logger.LogDebug(
                "Read a frame of type {Type} as unknown: {Reason}",
                unknown.Type,
                unknown.Reason ?? "no type of its own");
        }

        return decoded;
    }

    public string Encode(OutboundFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var writer = new WireJson();
        switch (frame)
        {
            case UserInputFrame user:
                WriteUserInput(writer, user);
                break;
            case OutboundControlRequest request:
                writer.BeginObject();
                writer.Property("type", FrameTypes.ControlRequest);
                writer.Property("request_id", request.RequestId.Value);
                writer.Name("request");
                RequestWriter.Write(writer, request.Request);
                writer.EndObject();
                break;
            case OutboundControlResponse response:
                writer.BeginObject();
                writer.Property("type", FrameTypes.ControlResponse);
                writer.Name("response");
                writer.BeginObject();
                writer.Property("subtype", "success");
                writer.Property("request_id", response.RequestId.Value);
                writer.Name("response");
                ReplyWriter.Write(writer, response.Reply);
                writer.EndObject();
                writer.EndObject();
                break;
            case OutboundControlError error:
                writer.BeginObject();
                writer.Property("type", FrameTypes.ControlResponse);
                writer.Name("response");
                writer.BeginObject();
                writer.Property("subtype", "error");
                writer.Property("request_id", error.RequestId.Value);
                writer.Property("error", error.Error);
                writer.EndObject();
                writer.EndObject();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(frame), frame, null);
        }

        var text = writer.ToString();
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Encoded {Frame}: {Line}", frame.GetType().Name, text);
        }

        return text;
    }

    private static Result<InboundFrame, ParseError> DecodeCore(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return new ParseError(ParseErrorKind.MalformedJson, "The line is empty.", line ?? string.Empty);
        }

        // A string of the line may hold half a surrogate pair. The document parses and the frame
        // binds; the throw comes later, wherever that string is read, and it is no JsonException, so
        // it would leave the read loop and end the session. The line is refused here instead.
        if (JsonSurrogateEscapes.Unpaired(line))
        {
            return new ParseError(
                ParseErrorKind.MalformedJson,
                "The line carries half of a surrogate pair, which is no text.",
                line);
        }

        JsonElement raw;
        try
        {
            using var document = JsonDocument.Parse(line);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new ParseError(ParseErrorKind.MalformedJson, "The line is not a JSON object.", line);
            }

            raw = document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            return new ParseError(ParseErrorKind.MalformedJson, exception.Message, line);
        }

        if (!raw.TryGetProperty("type", out var typeElement))
        {
            return JsonReading.Missing("type", line);
        }

        if (typeElement.ValueKind != JsonValueKind.String)
        {
            return JsonReading.Invalid("type", "expected a string.", line);
        }

        var type = typeElement.GetString() ?? string.Empty;
        if (type == FrameTypes.KeepAlive)
        {
            return new KeepAliveFrame();
        }

        Frame? frame;
        try
        {
            frame = Bind(type, raw);
        }
        catch (JsonException exception)
        {
            return Unknown(type, raw, exception.Message);
        }

        if (frame is null)
        {
            return Unknown(type, raw, null);
        }

        var routed = Route(frame, raw, line);
        return routed.TryGet(out var inbound, out var error) ? inbound : Unknown(type, raw, error.Message);
    }

    // What degrades rather than being rejected: a line of a type without a type here, and one whose
    // members do not fit its kind's type. It is handed out as the unknown, with the line.
    private static MessageFrame Unknown(string type, JsonElement raw, string? reason) =>
        new(new UnknownMessage(
            type,
            raw.StringOrNull("subtype"),
            Identity.Message(raw.StringOrNull("uuid")),
            Identity.Session(raw.StringOrNull("session_id")),
            raw)
        {
            Reason = reason,
        });

    private static Frame? Bind(string type, JsonElement raw)
    {
        var context = FrameJsonContext.Default;
        return type switch
        {
            FrameTypes.User => raw.Deserialize(context.UserFrame),
            FrameTypes.Assistant => raw.Deserialize(context.AssistantFrame),
            FrameTypes.System => raw.Deserialize(context.SystemFrame),
            FrameTypes.Result => raw.Deserialize(context.ResultFrame),
            FrameTypes.StreamEvent => raw.Deserialize(context.StreamEventFrame),
            FrameTypes.RateLimitEvent => raw.Deserialize(context.RateLimitEventFrame),
            FrameTypes.ConversationReset => raw.Deserialize(context.ConversationResetFrame),
            FrameTypes.CommandLifecycle => raw.Deserialize(context.CommandLifecycleFrame),
            FrameTypes.ToolProgress => new ToolProgressFrame(),
            FrameTypes.ToolUseSummary => new ToolUseSummaryFrame(),
            FrameTypes.AuthStatus => new AuthStatusFrame(),
            FrameTypes.PromptSuggestion => new PromptSuggestionFrame(),
            FrameTypes.ActiveGoal => new ActiveGoalFrame(),
            FrameTypes.ControlRequest => raw.Deserialize(context.ControlRequestFrameDto),
            FrameTypes.ControlResponse => raw.Deserialize(context.ControlResponseFrameDto),
            FrameTypes.ControlCancelRequest => raw.Deserialize(context.ControlCancelRequestFrame),
            _ => null,
        };
    }

    private static Result<InboundFrame, ParseError> Route(Frame frame, JsonElement raw, string line)
    {
        switch (frame)
        {
            case ControlRequestFrameDto request:
                return ControlParser.ParseRequest(request, line);
            case ControlResponseFrameDto response:
                return ControlParser.ParseResponse(response, line);
            case ControlCancelRequestFrame cancel:
                return cancel.RequestId switch
                {
                    null => JsonReading.Missing("request_id", line),
                    { Length: 0 } => JsonReading.Invalid("request_id", "must not be empty.", line),
                    { } cancelId => new ControlCancelFrame(new RequestId(cancelId)),
                };
            default:
                {
                    var message = MessageParser.Parse(frame, raw, line);
                    return message.TryGet(out var value, out var error)
                        ? new MessageFrame(value)
                        : error;
                }
        }
    }

    private static void WriteUserInput(WireJson writer, UserInputFrame user)
    {
        writer.BeginObject();
        writer.Property("type", FrameTypes.User);
        // The wire's client-side slot: empty on the one-shot path,
        // "default" from the client, in the position each path puts it. Never the binary's id.
        if (user.Form == UserInputForm.OneShot)
        {
            writer.Property("session_id", string.Empty);
        }

        writer.Name("message");
        writer.BeginObject();
        writer.Property("role", "user");
        writer.Name("content");
        if (user.Text is { } text)
        {
            writer.String(text);
        }
        else if (user.Blocks is { } blocks)
        {
            writer.BeginArray();
            foreach (var block in blocks)
            {
                writer.Element(block);
            }

            writer.EndArray();
        }
        else
        {
            writer.String(string.Empty);
        }

        writer.EndObject();
        writer.Name("parent_tool_use_id");
        if (user.ParentToolUseId is { } parentToolUseId)
        {
            writer.String(parentToolUseId);
        }
        else
        {
            writer.Null();
        }

        if (user.Form == UserInputForm.Client)
        {
            writer.Property("session_id", "default");
        }

        if (user.Uuid is { } uuid)
        {
            writer.Property("uuid", uuid.ToString("D"));
        }

        writer.EndObject();
    }

    private static void WriteOptional(WireJson writer, string name, string? value)
    {
        if (value is { } text)
        {
            writer.Property(name, text);
        }
    }
}
