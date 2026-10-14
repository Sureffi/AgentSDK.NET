using System.Text.Json;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Wire;

[TestClass]
public sealed class FrameCodecDecodeTests
{
    private static readonly string[] Recordings =
    [
        .. Directory.EnumerateFiles(Fixture.Root, "*.stdout.jsonl")
            .Select(path => Path.GetFileName(path)[..^".stdout.jsonl".Length])
            .Order(StringComparer.Ordinal),
    ];

    public static IEnumerable<object[]> RecordedStdoutLines()
    {
        foreach (var name in Recordings)
        {
            var lines = Fixture.Load(name).Stdout;
            for (var number = 1; number <= lines.Length; number++)
            {
                if (!string.IsNullOrWhiteSpace(lines[number - 1]))
                {
                    yield return [name, number];
                }
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(RecordedStdoutLines), DynamicDataSourceType.Method)]
    public void EveryRecordedStdoutLineDecodesToATypedFrameAndAMessageCarriesTheLineAsItsRaw(string fixture, int lineNumber)
    {
        var line = Fixture.Load(fixture).Stdout[lineNumber - 1];
        var where = $"{fixture}.stdout.jsonl line {lineNumber}";

        var decoded = new FrameCodec().Decode(line);

        Assert.IsTrue(decoded.IsOk, $"{where} was rejected: {Recorded.Reason(decoded)}");

        var frame = decoded.Value;
        switch (frame)
        {
            case MessageFrame { Message: UnknownMessage unknown }:
                Assert.Fail($"{where} decoded to an UnknownMessage of type '{unknown.Type}': {unknown.Reason ?? "no type of its own"}");
                break;
            case MessageFrame { Message: AssistantMessage { Content: var blocks } } when FirstUnknown(blocks) is { } block:
                Assert.Fail($"{where} carries an UnknownContentBlock of type '{block.Type}'.");
                break;
            case MessageFrame { Message: UserMessage { Content: var blocks } } when FirstUnknown(blocks) is { } block:
                Assert.Fail($"{where} carries an UnknownContentBlock of type '{block.Type}'.");
                break;
            case ControlRequestFrame { Request: UnparsedControlRequest unparsed }:
                Assert.Fail($"{where} decoded to UnparsedControlRequest '{unparsed.Subtype}': {unparsed.Reason.Message}");
                break;
            case ControlRequestFrame { Request: UnknownControlRequest unknown }:
                Assert.Fail($"{where} decoded to UnknownControlRequest, subtype '{unknown.Subtype}'.");
                break;
            case ControlRequestFrame { Request: HookCallbackRequest { Input: { Event.IsDefined: false } hook } }:
                Assert.Fail($"{where} decoded to a hook input whose event this library does not name, '{hook.Event}'.");
                break;
            case MessageFrame:
            case ControlRequestFrame:
            case ControlResponseFrame:
            case ControlCancelFrame:
                break;
            default:
                Assert.Fail($"{where} decoded to {frame.GetType().Name}.");
                break;
        }

        if (frame is MessageFrame { Message: var read })
        {
            Assert.IsNotNull(read.Raw, $"{where} decoded to a message with no Raw.");
            using var document = JsonDocument.Parse(line);
            Assert.IsTrue(JsonElement.DeepEquals(document.RootElement, read.Raw.Value), $"{where}: the message's Raw is not its line.");
        }
    }

    // A block of a kind the library has no type for is kept with its JSON, neither thrown nor
    // skipped, in a message's content and inside a tool_result's; the search the test above fails
    // on reaches both. A block of a kind it does have a type for, whose members do not fit that
    // type, is kept the same way: the document here carries no source, which its kind requires.
    // A tool_result without content reads as empty content. No recording carries any of it: every
    // recorded tool_result has content, and every recorded block is a kind this library names.
    [TestMethod]
    public void ABlockOfAnUnknownKindIsKeptWithItsJsonInAMessageAndInsideAToolResultAndAToolResultWithoutContentIsEmpty()
    {
        const string Line =
            """
            {"type": "user", "message": {"role": "user", "content": [
              {"type": "kettle_state", "source": {"type": "base64", "media_type": "image/png", "data": "AA=="}},
              {"type": "tool_result", "tool_use_id": "toolu_01", "content": [{"type": "text", "text": "read"}, {"type": "document", "title": "notes"}]},
              {"type": "tool_result", "tool_use_id": "toolu_02"}
            ]}, "parent_tool_use_id": null, "session_id": "s-1"}
            """;

        var decoded = new FrameCodec().Decode(Line.ReplaceLineEndings(string.Empty));

        Assert.IsTrue(decoded.IsOk, Recorded.Reason(decoded));
        Assert.IsInstanceOfType<MessageFrame>(decoded.Value, out var frame);
        Assert.IsInstanceOfType<UserMessage>(frame.Message, out var user);
        Assert.AreEqual(3, user.Content.Count);

        Assert.IsInstanceOfType<UnknownContentBlock>(user.Content[0], out var unnamed);
        Assert.AreEqual("kettle_state", unnamed.Type);
        Assert.AreEqual("AA==", unnamed.Raw.GetProperty("source").GetProperty("data").GetString());

        Assert.IsInstanceOfType<ToolResultBlock>(user.Content[1], out var answered);
        Assert.AreEqual("toolu_01", answered.ToolUseId);
        Assert.IsInstanceOfType<TextBlock>(answered.Content[0]);
        Assert.IsInstanceOfType<UnknownContentBlock>(answered.Content[1], out var document);
        Assert.AreEqual("document", document.Type);
        Assert.AreEqual("notes", document.Raw.GetProperty("title").GetString());

        Assert.IsInstanceOfType<ToolResultBlock>(user.Content[2], out var empty);
        Assert.AreEqual("toolu_02", empty.ToolUseId);
        Assert.AreEqual(0, empty.Content.Count);

        Assert.AreSame(unnamed, FirstUnknown(user.Content));
        Assert.AreSame(document, FirstUnknown(user.Content.Skip(1)));
    }

    // The first block of an unknown kind among `blocks`, a tool_result's content searched as well.
    private static UnknownContentBlock? FirstUnknown(IEnumerable<ContentBlock> blocks) =>
        blocks.Select(block => block switch
            {
                UnknownContentBlock unknown => unknown,
                ToolResultBlock result => FirstUnknown(result.Content),
                _ => null,
            })
            .FirstOrDefault(found => found is not null);
}
