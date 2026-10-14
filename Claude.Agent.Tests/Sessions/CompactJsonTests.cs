using System.Text;
using System.Text.Json.Nodes;

namespace Claude.Agent.Tests.Sessions;

[TestClass]
public sealed class CompactJsonTests
{
    private const string Session = "e67351d1-f522-421e-a7f3-45bfeef09aa5";

    [TestMethod]
    public void ARecordedUserLineRoundTripsByteForByte()
    {
        var line = Line("\"type\":\"user\"");

        var entry = SessionEntry.Parse(line);

        Assert.IsTrue(entry.IsOk);
        Assert.AreEqual(line, entry.Value.ToCompactJson());
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(line), Encoding.UTF8.GetBytes(entry.Value.ToCompactJson()));
    }

    // JSON.parse takes half a surrogate pair escaped without its other half, and so does the reader:
    // each such half, high or low, in a value or a key, reads as U+FFFD, a whole pair beside one is
    // the character it spells, and an escaped backslash before a u is no escape. Written back out,
    // the half is the replacement character.
    [TestMethod]
    public void ATranscriptLineCarryingHalfOfASurrogatePairIsReadWithTheHalfAsAReplacementCharacter()
    {
        var entry = SessionEntry.Parse("""{"type":"user","\udc00":"a","message":{"content":"\ud800 \ud83d\ude00 \\ud800 \ud83d"}}""");

        Assert.IsTrue(entry.IsOk, entry.IsError ? entry.Error.Message : string.Empty);
        Assert.AreEqual("\uFFFD \U0001F600 \\ud800 \uFFFD", entry.Value.Json["message"]!["content"]!.GetValue<string>());
        Assert.AreEqual("a", entry.Value.Field("\uFFFD"));
        Assert.AreEqual(
            "{\"type\":\"user\",\"\uFFFD\":\"a\",\"message\":{\"content\":\"\uFFFD \U0001F600 \\\\ud800 \uFFFD\"}}",
            entry.Value.ToCompactJson());
    }

    // JSON.parse takes a line nested past System.Text.Json's default of 64, and the reader takes one
    // to SessionEntry.MaxDepth and writes it back out as it came; a line nested deeper is refused.
    [TestMethod]
    public void ALineIsReadNestedToTheMaxDepthAndRefusedPastIt()
    {
        var deepest = SessionEntry.Parse(Nested(SessionEntry.MaxDepth));
        var deeper = SessionEntry.Parse(Nested(SessionEntry.MaxDepth + 1));

        Assert.IsTrue(deepest.IsOk, deepest.IsError ? deepest.Error.Message : string.Empty);
        Assert.AreEqual(Nested(SessionEntry.MaxDepth), deepest.Value.ToCompactJson());
        Assert.IsTrue(deeper.IsError, "a line nested past the bound was taken");
        Assert.AreEqual(ParseErrorKind.MalformedJson, deeper.Error.Kind);
    }

    // A whole pair is one character to JSON.stringify, which writes it as itself.
    [TestMethod]
    public void ATranscriptLineCarryingAnEscapedWholeSurrogatePairIsTakenAndWrittenRaw()
    {
        var entry = SessionEntry.Parse("""{"type":"user","message":{"content":"\ud83d\ude00"}}""");

        Assert.IsTrue(entry.IsOk, entry.IsError ? entry.Error.Message : string.Empty);
        Assert.AreEqual("{\"type\":\"user\",\"message\":{\"content\":\"\U0001F600\"}}", entry.Value.ToCompactJson());
    }

    [TestMethod]
    public void ARecordedAttachmentLineRoundTripsByteForByte()
    {
        var line = Line("\"type\":\"attachment\"");

        var entry = SessionEntry.Parse(line);

        Assert.IsTrue(entry.IsOk);
        Assert.AreEqual(line, entry.Value.ToCompactJson());
    }

    // Every line the binary wrote, those carrying non-ASCII raw among them, comes back out byte for
    // byte.
    [TestMethod]
    public void EveryRecordedLineRoundTripsByteForByte()
    {
        var lines = Transcripts.SessionIds().SelectMany(session => Transcripts.LinesOf(session)).ToArray();

        foreach (var line in lines)
        {
            CollectionAssert.AreEqual(
                Encoding.UTF8.GetBytes(line),
                Encoding.UTF8.GetBytes(SessionEntry.Parse(line).Value.ToCompactJson()),
                line);
        }

        Assert.IsTrue(lines.Length > 300, $"only {lines.Length} recorded lines were compared");
        Assert.IsTrue(lines.Count(line => !line.All(char.IsAscii)) > 20, "fewer than 21 recorded lines carry non-ASCII");
    }

    [TestMethod]
    public void ANonAsciiValueIsWrittenAsItself()
    {
        var entry = new SessionEntry(new JsonObject { ["type"] = "x", ["t"] = "h\u00e9llo \U0001F600 \u2028" });

        Assert.AreEqual("{\"type\":\"x\",\"t\":\"h\u00e9llo \U0001F600 \u2028\"}", entry.ToCompactJson());
    }

    // A surrogate without its partner is the one character outside the controls JSON.stringify
    // escapes, in lowercase hex.
    [TestMethod]
    public void ALoneSurrogateIsEscapedInLowercaseHex()
    {
        var entry = new SessionEntry(new JsonObject { ["type"] = "x", ["t"] = "a\uD800b\uDFFF" });

        Assert.AreEqual("""{"type":"x","t":"a\ud800b\udfff"}""", entry.ToCompactJson());
    }

    [TestMethod]
    public void AControlCharacterIsEscapedByNameOrInLowercaseHexAndDeleteIsLiteral()
    {
        var entry = new SessionEntry(new JsonObject { ["type"] = "x", ["t"] = "\u0008\u0009\u000a\u000c\u000d\u0000\u001f\u007f" });

        Assert.AreEqual("""{"type":"x","t":"\b\t\n\f\r\u0000\u001f""" + "\u007f\"}", entry.ToCompactJson());
    }

    [TestMethod]
    public void TheFourPunctuationCharactersAreLeftLiteral()
    {
        var entry = new SessionEntry(new JsonObject { ["type"] = "x", ["t"] = "<a> & 'b'" });

        Assert.AreEqual("""{"type":"x","t":"<a> & 'b'"}""", entry.ToCompactJson());
    }

    [TestMethod]
    public void SeparatorsCarryNoSpaceAndNestingIsCompact()
    {
        var entry = SessionEntry.Parse("""{"a":[1,2,{"b":null}],"c":true,"d":1.5}""");

        Assert.AreEqual("""{"a":[1,2,{"b":null}],"c":true,"d":1.5}""", entry.Value.ToCompactJson());
    }

    [TestMethod]
    public void ParseRefusesALineThatIsNotAJsonObject()
    {
        Assert.IsTrue(SessionEntry.Parse("[1,2]").IsError);
        Assert.IsTrue(SessionEntry.Parse("{\"a\":").IsError);
        Assert.IsTrue(SessionEntry.Parse("   ").IsError);
    }

    [TestMethod]
    public void AParsedEntryReadsItsTypeAndUuid()
    {
        var entry = SessionEntry.Parse(Line("\"type\":\"user\"")).Value;

        Assert.AreEqual("user", entry.Type);
        Assert.IsNotNull(entry.Uuid);
    }

    // JSON.parse keeps the last occurrence of a repeated key in the first one's place and never
    // throws; JsonNode's object is a dictionary that refuses the second one, and the parse keeps the
    // last instead of throwing.
    [TestMethod]
    public void ALineWithADuplicateKeyKeepsTheLastOccurrenceInTheFirstOnesPlace()
    {
        var entry = SessionEntry.Parse("""{"type":"user","uuid":"a","type":"tag"}""");

        Assert.IsTrue(entry.IsOk);
        Assert.AreEqual("tag", entry.Value.Type);
        Assert.AreEqual("""{"type":"tag","uuid":"a"}""", entry.Value.ToCompactJson());
    }

    [TestMethod]
    public void ADuplicateKeyNestedDeepInALineIsFoldedTheSameWay()
    {
        var entry = SessionEntry.Parse("""{"type":"user","message":{"content":[{"n":1,"n":2.5}]}}""");

        Assert.IsTrue(entry.IsOk);
        Assert.AreEqual("""{"type":"user","message":{"content":[{"n":2.5}]}}""", entry.Value.ToCompactJson());
    }

    [TestMethod]
    public void AnEntryOwnsItsBagAndHandsBackACopyOfIt()
    {
        var json = new JsonObject { ["type"] = "user", ["v"] = 1 };
        var entry = new SessionEntry(json);

        json["v"] = 2;
        entry.Json["v"] = 3;

        Assert.AreEqual("""{"type":"user","v":1}""", entry.ToCompactJson());
        Assert.AreEqual(1, entry.Json["v"]!.GetValue<int>());
    }

    private static string Line(string marker) =>
        Transcripts.LinesOf(Session).First(line => line.Contains(marker, StringComparison.Ordinal));

    // A line of `depth` objects, the line itself the first and each but the last holding the next.
    private static string Nested(int depth) =>
        """{"type":"user","n":""" + string.Concat(Enumerable.Repeat("""{"n":""", depth - 2)) + "{}" + new string('}', depth - 1);
}
