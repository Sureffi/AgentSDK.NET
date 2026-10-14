namespace Claude.Agent;

/// <summary>What the model asked the NotebookEdit tool to change.</summary>
/// <param name="notebookPath">The notebook to edit, as an absolute path.</param>
/// <param name="newSource">The source to put in the cell.</param>
/// <param name="cellId">The cell to change; absent means the tool decides from the mode.</param>
/// <param name="cellType">What the cell holds.</param>
/// <param name="editMode">What to do to the cell.</param>
public sealed class NotebookEditToolInput(
    string notebookPath,
    string newSource,
    string? cellId = null,
    NotebookCellType? cellType = null,
    NotebookEditMode? editMode = null) : ToolInput
{
    /// <summary>The notebook to edit, as an absolute path.</summary>
    public string NotebookPath { get; } = notebookPath;

    /// <summary>The source to put in the cell.</summary>
    public string NewSource { get; } = newSource;

    /// <summary>The cell to change; null when the call named none.</summary>
    public string? CellId { get; } = cellId;

    /// <summary>What the cell holds; null when the call said nothing.</summary>
    public NotebookCellType? CellType { get; } = cellType;

    /// <summary>What to do to the cell; null when the call said nothing.</summary>
    public NotebookEditMode? EditMode { get; } = editMode;
}

/// <summary>What a NotebookEdit handed back.</summary>
/// <param name="newSource">The source the cell now holds.</param>
/// <param name="cellType">What the cell holds.</param>
/// <param name="language">The language the cell is in.</param>
/// <param name="editMode">What was done to the cell; the declaration leaves this one open where the input's is closed.</param>
/// <param name="notebookPath">The notebook that was edited.</param>
/// <param name="originalFile">The notebook as it was, as text.</param>
/// <param name="updatedFile">The notebook as it now is, as text.</param>
/// <param name="oldSource">The source the cell held before.</param>
/// <param name="cellId">The cell that was changed.</param>
/// <param name="error">What went wrong, when something did.</param>
public sealed class NotebookEditToolOutput(
    string newSource,
    NotebookCellType cellType,
    string language,
    NotebookEditMode editMode,
    string notebookPath,
    string originalFile,
    string updatedFile,
    string? oldSource = null,
    string? cellId = null,
    string? error = null) : ToolOutput
{
    /// <summary>The source the cell now holds.</summary>
    public string NewSource { get; } = newSource;

    /// <summary>What the cell holds.</summary>
    public NotebookCellType CellType { get; } = cellType;

    /// <summary>The language the cell is in.</summary>
    public string Language { get; } = language;

    /// <summary>What was done to the cell; the declaration leaves this one open where the input's is closed.</summary>
    public NotebookEditMode EditMode { get; } = editMode;

    /// <summary>The notebook that was edited.</summary>
    public string NotebookPath { get; } = notebookPath;

    /// <summary>The notebook as it was, as text.</summary>
    public string OriginalFile { get; } = originalFile;

    /// <summary>The notebook as it now is, as text.</summary>
    public string UpdatedFile { get; } = updatedFile;

    /// <summary>The source the cell held before; null when the binary sent none.</summary>
    public string? OldSource { get; } = oldSource;

    /// <summary>The cell that was changed; null when the binary named none.</summary>
    public string? CellId { get; } = cellId;

    /// <summary>What went wrong; null when the binary sent none. The binary writes it empty when nothing did.</summary>
    public string? Error { get; } = error;
}
