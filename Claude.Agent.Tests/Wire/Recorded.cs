using System.Collections.Immutable;
using Claude.Agent.Tests.Replay;
using System.Text.Json;

namespace Claude.Agent.Tests.Wire;

internal static class Recorded
{
    public static ImmutableArray<InboundFrame> Stdout(string fixture)
    {
        var codec = new FrameCodec();
        var frames = ImmutableArray.CreateBuilder<InboundFrame>();
        foreach (var line in Fixture.Load(fixture).Stdout)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var decoded = codec.Decode(line);
            Assert.IsTrue(decoded.IsOk, $"{fixture}.stdout.jsonl: {Reason(decoded)}");
            frames.Add(decoded.Value);
        }

        return frames.ToImmutable();
    }

    public static string Stdin(string fixture, int lineNumber) => Fixture.Load(fixture).Stdin[lineNumber - 1];

    public static IEnumerable<TMessage> Messages<TMessage>(string fixture)
        where TMessage : AgentMessage =>
        Stdout(fixture).OfType<MessageFrame>().Select(frame => frame.Message).OfType<TMessage>();

    public static IEnumerable<TRequest> Requests<TRequest>(string fixture)
        where TRequest : ControlRequest =>
        Stdout(fixture).OfType<ControlRequestFrame>().Select(frame => frame.Request).OfType<TRequest>();

    public static ImmutableArray<RequestId> RequestIds(string fixture) =>
        [.. Stdout(fixture).OfType<ControlRequestFrame>().Select(frame => frame.RequestId)];

    public static JsonElement? SuccessPayload(string fixture, string requestId)
    {
        foreach (var frame in Stdout(fixture).OfType<ControlResponseFrame>())
        {
            if (frame.Response is ControlSuccess success && success.RequestId.Value == requestId)
            {
                return success.Response;
            }
        }

        throw new AssertFailedException($"{fixture}.stdout.jsonl carries no success response for '{requestId}'.");
    }

    public static string Reason<TValue>(Result<TValue, ParseError> result) =>
        result.IsOk ? "ok" : $"{result.Error.Kind}: {result.Error.Message}";

    public static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
