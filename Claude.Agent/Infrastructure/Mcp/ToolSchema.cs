using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;

namespace Claude.Agent;

internal readonly record struct Member(string Name, Type Type, bool Optional);

internal static class ToolSchema
{
    public const int MaxDepth = 8;

    public static Type Unwrap(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    public static bool IsInteger(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
        || type == typeof(sbyte) || type == typeof(uint) || type == typeof(ushort) || type == typeof(ulong);

    public static bool IsNumber(Type type) =>
        type == typeof(float) || type == typeof(double) || type == typeof(decimal);

    public static Type? ItemType(Type type)
    {
        if (type == typeof(string))
        {
            return null;
        }

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        if (!type.IsGenericType)
        {
            return null;
        }

        var definition = type.GetGenericTypeDefinition();
        return definition == typeof(List<>)
            || definition == typeof(IList<>)
            || definition == typeof(IReadOnlyList<>)
            || definition == typeof(ICollection<>)
            || definition == typeof(IReadOnlyCollection<>)
            || definition == typeof(IEnumerable<>)
            || definition == typeof(ImmutableArray<>)
                ? type.GetGenericArguments()[0]
                : null;
    }

    public static string JsonTypeName(Type type)
    {
        var bare = Unwrap(type);
        if (bare == typeof(string) || bare.IsEnum)
        {
            return "string";
        }

        if (bare == typeof(bool))
        {
            return "boolean";
        }

        if (IsInteger(bare))
        {
            return "integer";
        }

        if (IsNumber(bare))
        {
            return "number";
        }

        return ItemType(bare) is null ? "object" : "array";
    }

    [RequiresUnreferencedCode("Building a JSON schema reads the public properties of the parameter types.")]
    public static void Write(
        Utf8JsonWriter writer,
        Type type,
        string? description,
        int depth,
        NullabilityInfoContext nullability)
    {
        var bare = Unwrap(type);

        writer.WriteStartObject();
        if (description is { Length: > 0 })
        {
            writer.WriteString("description", description);
        }

        if (bare.IsEnum)
        {
            writer.WritePropertyName("enum");
            writer.WriteStartArray();
            foreach (var name in Enum.GetNames(bare))
            {
                writer.WriteStringValue(name);
            }

            writer.WriteEndArray();
            writer.WriteString("type", "string");
        }
        else if (ItemType(bare) is { } item)
        {
            writer.WritePropertyName("items");
            Write(writer, item, null, depth + 1, nullability);
            writer.WriteString("type", "array");
        }
        else if (JsonTypeName(bare) is not "object")
        {
            writer.WriteString("type", JsonTypeName(bare));
        }
        else if (depth >= MaxDepth)
        {
            writer.WriteString("type", "object");
        }
        else
        {
            WriteObject(writer, bare, depth, nullability);
        }

        writer.WriteEndObject();
    }

    [RequiresUnreferencedCode("Building a JSON schema reads the public properties of the parameter types.")]
    public static ImmutableArray<Member> Members(Type type, NullabilityInfoContext nullability)
    {
        ArgumentNullException.ThrowIfNull(nullability);

        var settable = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is { IsPublic: true } && property.GetIndexParameters().Length == 0)
            .Select(property => new Member(
                property.Name,
                property.PropertyType,
                IsOptional(nullability.Create(property), property.PropertyType)))
            .ToImmutableArray();

        return settable.IsEmpty ? Constructed(type, nullability) : settable;
    }

    public static bool IsOptional(NullabilityInfo info, Type type) =>
        Nullable.GetUnderlyingType(type) is not null || info.ReadState == NullabilityState.Nullable;

    [RequiresUnreferencedCode("Building a JSON schema reads the public properties of the parameter types.")]
    private static ImmutableArray<Member> Constructed(Type type, NullabilityInfoContext nullability)
    {
        if (type.GetConstructors() is not [{ } constructor] || constructor.GetParameters() is not { Length: > 0 } taken)
        {
            return [];
        }

        var readable = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        return
        [
            .. taken
                .Where(parameter => parameter.Name is { Length: > 0 })
                .Select(parameter => new Member(
                    readable.FirstOrDefault(property =>
                        string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))?.Name
                        ?? parameter.Name!,
                    parameter.ParameterType,
                    parameter.HasDefaultValue
                        || IsOptional(nullability.Create(parameter), parameter.ParameterType))),
        ];
    }

    [RequiresUnreferencedCode("Building a JSON schema reads the public properties of the parameter types.")]
    private static void WriteObject(
        Utf8JsonWriter writer,
        Type type,
        int depth,
        NullabilityInfoContext nullability)
    {
        var members = Members(type, nullability);

        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        foreach (var member in members)
        {
            writer.WritePropertyName(member.Name);
            Write(writer, member.Type, null, depth + 1, nullability);
        }

        writer.WriteEndObject();

        var required = members
            .Where(member => !member.Optional)
            .Select(member => member.Name)
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
    }
}
