
namespace Claude.Agent.Tests.Wire;

[TestClass]
public sealed class WireJsonTests
{
    [TestMethod]
    [DataRow(5.0, "5.0")]
    [DataRow(12.5, "12.5")]
    [DataRow(0.1, "0.1")]
    [DataRow(1e30, "1e+30")]
    [DataRow(1e16, "1e+16")]
    [DataRow(1e15, "1000000000000000.0")]
    [DataRow(1e-4, "0.0001")]
    [DataRow(1e-5, "1e-05")]
    [DataRow(0.0, "0.0")]
    [DataRow(-2.5, "-2.5")]
    public void FloatWritesTheShortestRoundTripFormWithATrailingZero(double value, string expected) =>
        Assert.AreEqual(expected, WireJson.Float(value));

    [TestMethod]
    public void FloatSpellsTheNonFiniteValuesAsTheWireDoes()
    {
        Assert.AreEqual("NaN", WireJson.Float(double.NaN));
        Assert.AreEqual("Infinity", WireJson.Float(double.PositiveInfinity));
        Assert.AreEqual("-Infinity", WireJson.Float(double.NegativeInfinity));
    }

    [TestMethod]
    public void SerializeWritesAnObjectWithTheWiresSpacingAndFloats() =>
        Assert.AreEqual(
            """{"cost": 2.5, "turns": 3, "ok": true, "note": null}""",
            WireJson.Serialize(Recorded.Json("""{"cost":2.5,"turns":3,"ok":true,"note":null}""")));

    // The escapes the writer spells, one string at a time. Each of these is written by a caller's
    // own text — a prompt, a tool input, a hook's reason — so every arm of the writer's switch is a
    // line the binary has to be able to read back.
    [TestMethod]
    [DataRow("\"", "\\\"")]
    [DataRow("\\", "\\\\")]
    [DataRow("\b", "\\b")]
    [DataRow("\f", "\\f")]
    [DataRow("\n", "\\n")]
    [DataRow("\r", "\\r")]
    [DataRow("\t", "\\t")]
    public void EveryEscapeTheWriterSpellsGoesOutAsTwoCharacters(string text, string expected) =>
        Assert.AreEqual($$""""{"note": "{{expected}}"}"""", Written(text));

    // Anything outside the printable ASCII range is a lowercase \uXXXX escape, and the range's own
    // ends are not: a space and a tilde are themselves.
    [TestMethod]
    [DataRow("\u0001", @"\u0001")]
    [DataRow("\u007f", @"\u007f")]
    [DataRow("ä", @"\u00e4")]
    [DataRow("\u2028", @"\u2028")]
    [DataRow("\ufeff", @"\ufeff")]
    [DataRow(" ", " ")]
    [DataRow("~", "~")]
    public void EveryCharacterOutsidePrintableAsciiIsALowercaseEscape(string text, string expected) =>
        Assert.AreEqual($$""""{"note": "{{expected}}"}"""", Written(text));

    // A character above the basic plane is two escapes, the pair that spells it, lowercase both.
    [TestMethod]
    public void ACharacterAboveTheBasicPlaneIsWrittenAsItsSurrogatePair() =>
        Assert.AreEqual("""{"note": "\ud83d\ude00"}""", Written("\U0001F600"));

    [TestMethod]
    public void TheEscapesAreWrittenInTheMiddleOfTextAsTheyComeAndNothingElseChanges() =>
        Assert.AreEqual(
            """{"note": "a\tb\n\"c\" \u00e4"}""",
            Written("a\tb\n\"c\" ä"));

    // Through the writer as a caller's string, not through a JSON reader, which would spell its own
    // escapes: a name is written the same way as a value.
    private static string Written(string text)
    {
        var writer = new WireJson();
        writer.BeginObject();
        writer.Property("note", text);
        writer.EndObject();
        return writer.ToString();
    }

    // A literal too large for a double parses as an infinity, whose spelling is not JSON. The line
    // came in as JSON, so it goes back out as it came rather than as a token the binary cannot read.
    [TestMethod]
    public void ANumberTooLargeForADoubleGoesBackOutAsTheBinaryWroteIt()
    {
        Assert.AreEqual(
            """{"timeout": 1e400, "floor": -1e400, "near": 2.5}""",
            WireJson.Serialize(Recorded.Json("""{"timeout":1e400,"floor":-1e400,"near":2.5}""")));
    }
}
