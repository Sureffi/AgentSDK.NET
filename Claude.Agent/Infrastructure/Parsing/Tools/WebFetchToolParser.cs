using System.Text.Json;

namespace Claude.Agent;

// The WebFetch tool's input and output.
internal static class WebFetchToolParser
{
    public static WebFetchToolInput? ReadInput(JsonElement raw) =>
        raw.Text("url", out var url) && raw.Text("prompt", out var prompt)
            ? new WebFetchToolInput(url, prompt)
            : null;

    public static void WriteInput(Utf8JsonWriter writer, WebFetchToolInput input)
    {
        writer.WriteStartObject();
        writer.WriteString("url", input.Url);
        writer.WriteString("prompt", input.Prompt);
        writer.WriteEndObject();
    }

    public static WebFetchToolOutput? ReadOutput(JsonElement raw)
    {
        if (!raw.Count("bytes", out var bytes)
            || !raw.Count("code", out var code)
            || !raw.Text("codeText", out var codeText)
            || !raw.Text("result", out var result)
            || !raw.Milliseconds("durationMs", out var duration)
            || !raw.Text("url", out var url)
            || !raw.Nested("artifactRead", Artifact, out var artifact))
        {
            return null;
        }

        return new WebFetchToolOutput(bytes, code, codeText, result, duration, url, artifact);
    }

    public static void WriteOutput(Utf8JsonWriter writer, WebFetchToolOutput output)
    {
        writer.WriteStartObject();
        writer.WriteNumber("bytes", output.Bytes);
        writer.WriteNumber("code", output.Code);
        writer.WriteString("codeText", output.CodeText);
        writer.WriteString("result", output.Result);
        writer.Milliseconds("durationMs", output.Duration);
        writer.WriteString("url", output.Url);
        writer.Nested("artifactRead", output.ArtifactRead, WriteArtifact);
        writer.WriteEndObject();
    }

    private static WebFetchArtifactReference? Artifact(JsonElement raw) =>
        raw.Text("slug", out var slug)
            ? new WebFetchArtifactReference(slug, raw.TextOrNull("ver"), raw.FlagOrNull("seeded"))
            : null;

    private static void WriteArtifact(Utf8JsonWriter writer, WebFetchArtifactReference artifact)
    {
        writer.WriteString("slug", artifact.Slug);
        writer.Text("ver", artifact.Version);
        writer.Flag("seeded", artifact.Seeded);
    }
}
