using System.Collections;
using System.Reflection;
using System.Text.Json;
using Claude.Agent.Tests.Architecture;
using Claude.Agent.Tests.Wire;

namespace Claude.Agent.Tests.Tools;

// Every tool model a caller reads is a model a caller can also build, through the factory, one
// builder a type, each parameter the property of its name. One case a type: the builder is called
// with nothing, and then with a value for every parameter, and each property comes back carrying
// what went in. A type added to the tool surface without a builder, or with a parameter that lands
// on no property, fails here.
[TestClass]
public sealed class ToolFactoryTests
{
    private static readonly Assembly Library = typeof(ClaudeAgentClient).Assembly;

    [TestMethod]
    [DynamicData(nameof(ToolModels), DynamicDataSourceType.Method)]
    public void TheFactoryBuildsTheToolModelAndEveryParameterLandsOnThePropertyOfItsName(string name)
    {
        var type = Library.GetType($"Claude.Agent.{name}", throwOnError: true)!;
        var builder = Builder(type);
        Assert.IsNotNull(builder, $"the factory has no builder for {name}");

        var bare = builder.Invoke(null, [.. builder.GetParameters().Select(parameter => parameter.DefaultValue)]);
        Assert.IsInstanceOfType(bare, type, name);

        // A bool has two values and a closed set often few, so one pass cannot give every position
        // its own value. Each pass reads one bit of the position instead, and two positions differ
        // in at least one bit, so a builder that crosses any two parameters fails in some pass.
        for (var pass = 0; pass < Passes; pass++)
        {
            Placed(type, builder, name, pass);
        }
    }

    private const int Passes = 6;

    private static void Placed(Type type, MethodInfo builder, string name, int pass)
    {
        var handed = builder.GetParameters().Select((parameter, index) => Value(parameter.ParameterType, parameter.Name!, index, pass)).ToArray();
        var built = builder.Invoke(null, handed);
        Assert.IsInstanceOfType(built, type, name);

        foreach (var (parameter, value) in builder.GetParameters().Zip(handed))
        {
            var property = type.GetProperty(char.ToUpperInvariant(parameter.Name![0]) + parameter.Name[1..], BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(property, $"{name}: the builder's {parameter.Name} lands on no property");

            var read = property.GetValue(built);
            if (value is JsonElement json)
            {
                Assert.IsTrue(JsonElement.DeepEquals(json, (JsonElement)read!), $"{name}.{property.Name}");
            }
            else if (value is IEnumerable items and not string)
            {
                CollectionAssert.AreEqual(
                    items.Cast<object?>().ToArray(),
                    ((IEnumerable)read!).Cast<object?>().ToArray(),
                    $"{name}.{property.Name}");
            }
            else
            {
                Assert.AreEqual(value, read, $"{name}.{property.Name}");
            }
        }
    }

    // What a caller builds is what the library writes: a tree built through the factory alone goes
    // out as the wire's JSON and comes back as the same members.
    [TestMethod]
    public void AToolOutputBuiltThroughTheFactoryAloneWritesAndReadsBack()
    {
        var built = ClaudeAgentModelFactory.BashToolOutput(
            standardOutput: "2 notes.txt",
            standardError: string.Empty,
            interrupted: false,
            gitOperation: ClaudeAgentModelFactory.BashGitOperation(
                commit: ClaudeAgentModelFactory.BashGitCommit("0f1e2d3", GitCommitAction.Committed, "main"),
                push: ClaudeAgentModelFactory.BashGitPush("main")));

        var read = (BashToolOutput)ToolTypes.ReadOutput("Bash", ToolTypes.WriteOutput(built))!;

        Assert.AreEqual("2 notes.txt", read.StandardOutput);
        Assert.AreEqual("0f1e2d3", read.GitOperation?.Commit?.Sha);
        Assert.AreEqual(GitCommitAction.Committed, read.GitOperation?.Commit?.Kind);
        Assert.AreEqual("main", read.GitOperation?.Push?.Branch);
        Assert.IsNull(read.GitOperation?.Branch);
        Assert.IsNull(read.GitOperation?.PullRequest);
    }

    // The tool models: every input and output this library types, and every shape they carry.
    public static IEnumerable<object[]> ToolModels() =>
        Reached().Select(type => new object[] { type.Name });

    private static SortedDictionary<string, Type>.ValueCollection Reached()
    {
        var found = new SortedDictionary<string, Type>(StringComparer.Ordinal);
        var pending = new Queue<Type>(Library.GetExportedTypes()
            .Where(type => type.IsClass && (type.IsSubclassOf(typeof(ToolInput)) || type.IsSubclassOf(typeof(ToolOutput)))));

        while (pending.Count != 0)
        {
            var type = pending.Dequeue();
            if (type.IsAbstract || !found.TryAdd(type.Name, type))
            {
                continue;
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var carried = Element(property.PropertyType);
                if (carried.IsClass && carried.Assembly == Library && carried != typeof(string))
                {
                    foreach (var one in carried.IsAbstract ? Concrete(carried) : [carried])
                    {
                        pending.Enqueue(one);
                    }
                }
            }
        }

        return found.Values;
    }

    private static IEnumerable<Type> Concrete(Type type) =>
        Library.GetExportedTypes().Where(other => other.IsClass && !other.IsAbstract && other.IsSubclassOf(type));

    // What a member carries: the item of a list, the value under a nullable, or the type itself.
    private static Type Element(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } value)
        {
            return value;
        }

        if (type != typeof(string) && type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
        {
            return type.GetGenericArguments()[0];
        }

        return type;
    }

    private static MethodInfo? Builder(Type type) =>
        typeof(ClaudeAgentModelFactory)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method => method.Name == type.Name && method.ReturnType == type);

    // A value for a builder's parameter, one a property can be checked against: distinct per
    // position, so a builder that crosses two parameters of the same type fails. Where the type has
    // too few values for that, <paramref name="pass"/> picks a different bit of the position, and
    // the caller runs every pass.
    private static object Value(Type type, string name, int index, int pass)
    {
        var bare = Nullable.GetUnderlyingType(type) ?? type;

        if (bare == typeof(string))
        {
            return $"the {name}";
        }

        if (bare == typeof(long))
        {
            return (long)(index + 1);
        }

        if (bare == typeof(bool))
        {
            return ((index >> pass) & 1) == 0;
        }

        if (bare == typeof(TimeSpan))
        {
            return TimeSpan.FromMilliseconds(index + 1);
        }

        if (bare == typeof(double))
        {
            return (double)(index + 1);
        }

        if (bare == typeof(DateTimeOffset))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(index + 1);
        }

        if (bare == typeof(JsonElement))
        {
            return Recorded.Json($$"""{"{{name}}": {{index + 1}}}""");
        }

        if (ClosedSets.Is(bare))
        {
            var named = ClosedSets.Named(bare).ToArray();
            return named[(index >> pass) % named.Length].Value;
        }

        if (bare != typeof(string) && bare.IsGenericType && typeof(IEnumerable).IsAssignableFrom(bare))
        {
            var item = bare.GetGenericArguments()[0];
            var one = Array.CreateInstance(item, 1);
            one.SetValue(Value(item, name, index, pass), 0);
            return one;
        }

        if (bare.IsClass && bare.Assembly == Library)
        {
            var carried = bare.IsAbstract ? Concrete(bare).First() : bare;
            var builder = Builder(carried);
            Assert.IsNotNull(builder, $"the factory has no builder for {carried.Name}, carried under {name}");
            return builder.Invoke(null, [.. builder.GetParameters().Select(parameter => parameter.DefaultValue)])!;
        }

        throw new NotSupportedException($"No value of {bare.Name} is made for the builder's {name}.");
    }
}
