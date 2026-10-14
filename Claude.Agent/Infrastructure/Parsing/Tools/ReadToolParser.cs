using System.Text.Json;

namespace Claude.Agent;

// The Read tool's input and output. The output is a union on `type`, one type a variant; a variant
// the library has no type for, or one whose members do not fit, makes the view null beside the raw.
internal static class ReadToolParser
{
    public static ReadToolInput? ReadInput(JsonElement raw)
    {
        if (!raw.Text("file_path", out var filePath))
        {
            return null;
        }

        return new ReadToolInput(filePath, raw.CountOrNull("offset"), raw.CountOrNull("limit"), raw.TextOrNull("pages"));
    }

    public static void WriteInput(Utf8JsonWriter writer, ReadToolInput input)
    {
        writer.WriteStartObject();
        writer.WriteString("file_path", input.FilePath);
        writer.Count("offset", input.Offset);
        writer.Count("limit", input.Limit);
        writer.Text("pages", input.Pages);
        writer.WriteEndObject();
    }

    public static ReadToolOutput? ReadOutput(JsonElement raw) =>
        raw.TextOrNull("type") switch
        {
            "text" => AsText(raw),
            "image" => AsImage(raw),
            "notebook" => AsNotebook(raw),
            "pdf" => AsPdf(raw),
            "parts" => AsParts(raw),
            "file_unchanged" => AsUnchanged(raw),
            _ => null,
        };

    public static void WriteOutput(Utf8JsonWriter writer, ReadToolOutput output)
    {
        switch (output)
        {
            case ReadTextToolOutput text:
                writer.WriteStartObject();
                writer.WriteString("type", "text");
                writer.WriteStartObject("file");
                writer.WriteString("filePath", text.File.FilePath);
                writer.WriteString("content", text.File.Content);
                writer.WriteNumber("numLines", text.File.LineCount);
                writer.WriteNumber("startLine", text.File.StartLine);
                writer.WriteNumber("totalLines", text.File.TotalLineCount);
                writer.Flag("truncatedByTokenCap", text.File.TruncatedByTokenCap);
                writer.WriteEndObject();
                if (text.ArtifactRead is { } artifact)
                {
                    writer.WriteStartObject("artifactRead");
                    writer.WriteString("slug", artifact.Slug);
                    writer.WriteString("ver", artifact.Version);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
                break;
            case ReadImageToolOutput image:
                writer.WriteStartObject();
                writer.WriteString("type", "image");
                writer.WriteStartObject("file");
                writer.WriteString("base64", image.File.Base64);
                writer.WriteString("type", image.File.MediaType.ToString());
                writer.WriteNumber("originalSize", image.File.OriginalSize);
                if (image.File.Dimensions is { } dimensions)
                {
                    writer.WriteStartObject("dimensions");
                    writer.Count("originalWidth", dimensions.OriginalWidth);
                    writer.Count("originalHeight", dimensions.OriginalHeight);
                    writer.Count("displayWidth", dimensions.DisplayWidth);
                    writer.Count("displayHeight", dimensions.DisplayHeight);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
                break;
            case ReadNotebookToolOutput notebook:
                writer.WriteStartObject();
                writer.WriteString("type", "notebook");
                writer.WriteStartObject("file");
                writer.WriteString("filePath", notebook.File.FilePath);
                writer.ItemObjects("cells", notebook.File.Cells, WriteCell);
                writer.WriteEndObject();
                writer.WriteEndObject();
                break;
            case ReadPdfToolOutput document:
                writer.WriteStartObject();
                writer.WriteString("type", "pdf");
                writer.WriteStartObject("file");
                writer.WriteString("filePath", document.File.FilePath);
                writer.WriteString("base64", document.File.Base64);
                writer.WriteNumber("originalSize", document.File.OriginalSize);
                writer.WriteEndObject();
                writer.WriteEndObject();
                break;
            case ReadPartsToolOutput parts:
                writer.WriteStartObject();
                writer.WriteString("type", "parts");
                writer.WriteStartObject("file");
                writer.WriteString("filePath", parts.File.FilePath);
                writer.WriteNumber("originalSize", parts.File.OriginalSize);
                writer.WriteNumber("count", parts.File.PageCount);
                writer.WriteString("outputDir", parts.File.OutputDirectory);
                writer.WriteEndObject();
                writer.Count("firstPage", parts.FirstPage);
                writer.Items("pages", parts.Pages, WritePage);
                writer.WriteEndObject();
                break;
            case ReadFileUnchangedToolOutput unchanged:
                writer.WriteStartObject();
                writer.WriteString("type", "file_unchanged");
                writer.WriteStartObject("file");
                writer.WriteString("filePath", unchanged.File.FilePath);
                writer.WriteEndObject();
                writer.Text("source", unchanged.Source?.ToString());
                writer.WriteEndObject();
                break;
            default:
                throw new NotSupportedException($"No Read output of type {output.GetType().Name} can be written.");
        }
    }

    private static void WritePage(Utf8JsonWriter writer, ReadPartsPage page)
    {
        writer.WriteStartObject();
        writer.WriteString("base64", page.Base64);
        writer.WriteString("mediaType", page.MediaType.ToString());
        writer.Text("error", page.Error);
        writer.WriteEndObject();
    }

    // The keys in the order the recording carries them, so a cell written back is the cell that came.
    private static void WriteCell(Utf8JsonWriter writer, ReadNotebookCell cell)
    {
        writer.WriteString("cellType", cell.CellType.ToString());
        writer.WriteString("source", cell.Source);
        writer.Text("cell_id", cell.CellId);
        writer.Text("language", cell.Language);
    }

    private static ReadTextToolOutput? AsText(JsonElement raw)
    {
        if (!raw.Object("file", out var file)
            || !file.Text("filePath", out var filePath)
            || !file.Text("content", out var content)
            || !file.Count("numLines", out var lineCount)
            || !file.Count("startLine", out var startLine)
            || !file.Count("totalLines", out var totalLineCount)
            || !raw.Nested("artifactRead", AsArtifact, out var artifact))
        {
            return null;
        }

        return new ReadTextToolOutput(
            new ReadTextFile(filePath, content, lineCount, startLine, totalLineCount, file.FlagOrNull("truncatedByTokenCap")),
            artifact);
    }

    private static ReadArtifactReference? AsArtifact(JsonElement raw) =>
        raw.Text("slug", out var slug) && raw.Text("ver", out var version)
            ? new ReadArtifactReference(slug, version)
            : null;

    private static ReadImageToolOutput? AsImage(JsonElement raw)
    {
        if (!raw.Object("file", out var file)
            || !file.Text("base64", out var base64)
            || !file.Text("type", out var mediaType)
            || !file.Count("originalSize", out var originalSize)
            || !file.Nested("dimensions", AsDimensions, out var dimensions))
        {
            return null;
        }

        return new ReadImageToolOutput(new ReadImageFile(base64, mediaType, originalSize, dimensions));
    }

    private static ReadImageDimensions? AsDimensions(JsonElement raw) =>
        new(
            raw.CountOrNull("originalWidth"),
            raw.CountOrNull("originalHeight"),
            raw.CountOrNull("displayWidth"),
            raw.CountOrNull("displayHeight"));

    private static ReadNotebookToolOutput? AsNotebook(JsonElement raw) =>
        raw.Object("file", out var file)
        && file.Text("filePath", out var filePath)
        && file.ItemsOrEmpty("cells", AsCell, out var cells)
            ? new ReadNotebookToolOutput(new ReadNotebookFile(filePath, cells))
            : null;

    private static ReadNotebookCell? AsCell(JsonElement raw) =>
        raw.Text("source", out var source) && raw.SetOrNull("cellType", value => new NotebookCellType(value)) is { } cellType
            ? new ReadNotebookCell(cellType, source, raw.TextOrNull("cell_id"), raw.TextOrNull("language"))
            : null;

    private static ReadPdfToolOutput? AsPdf(JsonElement raw)
    {
        if (!raw.Object("file", out var file)
            || !file.Text("filePath", out var filePath)
            || !file.Text("base64", out var base64)
            || !file.Count("originalSize", out var originalSize))
        {
            return null;
        }

        return new ReadPdfToolOutput(new ReadPdfFile(filePath, base64, originalSize));
    }

    private static ReadPartsToolOutput? AsParts(JsonElement raw)
    {
        if (!raw.Object("file", out var file)
            || !file.Text("filePath", out var filePath)
            || !file.Count("originalSize", out var originalSize)
            || !file.Count("count", out var pageCount)
            || !file.Text("outputDir", out var outputDirectory)
            || !raw.ItemsOrEmpty("pages", AsPage, out var pages))
        {
            return null;
        }

        return new ReadPartsToolOutput(
            new ReadPartsFile(filePath, originalSize, pageCount, outputDirectory),
            raw.CountOrNull("firstPage"),
            pages);
    }

    private static ReadPartsPage? AsPage(JsonElement raw) =>
        raw.Text("base64", out var base64) && raw.Text("mediaType", out var mediaType)
            ? new ReadPartsPage(base64, mediaType, raw.TextOrNull("error"))
            : null;

    private static ReadFileUnchangedToolOutput? AsUnchanged(JsonElement raw)
    {
        if (!raw.Object("file", out var file) || !file.Text("filePath", out var filePath))
        {
            return null;
        }

        return new ReadFileUnchangedToolOutput(
            new ReadUnchangedFile(filePath),
            raw.SetOrNull("source", value => new FileUnchangedSource(value)));
    }
}
