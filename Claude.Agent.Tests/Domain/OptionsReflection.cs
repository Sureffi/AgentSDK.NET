using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace Claude.Agent.Tests.Domain;

// The options, by reflection: every public property given a value a copy could lose, and a second
// value of every kind so a call can differ from a default. A property of a type this does not know
// fails the test that met it, so a member added to the options is a member these tests cover.
internal static class OptionsReflection
{
    public static PropertyInfo[] Properties(Type options) =>
        [.. options
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(property => property.Name, StringComparer.Ordinal)];

    public static void Distinguish(object options, PropertyInfo property, bool other = false)
    {
        if (property.CanWrite)
        {
            property.SetValue(options, Distinct(property, other));
            return;
        }

        switch (property.GetValue(options))
        {
            case IList<string> strings:
                strings.Add(other ? $"{property.Name}-other" : $"{property.Name}-entry");
                break;

            case IDictionary<string, string> map:
                map["key"] = other ? "other" : property.Name;
                break;

            case IDictionary<string, McpServerConfiguration> servers:
                servers[other ? "hive" : "mint"] = McpServerConfiguration.Stdio(other ? "hive-server" : "mint-server");
                break;

            case IDictionary<string, AgentDefinition> agents:
                agents[other ? "wasp" : "bee"] = Filled(new AgentDefinition("a bee", "buzz"));
                break;

            case ClaudeAgentSessionOptions defaults:
                foreach (var asked in Properties(typeof(ClaudeAgentSessionOptions)))
                {
                    Distinguish(defaults, asked, other);
                }

                break;

            default:
                throw new AssertFailedException(
                    $"{property.Name} is a get-only property this test does not know how to fill");
        }
    }

    public static object Distinct(PropertyInfo property, bool other = false)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (type == typeof(string))
        {
            return other ? $"{property.Name}-other" : $"{property.Name}-value";
        }

        if (type == typeof(bool))
        {
            return !other;
        }

        if (type == typeof(int))
        {
            return other ? 8 : 7;
        }

        if (type == typeof(decimal))
        {
            return other ? 4.5m : 3.5m;
        }

        if (type == typeof(TimeSpan))
        {
            return TimeSpan.FromSeconds(other ? 10 : 9);
        }

        if (type == typeof(PermissionMode))
        {
            return other ? PermissionMode.AcceptEdits : PermissionMode.Plan;
        }

        if (type == typeof(Effort))
        {
            return other ? Effort.Low : Effort.High;
        }

        if (type == typeof(AgentMemory))
        {
            return other ? AgentMemory.User : AgentMemory.Project;
        }

        if (type == typeof(AgentSystemPrompt))
        {
            return AgentSystemPrompt.FromText(other ? "be long" : "be brief");
        }

        if (type == typeof(ThinkingOptions))
        {
            return ThinkingOptions.WithBudget(other ? 4096 : 2048);
        }

        if (type == typeof(SandboxOptions))
        {
            return Filled(new SandboxOptions(), other);
        }

        if (type == typeof(JsonElement))
        {
            return JsonSerializer.Deserialize<JsonElement>(other ? """{"type":"string"}""" : """{"type":"object"}""");
        }

        if (type == typeof(IList<string>))
        {
            return other ? new List<string>() : new List<string> { "Read" };
        }

        if (type == typeof(IList<SettingSource>))
        {
            return other ? new List<SettingSource>() : new List<SettingSource> { SettingSource.Project };
        }

        if (type == typeof(TextWriter))
        {
            return new StringWriter();
        }

        if (type == typeof(Func<ToolPermissionRequest, CancellationToken, Task<ToolPermissionResult>>))
        {
            // Two lambdas, two methods: a delegate compares by target and method, so one expression
            // evaluated twice would be equal to itself whichever side the layering took.
            return other
                ? new Func<ToolPermissionRequest, CancellationToken, Task<ToolPermissionResult>>(
                    (_, _) => Task.FromResult(ToolPermissionResult.Deny("other")))
                : new Func<ToolPermissionRequest, CancellationToken, Task<ToolPermissionResult>>(
                    (_, _) => Task.FromResult(ToolPermissionResult.Allow()));
        }

        throw new AssertFailedException(
            $"{property.Name} has type {property.PropertyType} which this test cannot distinguish");
    }

    // Every settable member of one of the two mutable option objects, set to something Clone could lose.
    public static T Filled<T>(T target, bool other = false)
        where T : class
    {
        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.CanWrite))
        {
            property.SetValue(target, Distinct(property, other));
        }

        return target;
    }

    public static void AssertCarried(object? original, object? cloned, string name)
    {
        switch (original)
        {
            case JsonElement element:
                Assert.AreEqual(element.GetRawText(), ((JsonElement?)cloned)?.GetRawText(), $"{name} did not survive Clone");
                return;

            case SandboxOptions or AgentDefinition or ClaudeAgentSessionOptions:
                Assert.IsNotNull(cloned, $"{name} did not survive Clone");
                Assert.AreNotSame(original, cloned, $"{name} is shared with the clone");
                foreach (var property in original.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    AssertCarried(property.GetValue(original), property.GetValue(cloned), $"{name}.{property.Name}");
                }

                return;

            case IDictionary<string, AgentDefinition> agents:
                var copies = (IDictionary<string, AgentDefinition>)cloned!;
                CollectionAssert.AreEqual(agents.Keys.ToArray(), copies.Keys.ToArray(), $"{name} did not survive Clone");
                foreach (var (key, agent) in agents)
                {
                    AssertCarried(agent, copies[key], $"{name}[{key}]");
                }

                return;

            case IEnumerable source and not string:
                CollectionAssert.AreEqual(
                    source.Cast<object>().ToArray(),
                    ((IEnumerable?)cloned)?.Cast<object>().ToArray(),
                    $"{name} did not survive Clone");
                return;

            default:
                Assert.AreEqual(original, cloned, $"{name} did not survive Clone");
                return;
        }
    }
}
