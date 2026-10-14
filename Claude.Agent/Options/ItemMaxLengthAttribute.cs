namespace Claude.Agent;

/// <summary>The most characters each string item of a list member may hold, in a schema taken from a type by <see cref="ClaudeAgentSessionOptions.SetOutputSchema{T}(System.Text.Json.JsonSerializerOptions?, System.Text.Json.Schema.JsonSchemaExporterOptions?)"/>: the items' maxLength, beside the list's own maxItems, which <see cref="System.ComponentModel.DataAnnotations.MaxLengthAttribute"/> sets. On a list of byte arrays, which the contract writes as base64 strings, it counts bytes, and the maxLength is the base64 length of that many. On a member that is no list of strings it writes nothing. Only the schema reads it; nothing validates a value by it.</summary>
/// <param name="length">The most characters an item may hold, or bytes for a byte array. Below zero is an <see cref="ArgumentOutOfRangeException"/>.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ItemMaxLengthAttribute(int length) : Attribute
{
    /// <summary>The most characters an item may hold, or bytes for a byte array.</summary>
    public int Length { get; } = length >= 0 ? length : throw new ArgumentOutOfRangeException(nameof(length), length, "An item's maximum length is zero or more.");
}
