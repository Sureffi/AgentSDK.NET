using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace Claude.Agent;

// The prompt an ask writes: the task as the caller gave it, then the evidence's schema, then the
// evidence, each in a tag of its own and the three a blank line apart. Schema and evidence are
// written off the one contract, indented two spaces with a line feed, and a character is escaped
// only where JSON needs it — a quotation mark, a backslash, a control character — so the model
// reads the text as the evidence holds it. The schema is SchemaExporter's, so a member's
// [Description] is in what the model reads; a caller's transform that makes its root null is an
// ArgumentException. What System.Text.Json throws writing either comes out as it threw it.
internal static class EvidencePrompt
{
    private static readonly JsonWriterOptions Indented = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        MaxDepth = int.MaxValue,
    };

    public static string Write<TEvidence>(string task, TEvidence evidence, JsonTypeInfo<TEvidence> contract, JsonSchemaExporterOptions? exporterOptions)
    {
        var schema = SchemaExporter.Evidence(contract, exporterOptions)
            ?? throw new ArgumentException($"The schema of {typeof(TEvidence)} is null: the exporter's transform made its root null.", nameof(exporterOptions));

        // Written to an element first, so the member names are escaped by the writer below like
        // every other string, and not by the encoder the contract carries.
        var written = JsonSerializer.SerializeToElement(evidence, contract);
        return new StringBuilder(task)
            .Append("\n\n<evidence_schema>\n")
            .Append(Text(writer => schema.WriteTo(writer)))
            .Append("\n</evidence_schema>\n\n<evidence>\n")
            .Append(Text(written.WriteTo))
            .Append("\n</evidence>")
            .ToString();
    }

    private static string Text(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, Indented))
        {
            write(writer);
        }

        return Unescaped(Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    // The relaxed encoder still writes some characters JSON does not need escaped as \uXXXX: those
    // outside the Basic Multilingual Plane as two, and a few within it, a no-break space and the
    // line and paragraph separators among them. Each such escape is written back as its character;
    // what JSON needs escaped, and a lone surrogate, stays as the encoder wrote it.
    private static string Unescaped(string json)
    {
        if (!json.Contains("\\u", StringComparison.Ordinal))
        {
            return json;
        }

        var text = new StringBuilder(json.Length);
        var at = 0;
        while (at < json.Length)
        {
            if (json[at] != '\\' || at + 1 >= json.Length)
            {
                text.Append(json[at++]);
                continue;
            }

            if (json[at + 1] != 'u' || CodeUnit(json, at) is not { } unit)
            {
                // An escape of one character after the backslash: \" \\ \n and the rest.
                text.Append(json, at, 2);
                at += 2;
                continue;
            }

            if (char.IsHighSurrogate(unit) && CodeUnit(json, at + 6) is { } low && char.IsLowSurrogate(low))
            {
                text.Append(unit).Append(low);
                at += 12;
            }
            else if (unit < 0x20 || unit == '"' || unit == '\\' || char.IsSurrogate(unit))
            {
                text.Append(json, at, 6);
                at += 6;
            }
            else
            {
                text.Append(unit);
                at += 6;
            }
        }

        return text.ToString();
    }

    // The code unit a \uXXXX at `at` names, or none when there is no such escape there.
    private static char? CodeUnit(string json, int at) =>
        at + 6 <= json.Length
        && json[at] == '\\'
        && json[at + 1] == 'u'
        && ushort.TryParse(json.AsSpan(at + 2, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var unit)
            ? (char)unit
            : null;
}
