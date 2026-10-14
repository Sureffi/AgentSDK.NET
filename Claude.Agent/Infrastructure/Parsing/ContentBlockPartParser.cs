using System.Collections.Immutable;
using System.Text.Json;

namespace Claude.Agent;

// The parts a content block is made of: the discriminated sets nested in the blocks, and the small
// objects they carry. A part of a kind this library has no type for, or one whose members do not fit
// its kind's type, is the unknown of its own set. A part that is not an object where one is declared
// is a misfit: the reader says so and the block carrying it becomes the unknown block.
internal static class ContentBlockPartParser
{
    public static bool TryCacheControl(JsonElement block, out CacheControl? cacheControl)
    {
        cacheControl = null;
        if (!block.TryProperty("cache_control", out var value))
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        cacheControl = new CacheControl
        {
            TimeToLive = value.StringOrNull("ttl") is { } ttl ? new CacheControlTimeToLive(ttl) : (CacheControlTimeToLive?)null,
        };
        return true;
    }

    public static bool TryCitationsConfiguration(JsonElement block, out CitationsConfiguration? citations)
    {
        citations = null;
        if (!block.TryProperty("citations", out var value))
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        citations = new CitationsConfiguration { Enabled = value.BoolOrNull("enabled") };
        return true;
    }

    public static bool TryImageTransformations(JsonElement block, out ImageTransformations? transformations)
    {
        transformations = null;
        if (!block.TryProperty("transformations", out var value))
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        transformations = new ImageTransformations
        {
            OversizedImage = value.StringOrNull("oversized_image") is { } handling
                ? new ImageOversizedHandling(handling)
                : (ImageOversizedHandling?)null,
        };
        return true;
    }

    public static bool TryCaller(JsonElement block, out ToolCaller? caller)
    {
        caller = null;
        if (!block.TryProperty("caller", out var value))
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Object || value.StringOrNull("type") is not { } type)
        {
            return false;
        }

        caller = Caller(type, value) ?? new UnknownToolCaller(type, value.Clone());
        return true;
    }

    private static ToolCaller? Caller(string type, JsonElement value) => type switch
    {
        "direct" => new DirectToolCaller(),
        "code_execution_20250825" => value.StringOrNull("tool_id") is { } early
            ? new CodeExecutionToolCaller20250825(early)
            : (ToolCaller?)null,
        "code_execution_20260120" => value.StringOrNull("tool_id") is { } late
            ? new CodeExecutionToolCaller20260120(late)
            : null,
        _ => null,
    };

    public static bool TryCitations(JsonElement block, out IReadOnlyList<Citation> citations)
    {
        citations = [];
        if (!block.TryProperty("citations", out var value))
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<Citation>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || item.StringOrNull("type") is not { } type)
            {
                return false;
            }

            builder.Add(Citation(type, item) ?? new UnknownCitation(type, item.Clone()));
        }

        citations = builder.ToImmutable();
        return true;
    }

    private static Citation? Citation(string type, JsonElement citation) => type switch
    {
        "char_location" => (Citation?)CharacterLocation(citation),
        "page_location" => PageLocation(citation),
        "content_block_location" => ContentBlockLocation(citation),
        "web_search_result_location" => WebSearchResultLocation(citation),
        "search_result_location" => SearchResultLocation(citation),
        _ => null,
    };

    private static CharacterLocationCitation? CharacterLocation(JsonElement citation) =>
        citation.StringOrNull("cited_text") is { } text
        && citation.Int64OrNull("document_index") is { } document
        && citation.Int64OrNull("start_char_index") is { } start
        && citation.Int64OrNull("end_char_index") is { } end
            ? new CharacterLocationCitation(text, document, start, end)
            {
                DocumentTitle = citation.StringOrNull("document_title"),
                FileId = citation.StringOrNull("file_id"),
            }
            : null;

    private static PageLocationCitation? PageLocation(JsonElement citation) =>
        citation.StringOrNull("cited_text") is { } text
        && citation.Int64OrNull("document_index") is { } document
        && citation.Int64OrNull("start_page_number") is { } start
        && citation.Int64OrNull("end_page_number") is { } end
            ? new PageLocationCitation(text, document, start, end)
            {
                DocumentTitle = citation.StringOrNull("document_title"),
                FileId = citation.StringOrNull("file_id"),
            }
            : null;

    private static ContentBlockLocationCitation? ContentBlockLocation(JsonElement citation) =>
        citation.StringOrNull("cited_text") is { } text
        && citation.Int64OrNull("document_index") is { } document
        && citation.Int64OrNull("start_block_index") is { } start
        && citation.Int64OrNull("end_block_index") is { } end
            ? new ContentBlockLocationCitation(text, document, start, end)
            {
                DocumentTitle = citation.StringOrNull("document_title"),
                FileId = citation.StringOrNull("file_id"),
            }
            : null;

    private static WebSearchResultLocationCitation? WebSearchResultLocation(JsonElement citation) =>
        citation.StringOrNull("cited_text") is { } text
        && citation.StringOrNull("encrypted_index") is { } index
        && citation.StringOrNull("url") is { } url
            ? new WebSearchResultLocationCitation(text, index, url) { Title = citation.StringOrNull("title") }
            : null;

    private static SearchResultLocationCitation? SearchResultLocation(JsonElement citation) =>
        citation.StringOrNull("cited_text") is { } text
        && citation.Int64OrNull("search_result_index") is { } index
        && citation.StringOrNull("source") is { } source
        && citation.Int64OrNull("start_block_index") is { } start
        && citation.Int64OrNull("end_block_index") is { } end
            ? new SearchResultLocationCitation(text, index, source, start, end) { Title = citation.StringOrNull("title") }
            : null;

    public static ImageSource? ImageSource(JsonElement source)
    {
        if (source.ValueKind != JsonValueKind.Object || source.StringOrNull("type") is not { } type)
        {
            return null;
        }

        return type switch
        {
            "base64" => source.StringOrNull("data") is { } data && source.StringOrNull("media_type") is { } media
                ? new Base64ImageSource(data, new ImageMediaType(media))
                : (ImageSource?)null,
            "url" => source.StringOrNull("url") is { } url ? new UrlImageSource(url) : null,
            "file" => source.StringOrNull("file_id") is { } file ? new FileImageSource(file) : null,
            _ => null,
        } ?? new UnknownImageSource(type, source.Clone());
    }

    public static DocumentSource? DocumentSource(JsonElement source, string line, string path)
    {
        if (source.ValueKind != JsonValueKind.Object || source.StringOrNull("type") is not { } type)
        {
            return null;
        }

        return type switch
        {
            "base64" => source.StringOrNull("data") is { } data && source.StringOrNull("media_type") is { } media
                ? new Base64DocumentSource(data, new DocumentMediaType(media))
                : (DocumentSource?)null,
            "text" => source.StringOrNull("data") is { } text && source.StringOrNull("media_type") is { } plain
                ? new TextDocumentSource(text, new DocumentMediaType(plain))
                : null,
            "content" => Content(source, line, path),
            "url" => source.StringOrNull("url") is { } url ? new UrlDocumentSource(url) : null,
            "file" => source.StringOrNull("file_id") is { } file ? new FileDocumentSource(file) : null,
            _ => null,
        } ?? new UnknownDocumentSource(type, source.Clone());
    }

    private static ContentDocumentSource? Content(JsonElement source, string line, string path)
    {
        if (!source.TryProperty("content", out var content)
            || !ContentBlockParser.ParseContent(content, line, path).TryGet(out var blocks, out _))
        {
            return null;
        }

        return new ContentDocumentSource(blocks);
    }

    public static bool TryTabs(JsonElement value, out IReadOnlyList<BrowserTab> tabs)
    {
        tabs = [];
        if (value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<BrowserTab>();
        foreach (var tab in value.EnumerateArray())
        {
            if (tab.ValueKind != JsonValueKind.Object
                || tab.StringOrNull("tab_id") is not { } id
                || tab.StringOrNull("title") is not { } title
                || tab.StringOrNull("url") is not { } url)
            {
                return false;
            }

            builder.Add(new BrowserTab(id, title, url) { Active = tab.BoolOrNull("active") });
        }

        tabs = builder.ToImmutable();
        return true;
    }

    public static bool TryStateChanges(JsonElement block, out IReadOnlyList<BrowserStateChange> changes)
    {
        changes = [];
        if (!block.TryProperty("state_changes", out var value))
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<BrowserStateChange>();
        foreach (var change in value.EnumerateArray())
        {
            if (change.ValueKind != JsonValueKind.Object || change.StringOrNull("type") is not { } type)
            {
                return false;
            }

            builder.Add(StateChange(type, change) ?? new UnknownBrowserStateChange(type, change.Clone()));
        }

        changes = builder.ToImmutable();
        return true;
    }

    private static BrowserStateChange? StateChange(string type, JsonElement change)
    {
        var download = change.StringOrNull("download_id");
        var url = change.StringOrNull("url");
        var named = download is not null && url is not null;

        return type switch
        {
            "tab_opened" => change.StringOrNull("tab_id") is { } tab
                ? new TabOpenedBrowserStateChange(tab)
                : (BrowserStateChange?)null,
            "download_started" => named ? new DownloadStartedBrowserStateChange(download!, url!) : null,
            "download_completed" => named
                ? new DownloadCompletedBrowserStateChange(download!, url!)
                {
                    Path = change.StringOrNull("path"),
                    SizeBytes = change.Int64OrNull("size_bytes"),
                }
                : null,
            "download_failed" => named
                ? new DownloadFailedBrowserStateChange(download!, url!) { Error = change.StringOrNull("error") }
                : null,
            _ => null,
        };
    }

    public static FallbackModel? FallbackModel(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object && value.StringOrNull("model") is { } model
            ? new FallbackModel(model)
            : null;

    public static FallbackTrigger? FallbackTrigger(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object
            ? new FallbackTrigger
            {
                Category = value.StringOrNull("category") is { } category
                    ? new RefusalCategory(category)
                    : (RefusalCategory?)null,
            }
            : null;
}
