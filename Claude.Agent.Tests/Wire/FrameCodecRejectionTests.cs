using System.Text.Json.Nodes;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Wire;

// Only a line that is not a JSON object, or has no string type, is rejected; any other line the codec
// cannot read as its kind degrades to the unknown message, with the line.
[TestClass]
public sealed class FrameCodecRejectionTests
{
    [TestMethod]
    public void ATruncatedLineIsRejectedAsMalformedJson()
    {
        var decoded = new FrameCodec().Decode(
            """
            {"type": "result", "subtype":
            """);

        Assert.IsTrue(decoded.IsError);
        Assert.AreEqual(ParseErrorKind.MalformedJson, decoded.Error.Kind);
    }

    [TestMethod]
    public void ALineThatIsNotAJsonObjectIsRejectedAsMalformedJson()
    {
        var decoded = new FrameCodec().Decode("""["type", "result"]""");

        Assert.IsTrue(decoded.IsError);
        Assert.AreEqual(ParseErrorKind.MalformedJson, decoded.Error.Kind);
    }

    [TestMethod]
    public void ALineWithoutATypeIsRejectedNamingTheMissingField()
    {
        var decoded = new FrameCodec().Decode("""{"subtype": "success"}""");

        Assert.IsTrue(decoded.IsError);
        Assert.AreEqual(ParseErrorKind.MissingField, decoded.Error.Kind);
        Assert.IsTrue(decoded.Error.Message.Contains("type", StringComparison.Ordinal), decoded.Error.Message);
    }

    [TestMethod]
    public void ALineWhoseTypeIsNotAStringIsRejected()
    {
        var decoded = new FrameCodec().Decode("""{"type": 7}""");

        Assert.IsTrue(decoded.IsError);
        Assert.AreEqual(ParseErrorKind.InvalidValue, decoded.Error.Kind);
    }

    [TestMethod]
    public void AFrameTypeTheCodecHasNoTypeForReadsAsAnUnknownMessageWithItsLine()
    {
        var unknown = Unknown("""{"type": "sparkle", "subtype": "glitter", "uuid": "u-1", "session_id": "s-1", "payload": 1}""");

        Assert.AreEqual("sparkle", unknown.Type);
        Assert.AreEqual("glitter", unknown.Subtype);
        Assert.AreEqual("u-1", unknown.Uuid);
        Assert.AreEqual("s-1", unknown.SessionId);
        Assert.AreEqual(1, unknown.Line().GetProperty("payload").GetInt32());
        Assert.IsNull(unknown.Reason);
    }

    [TestMethod]
    public void AResultFrameWithoutASessionIdReadsAsAnUnknownMessageNamingTheMissingField()
    {
        var unknown = Unknown(
            """
            {"type": "result", "subtype": "success", "duration_ms": 5, "duration_api_ms": 4, "is_error": false, "num_turns": 1}
            """);

        Assert.AreEqual("result", unknown.Type);
        Assert.AreEqual("success", unknown.Subtype);
        StringAssert.Contains(unknown.Reason, "session_id");
    }

    [TestMethod]
    public void AnAssistantFrameWhoseContentIsNotAnArrayReadsAsAnUnknownMessage()
    {
        var unknown = Unknown(
            """
            {"type": "assistant", "message": {"model": "claude-haiku-4-5", "content": "hello"}, "session_id": "s-1"}
            """);

        Assert.AreEqual("assistant", unknown.Type);
        Assert.IsNull(unknown.Subtype);
        Assert.AreEqual("s-1", unknown.SessionId);
    }

    [TestMethod]
    public void AFrameWhoseMembersHaveTheWrongJsonKindReadsAsAnUnknownMessage()
    {
        var unknown = Unknown("""{"type": "result", "subtype": "success", "num_turns": "one"}""");

        Assert.AreEqual("result", unknown.Type);
        Assert.IsNotNull(unknown.Reason);
    }

    // Half a surrogate pair is what a Node process writes for a string cut through one. The document
    // parses; reading that string throws where it is read, and not as a JsonException, so a line
    // carrying one is refused at the door rather than in the read loop.
    [TestMethod]
    public void ALineCarryingHalfOfASurrogatePairIsRejected()
    {
        var decoded = new FrameCodec().Decode(
            """{"type": "assistant", "message": {"model": "claude-haiku-4-5", "content": [{"type": "text", "text": "\ud83d"}]}, "session_id": "s-1"}""");

        Assert.IsTrue(decoded.IsError, "a line carrying half a pair was read");
        Assert.AreEqual(ParseErrorKind.MalformedJson, decoded.Error.Kind);
    }

    [TestMethod]
    public void ALineCarryingHalfOfASurrogatePairInItsTypeIsRejected()
    {
        var decoded = new FrameCodec().Decode("""{"type": "\udc00"}""");

        Assert.IsTrue(decoded.IsError, "a type of half a pair was read");
        Assert.AreEqual(ParseErrorKind.MalformedJson, decoded.Error.Kind);
    }

    [TestMethod]
    public void AWholeSurrogatePairIsTextAndReadsAsTheCharacterItSpells()
    {
        var decoded = new FrameCodec().Decode(
            """{"type": "assistant", "message": {"model": "claude-haiku-4-5", "content": [{"type": "text", "text": "\ud83d\ude00"}]}, "session_id": "s-1"}""");

        Assert.IsTrue(decoded.IsOk, decoded.IsError ? decoded.Error.Message : string.Empty);
        var message = (AssistantMessage)((MessageFrame)decoded.Value).Message;
        Assert.AreEqual("\U0001F600", ((TextBlock)message.Content[0]).Text);
    }

    // A backslash of its own is written as two, so the u after them starts no escape and the four
    // characters that follow are text.
    [TestMethod]
    public void AnEscapedBackslashBeforeAUIsNoEscapeAndTheLineIsRead()
    {
        var decoded = new FrameCodec().Decode(
            """{"type": "assistant", "message": {"model": "claude-haiku-4-5", "content": [{"type": "text", "text": "\\ud83d"}]}, "session_id": "s-1"}""");

        Assert.IsTrue(decoded.IsOk, decoded.IsError ? decoded.Error.Message : string.Empty);
        var message = (AssistantMessage)((MessageFrame)decoded.Value).Message;
        Assert.AreEqual(@"\ud83d", ((TextBlock)message.Content[0]).Text);
    }

    [TestMethod]
    public void AKeepAliveLineIsDroppedAtTheCodec()
    {
        var decoded = new FrameCodec().Decode("""{"type": "keep_alive"}""");

        Assert.IsTrue(decoded.IsOk, Recorded.Reason(decoded));
        Assert.IsInstanceOfType<KeepAliveFrame>(decoded.Value);
    }

    [TestMethod]
    public void AControlRequestWithABrokenBodyStillCarriesItsRequestId()
    {
        var decoded = new FrameCodec().Decode(
            """
            {"type": "control_request", "request_id": "req_7_abcdef01", "request": {"subtype": "hook_callback"}}
            """);

        Assert.IsTrue(decoded.IsOk, Recorded.Reason(decoded));

        var frame = (ControlRequestFrame)decoded.Value;
        Assert.AreEqual("req_7_abcdef01", frame.RequestId.Value);

        var unparsed = (UnparsedControlRequest)frame.Request;
        Assert.AreEqual("hook_callback", unparsed.Subtype);
        Assert.AreEqual(ParseErrorKind.MissingField, unparsed.Reason.Kind);
        Assert.IsTrue(
            unparsed.Reason.Message.Contains("callback_id", StringComparison.Ordinal),
            unparsed.Reason.Message);
    }

    // A number that fits the wire's integer but no TimeSpan or DateTimeOffset is a misfit like any
    // other: a result's required duration makes the frame the unknown, naming it; an optional
    // member reads as null and the frame keeps its type. No recording carries one; the lines are
    // recorded ones with the number replaced.
    [TestMethod]
    public void AResultWhoseDurationNoTimeSpanHoldsReadsAsAnUnknownMessageNamingIt()
    {
        foreach (var key in new[] { "duration_ms", "duration_api_ms" })
        {
            var result = JsonNode.Parse(Fixture.Line("trivial", "result"))!;
            result[key] = long.MaxValue;

            var unknown = Unknown(result.ToJsonString());

            Assert.AreEqual("result", unknown.Type, key);
            StringAssert.Contains(unknown.Reason, key);
        }
    }

    [TestMethod]
    public void ARateLimitEventWhoseResetsNoDateTimeOffsetHoldsReadsWithoutThem()
    {
        var recorded = JsonNode.Parse(Fixture.Line("trivial", "rate_limit_event"))!;
        recorded["rate_limit_info"]!["resetsAt"] = long.MaxValue;
        recorded["rate_limit_info"]!["overageResetsAt"] = long.MinValue;

        var decoded = new FrameCodec().Decode(recorded.ToJsonString());

        Assert.IsTrue(decoded.IsOk, Recorded.Reason(decoded));
        Assert.IsInstanceOfType<MessageFrame>(decoded.Value, out var frame);
        Assert.IsInstanceOfType<RateLimitMessage>(frame.Message, out var limit);
        Assert.IsNull(limit.ResetsAt);
        Assert.IsNull(limit.OverageResetsAt);
        Assert.AreEqual("five_hour", limit.LimitType);
    }

    [TestMethod]
    public void ATaskProgressWhoseDurationNoTimeSpanHoldsReadsWithoutIt()
    {
        var recorded = JsonNode.Parse(Fixture.Load("task-background").Stdout[19])!;
        Assert.AreEqual("task_progress", (string?)recorded["subtype"]);
        recorded["usage"]!["duration_ms"] = long.MaxValue;

        var decoded = new FrameCodec().Decode(recorded.ToJsonString());

        Assert.IsTrue(decoded.IsOk, Recorded.Reason(decoded));
        Assert.IsInstanceOfType<MessageFrame>(decoded.Value, out var frame);
        Assert.IsInstanceOfType<TaskProgressMessage>(frame.Message, out var task);
        Assert.IsNull(task.Duration);
        Assert.AreEqual(6499, task.TotalTokens);
    }

    // A model's usage entry reads a number that is missing or does not fit as null, not as 0; the
    // recorded entry beside it keeps its numbers.
    [TestMethod]
    public void AModelUsageEntryWhoseNumbersAreMissingOrMisfitReadsThemAsNull()
    {
        var result = JsonNode.Parse(Fixture.Line("trivial", "result"))!;
        result["modelUsage"]!["claude-sonnet-4-5"] = new JsonObject
        {
            ["inputTokens"] = "many",
            ["outputTokens"] = 1.5,
            ["costUSD"] = true,
        };

        var decoded = new FrameCodec().Decode(result.ToJsonString());

        Assert.IsTrue(decoded.IsOk, Recorded.Reason(decoded));
        Assert.IsInstanceOfType<MessageFrame>(decoded.Value, out var frame);
        Assert.IsInstanceOfType<ResultMessage>(frame.Message, out var read);

        var misfit = read.ModelUsage["claude-sonnet-4-5"];
        Assert.IsNull(misfit.InputTokens);
        Assert.IsNull(misfit.OutputTokens);
        Assert.IsNull(misfit.CacheReadInputTokens);
        Assert.IsNull(misfit.CacheCreationInputTokens);
        Assert.IsNull(misfit.WebSearchRequests);
        Assert.IsNull(misfit.CostUsd);
        Assert.IsNull(misfit.ContextWindow);
        Assert.IsNull(misfit.MaxOutputTokens);

        var haiku = read.ModelUsage["claude-haiku-4-5-20251001"];
        Assert.AreEqual(910, haiku.InputTokens);
        Assert.AreEqual(0, haiku.WebSearchRequests);
        Assert.AreEqual(0.047111m, haiku.CostUsd);
    }

    private static UnknownMessage Unknown(string line)
    {
        var decoded = new FrameCodec().Decode(line);

        Assert.IsTrue(decoded.IsOk, Recorded.Reason(decoded));
        Assert.IsInstanceOfType<MessageFrame>(decoded.Value, out var frame);
        Assert.IsInstanceOfType<UnknownMessage>(frame.Message, out var unknown);
        return unknown;
    }
}
