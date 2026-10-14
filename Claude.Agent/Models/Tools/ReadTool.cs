namespace Claude.Agent;

/// <summary>What the model asked the Read tool to read.</summary>
/// <param name="filePath">The file to read, as an absolute path.</param>
/// <param name="offset">The line to start at; absent means the first.</param>
/// <param name="limit">How many lines to read; absent means as many as the cap allows.</param>
/// <param name="pages">A page range, for a document read page by page.</param>
public sealed class ReadToolInput(string filePath, long? offset = null, long? limit = null, string? pages = null) : ToolInput
{
    /// <summary>The file to read, as an absolute path.</summary>
    public string FilePath { get; } = filePath;

    /// <summary>The line to start at; null when the call named none.</summary>
    public long? Offset { get; } = offset;

    /// <summary>How many lines to read; null when the call named none.</summary>
    public long? Limit { get; } = limit;

    /// <summary>A page range, for a document read page by page; null when the call named none.</summary>
    public string? Pages { get; } = pages;
}

/// <summary>What a Read handed back. The binary answers in one of six shapes, one type each.</summary>
public abstract class ReadToolOutput : ToolOutput
{
    private protected ReadToolOutput()
    {
    }
}

/// <summary>An artifact version a Read marked as seen.</summary>
/// <param name="slug">The artifact's name.</param>
/// <param name="version">The version of it that now counts as read.</param>
public sealed class ReadArtifactReference(string slug, string version)
{
    /// <summary>The artifact's name.</summary>
    public string Slug { get; } = slug;

    /// <summary>The version of it that now counts as read.</summary>
    public string Version { get; } = version;
}

/// <summary>The lines a Read of a text file returned.</summary>
/// <param name="filePath">The file that was read.</param>
/// <param name="content">The lines themselves.</param>
/// <param name="lineCount">How many lines the returned content holds.</param>
/// <param name="startLine">The line the content starts at.</param>
/// <param name="totalLineCount">How many lines the whole file holds.</param>
/// <param name="truncatedByTokenCap">Whether a whole-file read was cut short at the token cap, leaving a first page.</param>
public sealed class ReadTextFile(
    string filePath,
    string content,
    long lineCount,
    long startLine,
    long totalLineCount,
    bool? truncatedByTokenCap = null)
{
    /// <summary>The file that was read.</summary>
    public string FilePath { get; } = filePath;

    /// <summary>The lines themselves.</summary>
    public string Content { get; } = content;

    /// <summary>How many lines the returned content holds.</summary>
    public long LineCount { get; } = lineCount;

    /// <summary>The line the content starts at.</summary>
    public long StartLine { get; } = startLine;

    /// <summary>How many lines the whole file holds.</summary>
    public long TotalLineCount { get; } = totalLineCount;

    /// <summary>Whether a whole-file read was cut short at the token cap; null when the binary said nothing.</summary>
    public bool? TruncatedByTokenCap { get; } = truncatedByTokenCap;
}

/// <summary>A Read of a text file.</summary>
/// <param name="file">The lines themselves and where in the file they came from.</param>
/// <param name="artifactRead">The artifact version this read marked as seen, when it completed one.</param>
public sealed class ReadTextToolOutput(ReadTextFile file, ReadArtifactReference? artifactRead = null) : ReadToolOutput
{
    /// <summary>The lines themselves and where in the file they came from.</summary>
    public ReadTextFile File { get; } = file;

    /// <summary>The artifact version this read marked as seen; null when it completed none.</summary>
    public ReadArtifactReference? ArtifactRead { get; } = artifactRead;
}

/// <summary>How large an image a Read returned is, on disk and as the model sees it.</summary>
/// <param name="originalWidth">The image's own width in pixels.</param>
/// <param name="originalHeight">The image's own height in pixels.</param>
/// <param name="displayWidth">The width in pixels after any resizing.</param>
/// <param name="displayHeight">The height in pixels after any resizing.</param>
public sealed class ReadImageDimensions(
    long? originalWidth = null,
    long? originalHeight = null,
    long? displayWidth = null,
    long? displayHeight = null)
{
    /// <summary>The image's own width in pixels; null when the binary gave none.</summary>
    public long? OriginalWidth { get; } = originalWidth;

    /// <summary>The image's own height in pixels; null when the binary gave none.</summary>
    public long? OriginalHeight { get; } = originalHeight;

    /// <summary>The width in pixels after any resizing; null when the binary gave none.</summary>
    public long? DisplayWidth { get; } = displayWidth;

    /// <summary>The height in pixels after any resizing; null when the binary gave none.</summary>
    public long? DisplayHeight { get; } = displayHeight;
}

/// <summary>The image a Read of an image file returned.</summary>
/// <param name="base64">The image's bytes, base64 encoded.</param>
/// <param name="mediaType">The image's media type.</param>
/// <param name="originalSize">The file's size in bytes.</param>
/// <param name="dimensions">How large the image is, on disk and as the model sees it.</param>
public sealed class ReadImageFile(string base64, ImageMediaType mediaType, long originalSize, ReadImageDimensions? dimensions = null)
{
    /// <summary>The image's bytes, base64 encoded.</summary>
    public string Base64 { get; } = base64;

    /// <summary>The image's media type; the wire carries it under <c>type</c>.</summary>
    public ImageMediaType MediaType { get; } = mediaType;

    /// <summary>The file's size in bytes.</summary>
    public long OriginalSize { get; } = originalSize;

    /// <summary>How large the image is, on disk and as the model sees it; null when the binary gave no sizes.</summary>
    public ReadImageDimensions? Dimensions { get; } = dimensions;
}

/// <summary>A Read of an image file.</summary>
/// <param name="file">The image itself.</param>
public sealed class ReadImageToolOutput(ReadImageFile file) : ReadToolOutput
{
    /// <summary>The image itself.</summary>
    public ReadImageFile File { get; } = file;
}

/// <summary>One cell of a notebook a Read returned.</summary>
/// <param name="cellType">What kind of cell it is.</param>
/// <param name="source">The cell's text.</param>
/// <param name="cellId">The notebook's own id for the cell; null when it carries none.</param>
/// <param name="language">The language the cell is written in; null on a cell that names none.</param>
public sealed class ReadNotebookCell(NotebookCellType cellType, string source, string? cellId, string? language)
{
    /// <summary>What kind of cell it is.</summary>
    public NotebookCellType CellType { get; } = cellType;

    /// <summary>The cell's text.</summary>
    public string Source { get; } = source;

    /// <summary>The notebook's own id for the cell; null when it carries none.</summary>
    public string? CellId { get; } = cellId;

    /// <summary>The language the cell is written in; null on a cell that names none.</summary>
    public string? Language { get; } = language;
}

/// <summary>The notebook a Read of a notebook returned.</summary>
/// <param name="filePath">The notebook that was read.</param>
/// <param name="cells">The notebook's cells, in the order it holds them.</param>
public sealed class ReadNotebookFile(string filePath, IReadOnlyList<ReadNotebookCell> cells)
{
    /// <summary>The notebook that was read.</summary>
    public string FilePath { get; } = filePath;

    /// <summary>The notebook's cells, in the order it holds them.</summary>
    public IReadOnlyList<ReadNotebookCell> Cells { get; } = cells;
}

/// <summary>A Read of a notebook.</summary>
/// <param name="file">The notebook itself.</param>
public sealed class ReadNotebookToolOutput(ReadNotebookFile file) : ReadToolOutput
{
    /// <summary>The notebook itself.</summary>
    public ReadNotebookFile File { get; } = file;
}

/// <summary>The document a Read of a PDF returned whole.</summary>
/// <param name="filePath">The document that was read.</param>
/// <param name="base64">The document's bytes, base64 encoded.</param>
/// <param name="originalSize">The file's size in bytes.</param>
public sealed class ReadPdfFile(string filePath, string base64, long originalSize)
{
    /// <summary>The document that was read.</summary>
    public string FilePath { get; } = filePath;

    /// <summary>The document's bytes, base64 encoded.</summary>
    public string Base64 { get; } = base64;

    /// <summary>The file's size in bytes.</summary>
    public long OriginalSize { get; } = originalSize;
}

/// <summary>A Read of a PDF returned whole.</summary>
/// <param name="file">The document itself.</param>
public sealed class ReadPdfToolOutput(ReadPdfFile file) : ReadToolOutput
{
    /// <summary>The document itself.</summary>
    public ReadPdfFile File { get; } = file;
}

/// <summary>The document a Read returned as page images.</summary>
/// <param name="filePath">The document that was read.</param>
/// <param name="originalSize">The file's size in bytes.</param>
/// <param name="pageCount">How many pages were taken out of it.</param>
/// <param name="outputDirectory">The directory the page images were written to.</param>
public sealed class ReadPartsFile(string filePath, long originalSize, long pageCount, string outputDirectory)
{
    /// <summary>The document that was read.</summary>
    public string FilePath { get; } = filePath;

    /// <summary>The file's size in bytes.</summary>
    public long OriginalSize { get; } = originalSize;

    /// <summary>How many pages were taken out of it; the wire carries it under <c>count</c>.</summary>
    public long PageCount { get; } = pageCount;

    /// <summary>The directory the page images were written to; the wire carries it under <c>outputDir</c>.</summary>
    public string OutputDirectory { get; } = outputDirectory;
}

/// <summary>One page image a Read took out of a document.</summary>
/// <param name="base64">The page image's bytes, base64 encoded; empty when the page could not be made into one.</param>
/// <param name="mediaType">The page image's media type.</param>
/// <param name="error">Why the page could not be made into an image; set only when the bytes are empty.</param>
public sealed class ReadPartsPage(string base64, ImageMediaType mediaType, string? error = null)
{
    /// <summary>The page image's bytes, base64 encoded; empty when the page could not be made into one.</summary>
    public string Base64 { get; } = base64;

    /// <summary>The page image's media type.</summary>
    public ImageMediaType MediaType { get; } = mediaType;

    /// <summary>Why the page could not be made into an image; null when it could.</summary>
    public string? Error { get; } = error;
}

/// <summary>A Read that took a document apart into page images.</summary>
/// <param name="file">The document and where its pages were written.</param>
/// <param name="firstPage">The document page the first extracted page is; absent means the first page of the document.</param>
/// <param name="pages">The page images themselves; the binary drops them from the result it emits, so this is usually empty.</param>
public sealed class ReadPartsToolOutput(ReadPartsFile file, long? firstPage = null, IReadOnlyList<ReadPartsPage>? pages = null) : ReadToolOutput
{
    /// <summary>The document and where its pages were written.</summary>
    public ReadPartsFile File { get; } = file;

    /// <summary>The document page the first extracted page is; null when the binary named none.</summary>
    public long? FirstPage { get; } = firstPage;

    /// <summary>The page images themselves; empty when the emitted result carried none, which is the usual case.</summary>
    public IReadOnlyList<ReadPartsPage> Pages { get; } = pages ?? [];
}

/// <summary>The file a Read found unchanged since it was last read.</summary>
/// <param name="filePath">The file that was asked for.</param>
public sealed class ReadUnchangedFile(string filePath)
{
    /// <summary>The file that was asked for.</summary>
    public string FilePath { get; } = filePath;
}

/// <summary>A Read the binary answered without re-reading, because it already held the file unchanged.</summary>
/// <param name="file">The file that was asked for.</param>
/// <param name="source">What the binary already held it from.</param>
public sealed class ReadFileUnchangedToolOutput(ReadUnchangedFile file, FileUnchangedSource? source = null) : ReadToolOutput
{
    /// <summary>The file that was asked for.</summary>
    public ReadUnchangedFile File { get; } = file;

    /// <summary>What the binary already held it from; null when it said nothing.</summary>
    public FileUnchangedSource? Source { get; } = source;
}
