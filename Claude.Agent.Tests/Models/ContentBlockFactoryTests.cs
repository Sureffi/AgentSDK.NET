using System.Collections;
using System.Reflection;
using System.Text.Json;
using Claude.Agent.Tests.Wire;

namespace Claude.Agent.Tests.Models;

// The block family through the model factory: a caller builds any of it for their own test. The
// family is found the way a caller reaches it — from ContentBlock down, through every property, the
// nested sets and their unknowns included; the tool views behind ToolUseBlock.Input are another
// lane's and are left where the walk meets them. That every concrete model has a builder at all is
// held in ShapeTests; what is held here is that the builders name the properties they set, that a
// builder called with nothing hands out a whole model, and that a builder that cannot stand without
// a member says so.
[TestClass]
public sealed class ContentBlockFactoryTests
{
    private static readonly Assembly Library = typeof(ContentBlock).Assembly;

    private static readonly Type[] Family = [.. FamilyOf(typeof(ContentBlock))];

    private static readonly MethodInfo[] Builders =
        [.. typeof(ClaudeAgentModelFactory)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => Family.Contains(method.ReturnType))];

    [TestMethod]
    public void TheWalkFindsTheWholeBlockFamilyAndStopsAtTheToolViews()
    {
        Assert.IsTrue(Family.Length > 60, $"{Family.Length} types found: the walk has stopped descending");
        CollectionAssert.IsSubsetOf(
            new[]
            {
                typeof(TextBlock), typeof(UnknownContentBlock), typeof(FallbackBlock),
                typeof(CharacterLocationCitation), typeof(UnknownCitation),
                typeof(DirectToolCaller), typeof(Base64ImageSource), typeof(ContentDocumentSource),
                typeof(BrowserTab), typeof(DownloadCompletedBrowserStateChange),
                typeof(WebSearchResult), typeof(WebFetchResult), typeof(AdvisorRedactedResult),
                typeof(EncryptedCodeExecutionResult), typeof(BashCodeExecutionOutput),
                typeof(TextEditorCodeExecutionStringReplaceResult), typeof(ToolSearchToolSearchResult),
                typeof(CacheControl), typeof(CitationsConfiguration), typeof(ImageTransformations),
                typeof(FallbackModel), typeof(FallbackTrigger),
            },
            Family);
        CollectionAssert.DoesNotContain(Family, typeof(ToolInput));
        CollectionAssert.DoesNotContain(Family, typeof(ToolOutput));
    }

    [TestMethod]
    public void EveryConcreteModelOfTheFamilyHasExactlyOneBuilder()
    {
        foreach (var type in Family.Where(type => !type.IsAbstract))
        {
            Assert.AreEqual(
                1,
                Builders.Count(builder => builder.ReturnType == type),
                $"{type.Name} does not have exactly one builder on the model factory");
        }

        Assert.AreEqual(Family.Count(type => !type.IsAbstract), Builders.Length);
    }

    // The builder's word: each parameter is the property of its name, and no property is left
    // unreachable. A property the builder cannot set is a member a caller's test cannot stand up.
    [TestMethod]
    public void EveryBuilderNamesEveryPropertyOfItsTypeAndNothingElse()
    {
        foreach (var builder in Builders)
        {
            var properties = builder.ReturnType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .ToArray();
            var parameters = builder.GetParameters().Select(parameter => Capitalised(parameter.Name!)).ToArray();

            CollectionAssert.AreEquivalent(
                properties,
                parameters,
                $"{builder.ReturnType.Name}: the builder takes [{string.Join(", ", parameters)}] "
                + $"and the type has [{string.Join(", ", properties)}]");
        }
    }

    // A builder called with nothing stands: every member the type says is always there is there —
    // a list is empty and not null, a string is a string — and the raw JSON member of an unknown is
    // an object. A member the type declares nullable is left where it is.
    [TestMethod]
    public void EveryBuilderCalledWithNothingHandsOutAModelWithNoNullListOrString()
    {
        var nullability = new NullabilityInfoContext();
        foreach (var builder in Builders.Where(builder => builder.GetParameters().All(parameter => parameter.IsOptional)))
        {
            var built = builder.Invoke(null, BindingFlags.Default, Type.DefaultBinder, [.. builder.GetParameters().Select(_ => Type.Missing)], culture: null);
            Assert.IsNotNull(built, builder.ReturnType.Name);

            foreach (var property in builder.ReturnType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var value = property.GetValue(built);
                var where = $"{builder.ReturnType.Name}.{property.Name}";
                if ((property.PropertyType == typeof(string) || IsList(property.PropertyType))
                    && nullability.Create(property).ReadState == NullabilityState.NotNull)
                {
                    Assert.IsNotNull(value, where);
                }

                if (value is ICollection list)
                {
                    Assert.AreEqual(0, list.Count, where);
                }

                if (value is JsonElement raw)
                {
                    Assert.AreEqual(JsonValueKind.Object, raw.ValueKind, where);
                }
            }
        }
    }

    // The four builders whose model cannot stand without a member: the member is required, and null
    // is refused rather than kept.
    [TestMethod]
    public void ABuilderWhoseModelCannotStandWithoutAMemberRefusesNullForIt()
    {
        var required = Builders
            .SelectMany(builder => builder.GetParameters().Where(parameter => !parameter.IsOptional).Select(parameter => $"{builder.ReturnType.Name}.{Capitalised(parameter.Name!)}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "DocumentBlock.Source",
                "FallbackBlock.From",
                "FallbackBlock.To",
                "FallbackBlock.Trigger",
                "ImageBlock.Source",
                "WebFetchResult.Content",
            },
            required);

        Assert.ThrowsExactly<ArgumentNullException>(() => ClaudeAgentModelFactory.ImageBlock(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ClaudeAgentModelFactory.DocumentBlock(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ClaudeAgentModelFactory.WebFetchResult(null!));
        var model = ClaudeAgentModelFactory.FallbackModel("a-model");
        var trigger = ClaudeAgentModelFactory.FallbackTrigger();
        Assert.ThrowsExactly<ArgumentNullException>(() => ClaudeAgentModelFactory.FallbackBlock(null!, model, trigger));
        Assert.ThrowsExactly<ArgumentNullException>(() => ClaudeAgentModelFactory.FallbackBlock(model, null!, trigger));
        Assert.ThrowsExactly<ArgumentNullException>(() => ClaudeAgentModelFactory.FallbackBlock(model, model, null!));
    }

    // One tree built by hand, the way a caller's test builds the answer it wants to see: what goes
    // in comes back out, through every level.
    [TestMethod]
    public void AModelBuiltThroughTheFactoryHandsBackWhatWentIn()
    {
        var text = ClaudeAgentModelFactory.TextBlock(
            "Tea steeps for three minutes.",
            [ClaudeAgentModelFactory.CharacterLocationCitation("three minutes", 0, 12, 25, "Brewing", "file_01")],
            ClaudeAgentModelFactory.CacheControl(CacheControlTimeToLive.OneHour));

        Assert.AreEqual("Tea steeps for three minutes.", text.Text);
        Assert.AreEqual(CacheControlTimeToLive.OneHour, text.CacheControl!.TimeToLive);
        var citation = (CharacterLocationCitation)text.Citations.Single();
        Assert.AreEqual("three minutes", citation.CitedText);
        Assert.AreEqual(25, citation.EndCharacterIndex);

        var call = ClaudeAgentModelFactory.ServerToolUseBlock(
            "srvtoolu_01",
            ServerToolName.WebFetch,
            Recorded.Json("""{"url":"https://example.com/brew"}"""),
            ClaudeAgentModelFactory.CodeExecutionToolCaller20260120("srvtoolu_00"));

        Assert.AreEqual(ServerToolName.WebFetch, call.Name);
        Assert.AreEqual("https://example.com/brew", call.RawInput.GetProperty("url").GetString());
        Assert.AreEqual("srvtoolu_00", ((CodeExecutionToolCaller20260120)call.Caller!).ToolId);

        var answer = ClaudeAgentModelFactory.WebFetchToolResultBlock(
            "srvtoolu_01",
            [
                ClaudeAgentModelFactory.WebFetchResult(
                    ClaudeAgentModelFactory.DocumentBlock(ClaudeAgentModelFactory.TextDocumentSource("steep", DocumentMediaType.PlainText), "Brewing"),
                    "https://example.com/brew",
                    new DateTimeOffset(2026, 9, 18, 10, 11, 12, TimeSpan.Zero)),
            ]);

        var page = (WebFetchResult)answer.Content.Single();
        Assert.AreEqual("srvtoolu_01", answer.ToolUseId);
        Assert.AreEqual("Brewing", page.Content.Title);
        Assert.AreEqual("steep", ((TextDocumentSource)page.Content.Source).Data);
        Assert.AreEqual(DocumentMediaType.PlainText, ((TextDocumentSource)page.Content.Source).MediaType);
        Assert.AreEqual(new DateTimeOffset(2026, 9, 18, 10, 11, 12, TimeSpan.Zero), page.RetrievedAt);

        var browser = ClaudeAgentModelFactory.BrowserStateBlock(
            [ClaudeAgentModelFactory.BrowserTab("tab_01", "Brewing", "https://example.com/brew", active: true)],
            [ClaudeAgentModelFactory.DownloadCompletedBrowserStateChange("dl_01", "https://example.com/notes.pdf", "/home/user/notes.pdf", 4096)]);

        Assert.AreEqual("tab_01", browser.Tabs.Single().TabId);
        var completed = (DownloadCompletedBrowserStateChange)browser.StateChanges.Single();
        Assert.AreEqual("/home/user/notes.pdf", completed.Path);
        Assert.AreEqual(4096, completed.SizeBytes);
    }

    private static bool IsList(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>);

    private static string Capitalised(string name) => char.ToUpperInvariant(name[0]) + name[1..];

    // The family a caller reaches from the base: the base itself, every type of the library deriving
    // from it, and every class of the library a property carries, the same way down. ToolInput and
    // ToolOutput are where it stops: the typed views behind them are another family.
    private static HashSet<Type> FamilyOf(Type root)
    {
        var types = Library.GetTypes();
        var found = new HashSet<Type>();
        var pending = new Queue<Type>([root]);
        while (pending.TryDequeue(out var type))
        {
            if (type == typeof(ToolInput) || type == typeof(ToolOutput) || !found.Add(type))
            {
                continue;
            }

            foreach (var derived in types.Where(candidate => candidate.IsSubclassOf(type)))
            {
                pending.Enqueue(derived);
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var carried in Carried(property.PropertyType).Where(carried => carried.IsClass && !carried.IsArray && carried.Assembly == Library))
                {
                    pending.Enqueue(carried);
                }
            }
        }

        return found;
    }

    private static IEnumerable<Type> Carried(Type type)
    {
        yield return type;
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments().SelectMany(Carried))
            {
                yield return argument;
            }
        }
    }
}
