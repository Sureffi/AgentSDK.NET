namespace Claude.Agent;

/// <summary>A file the API's shell execution left in its container.</summary>
public sealed class BashCodeExecutionOutput
{
    internal BashCodeExecutionOutput(string fileId)
    {
        FileId = fileId;
    }

    /// <summary>The file, by the id the container gave it.</summary>
    public string FileId { get; }
}
