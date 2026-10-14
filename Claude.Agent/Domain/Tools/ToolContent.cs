namespace Claude.Agent;

internal abstract record ToolContent
{
    private protected ToolContent()
    {
    }
}

internal sealed record ToolText(string Text) : ToolContent;

internal sealed record ToolImage(string Base64Data, string MimeType) : ToolContent;
