using System.Reflection;
using System.Text.RegularExpressions;

namespace Claude.Agent.Tests.Architecture;

// Every exported struct of the library is a closed set in one shape, held here by reflection: a
// struct a later lane adds is held without a line being added below. The shape is the page's
// closed-sets row — a readonly struct over the string, a static member for each value the library
// names, equal by the string, open to a value it has not named.
[TestClass]
public sealed partial class ClosedSetTests
{
    // A string no set names, used to hold that an unnamed value survives whole.
    private const string Unnamed = "a-value-no-set-of-this-library-names";

    [TestMethod]
    public void TheAssemblyExportsTheClosedSetsAndNoOtherStruct()
    {
        Assert.IsTrue(ClosedSets.Structs.Length >= 121, $"{ClosedSets.Structs.Length} closed sets found: the scan has lost them");
        CollectionAssert.IsSubsetOf(
            new[] { typeof(PermissionMode), typeof(HookEvent), typeof(Effort), typeof(ResultMessageSubtype), typeof(StopReason) },
            ClosedSets.Structs);
    }

    [TestMethod]
    public void EveryClosedSetIsAReadonlyStructEquatableToItself()
    {
        foreach (var type in ClosedSets.Structs)
        {
            Assert.IsTrue(type.IsValueType && !type.IsEnum, $"{type.Name} is not a struct");
            Assert.IsTrue(
                type.GetCustomAttributes().Any(attribute => attribute.GetType().Name == "IsReadOnlyAttribute"),
                $"{type.Name} is not readonly");
            Assert.IsTrue(
                typeof(IEquatable<>).MakeGenericType(type).IsAssignableFrom(type),
                $"{type.Name} is not IEquatable of itself");
            Assert.AreEqual(
                0,
                type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Length,
                $"{type.Name} has a public field");
        }
    }

    [TestMethod]
    public void EveryClosedSetIsMadeFromAStringAndRefusesNull()
    {
        foreach (var type in ClosedSets.Structs)
        {
            var constructor = type.GetConstructor([typeof(string)]);
            Assert.IsNotNull(constructor, $"{type.Name} has no public constructor from a string");
            Assert.IsTrue(constructor.IsPublic, $"{type.Name}'s constructor from a string is not public");

            var refused = Assert.ThrowsExactly<TargetInvocationException>(
                () => constructor.Invoke([null]),
                $"{type.Name} accepted a null value");
            Assert.IsInstanceOfType<ArgumentNullException>(refused.InnerException, $"{type.Name} refused null with the wrong exception");
        }
    }

    [TestMethod]
    public void EveryClosedSetTakesAStringImplicitlyAndGivesItBackFromToString()
    {
        foreach (var type in ClosedSets.Structs)
        {
            var conversion = type.GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
            Assert.IsNotNull(conversion, $"{type.Name} has no implicit conversion from a string");
            Assert.AreEqual(type, conversion.ReturnType, $"{type.Name}'s implicit conversion does not return it");

            var converted = conversion.Invoke(null, [Unnamed])!;
            Assert.AreEqual(Unnamed, converted.ToString(), $"{type.Name} did not give back the string it was made from");
            Assert.AreEqual(ClosedSets.Make(type, Unnamed), converted, $"{type.Name}'s conversion and constructor differ");
        }
    }

    [TestMethod]
    public void EveryClosedSetIsEqualByItsStringOrdinallyThroughTheOperatorsToo()
    {
        foreach (var type in ClosedSets.Structs)
        {
            var same = ClosedSets.Make(type, Unnamed);
            var again = ClosedSets.Make(type, Unnamed);
            var other = ClosedSets.Make(type, Unnamed.ToUpperInvariant());

            Assert.AreEqual(same, again, $"{type.Name} is not equal to the same name");
            Assert.AreEqual(same.GetHashCode(), again.GetHashCode(), $"{type.Name} hashes the same name two ways");
            Assert.AreNotEqual(same, other, $"{type.Name} is equal to a name of another case: it is not ordinal");

            Assert.IsTrue(ClosedSets.Operator(type, "op_Equality", same, again), $"{type.Name} == said the same name differs");
            Assert.IsFalse(ClosedSets.Operator(type, "op_Inequality", same, again), $"{type.Name} != said the same name differs");
            Assert.IsFalse(ClosedSets.Operator(type, "op_Equality", same, other), $"{type.Name} == ignored case");
            Assert.IsTrue(ClosedSets.Operator(type, "op_Inequality", same, other), $"{type.Name} != ignored case");
        }
    }

    // The sets that name no value yet, each with its reason: the declaration leaves the set open and
    // no recording carries a value of it, so there is nothing to name. A set not listed here names at
    // least one value; a listed one names none, and leaves the list when a recording founds a value,
    // which it then names.
    private static readonly Dictionary<Type, string> NamingNothingYet = new()
    {
        [typeof(NotificationType)] = "the Notification hook is registered in hooks-more and never fired, so no recording carries a notification_type",
    };

    [TestMethod]
    public void EverySetThatNamesNothingYetIsAClosedSetWithAReasonAndStillNamesNothing()
    {
        foreach (var (type, reason) in NamingNothingYet)
        {
            CollectionAssert.Contains(ClosedSets.Structs, type, $"{type.Name} is listed as naming nothing yet and is not a closed set");
            Assert.IsFalse(string.IsNullOrWhiteSpace(reason), $"{type.Name} is listed as naming nothing yet for no reason");
            Assert.AreEqual(0, ClosedSets.NamedProperties(type).Length, $"{type.Name} names a value now: its entry goes");
        }
    }

    [TestMethod]
    public void EveryNamedValueOfEveryClosedSetIsAStaticGetOnlyPropertyOfItsOwnType()
    {
        foreach (var type in ClosedSets.Structs)
        {
            var named = ClosedSets.NamedProperties(type);
            if (!NamingNothingYet.ContainsKey(type))
            {
                Assert.AreNotEqual(0, named.Length, $"{type.Name} names no value and is not listed as naming nothing yet");
            }

            foreach (var property in named)
            {
                Assert.IsNull(property.GetSetMethod(nonPublic: true), $"{type.Name}.{property.Name} is settable");
                Assert.AreNotEqual(
                    string.Empty,
                    property.GetValue(null)!.ToString(),
                    $"{type.Name}.{property.Name} names nothing");
            }
        }
    }

    [TestMethod]
    public void EveryClosedSetSaysItNamesItsOwnValuesAndNoOther()
    {
        foreach (var type in ClosedSets.Structs)
        {
            foreach (var (name, value) in ClosedSets.Named(type))
            {
                Assert.IsTrue(ClosedSets.IsDefined(value), $"{type.Name}.{name} is not one the type says it names");
            }

            Assert.IsFalse(ClosedSets.IsDefined(ClosedSets.Make(type, Unnamed)), $"{type.Name} says it names {Unnamed}");
        }
    }

    // The other direction of the same claim, which reflection cannot reach: what a set says it names
    // is an expression over the string, not a member, so a set could accept a value it exposes no
    // member for and every check above would still pass. The recorded-fixture walk asks IsDefined and
    // nothing else, so an over-claiming set would let a recorded value the library has no member for
    // read as named. Held by reading the declaration.
    [TestMethod]
    public void EveryClosedSetAcceptsInIsDefinedExactlyTheValuesItNames()
    {
        foreach (var type in ClosedSets.Structs)
        {
            var matched = IsDefinedBody().Match(ClosedSets.Source(type));
            Assert.IsTrue(matched.Success, $"{type.Name} has no IsDefined written as an expression over the string");
            var accepted = Literal().Matches(matched.Groups[1].Value).Select(found => found.Groups[1].Value).ToArray();
            var names = ClosedSets.Named(type).Select(named => named.Value.ToString()!).ToArray();
            var over = accepted.Except(names, StringComparer.Ordinal).ToArray();
            var under = names.Except(accepted, StringComparer.Ordinal).ToArray();
            Assert.AreEqual(0, over.Length, $"{type.Name} says it names {string.Join(", ", over)}, which it has no member for");
            Assert.AreEqual(0, under.Length, $"{type.Name} has a member for {string.Join(", ", under)}, which it does not say it names");
            Assert.AreEqual(names.Length, accepted.Length, $"{type.Name} says it names {accepted.Length} values and has {names.Length} members: one is written twice");
        }
    }

    [GeneratedRegex(@"bool IsDefined\s*=>\s*([^;]*);")]
    private static partial Regex IsDefinedBody();

    [GeneratedRegex("\"([^\"]*)\"")]
    private static partial Regex Literal();

    // The default: what a member never set carries. It prints empty, is no value the set names, and
    // is not the empty string either, which is a name the binary could have written.
    [TestMethod]
    public void TheDefaultOfEveryClosedSetPrintsEmptyAndIsNoValueItNames()
    {
        foreach (var type in ClosedSets.Structs)
        {
            var blank = Activator.CreateInstance(type)!;
            Assert.AreEqual(string.Empty, blank.ToString(), $"the default {type.Name} does not print empty");
            Assert.IsFalse(ClosedSets.IsDefined(blank), $"the default {type.Name} is a value it names");
            foreach (var (name, value) in ClosedSets.Named(type))
            {
                Assert.AreNotEqual(value, blank, $"the default {type.Name} equals {name}");
            }

            Assert.AreNotEqual(ClosedSets.Make(type, string.Empty), blank, $"the default {type.Name} equals the empty name");
        }
    }
}

// The closed sets of the library, found by reflection: every exported struct, and the values each
// names. Three tests read them — the shape here, the founding in TreeTests, the recorded fixtures
// in RecordedFixtureTests.
internal static class ClosedSets
{
    public static Type[] Structs { get; } =
        [.. typeof(ClaudeAgentClient).Assembly.GetExportedTypes()
            .Where(type => type.IsValueType && !type.IsEnum)
            .OrderBy(type => type.Name, StringComparer.Ordinal)];

    private static readonly HashSet<Type> Set = [.. Structs];

    private static readonly string[] Sources =
        [.. Directory.EnumerateFiles(Path.Combine(TreeTests.Root, "Claude.Agent"), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)];

    public static bool Is(Type type) => Set.Contains(type);

    // The declaration of a set, wherever in the library it is written, so a set a later lane adds is
    // read here without a line being added.
    public static string Source(Type type) =>
        Sources.FirstOrDefault(text => text.Contains($"readonly struct {type.Name} ", StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"no file of Claude.Agent declares readonly struct {type.Name}");

    public static object Make(Type type, string value) => Activator.CreateInstance(type, [value])!;

    public static PropertyInfo[] NamedProperties(Type type) =>
        [.. type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(property => property.PropertyType == type)
            .OrderBy(property => property.Name, StringComparer.Ordinal)];

    public static IEnumerable<(string Name, object Value)> Named(Type type) =>
        NamedProperties(type).Select(property => (property.Name, property.GetValue(null)!));

    public static bool IsDefined(object value) =>
        (bool)value.GetType().GetProperty(nameof(PermissionMode.IsDefined), BindingFlags.Public | BindingFlags.Instance)!
            .GetValue(value)!;

    public static bool Operator(Type type, string name, object left, object right) =>
        (bool)type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, [type, type])!.Invoke(null, [left, right])!;
}
