using System.Buffers;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace Claude.Agent;

// The schema a type is taken into for --json-schema: the platform exporter's, with the library's
// transform writing into each node what the exporter leaves out. The root's type is "object", never
// ["object", "null"], and where the exporter wrote it nullable each reference back to it, "$ref":
// "#", is made to admit null as the root is reached; a member's [Description], or else its type's,
// is its description; [MaxLength], [StringLength] and [Length] are a string's maxLength and an
// array's maxItems, and on a byte array, which the contract writes as base64, they count bytes, so
// the string's maxLength is the base64 length of that many; [ItemMaxLength] on an array of strings
// is its items' maxLength, counted the same way for byte arrays; an attribute on the member an
// override overrides is the override's too; an object the contract reads member by member, keeping
// no extension data, has additionalProperties false, and a $ref or a polymorphic type's anyOf is
// left to the nodes it names. The exporter writes required and an enum's names itself. A caller's
// settings apply and its transform runs after the library's on each node; a root that is then not
// one OutputSchemaRoot takes — not an object, or with anyOf, oneOf or allOf at it as a polymorphic
// type's has — is an ArgumentException. The schema comes back as the element OutputSchema holds.
// An ask's evidence is taken into a schema by the same transform, for the model to read beside it:
// its root is any type the contract writes, so no rule holds it, and it comes back as the node.
internal static class SchemaExporter
{
    private const string Object = "object";

    private const string RootReference = "#";

    public static JsonElement Export(JsonSerializerOptions contract, Type type, JsonSchemaExporterOptions? exporterOptions)
    {
        var schema = contract.GetJsonSchemaAsNode(type, Compose(exporterOptions));
        return EnsureObjectRoot(schema, type, paramName: null);
    }

    public static JsonElement Export(JsonTypeInfo contract, JsonSchemaExporterOptions? exporterOptions, string paramName)
    {
        var schema = contract.GetJsonSchemaAsNode(Compose(exporterOptions));
        return EnsureObjectRoot(schema, contract.Type, paramName);
    }

    // Null when a caller's transform made the root null.
    public static JsonNode? Evidence(JsonTypeInfo contract, JsonSchemaExporterOptions? exporterOptions) =>
        contract.GetJsonSchemaAsNode(Compose(exporterOptions));

    private static JsonSchemaExporterOptions Compose(JsonSchemaExporterOptions? caller)
    {
        var callerTransform = caller?.TransformSchemaNode;
        return new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = (caller ?? JsonSchemaExporterOptions.Default).TreatNullObliviousAsNonNullable,
            TransformSchemaNode = callerTransform is null
                ? Transform
                : (context, node) => callerTransform(context, Transform(context, node)),
        };
    }

    private static JsonNode Transform(JsonSchemaExporterContext context, JsonNode node)
    {
        var description = Description(context);
        if (node is not JsonObject schema)
        {
            // `true`, the schema of any value: a description makes it the object that says so.
            if (description is null || node.GetValueKind() is not JsonValueKind.True)
            {
                return node;
            }

            schema = new JsonObject();
        }

        if (context.Path.IsEmpty && IsNullableObject(schema))
        {
            schema["type"] = Object;
            AdmitNullWhereTheRootIsReferenced(schema);
        }

        if (description is not null)
        {
            schema.Insert(0, "description", description);
        }

        if (MaximumLength(context.PropertyInfo) is { } maximum)
        {
            if (Names(schema, "string"))
            {
                schema["maxLength"] = IsBytes(context.TypeInfo.Type) ? Base64Length(maximum) : maximum;
            }
            else if (Names(schema, "array"))
            {
                schema["maxItems"] = maximum;
            }
        }

        if (ItemMaximumLength(context.PropertyInfo) is { } itemMaximum
            && Names(schema, "array")
            && schema["items"] is JsonObject items
            && Names(items, "string"))
        {
            items["maxLength"] = context.TypeInfo.ElementType is { } element && IsBytes(element) ? Base64Length(itemMaximum) : itemMaximum;
        }

        if (FixesItsMembers(context.TypeInfo)
            && !schema.ContainsKey("$ref")
            && !schema.ContainsKey("anyOf")
            && !schema.ContainsKey("additionalProperties"))
        {
            schema["additionalProperties"] = false;
        }

        return schema;
    }

    // The schema as the element OutputSchema holds, held to the rule a schema handed in is. A caller's
    // transform that made the root null gives the element null, which the rule names. It is written
    // and read back at any depth: how deep a schema may go is the contract's MaxDepth, which the
    // exporter has already held it to.
    private static JsonElement EnsureObjectRoot(JsonNode? schema, Type type, string? paramName)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { MaxDepth = int.MaxValue }))
        {
            if (schema is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                schema.WriteTo(writer);
            }
        }

        JsonElement element;
        using (var document = JsonDocument.Parse(buffer.WrittenMemory, new JsonDocumentOptions { MaxDepth = int.MaxValue }))
        {
            element = document.RootElement.Clone();
        }

        return OutputSchemaRoot.Problem(element, $"The schema of {type}") is { } problem
            ? throw new ArgumentException(problem, paramName)
            : element;
    }

    // The exporter writes a reference back to the root as "$ref": "#", against the root as it wrote
    // it. Once a nullable root is an object, each such reference is the root or null, so an element
    // or a value the type reads as null is one the schema admits.
    private static void AdmitNullWhereTheRootIsReferenced(JsonObject root)
    {
        foreach (var reference in Descendants(root).OfType<JsonObject>().Where(IsRootReference).ToArray())
        {
            reference.ReplaceWith(new JsonObject
            {
                ["anyOf"] = new JsonArray(reference.DeepClone(), new JsonObject { ["type"] = "null" }),
            });
        }
    }

    private static IEnumerable<JsonNode> Descendants(JsonNode node)
    {
        IEnumerable<JsonNode?> children = node switch
        {
            JsonObject members => members.Select(member => member.Value),
            JsonArray items => items,
            _ => [],
        };

        foreach (var child in children.OfType<JsonNode>())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool IsRootReference(JsonObject schema) =>
        schema["$ref"] is JsonValue reference
        && reference.GetValueKind() is JsonValueKind.String
        && reference.GetValue<string>() == RootReference;

    // A member's own description, on the property or the constructor parameter that sets it, and
    // otherwise its type's.
    private static string? Description(JsonSchemaExporterContext context)
    {
        var member = context.PropertyInfo;
        return Attribute<DescriptionAttribute>(member?.AttributeProvider)?.Description
            ?? Attribute<DescriptionAttribute>(member?.AssociatedParameter?.AttributeProvider)?.Description
            ?? (Nullable.GetUnderlyingType(context.TypeInfo.Type) ?? context.TypeInfo.Type).GetCustomAttribute<DescriptionAttribute>()?.Description;
    }

    // The least of the maximum lengths the member's attributes set, on the property or the
    // constructor parameter. [MaxLength] with no length sets none.
    private static int? MaximumLength(JsonPropertyInfo? member)
    {
        int? least = null;
        foreach (var provider in new[] { member?.AttributeProvider, member?.AssociatedParameter?.AttributeProvider })
        {
            foreach (var attribute in Attributes(provider))
            {
                int? maximum = attribute switch
                {
                    MaxLengthAttribute { Length: > 0 } maxLength => maxLength.Length,
                    StringLengthAttribute stringLength => stringLength.MaximumLength,
                    LengthAttribute length => length.MaximumLength,
                    _ => null,
                };

                if (maximum < (least ?? int.MaxValue))
                {
                    least = maximum;
                }
            }
        }

        return least;
    }

    // The least of the item lengths [ItemMaxLength] sets, on the property or the constructor parameter.
    private static int? ItemMaximumLength(JsonPropertyInfo? member)
    {
        int? least = null;
        foreach (var provider in new[] { member?.AttributeProvider, member?.AssociatedParameter?.AttributeProvider })
        {
            foreach (var attribute in Attributes(provider).OfType<ItemMaxLengthAttribute>())
            {
                if (attribute.Length < (least ?? int.MaxValue))
                {
                    least = attribute.Length;
                }
            }
        }

        return least;
    }

    // An object the contract reads member by member, not a dictionary, and one that keeps no
    // extension data: a member it does not name has nowhere to go.
    private static bool FixesItsMembers(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind is not JsonTypeInfoKind.Object)
        {
            return false;
        }

        var described = Nullable.GetUnderlyingType(typeInfo.Type) is { } underlying
            ? typeInfo.Options.GetTypeInfo(underlying)
            : typeInfo;
        return !described.Properties.Any(property => property.IsExtensionData);
    }

    // What the contract writes as a base64 string, whose length attributes count bytes.
    private static bool IsBytes(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) is var bytes
        && (bytes == typeof(byte[]) || bytes == typeof(ReadOnlyMemory<byte>) || bytes == typeof(Memory<byte>));

    // Base64 writes each three bytes, and a last one or two, as four characters.
    private static long Base64Length(int bytes) => 4L * ((bytes + 2L) / 3);

    private static bool IsNullableObject(JsonObject schema) =>
        schema["type"] is JsonArray { Count: 2 } types
        && types.Any(type => type?.GetValue<string>() == Object)
        && types.Any(type => type?.GetValue<string>() == "null");

    private static bool Names(JsonObject schema, string type) => schema["type"] switch
    {
        JsonArray types => types.Any(name => name?.GetValue<string>() == type),
        JsonValue name => name.GetValueKind() is JsonValueKind.String && name.GetValue<string>() == type,
        _ => false,
    };

    private static T? Attribute<T>(ICustomAttributeProvider? provider)
        where T : Attribute =>
        Attributes(provider).OfType<T>().FirstOrDefault();

    // A member's attributes and those on the declarations it overrides, as DataAnnotations reads
    // them: a PropertyInfo's own lookup ignores inherit, and Attribute's honours it.
    private static object[] Attributes(ICustomAttributeProvider? provider) => provider switch
    {
        null => [],
        MemberInfo member => System.Attribute.GetCustomAttributes(member, inherit: true),
        _ => provider.GetCustomAttributes(inherit: true),
    };
}
