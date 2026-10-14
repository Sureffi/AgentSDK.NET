namespace Claude.Agent;

// The checks the wire ids carry: a name is non-empty; a key part is non-empty and unpadded; a
// session or message id that names a transcript file, or a line in one, is a hyphenated uuid.
// Everything else about an id is the binary's business.
internal static class Ids
{
    public static bool IsName(string? value) => !string.IsNullOrEmpty(value);

    public static bool IsKeyPart(string? value) => !string.IsNullOrEmpty(value) && value.Trim() == value;

    public static string? NameOrNull(string? value) => IsName(value) ? value : null;

    public static string? KeyPartOrNull(string? value) => IsKeyPart(value) ? value : null;

    public static bool IsUuid(string? value) => value is not null && Guid.TryParseExact(value, "D", out _);
}
