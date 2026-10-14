namespace Claude.Agent;

public static partial class ClaudeAgentModelFactory
{
    /// <summary>Builds a <see cref="Claude.Agent.ReadToolInput"/> for a test. Each parameter is the property of its name.</summary>
    public static ReadToolInput ReadToolInput(
        string filePath = "",
        long? offset = null,
        long? limit = null,
        string? pages = null) =>
        new(filePath, offset, limit, pages);

    /// <summary>Builds a <see cref="Claude.Agent.ReadArtifactReference"/> for a test. Each parameter is the property of its name.</summary>
    public static ReadArtifactReference ReadArtifactReference(string slug = "", string version = "") =>
        new(slug, version);

    /// <summary>Builds a <see cref="Claude.Agent.ReadTextFile"/> for a test. Each parameter is the property of its name.</summary>
    public static ReadTextFile ReadTextFile(
        string filePath = "",
        string content = "",
        long lineCount = 0,
        long startLine = 0,
        long totalLineCount = 0,
        bool? truncatedByTokenCap = null) =>
        new(filePath, content, lineCount, startLine, totalLineCount, truncatedByTokenCap);

    /// <summary>Builds a <see cref="Claude.Agent.ReadTextToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ReadTextToolOutput ReadTextToolOutput(ReadTextFile? file = null, ReadArtifactReference? artifactRead = null) =>
        new(file ?? ReadTextFile(), artifactRead);

    /// <summary>Builds a <see cref="Claude.Agent.ReadImageDimensions"/> for a test. Each parameter is the property of its name.</summary>
    public static ReadImageDimensions ReadImageDimensions(
        long? originalWidth = null,
        long? originalHeight = null,
        long? displayWidth = null,
        long? displayHeight = null) =>
        new(originalWidth, originalHeight, displayWidth, displayHeight);

    /// <summary>Builds a <see cref="Claude.Agent.ReadImageFile"/> for a test. Each parameter is the property of its name.</summary>
    public static ReadImageFile ReadImageFile(
        string base64 = "",
        ImageMediaType mediaType = default,
        long originalSize = 0,
        ReadImageDimensions? dimensions = null) =>
        new(base64, mediaType, originalSize, dimensions);

    /// <summary>Builds a <see cref="Claude.Agent.ReadImageToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ReadImageToolOutput ReadImageToolOutput(ReadImageFile? file = null) => new(file ?? ReadImageFile());

    /// <summary>Builds a <see cref="Claude.Agent.ReadNotebookCell"/> for a test. Each parameter is the property of its name.</summary>
    public static ReadNotebookCell ReadNotebookCell(
        NotebookCellType cellType = default,
        string source = "",
        string? cellId = null,
        string? language = null) =>
        new(cellType, source, cellId, language);

    /// <summary>Builds a <see cref="Claude.Agent.ReadNotebookFile"/> for a test. Each parameter is the property of its name.</summary>
    public static ReadNotebookFile ReadNotebookFile(string filePath = "", IReadOnlyList<ReadNotebookCell>? cells = null) =>
        new(filePath, cells ?? []);

    /// <summary>Builds a <see cref="Claude.Agent.ReadNotebookToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ReadNotebookToolOutput ReadNotebookToolOutput(ReadNotebookFile? file = null) => new(file ?? ReadNotebookFile());

    /// <summary>Builds a <see cref="Claude.Agent.ReadPdfFile"/> for a test. Each parameter is the property of its name.</summary>
    public static ReadPdfFile ReadPdfFile(string filePath = "", string base64 = "", long originalSize = 0) =>
        new(filePath, base64, originalSize);

    /// <summary>Builds a <see cref="Claude.Agent.ReadPdfToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ReadPdfToolOutput ReadPdfToolOutput(ReadPdfFile? file = null) => new(file ?? ReadPdfFile());

    /// <summary>Builds a <see cref="Claude.Agent.ReadPartsFile"/> for a test. Each parameter is the property of its name.</summary>
    public static ReadPartsFile ReadPartsFile(
        string filePath = "",
        long originalSize = 0,
        long pageCount = 0,
        string outputDirectory = "") =>
        new(filePath, originalSize, pageCount, outputDirectory);

    /// <summary>Builds a <see cref="Claude.Agent.ReadPartsPage"/> for a test. Each parameter is the property of its name.</summary>
    public static ReadPartsPage ReadPartsPage(string base64 = "", ImageMediaType mediaType = default, string? error = null) =>
        new(base64, mediaType, error);

    /// <summary>Builds a <see cref="Claude.Agent.ReadPartsToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ReadPartsToolOutput ReadPartsToolOutput(
        ReadPartsFile? file = null,
        long? firstPage = null,
        IEnumerable<ReadPartsPage>? pages = null) =>
        new(file ?? ReadPartsFile(), firstPage, [.. pages ?? []]);

    /// <summary>Builds a <see cref="Claude.Agent.ReadUnchangedFile"/> for a test. Each parameter is the property of its name.</summary>
    public static ReadUnchangedFile ReadUnchangedFile(string filePath = "") => new(filePath);

    /// <summary>Builds a <see cref="Claude.Agent.ReadFileUnchangedToolOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static ReadFileUnchangedToolOutput ReadFileUnchangedToolOutput(
        ReadUnchangedFile? file = null,
        FileUnchangedSource? source = null) =>
        new(file ?? ReadUnchangedFile(), source);
}
