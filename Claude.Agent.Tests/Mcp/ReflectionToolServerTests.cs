using System.Text.Json;

namespace Claude.Agent.Tests.Mcp;

[TestClass]
public sealed class ReflectionToolServerTests
{
    private static readonly ReflectionToolServer Server = new(new Probe(), "probe", "2.0.0");

    [TestMethod]
    public void EveryMethodCarryingTheAttributeIsAToolUnderItsAttributeName()
    {
        var definition = Definition("greet");

        Assert.AreEqual("Greets someone", definition.Description);
        Assert.IsNull(definition.Annotations);
    }

    [TestMethod]
    public void EachPrimitiveParameterKindCarriesItsJsonTypeName()
    {
        Assert.AreEqual("string", TypeOf("kinds", "text"));
        Assert.AreEqual("integer", TypeOf("kinds", "count"));
        Assert.AreEqual("number", TypeOf("kinds", "amount"));
        Assert.AreEqual("boolean", TypeOf("kinds", "flag"));
    }

    [TestMethod]
    public void AnEnumParameterIsAStringThatEnumeratesItsMembers()
    {
        var shade = Property("kinds", "shade");

        Assert.AreEqual("string", shade.GetProperty("type").GetString());
        CollectionAssert.AreEqual(new[] { "Light", "Dark" }, Names(shade.GetProperty("enum")));
    }

    [TestMethod]
    public void ACollectionParameterIsAnArrayOfItsElementType()
    {
        var tags = Property("kinds", "tags");

        Assert.AreEqual("array", tags.GetProperty("type").GetString());
        Assert.AreEqual("string", tags.GetProperty("items").GetProperty("type").GetString());
    }

    [TestMethod]
    public void ARecordParameterIsANestedObjectWithItsOwnRequiredProperties()
    {
        var point = Property("kinds", "point");

        Assert.AreEqual("object", point.GetProperty("type").GetString());
        Assert.AreEqual("integer", point.GetProperty("properties").GetProperty("X").GetProperty("type").GetString());
        CollectionAssert.AreEqual(new[] { "X", "Y" }, Names(point.GetProperty("required")));
    }

    [TestMethod]
    public void RequiredNamesEveryParameterThatIsNeitherNullableNorDefaulted()
    {
        CollectionAssert.AreEqual(new[] { "subject" }, Names(Schema("optional").GetProperty("required")));
    }

    [TestMethod]
    public async Task AnAbsentOptionalArgumentFallsBackToItsDefault()
    {
        var result = await Server.CallAsync("optional", Arguments("""{"subject":"bee"}"""), CancellationToken.None);

        Assert.AreEqual("bee/-/7", TextOf(result));
    }

    [TestMethod]
    public void AParameterAttributeBecomesTheDescriptionOfThatProperty()
    {
        Assert.AreEqual("the first number", Property("described", "a").GetProperty("description").GetString());
        Assert.IsFalse(
            Property("described", "b").TryGetProperty("description", out _),
            "a parameter without the attribute carries no description.");
    }

    [TestMethod]
    public void ACancellationTokenParameterIsNotPartOfTheSchema()
    {
        Assert.AreEqual(0, Schema("token").GetProperty("properties").EnumerateObject().Count());
    }

    [TestMethod]
    public async Task ACancellationTokenParameterIsHandedTheCallersToken()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var result = await Server.CallAsync("token", Arguments("{}"), cancelled.Token);

        Assert.AreEqual("cancelled", TextOf(result));
    }

    [TestMethod]
    public async Task AStringReturnBecomesOneTextBlock()
    {
        var result = await Server.CallAsync("greet", Arguments("""{"who":"bee"}"""), CancellationToken.None);

        Assert.IsFalse(result.IsError);
        Assert.AreEqual(1, result.Content.Length);
        Assert.AreEqual("hello bee", TextOf(result));
    }

    [TestMethod]
    public async Task AToolResultReturnIsPassedThroughWithItsErrorFlag()
    {
        var result = await Server.CallAsync("refuse", Arguments("{}"), CancellationToken.None);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("nope", TextOf(result));
    }

    [TestMethod]
    public async Task ATaskReturnIsAwaitedAndItsValueRendered()
    {
        var result = await Server.CallAsync("later", Arguments("""{"what":"bee"}"""), CancellationToken.None);

        Assert.AreEqual("BEE", TextOf(result));
    }

    [TestMethod]
    public async Task AValueTaskReturnIsAwaitedAndItsValueRendered()
    {
        var result = await Server.CallAsync("half", Arguments("""{"value":5}"""), CancellationToken.None);

        Assert.AreEqual("2.5", TextOf(result));
    }

    [TestMethod]
    public async Task AMissingRequiredArgumentIsAnErrorResultNamingTheProperty()
    {
        var result = await Server.CallAsync("described", Arguments("""{"a":1}"""), CancellationToken.None);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("Input validation error: 'b' is a required property", TextOf(result));
    }

    [TestMethod]
    public async Task AnArgumentOfTheWrongTypeIsAnErrorResultNamingTheTypeItWanted()
    {
        var result = await Server.CallAsync("described", Arguments("""{"a":"two","b":3}"""), CancellationToken.None);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("Input validation error: 'a' is not of type 'number'", TextOf(result));
    }

    [TestMethod]
    public async Task AToolThatThrowsIsAnErrorResultCarryingTheExceptionMessage()
    {
        var result = await Server.CallAsync("boom", Arguments("{}"), CancellationToken.None);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("the tool fell over", TextOf(result));
    }

    [TestMethod]
    public async Task AnUnknownToolNameIsAnErrorResultRatherThanAThrow()
    {
        var result = await Server.CallAsync("ghost", Arguments("{}"), CancellationToken.None);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("This server has no tool named 'ghost'.", TextOf(result));
    }

    [TestMethod]
    public async Task ANestedObjectMissingARequiredPropertyIsAnErrorNamingThatProperty()
    {
        var result = await Server.CallAsync("place", Arguments("""{"point":{"X":1}}"""), CancellationToken.None);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("Input validation error: 'Y' is a required property", TextOf(result));
    }

    [TestMethod]
    public async Task AnExplicitNullForAParameterThatIsNeitherNullableNorOptionalIsATypeMismatch()
    {
        var result = await Server.CallAsync(
            "optional",
            Arguments("""{"subject":"bee","limit":null}"""),
            CancellationToken.None);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("Input validation error: 'limit' is not of type 'integer'", TextOf(result));
    }

    [TestMethod]
    public async Task AnExplicitNullForANullableParameterBindsAsNull()
    {
        var result = await Server.CallAsync(
            "optional",
            Arguments("""{"subject":"bee","note":null}"""),
            CancellationToken.None);

        Assert.IsFalse(result.IsError);
        Assert.AreEqual("bee/-/7", TextOf(result));
    }

    [TestMethod]
    public async Task AnEnumInsideANestedObjectBindsFromTheNameTheSchemaAdvertises()
    {
        var result = await Server.CallAsync(
            "recoat",
            Arguments("""{"coat":{"Shade":"Dark","Layers":2}}"""),
            CancellationToken.None);

        Assert.IsFalse(result.IsError);
        Assert.AreEqual("Dark/2", TextOf(result));
    }

    [TestMethod]
    public void AParameterWithNoSettablePropertiesAdvertisesItsConstructorParameters()
    {
        var crate = Property("stow", "crate");

        Assert.AreEqual("string", crate.GetProperty("properties").GetProperty("Label").GetProperty("type").GetString());
        CollectionAssert.AreEqual(new[] { "Label", "Count" }, Names(crate.GetProperty("required")));
    }

    [TestMethod]
    public async Task AParameterWithNoSettablePropertiesBindsThroughItsConstructor()
    {
        var result = await Server.CallAsync(
            "stow",
            Arguments("""{"crate":{"Label":"bees","Count":3}}"""),
            CancellationToken.None);

        Assert.IsFalse(result.IsError);
        Assert.AreEqual("bees x3", TextOf(result));
    }

    [TestMethod]
    public async Task AStaticMethodCarryingTheAttributeIsAToolLikeAnInstanceMethod()
    {
        var server = new ReflectionToolServer(new Calc(), "calc", "1.0.0");

        var result = await server.CallAsync("add", Arguments("""{"a":2,"b":3}"""), CancellationToken.None);

        Assert.AreEqual("5", TextOf(result));
    }

    [TestMethod]
    public void TheToolsAreListedByNameInOrdinalOrder()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "boom", "described", "greet", "half", "kinds", "later", "optional", "place", "recoat", "refuse",
                "stow", "token",
            },
            Server.Tools.Select(tool => tool.Name).ToArray());
    }

    [TestMethod]
    public void TwoMethodsSharingAToolNameRefuseToBecomeAServer()
    {
        var failure = Assert.ThrowsExactly<InvalidOperationException>(
            () => new ReflectionToolServer(new Twice(), "twice", "1.0.0"));

        StringAssert.Contains(failure.Message, "'same'");
        StringAssert.Contains(failure.Message, "Twice.One");
        StringAssert.Contains(failure.Message, "Twice.Two");
    }

    [TestMethod]
    public async Task AnArgumentThatIsNotAnArrayIsAnErrorNamingTheTypeItWanted()
    {
        var result = await Server.CallAsync("kinds", Arguments(Kinds("\"bee\"")), CancellationToken.None);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("Input validation error: 'tags' is not of type 'array'", TextOf(result));
    }

    [TestMethod]
    public async Task ABindFailureInsideAnArrayReportsItsReasonWithoutTheJsonPath()
    {
        var result = await Server.CallAsync("kinds", Arguments(Kinds("[1]")), CancellationToken.None);

        Assert.IsTrue(result.IsError);
        StringAssert.StartsWith(TextOf(result), "Input validation error: 'tags' is invalid: ");
        Assert.IsFalse(TextOf(result).Contains("Path:", StringComparison.Ordinal), TextOf(result));
    }

    private static string Kinds(string tags) =>
        $$$"""{"text":"a","count":1,"amount":2.5,"flag":true,"shade":"Light","tags":{{{tags}}},"point":{"X":1,"Y":2}}""";

    private static ToolDefinition Definition(string tool) => Server.Tools.Single(one => one.Name == tool);

    private static JsonElement Schema(string tool) => Definition(tool).InputSchema;

    private static JsonElement Property(string tool, string name) =>
        Schema(tool).GetProperty("properties").GetProperty(name);

    private static string? TypeOf(string tool, string name) =>
        Property(tool, name).GetProperty("type").GetString();

    private static string?[] Names(JsonElement array) =>
        [.. array.EnumerateArray().Select(element => element.GetString())];

    private static JsonElement Arguments(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string TextOf(ToolResult result) =>
        string.Concat(result.Content.OfType<ToolText>().Select(text => text.Text));
}

internal enum Shade
{
    Light,
    Dark,
}

internal sealed record Point(int X, int Y);

internal sealed record Coat(Shade Shade, int Layers);

internal sealed class Crate
{
    public Crate(string label, int count)
    {
        Label = label;
        Count = count;
    }

    public string Label { get; }

    public int Count { get; }
}

internal sealed class Twice
{
    [McpTool("same", "One of two")]
    public static string One() => "one";

    [McpTool("same", "Two of two")]
    public static string Two() => "two";
}

internal sealed class Probe
{
    private readonly string _greeting = "hello";

    [McpTool("kinds", "Takes one of every parameter kind")]
    public static string Kinds(string text, int count, double amount, bool flag, Shade shade, string[] tags, Point point)
    {
        ArgumentNullException.ThrowIfNull(point);

        return $"{text}/{count}/{amount}/{flag}/{shade}/{tags?.Length}/{point.X}";
    }

    [McpTool("optional", "Takes a required, a nullable and a defaulted parameter")]
    public static string Optional(string subject, string? note, int limit = 7) => $"{subject}/{note ?? "-"}/{limit}";

    [McpTool("described", "Adds two described numbers")]
    public static double Described([System.ComponentModel.Description("the first number")] double a, double b) => a + b;

    [McpTool("place", "Reads a nested object")]
    public static string Place(Point point)
    {
        ArgumentNullException.ThrowIfNull(point);

        return $"{point.X},{point.Y}";
    }

    [McpTool("recoat", "Reads a nested enum")]
    public static string Recoat(Coat coat)
    {
        ArgumentNullException.ThrowIfNull(coat);

        return $"{coat.Shade}/{coat.Layers}";
    }

    [McpTool("stow", "Reads an object built through its constructor")]
    public static string Stow(Crate crate)
    {
        ArgumentNullException.ThrowIfNull(crate);

        return $"{crate.Label} x{crate.Count}";
    }

    [McpTool("refuse", "Refuses")]
    public static ToolResult Refuse() => ToolResult.FromError("nope");

    [McpTool("later", "Answers after a yield")]
    public static async Task<string> Later(string what)
    {
        await Task.Yield();
        return what.ToUpperInvariant();
    }

    [McpTool("half", "Halves a number")]
    public static ValueTask<double> Half(double value) => ValueTask.FromResult(value / 2);

    [McpTool("token", "Reports the state of the call's token")]
    public static string Token(CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested ? "cancelled" : "live";

    [McpTool("boom", "Falls over")]
    public static string Boom() => throw new InvalidOperationException("the tool fell over");

    [McpTool("greet", "Greets someone")]
    public string Greet(string who) => $"{_greeting} {who}";
}
