using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace Claude.Agent;

// What the tools the API runs itself hand back. Each tool has a set of its own: the result it gives,
// and the part that says why it gave none. The wire writes one object for every tool but the web
// search, which writes an array of results or one error object; each set is read as a list, so a
// single object is one part of it. A part of a kind this library has no type for, or one whose
// members do not fit its kind's type, is the unknown of its set; content that is neither an object
// nor an array is a misfit, and the block carrying it becomes the unknown block.
internal static class ServerToolResultParser
{
    public static IReadOnlyList<WebSearchToolResultContent>? WebSearch(JsonElement content) =>
        TryParts<WebSearchToolResultContent>(
            content,
            WebSearchPart,
            (type, raw) => new UnknownWebSearchToolResultContent(type, raw),
            out var parts)
            ? parts
            : null;

    private static WebSearchToolResultContent? WebSearchPart(string type, JsonElement part) => type switch
    {
        "web_search_result" => part.StringOrNull("encrypted_content") is { } encrypted
            && part.StringOrNull("title") is { } title
            && part.StringOrNull("url") is { } url
                ? new WebSearchResult(encrypted, title, url) { PageAge = part.StringOrNull("page_age") }
                : (WebSearchToolResultContent?)null,
        "web_search_tool_result_error" => part.StringOrNull("error_code") is { } code
            ? new WebSearchToolResultErrorContent(new WebSearchErrorCode(code))
            : null,
        _ => null,
    };

    public static IReadOnlyList<WebFetchToolResultContent>? WebFetch(JsonElement content, string line, string path) =>
        TryParts<WebFetchToolResultContent>(
            content,
            (type, part) => WebFetchPart(type, part, line, path),
            (type, raw) => new UnknownWebFetchToolResultContent(type, raw),
            out var parts)
            ? parts
            : null;

    private static WebFetchToolResultContent? WebFetchPart(string type, JsonElement part, string line, string path) => type switch
    {
        "web_fetch_result" => part.TryProperty("content", out var document)
            && ContentBlockParser.Document(document, line, path) is { } fetched
            && part.StringOrNull("url") is { } url
                ? new WebFetchResult(fetched, url) { RetrievedAt = Instant(part.StringOrNull("retrieved_at")) }
                : (WebFetchToolResultContent?)null,
        "web_fetch_tool_result_error" => part.StringOrNull("error_code") is { } code
            ? new WebFetchToolResultErrorContent(new WebFetchErrorCode(code))
            : null,
        _ => null,
    };

    public static IReadOnlyList<AdvisorToolResultContent>? Advisor(JsonElement content) =>
        TryParts<AdvisorToolResultContent>(
            content,
            AdvisorPart,
            (type, raw) => new UnknownAdvisorToolResultContent(type, raw),
            out var parts)
            ? parts
            : null;

    private static AdvisorToolResultContent? AdvisorPart(string type, JsonElement part) => type switch
    {
        "advisor_result" => part.StringOrNull("text") is { } text
            ? new AdvisorResult(text) { StopReason = Stopped(part) }
            : (AdvisorToolResultContent?)null,
        "advisor_redacted_result" => part.StringOrNull("encrypted_content") is { } encrypted
            ? new AdvisorRedactedResult(encrypted) { StopReason = Stopped(part) }
            : null,
        "advisor_tool_result_error" => part.StringOrNull("error_code") is { } code
            ? new AdvisorToolResultErrorContent(new AdvisorErrorCode(code))
            : null,
        _ => null,
    };

    public static IReadOnlyList<CodeExecutionToolResultContent>? CodeExecution(JsonElement content) =>
        TryParts<CodeExecutionToolResultContent>(
            content,
            CodeExecutionPart,
            (type, raw) => new UnknownCodeExecutionToolResultContent(type, raw),
            out var parts)
            ? parts
            : null;

    private static CodeExecutionToolResultContent? CodeExecutionPart(string type, JsonElement part) => type switch
    {
        "code_execution_result" => TryOutputs(part, file => new CodeExecutionOutput(file), out var outputs)
            && part.Int64OrNull("return_code") is { } code
            && part.StringOrNull("stderr") is { } standardError
            && part.StringOrNull("stdout") is { } standardOutput
                ? new CodeExecutionResult(outputs, code, standardError, standardOutput)
                : (CodeExecutionToolResultContent?)null,
        "encrypted_code_execution_result" => TryOutputs(part, file => new CodeExecutionOutput(file), out var kept)
            && part.StringOrNull("encrypted_stdout") is { } encrypted
            && part.Int64OrNull("return_code") is { } keptCode
            && part.StringOrNull("stderr") is { } keptError
                ? new EncryptedCodeExecutionResult(kept, encrypted, keptCode, keptError)
                : null,
        "code_execution_tool_result_error" => part.StringOrNull("error_code") is { } failure
            ? new CodeExecutionToolResultErrorContent(new CodeExecutionErrorCode(failure))
            : null,
        _ => null,
    };

    public static IReadOnlyList<BashCodeExecutionToolResultContent>? BashCodeExecution(JsonElement content) =>
        TryParts<BashCodeExecutionToolResultContent>(
            content,
            BashCodeExecutionPart,
            (type, raw) => new UnknownBashCodeExecutionToolResultContent(type, raw),
            out var parts)
            ? parts
            : null;

    private static BashCodeExecutionToolResultContent? BashCodeExecutionPart(string type, JsonElement part) => type switch
    {
        "bash_code_execution_result" => TryOutputs(part, file => new BashCodeExecutionOutput(file), out var outputs)
            && part.Int64OrNull("return_code") is { } code
            && part.StringOrNull("stderr") is { } standardError
            && part.StringOrNull("stdout") is { } standardOutput
                ? new BashCodeExecutionResult(outputs, code, standardError, standardOutput)
                : (BashCodeExecutionToolResultContent?)null,
        "bash_code_execution_tool_result_error" => part.StringOrNull("error_code") is { } failure
            ? new BashCodeExecutionToolResultErrorContent(new BashCodeExecutionErrorCode(failure))
            : null,
        _ => null,
    };

    public static IReadOnlyList<TextEditorCodeExecutionToolResultContent>? TextEditorCodeExecution(JsonElement content) =>
        TryParts<TextEditorCodeExecutionToolResultContent>(
            content,
            TextEditorCodeExecutionPart,
            (type, raw) => new UnknownTextEditorCodeExecutionToolResultContent(type, raw),
            out var parts)
            ? parts
            : null;

    private static TextEditorCodeExecutionToolResultContent? TextEditorCodeExecutionPart(string type, JsonElement part) => type switch
    {
        "text_editor_code_execution_view_result" => part.StringOrNull("content") is { } text
            && part.StringOrNull("file_type") is { } fileType
                ? new TextEditorCodeExecutionViewResult(text, new TextEditorFileType(fileType))
                {
                    LineCount = part.Int64OrNull("num_lines"),
                    StartLine = part.Int64OrNull("start_line"),
                    TotalLines = part.Int64OrNull("total_lines"),
                }
                : (TextEditorCodeExecutionToolResultContent?)null,
        "text_editor_code_execution_create_result" => part.BoolOrNull("is_file_update") is { } update
            ? new TextEditorCodeExecutionCreateResult(update)
            : null,
        "text_editor_code_execution_str_replace_result" => TryLines(part, out var lines)
            ? new TextEditorCodeExecutionStringReplaceResult(lines)
            {
                NewLineCount = part.Int64OrNull("new_lines"),
                NewStartLine = part.Int64OrNull("new_start"),
                OldLineCount = part.Int64OrNull("old_lines"),
                OldStartLine = part.Int64OrNull("old_start"),
            }
            : null,
        "text_editor_code_execution_tool_result_error" => part.StringOrNull("error_code") is { } failure
            ? new TextEditorCodeExecutionToolResultErrorContent(new TextEditorCodeExecutionErrorCode(failure))
            {
                ErrorMessage = part.StringOrNull("error_message"),
            }
            : null,
        _ => null,
    };

    public static IReadOnlyList<ToolSearchToolResultContent>? ToolSearch(JsonElement content) =>
        TryParts<ToolSearchToolResultContent>(
            content,
            ToolSearchPart,
            (type, raw) => new UnknownToolSearchToolResultContent(type, raw),
            out var parts)
            ? parts
            : null;

    private static ToolSearchToolResultContent? ToolSearchPart(string type, JsonElement part) => type switch
    {
        "tool_search_tool_search_result" => TryReferences(part, out var references)
            ? new ToolSearchToolSearchResult(references)
            : (ToolSearchToolResultContent?)null,
        "tool_search_tool_result_error" => part.StringOrNull("error_code") is { } failure
            ? new ToolSearchToolResultErrorContent(new ToolSearchErrorCode(failure))
            {
                ErrorMessage = part.StringOrNull("error_message"),
            }
            : null,
        _ => null,
    };

    // The one set whose values the API declares as a bare string: the advisor's stop reason is the
    // message's set, so it is read into the same struct.
    private static StopReason? Stopped(JsonElement part) =>
        part.StringOrNull("stop_reason") is { } reason ? new StopReason(reason) : (StopReason?)null;

    private static DateTimeOffset? Instant(string? value) =>
        value is not null
        && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instant)
            ? instant
            : null;

    private static bool TryLines(JsonElement part, out IReadOnlyList<string> lines)
    {
        lines = [];
        if (!part.TryProperty("lines", out var value))
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            builder.Add(item.GetString() ?? string.Empty);
        }

        lines = builder.ToImmutable();
        return true;
    }

    private static bool TryReferences(JsonElement part, out IReadOnlyList<ToolReferenceBlock> references)
    {
        references = [];
        if (!part.TryProperty("tool_references", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<ToolReferenceBlock>();
        foreach (var reference in value.EnumerateArray())
        {
            if (reference.ValueKind != JsonValueKind.Object
                || reference.StringOrNull("tool_name") is not { } name
                || !ContentBlockPartParser.TryCacheControl(reference, out var cacheControl))
            {
                return false;
            }

            builder.Add(new ToolReferenceBlock(name) { CacheControl = cacheControl });
        }

        references = builder.ToImmutable();
        return true;
    }

    private static bool TryOutputs<TOutput>(JsonElement part, Func<string, TOutput> output, out IReadOnlyList<TOutput> outputs)
    {
        outputs = [];
        if (!part.TryProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<TOutput>();
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || item.StringOrNull("file_id") is not { } file)
            {
                return false;
            }

            builder.Add(output(file));
        }

        outputs = builder.ToImmutable();
        return true;
    }

    private static bool TryParts<TContent>(
        JsonElement content,
        Func<string, JsonElement, TContent?> read,
        Func<string, JsonElement, TContent> unknown,
        out IReadOnlyList<TContent> parts)
        where TContent : class
    {
        parts = [];
        if (content.ValueKind == JsonValueKind.Object)
        {
            if (Part(content, read, unknown) is not { } only)
            {
                return false;
            }

            parts = ImmutableArray.Create(only);
            return true;
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<TContent>();
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || Part(item, read, unknown) is not { } part)
            {
                return false;
            }

            builder.Add(part);
        }

        parts = builder.ToImmutable();
        return true;
    }

    private static TContent? Part<TContent>(
        JsonElement part,
        Func<string, JsonElement, TContent?> read,
        Func<string, JsonElement, TContent> unknown)
        where TContent : class =>
        part.StringOrNull("type") is { } type ? read(type, part) ?? unknown(type, part.Clone()) : null;
}
