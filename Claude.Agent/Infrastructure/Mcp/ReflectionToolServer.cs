using System.Collections.Frozen;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Claude.Agent;

internal sealed class ReflectionToolServer : IToolServer
{
    private const string PathSuffix = ". Path:";

    private static readonly JsonSerializerOptions Binding = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _instance;
    private readonly FrozenDictionary<string, Tool> _tools;

    [RequiresUnreferencedCode("Tool discovery reflects over the instance's methods and their parameter types.")]
    public ReflectionToolServer(object instance, string name, string version)
        : this(instance, version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    // Unnamed until it is registered: the name is the key it sits under in McpServers.
    [RequiresUnreferencedCode("Tool discovery reflects over the instance's methods and their parameter types.")]
    public ReflectionToolServer(object instance, string version)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        _instance = instance;
        Name = string.Empty;
        Version = version;

        var nullability = new NullabilityInfoContext();
        var discovered = instance.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Select(candidate => (Method: candidate, Attribute: candidate.GetCustomAttribute<McpToolAttribute>()))
            .Where(candidate => candidate.Attribute is not null)
            .Select(candidate => Describe(candidate.Method, candidate.Attribute!, nullability))
            .OrderBy(tool => tool.Definition.Name, StringComparer.Ordinal)
            .ToArray();

        var named = new Dictionary<string, Tool>(StringComparer.Ordinal);
        foreach (var tool in discovered)
        {
            if (!named.TryAdd(tool.Definition.Name, tool))
            {
                throw new InvalidOperationException(
                    $"Two [McpTool] methods are named '{tool.Definition.Name}': "
                    + $"{NameOf(named[tool.Definition.Name].Method)} and {NameOf(tool.Method)}.");
            }
        }

        _tools = named.ToFrozenDictionary(StringComparer.Ordinal);
        Tools = [.. discovered.Select(tool => tool.Definition)];
    }

    private ReflectionToolServer(ReflectionToolServer source, string name)
    {
        _instance = source._instance;
        _tools = source._tools;
        Tools = source.Tools;
        Version = source.Version;
        Name = name;
    }

    private enum Completion
    {
        Value,
        Task,
        TaskOfValue,
        ValueTask,
        ValueTaskOfValue,
    }

    public string Name { get; }

    public string Version { get; }

    public ReflectionToolServer Named(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new ReflectionToolServer(this, name);
    }

    public IReadOnlyList<ToolDefinition> Tools { get; }

    public async ValueTask<ToolResult> CallAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!_tools.TryGetValue(name, out var tool))
        {
            return ToolResult.FromError($"This server has no tool named '{name}'.");
        }

        var values = new object?[tool.Parameters.Length];
        for (var index = 0; index < tool.Parameters.Length; index++)
        {
            var parameter = tool.Parameters[index];
            if (parameter.IsCancellation)
            {
                values[index] = cancellationToken;
                continue;
            }

            var bound = Bind(parameter, arguments);
            if (bound.Error is { } invalid)
            {
                return ToolResult.FromError($"Input validation error: {invalid}");
            }

            values[index] = bound.Value;
        }

        try
        {
            var target = tool.Method.IsStatic ? null : _instance;
            return await CompleteAsync(tool, tool.Method.Invoke(target, values)).ConfigureAwait(false);
        }
        catch (TargetInvocationException failure)
        {
            return ToolResult.FromError(Describe(failure.InnerException ?? failure));
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return ToolResult.FromError(Describe(failure));
        }
    }

    [RequiresUnreferencedCode("Tool discovery reflects over the instance's methods and their parameter types.")]
    private static Tool Describe(MethodInfo method, McpToolAttribute attribute, NullabilityInfoContext nullability)
    {
        var parameters = method.GetParameters()
            .Select(parameter => Describe(parameter, nullability))
            .ToImmutableArray();

        var schema = JsonRpc.Build(writer =>
        {
            writer.WriteStartObject();
            writer.WritePropertyName("properties");
            writer.WriteStartObject();
            foreach (var parameter in parameters.Where(parameter => !parameter.IsCancellation))
            {
                writer.WritePropertyName(parameter.Name);
                ToolSchema.Write(writer, parameter.Type, parameter.Description, 1, nullability);
            }

            writer.WriteEndObject();

            var required = parameters
                .Where(parameter => parameter is { IsCancellation: false, Required: true })
                .Select(parameter => parameter.Name)
                .ToArray();

            if (required.Length > 0)
            {
                writer.WritePropertyName("required");
                writer.WriteStartArray();
                foreach (var name in required)
                {
                    writer.WriteStringValue(name);
                }

                writer.WriteEndArray();
            }

            writer.WriteString("type", "object");
            writer.WriteEndObject();
        });

        var advertised = schema.GetProperty("properties");
        var described = parameters
            .Select(parameter => parameter.IsCancellation
                ? parameter
                : parameter with { Schema = advertised.GetProperty(parameter.Name) })
            .ToImmutableArray();

        var definition = new ToolDefinition(
            attribute.Name,
            attribute.Description,
            schema,
            attribute.Annotations,
            attribute.MaxResultSizeChars > 0 ? attribute.MaxResultSizeChars : null);

        return new Tool(method, described, definition, CompletionOf(method.ReturnType));
    }

    private static Parameter Describe(ParameterInfo parameter, NullabilityInfoContext nullability)
    {
        var type = parameter.ParameterType;
        var acceptsNull = ToolSchema.IsOptional(nullability.Create(parameter), type);

        return new Parameter(
            parameter.Name ?? string.Empty,
            type,
            parameter.GetCustomAttribute<DescriptionAttribute>()?.Description,
            type == typeof(CancellationToken),
            !(parameter.HasDefaultValue || acceptsNull),
            acceptsNull,
            parameter.HasDefaultValue ? parameter.DefaultValue : null);
    }

    private static string NameOf(MethodInfo method) =>
        method.DeclaringType is { } declaring ? $"{declaring.Name}.{method.Name}" : method.Name;

    private static Completion CompletionOf(Type type)
    {
        if (type == typeof(Task))
        {
            return Completion.Task;
        }

        if (type == typeof(ValueTask))
        {
            return Completion.ValueTask;
        }

        if (!type.IsGenericType)
        {
            return Completion.Value;
        }

        var definition = type.GetGenericTypeDefinition();
        if (definition == typeof(Task<>))
        {
            return Completion.TaskOfValue;
        }

        return definition == typeof(ValueTask<>) ? Completion.ValueTaskOfValue : Completion.Value;
    }

    private static string Describe(Exception failure) =>
        failure.Message is { Length: > 0 } message ? message : failure.GetType().Name;

    [RequiresUnreferencedCode("Awaiting a tool's result reads the task's Result property by reflection.")]
    private static async ValueTask<ToolResult> CompleteAsync(Tool tool, object? returned)
    {
        switch (tool.Completion)
        {
            case Completion.Task:
                await ((Task)returned!).ConfigureAwait(false);
                return Content(null);
            case Completion.ValueTask:
                await ((ValueTask)returned!).ConfigureAwait(false);
                return Content(null);
            case Completion.TaskOfValue:
            {
                var task = (Task)returned!;
                await task.ConfigureAwait(false);
                return Content(task.GetType().GetProperty("Result")?.GetValue(task));
            }

            case Completion.ValueTaskOfValue:
            {
                var task = (Task)returned!.GetType().GetMethod("AsTask")!.Invoke(returned, null)!;
                await task.ConfigureAwait(false);
                return Content(task.GetType().GetProperty("Result")?.GetValue(task));
            }

            case Completion.Value:
            default:
                return Content(returned);
        }
    }

    private static ToolResult Content(object? value) => value switch
    {
        null => ToolResult.Empty,
        ToolResult result => result,
        string text => ToolResult.FromText(text),
        _ => ToolResult.FromText(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty),
    };

    private static JsonElement? Argument(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (arguments.TryGetProperty(name, out var exact))
        {
            return exact;
        }

        foreach (var property in arguments.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    [RequiresUnreferencedCode("Binding a nested tool argument deserializes it into its declared type.")]
    private static (object? Value, string? Error) Bind(Parameter parameter, JsonElement arguments)
    {
        if (Argument(arguments, parameter.Name) is not { } value)
        {
            return parameter.Required
                ? (null, $"'{parameter.Name}' is a required property")
                : (parameter.Default, null);
        }

        var expected = ToolSchema.JsonTypeName(parameter.Type);
        if (value.ValueKind == JsonValueKind.Null)
        {
            return parameter.AcceptsNull ? (null, null) : (null, Mismatch(parameter, expected));
        }

        var type = ToolSchema.Unwrap(parameter.Type);

        if (type == typeof(string))
        {
            return value.ValueKind == JsonValueKind.String
                ? (value.GetString(), null)
                : (null, Mismatch(parameter, expected));
        }

        if (type == typeof(bool))
        {
            return value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? (value.GetBoolean(), null)
                : (null, Mismatch(parameter, expected));
        }

        if (type.IsEnum)
        {
            if (value.ValueKind != JsonValueKind.String)
            {
                return (null, Mismatch(parameter, expected));
            }

            var name = value.GetString() ?? string.Empty;
            return Enum.TryParse(type, name, out var member) || Enum.TryParse(type, name, ignoreCase: true, out member)
                ? (member, null)
                : (null, NotOneOf(name, Enum.GetNames(type)));
        }

        if (value.ValueKind == JsonValueKind.Number && (ToolSchema.IsInteger(type) || ToolSchema.IsNumber(type)))
        {
            return Number(parameter, value, type, expected);
        }

        if (expected is "array" && value.ValueKind != JsonValueKind.Array)
        {
            return (null, Mismatch(parameter, expected));
        }

        if (expected is "object" && value.ValueKind != JsonValueKind.Object)
        {
            return (null, Mismatch(parameter, expected));
        }

        if (expected is "integer" or "number")
        {
            return (null, Mismatch(parameter, expected));
        }

        if (Validate(parameter.Name, parameter.Schema, value) is { } invalid)
        {
            return (null, invalid);
        }

        try
        {
            return (value.Deserialize(parameter.Type, Binding), null);
        }
        catch (JsonException failure)
        {
            return (null, $"'{parameter.Name}' is invalid: {Truncate(failure.Message)}");
        }
        catch (NotSupportedException failure)
        {
            return (null, $"'{parameter.Name}' is invalid: {Truncate(failure.Message)}");
        }
    }

    private static string? Validate(string name, JsonElement schema, JsonElement value)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (schema.TryGetProperty("enum", out var members))
        {
            var names = members.EnumerateArray().Select(member => member.GetString() ?? string.Empty).ToArray();
            return value.ValueKind == JsonValueKind.String && names.Contains(value.GetString(), StringComparer.Ordinal)
                ? null
                : NotOneOf(Rendered(value), names);
        }

        return Kind(schema) switch
        {
            "object" => ValidateObject(name, schema, value),
            "array" => ValidateArray(name, schema, value),
            { } kind => Matches(kind, value) ? null : NotOfType(name, kind),
            null => null,
        };
    }

    private static string? ValidateObject(string name, JsonElement schema, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return NotOfType(name, "object");
        }

        HashSet<string> required = schema.TryGetProperty("required", out var listed)
            ? listed.EnumerateArray().Select(one => one.GetString() ?? string.Empty).ToHashSet(StringComparer.Ordinal)
            : [];

        foreach (var property in required)
        {
            if (Argument(value, property) is null)
            {
                return $"'{property}' is a required property";
            }
        }

        if (!schema.TryGetProperty("properties", out var properties))
        {
            return null;
        }

        foreach (var property in properties.EnumerateObject())
        {
            if (Argument(value, property.Name) is not { } given)
            {
                continue;
            }

            if (given.ValueKind == JsonValueKind.Null)
            {
                if (!required.Contains(property.Name))
                {
                    continue;
                }

                return Kind(property.Value) is { } kind ? NotOfType(property.Name, kind) : null;
            }

            if (Validate(property.Name, property.Value, given) is { } invalid)
            {
                return invalid;
            }
        }

        return null;
    }

    private static string? ValidateArray(string name, JsonElement schema, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return NotOfType(name, "array");
        }

        if (!schema.TryGetProperty("items", out var items) || Kind(items) is not "object")
        {
            return null;
        }

        foreach (var item in value.EnumerateArray())
        {
            if (Validate(name, items, item) is { } invalid)
            {
                return invalid;
            }
        }

        return null;
    }

    private static string? Kind(JsonElement schema) =>
        schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("type", out var kind)
        && kind.ValueKind == JsonValueKind.String
            ? kind.GetString()
            : null;

    private static bool Matches(string kind, JsonElement value) => kind switch
    {
        "string" => value.ValueKind == JsonValueKind.String,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        "number" => value.ValueKind == JsonValueKind.Number,
        _ => true,
    };

    private static string Rendered(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();

    private static string NotOneOf(string value, IEnumerable<string> members) =>
        $"'{value}' is not one of {string.Join(", ", members.Select(one => $"'{one}'"))}";

    private static string NotOfType(string name, string kind) => $"'{name}' is not of type '{kind}'";

    private static string Truncate(string message)
    {
        var cut = message.IndexOf(PathSuffix, StringComparison.Ordinal);
        return cut > 0 ? message[..cut] : message;
    }

    private static (object? Value, string? Error) Number(
        Parameter parameter,
        JsonElement value,
        Type type,
        string expected)
    {
        if (ToolSchema.IsInteger(type) && !value.TryGetInt64(out _))
        {
            return (null, Mismatch(parameter, expected));
        }

        try
        {
            return (value.Deserialize(type, Binding), null);
        }
        catch (JsonException)
        {
            return (null, Mismatch(parameter, expected));
        }
    }

    private static string Mismatch(Parameter parameter, string expected) => NotOfType(parameter.Name, expected);

    private sealed record Parameter(
        string Name,
        Type Type,
        string? Description,
        bool IsCancellation,
        bool Required,
        bool AcceptsNull,
        object? Default)
    {
        public JsonElement Schema { get; init; }
    }

    private sealed record Tool(
        MethodInfo Method,
        ImmutableArray<Parameter> Parameters,
        ToolDefinition Definition,
        Completion Completion);
}
