using System.Text.Json;

namespace Claude.Agent;

public static partial class ClaudeAgentModelFactory
{
    /// <summary>Builds a <see cref="Claude.Agent.TextBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static TextBlock TextBlock(
        string? text = null,
        IEnumerable<Citation>? citations = null,
        CacheControl? cacheControl = null) =>
        new(text ?? string.Empty, [.. citations ?? []]) { CacheControl = cacheControl };

    /// <summary>Builds a <see cref="Claude.Agent.ThinkingBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static ThinkingBlock ThinkingBlock(string? thinking = null, string? signature = null) =>
        new(thinking ?? string.Empty, signature ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.RedactedThinkingBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static RedactedThinkingBlock RedactedThinkingBlock(string? data = null) =>
        new(data ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.ToolUseBlock"/> for a test. Each parameter is the property of its name. <c>rawInput</c> defaults to an empty object.</summary>
    public static ToolUseBlock ToolUseBlock(
        string? id = null,
        string? name = null,
        ToolInput? input = null,
        JsonElement rawInput = default,
        ToolCaller? caller = null,
        string? toolsetName = null,
        CacheControl? cacheControl = null) =>
        new(id ?? string.Empty, name ?? string.Empty, input, Object(rawInput))
        {
            Caller = caller,
            ToolsetName = toolsetName,
            CacheControl = cacheControl,
        };

    /// <summary>Builds a <see cref="Claude.Agent.ToolResultBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static ToolResultBlock ToolResultBlock(
        string? toolUseId = null,
        IEnumerable<ContentBlock>? content = null,
        bool? isError = null,
        string? toolsetName = null,
        CacheControl? cacheControl = null) =>
        new(toolUseId ?? string.Empty, [.. content ?? []], isError)
        {
            ToolsetName = toolsetName,
            CacheControl = cacheControl,
        };

    /// <summary>Builds an <see cref="Claude.Agent.ImageBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static ImageBlock ImageBlock(
        ImageSource source,
        ImageTransformations? transformations = null,
        CacheControl? cacheControl = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new(source) { Transformations = transformations, CacheControl = cacheControl };
    }

    /// <summary>Builds a <see cref="Claude.Agent.DocumentBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static DocumentBlock DocumentBlock(
        DocumentSource source,
        string? title = null,
        string? context = null,
        CitationsConfiguration? citations = null,
        CacheControl? cacheControl = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new(source)
        {
            Title = title,
            Context = context,
            Citations = citations,
            CacheControl = cacheControl,
        };
    }

    /// <summary>Builds a <see cref="Claude.Agent.SearchResultBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static SearchResultBlock SearchResultBlock(
        IEnumerable<ContentBlock>? content = null,
        string? source = null,
        string? title = null,
        CitationsConfiguration? citations = null,
        CacheControl? cacheControl = null) =>
        new([.. content ?? []], source ?? string.Empty, title ?? string.Empty)
        {
            Citations = citations,
            CacheControl = cacheControl,
        };

    /// <summary>Builds a <see cref="Claude.Agent.ToolReferenceBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static ToolReferenceBlock ToolReferenceBlock(string? toolName = null, CacheControl? cacheControl = null) =>
        new(toolName ?? string.Empty) { CacheControl = cacheControl };

    /// <summary>Builds a <see cref="Claude.Agent.BrowserStateBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static BrowserStateBlock BrowserStateBlock(
        IEnumerable<BrowserTab>? tabs = null,
        IEnumerable<BrowserStateChange>? stateChanges = null,
        CacheControl? cacheControl = null) =>
        new([.. tabs ?? []], [.. stateChanges ?? []]) { CacheControl = cacheControl };

    /// <summary>Builds a <see cref="Claude.Agent.ServerToolUseBlock"/> for a test. Each parameter is the property of its name. <c>rawInput</c> defaults to an empty object.</summary>
    public static ServerToolUseBlock ServerToolUseBlock(
        string? id = null,
        ServerToolName name = default,
        JsonElement rawInput = default,
        ToolCaller? caller = null,
        CacheControl? cacheControl = null) =>
        new(id ?? string.Empty, name, Object(rawInput)) { Caller = caller, CacheControl = cacheControl };

    /// <summary>Builds a <see cref="Claude.Agent.WebSearchToolResultBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static WebSearchToolResultBlock WebSearchToolResultBlock(
        string? toolUseId = null,
        IEnumerable<WebSearchToolResultContent>? content = null,
        ToolCaller? caller = null,
        CacheControl? cacheControl = null) =>
        new(toolUseId ?? string.Empty, [.. content ?? []]) { Caller = caller, CacheControl = cacheControl };

    /// <summary>Builds a <see cref="Claude.Agent.WebFetchToolResultBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static WebFetchToolResultBlock WebFetchToolResultBlock(
        string? toolUseId = null,
        IEnumerable<WebFetchToolResultContent>? content = null,
        ToolCaller? caller = null,
        CacheControl? cacheControl = null) =>
        new(toolUseId ?? string.Empty, [.. content ?? []]) { Caller = caller, CacheControl = cacheControl };

    /// <summary>Builds an <see cref="Claude.Agent.AdvisorToolResultBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static AdvisorToolResultBlock AdvisorToolResultBlock(
        string? toolUseId = null,
        IEnumerable<AdvisorToolResultContent>? content = null) =>
        new(toolUseId ?? string.Empty, [.. content ?? []]);

    /// <summary>Builds a <see cref="Claude.Agent.CodeExecutionToolResultBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static CodeExecutionToolResultBlock CodeExecutionToolResultBlock(
        string? toolUseId = null,
        IEnumerable<CodeExecutionToolResultContent>? content = null,
        CacheControl? cacheControl = null) =>
        new(toolUseId ?? string.Empty, [.. content ?? []]) { CacheControl = cacheControl };

    /// <summary>Builds a <see cref="Claude.Agent.BashCodeExecutionToolResultBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static BashCodeExecutionToolResultBlock BashCodeExecutionToolResultBlock(
        string? toolUseId = null,
        IEnumerable<BashCodeExecutionToolResultContent>? content = null,
        CacheControl? cacheControl = null) =>
        new(toolUseId ?? string.Empty, [.. content ?? []]) { CacheControl = cacheControl };

    /// <summary>Builds a <see cref="Claude.Agent.TextEditorCodeExecutionToolResultBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static TextEditorCodeExecutionToolResultBlock TextEditorCodeExecutionToolResultBlock(
        string? toolUseId = null,
        IEnumerable<TextEditorCodeExecutionToolResultContent>? content = null,
        CacheControl? cacheControl = null) =>
        new(toolUseId ?? string.Empty, [.. content ?? []]) { CacheControl = cacheControl };

    /// <summary>Builds a <see cref="Claude.Agent.ToolSearchToolResultBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static ToolSearchToolResultBlock ToolSearchToolResultBlock(
        string? toolUseId = null,
        IEnumerable<ToolSearchToolResultContent>? content = null,
        CacheControl? cacheControl = null) =>
        new(toolUseId ?? string.Empty, [.. content ?? []]) { CacheControl = cacheControl };

    /// <summary>Builds an <see cref="Claude.Agent.McpToolUseBlock"/> for a test. Each parameter is the property of its name. <c>rawInput</c> defaults to an empty object.</summary>
    public static McpToolUseBlock McpToolUseBlock(
        string? id = null,
        string? name = null,
        string? serverName = null,
        JsonElement rawInput = default) =>
        new(id ?? string.Empty, name ?? string.Empty, serverName ?? string.Empty, Object(rawInput));

    /// <summary>Builds an <see cref="Claude.Agent.McpToolResultBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static McpToolResultBlock McpToolResultBlock(
        string? toolUseId = null,
        IEnumerable<ContentBlock>? content = null,
        bool isError = false) =>
        new(toolUseId ?? string.Empty, [.. content ?? []], isError);

    /// <summary>Builds a <see cref="Claude.Agent.ContainerUploadBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static ContainerUploadBlock ContainerUploadBlock(string? fileId = null, CacheControl? cacheControl = null) =>
        new(fileId ?? string.Empty) { CacheControl = cacheControl };

    /// <summary>Builds a <see cref="Claude.Agent.CompactionBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static CompactionBlock CompactionBlock(
        string? content = null,
        string? encryptedContent = null,
        string? signature = null) =>
        new() { Content = content, EncryptedContent = encryptedContent, Signature = signature };

    /// <summary>Builds a <see cref="Claude.Agent.FallbackBlock"/> for a test. Each parameter is the property of its name.</summary>
    public static FallbackBlock FallbackBlock(FallbackModel from, FallbackModel to, FallbackTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(trigger);
        return new(from, to, trigger);
    }

    /// <summary>Builds an <see cref="Claude.Agent.UnknownContentBlock"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownContentBlock UnknownContentBlock(
        string? type = null,
        JsonElement raw = default) =>
        new(type ?? string.Empty, Object(raw));

    /// <summary>Builds a <see cref="Claude.Agent.CacheControl"/> for a test. Each parameter is the property of its name.</summary>
    public static CacheControl CacheControl(CacheControlTimeToLive? timeToLive = null) =>
        new() { TimeToLive = timeToLive };

    /// <summary>Builds a <see cref="Claude.Agent.CitationsConfiguration"/> for a test. Each parameter is the property of its name.</summary>
    public static CitationsConfiguration CitationsConfiguration(bool? enabled = null) =>
        new() { Enabled = enabled };

    /// <summary>Builds an <see cref="Claude.Agent.ImageTransformations"/> for a test. Each parameter is the property of its name.</summary>
    public static ImageTransformations ImageTransformations(ImageOversizedHandling? oversizedImage = null) =>
        new() { OversizedImage = oversizedImage };

    /// <summary>Builds a <see cref="Claude.Agent.CharacterLocationCitation"/> for a test. Each parameter is the property of its name.</summary>
    public static CharacterLocationCitation CharacterLocationCitation(
        string? citedText = null,
        long documentIndex = 0,
        long startCharacterIndex = 0,
        long endCharacterIndex = 0,
        string? documentTitle = null,
        string? fileId = null) =>
        new(citedText ?? string.Empty, documentIndex, startCharacterIndex, endCharacterIndex)
        {
            DocumentTitle = documentTitle,
            FileId = fileId,
        };

    /// <summary>Builds a <see cref="Claude.Agent.PageLocationCitation"/> for a test. Each parameter is the property of its name.</summary>
    public static PageLocationCitation PageLocationCitation(
        string? citedText = null,
        long documentIndex = 0,
        long startPageNumber = 0,
        long endPageNumber = 0,
        string? documentTitle = null,
        string? fileId = null) =>
        new(citedText ?? string.Empty, documentIndex, startPageNumber, endPageNumber)
        {
            DocumentTitle = documentTitle,
            FileId = fileId,
        };

    /// <summary>Builds a <see cref="Claude.Agent.ContentBlockLocationCitation"/> for a test. Each parameter is the property of its name.</summary>
    public static ContentBlockLocationCitation ContentBlockLocationCitation(
        string? citedText = null,
        long documentIndex = 0,
        long startBlockIndex = 0,
        long endBlockIndex = 0,
        string? documentTitle = null,
        string? fileId = null) =>
        new(citedText ?? string.Empty, documentIndex, startBlockIndex, endBlockIndex)
        {
            DocumentTitle = documentTitle,
            FileId = fileId,
        };

    /// <summary>Builds a <see cref="Claude.Agent.WebSearchResultLocationCitation"/> for a test. Each parameter is the property of its name.</summary>
    public static WebSearchResultLocationCitation WebSearchResultLocationCitation(
        string? citedText = null,
        string? encryptedIndex = null,
        string? url = null,
        string? title = null) =>
        new(citedText ?? string.Empty, encryptedIndex ?? string.Empty, url ?? string.Empty) { Title = title };

    /// <summary>Builds a <see cref="Claude.Agent.SearchResultLocationCitation"/> for a test. Each parameter is the property of its name.</summary>
    public static SearchResultLocationCitation SearchResultLocationCitation(
        string? citedText = null,
        long searchResultIndex = 0,
        string? source = null,
        long startBlockIndex = 0,
        long endBlockIndex = 0,
        string? title = null) =>
        new(citedText ?? string.Empty, searchResultIndex, source ?? string.Empty, startBlockIndex, endBlockIndex)
        {
            Title = title,
        };

    /// <summary>Builds an <see cref="Claude.Agent.UnknownCitation"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownCitation UnknownCitation(string? type = null, JsonElement raw = default) =>
        new(type ?? string.Empty, Object(raw));

    /// <summary>Builds a <see cref="Claude.Agent.DirectToolCaller"/> for a test.</summary>
    public static DirectToolCaller DirectToolCaller() => new();

    /// <summary>Builds a <see cref="Claude.Agent.CodeExecutionToolCaller20250825"/> for a test. Each parameter is the property of its name.</summary>
    public static CodeExecutionToolCaller20250825 CodeExecutionToolCaller20250825(string? toolId = null) =>
        new(toolId ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.CodeExecutionToolCaller20260120"/> for a test. Each parameter is the property of its name.</summary>
    public static CodeExecutionToolCaller20260120 CodeExecutionToolCaller20260120(string? toolId = null) =>
        new(toolId ?? string.Empty);

    /// <summary>Builds an <see cref="Claude.Agent.UnknownToolCaller"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownToolCaller UnknownToolCaller(string? type = null, JsonElement raw = default) =>
        new(type ?? string.Empty, Object(raw));

    /// <summary>Builds a <see cref="Claude.Agent.Base64ImageSource"/> for a test. Each parameter is the property of its name.</summary>
    public static Base64ImageSource Base64ImageSource(string? data = null, ImageMediaType mediaType = default) =>
        new(data ?? string.Empty, mediaType);

    /// <summary>Builds a <see cref="Claude.Agent.UrlImageSource"/> for a test. Each parameter is the property of its name.</summary>
    public static UrlImageSource UrlImageSource(string? url = null) => new(url ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.FileImageSource"/> for a test. Each parameter is the property of its name.</summary>
    public static FileImageSource FileImageSource(string? fileId = null) => new(fileId ?? string.Empty);

    /// <summary>Builds an <see cref="Claude.Agent.UnknownImageSource"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownImageSource UnknownImageSource(string? type = null, JsonElement raw = default) =>
        new(type ?? string.Empty, Object(raw));

    /// <summary>Builds a <see cref="Claude.Agent.Base64DocumentSource"/> for a test. Each parameter is the property of its name.</summary>
    public static Base64DocumentSource Base64DocumentSource(string? data = null, DocumentMediaType mediaType = default) =>
        new(data ?? string.Empty, mediaType);

    /// <summary>Builds a <see cref="Claude.Agent.TextDocumentSource"/> for a test. Each parameter is the property of its name.</summary>
    public static TextDocumentSource TextDocumentSource(string? data = null, DocumentMediaType mediaType = default) =>
        new(data ?? string.Empty, mediaType);

    /// <summary>Builds a <see cref="Claude.Agent.ContentDocumentSource"/> for a test. Each parameter is the property of its name.</summary>
    public static ContentDocumentSource ContentDocumentSource(IEnumerable<ContentBlock>? content = null) =>
        new([.. content ?? []]);

    /// <summary>Builds a <see cref="Claude.Agent.UrlDocumentSource"/> for a test. Each parameter is the property of its name.</summary>
    public static UrlDocumentSource UrlDocumentSource(string? url = null) => new(url ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.FileDocumentSource"/> for a test. Each parameter is the property of its name.</summary>
    public static FileDocumentSource FileDocumentSource(string? fileId = null) => new(fileId ?? string.Empty);

    /// <summary>Builds an <see cref="Claude.Agent.UnknownDocumentSource"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownDocumentSource UnknownDocumentSource(string? type = null, JsonElement raw = default) =>
        new(type ?? string.Empty, Object(raw));

    /// <summary>Builds a <see cref="Claude.Agent.BrowserTab"/> for a test. Each parameter is the property of its name.</summary>
    public static BrowserTab BrowserTab(
        string? tabId = null,
        string? title = null,
        string? url = null,
        bool? active = null) =>
        new(tabId ?? string.Empty, title ?? string.Empty, url ?? string.Empty) { Active = active };

    /// <summary>Builds a <see cref="Claude.Agent.TabOpenedBrowserStateChange"/> for a test. Each parameter is the property of its name.</summary>
    public static TabOpenedBrowserStateChange TabOpenedBrowserStateChange(string? tabId = null) =>
        new(tabId ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.DownloadStartedBrowserStateChange"/> for a test. Each parameter is the property of its name.</summary>
    public static DownloadStartedBrowserStateChange DownloadStartedBrowserStateChange(
        string? downloadId = null,
        string? url = null) =>
        new(downloadId ?? string.Empty, url ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.DownloadCompletedBrowserStateChange"/> for a test. Each parameter is the property of its name.</summary>
    public static DownloadCompletedBrowserStateChange DownloadCompletedBrowserStateChange(
        string? downloadId = null,
        string? url = null,
        string? path = null,
        long? sizeBytes = null) =>
        new(downloadId ?? string.Empty, url ?? string.Empty) { Path = path, SizeBytes = sizeBytes };

    /// <summary>Builds a <see cref="Claude.Agent.DownloadFailedBrowserStateChange"/> for a test. Each parameter is the property of its name.</summary>
    public static DownloadFailedBrowserStateChange DownloadFailedBrowserStateChange(
        string? downloadId = null,
        string? url = null,
        string? error = null) =>
        new(downloadId ?? string.Empty, url ?? string.Empty) { Error = error };

    /// <summary>Builds an <see cref="Claude.Agent.UnknownBrowserStateChange"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownBrowserStateChange UnknownBrowserStateChange(string? type = null, JsonElement raw = default) =>
        new(type ?? string.Empty, Object(raw));

    /// <summary>Builds a <see cref="Claude.Agent.WebSearchResult"/> for a test. Each parameter is the property of its name.</summary>
    public static WebSearchResult WebSearchResult(
        string? encryptedContent = null,
        string? title = null,
        string? url = null,
        string? pageAge = null) =>
        new(encryptedContent ?? string.Empty, title ?? string.Empty, url ?? string.Empty) { PageAge = pageAge };

    /// <summary>Builds a <see cref="Claude.Agent.WebSearchToolResultErrorContent"/> for a test. Each parameter is the property of its name.</summary>
    public static WebSearchToolResultErrorContent WebSearchToolResultErrorContent(WebSearchErrorCode errorCode = default) =>
        new(errorCode);

    /// <summary>Builds an <see cref="Claude.Agent.UnknownWebSearchToolResultContent"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownWebSearchToolResultContent UnknownWebSearchToolResultContent(
        string? type = null,
        JsonElement raw = default) =>
        new(type ?? string.Empty, Object(raw));

    /// <summary>Builds a <see cref="Claude.Agent.WebFetchResult"/> for a test. Each parameter is the property of its name.</summary>
    public static WebFetchResult WebFetchResult(
        DocumentBlock content,
        string? url = null,
        DateTimeOffset? retrievedAt = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new(content, url ?? string.Empty) { RetrievedAt = retrievedAt };
    }

    /// <summary>Builds a <see cref="Claude.Agent.WebFetchToolResultErrorContent"/> for a test. Each parameter is the property of its name.</summary>
    public static WebFetchToolResultErrorContent WebFetchToolResultErrorContent(WebFetchErrorCode errorCode = default) =>
        new(errorCode);

    /// <summary>Builds an <see cref="Claude.Agent.UnknownWebFetchToolResultContent"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownWebFetchToolResultContent UnknownWebFetchToolResultContent(
        string? type = null,
        JsonElement raw = default) =>
        new(type ?? string.Empty, Object(raw));

    /// <summary>Builds an <see cref="Claude.Agent.AdvisorResult"/> for a test. Each parameter is the property of its name.</summary>
    public static AdvisorResult AdvisorResult(string? text = null, StopReason? stopReason = null) =>
        new(text ?? string.Empty) { StopReason = stopReason };

    /// <summary>Builds an <see cref="Claude.Agent.AdvisorRedactedResult"/> for a test. Each parameter is the property of its name.</summary>
    public static AdvisorRedactedResult AdvisorRedactedResult(
        string? encryptedContent = null,
        StopReason? stopReason = null) =>
        new(encryptedContent ?? string.Empty) { StopReason = stopReason };

    /// <summary>Builds an <see cref="Claude.Agent.AdvisorToolResultErrorContent"/> for a test. Each parameter is the property of its name.</summary>
    public static AdvisorToolResultErrorContent AdvisorToolResultErrorContent(AdvisorErrorCode errorCode = default) =>
        new(errorCode);

    /// <summary>Builds an <see cref="Claude.Agent.UnknownAdvisorToolResultContent"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownAdvisorToolResultContent UnknownAdvisorToolResultContent(
        string? type = null,
        JsonElement raw = default) =>
        new(type ?? string.Empty, Object(raw));

    /// <summary>Builds a <see cref="Claude.Agent.CodeExecutionOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static CodeExecutionOutput CodeExecutionOutput(string? fileId = null) => new(fileId ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.CodeExecutionResult"/> for a test. Each parameter is the property of its name.</summary>
    public static CodeExecutionResult CodeExecutionResult(
        IEnumerable<CodeExecutionOutput>? content = null,
        long returnCode = 0,
        string? standardError = null,
        string? standardOutput = null) =>
        new([.. content ?? []], returnCode, standardError ?? string.Empty, standardOutput ?? string.Empty);

    /// <summary>Builds an <see cref="Claude.Agent.EncryptedCodeExecutionResult"/> for a test. Each parameter is the property of its name.</summary>
    public static EncryptedCodeExecutionResult EncryptedCodeExecutionResult(
        IEnumerable<CodeExecutionOutput>? content = null,
        string? encryptedStandardOutput = null,
        long returnCode = 0,
        string? standardError = null) =>
        new([.. content ?? []], encryptedStandardOutput ?? string.Empty, returnCode, standardError ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.CodeExecutionToolResultErrorContent"/> for a test. Each parameter is the property of its name.</summary>
    public static CodeExecutionToolResultErrorContent CodeExecutionToolResultErrorContent(
        CodeExecutionErrorCode errorCode = default) =>
        new(errorCode);

    /// <summary>Builds an <see cref="Claude.Agent.UnknownCodeExecutionToolResultContent"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownCodeExecutionToolResultContent UnknownCodeExecutionToolResultContent(
        string? type = null,
        JsonElement raw = default) =>
        new(type ?? string.Empty, Object(raw));

    /// <summary>Builds a <see cref="Claude.Agent.BashCodeExecutionOutput"/> for a test. Each parameter is the property of its name.</summary>
    public static BashCodeExecutionOutput BashCodeExecutionOutput(string? fileId = null) => new(fileId ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.BashCodeExecutionResult"/> for a test. Each parameter is the property of its name.</summary>
    public static BashCodeExecutionResult BashCodeExecutionResult(
        IEnumerable<BashCodeExecutionOutput>? content = null,
        long returnCode = 0,
        string? standardError = null,
        string? standardOutput = null) =>
        new([.. content ?? []], returnCode, standardError ?? string.Empty, standardOutput ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.BashCodeExecutionToolResultErrorContent"/> for a test. Each parameter is the property of its name.</summary>
    public static BashCodeExecutionToolResultErrorContent BashCodeExecutionToolResultErrorContent(
        BashCodeExecutionErrorCode errorCode = default) =>
        new(errorCode);

    /// <summary>Builds an <see cref="Claude.Agent.UnknownBashCodeExecutionToolResultContent"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownBashCodeExecutionToolResultContent UnknownBashCodeExecutionToolResultContent(
        string? type = null,
        JsonElement raw = default) =>
        new(type ?? string.Empty, Object(raw));

    /// <summary>Builds a <see cref="Claude.Agent.TextEditorCodeExecutionViewResult"/> for a test. Each parameter is the property of its name.</summary>
    public static TextEditorCodeExecutionViewResult TextEditorCodeExecutionViewResult(
        string? content = null,
        TextEditorFileType fileType = default,
        long? lineCount = null,
        long? startLine = null,
        long? totalLines = null) =>
        new(content ?? string.Empty, fileType)
        {
            LineCount = lineCount,
            StartLine = startLine,
            TotalLines = totalLines,
        };

    /// <summary>Builds a <see cref="Claude.Agent.TextEditorCodeExecutionCreateResult"/> for a test. Each parameter is the property of its name.</summary>
    public static TextEditorCodeExecutionCreateResult TextEditorCodeExecutionCreateResult(bool isFileUpdate = false) =>
        new(isFileUpdate);

    /// <summary>Builds a <see cref="Claude.Agent.TextEditorCodeExecutionStringReplaceResult"/> for a test. Each parameter is the property of its name.</summary>
    public static TextEditorCodeExecutionStringReplaceResult TextEditorCodeExecutionStringReplaceResult(
        IEnumerable<string>? lines = null,
        long? newLineCount = null,
        long? newStartLine = null,
        long? oldLineCount = null,
        long? oldStartLine = null) =>
        new([.. lines ?? []])
        {
            NewLineCount = newLineCount,
            NewStartLine = newStartLine,
            OldLineCount = oldLineCount,
            OldStartLine = oldStartLine,
        };

    /// <summary>Builds a <see cref="Claude.Agent.TextEditorCodeExecutionToolResultErrorContent"/> for a test. Each parameter is the property of its name.</summary>
    public static TextEditorCodeExecutionToolResultErrorContent TextEditorCodeExecutionToolResultErrorContent(
        TextEditorCodeExecutionErrorCode errorCode = default,
        string? errorMessage = null) =>
        new(errorCode) { ErrorMessage = errorMessage };

    /// <summary>Builds an <see cref="Claude.Agent.UnknownTextEditorCodeExecutionToolResultContent"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownTextEditorCodeExecutionToolResultContent UnknownTextEditorCodeExecutionToolResultContent(
        string? type = null,
        JsonElement raw = default) =>
        new(type ?? string.Empty, Object(raw));

    /// <summary>Builds a <see cref="Claude.Agent.ToolSearchToolSearchResult"/> for a test. Each parameter is the property of its name.</summary>
    public static ToolSearchToolSearchResult ToolSearchToolSearchResult(IEnumerable<ToolReferenceBlock>? toolReferences = null) =>
        new([.. toolReferences ?? []]);

    /// <summary>Builds a <see cref="Claude.Agent.ToolSearchToolResultErrorContent"/> for a test. Each parameter is the property of its name.</summary>
    public static ToolSearchToolResultErrorContent ToolSearchToolResultErrorContent(
        ToolSearchErrorCode errorCode = default,
        string? errorMessage = null) =>
        new(errorCode) { ErrorMessage = errorMessage };

    /// <summary>Builds an <see cref="Claude.Agent.UnknownToolSearchToolResultContent"/> for a test. Each parameter is the property of its name. <c>raw</c> defaults to an empty object.</summary>
    public static UnknownToolSearchToolResultContent UnknownToolSearchToolResultContent(
        string? type = null,
        JsonElement raw = default) =>
        new(type ?? string.Empty, Object(raw));

    /// <summary>Builds a <see cref="Claude.Agent.FallbackModel"/> for a test. Each parameter is the property of its name.</summary>
    public static FallbackModel FallbackModel(string? model = null) => new(model ?? string.Empty);

    /// <summary>Builds a <see cref="Claude.Agent.FallbackTrigger"/> for a test. Each parameter is the property of its name.</summary>
    public static FallbackTrigger FallbackTrigger(RefusalCategory? category = null) =>
        new() { Category = category };
}
