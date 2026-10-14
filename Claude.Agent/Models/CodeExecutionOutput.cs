namespace Claude.Agent;

/// <summary>A file the API's code execution left in its container.</summary>
public sealed class CodeExecutionOutput
{
    internal CodeExecutionOutput(string fileId)
    {
        FileId = fileId;
    }

    /// <summary>The file, by the id the container gave it.</summary>
    public string FileId { get; }
}
