using System.Collections.Immutable;
using System.Text.Json;

namespace Claude.Agent;

// The content block set: one type per kind the wire names, across the assistant side, the user side
// and the content nested in a tool result. A block of a kind this library has no type for, or one
// whose members do not fit its kind's type, is the unknown block, with the kind's name and the block
// as written. Nothing is skipped and nothing is thrown; only an element that is not a block at all
// fails the content, and so the message.
internal static class ContentBlockParser
{
    // A message's content where the wire allows a string or blocks: a string is one text block.
    public static Result<IReadOnlyList<ContentBlock>, ParseError> ParseContent(JsonElement content, string line, string path) =>
        content.ValueKind switch
        {
            JsonValueKind.String => ImmutableArray.Create<ContentBlock>(new TextBlock(content.GetString() ?? string.Empty)),
            JsonValueKind.Array => ParseBlocks(content, line, path),
            _ => JsonReading.Invalid(path, "expected a string or an array.", line),
        };

    // Every element is a block: an object with a string type. A block of a kind without a type here,
    // or one that does not fit its kind's type, is the unknown block; an element that is not a block
    // at all fails the content, and so the message.
    public static Result<IReadOnlyList<ContentBlock>, ParseError> ParseBlocks(JsonElement blocks, string line, string path)
    {
        if (blocks.ValueKind != JsonValueKind.Array)
        {
            return JsonReading.Invalid(path, "expected an array.", line);
        }

        return ParseBlocks([.. blocks.EnumerateArray()], line, path);
    }

    public static Result<IReadOnlyList<ContentBlock>, ParseError> ParseBlocks(IReadOnlyList<JsonElement> blocks, string line, string path)
    {
        var builder = ImmutableArray.CreateBuilder<ContentBlock>(blocks.Count);
        foreach (var block in blocks)
        {
            if (!Parse(block, line, path).TryGet(out var parsed, out var error))
            {
                return error;
            }

            builder.Add(parsed);
        }

        return builder.MoveToImmutable();
    }

    public static Result<ContentBlock, ParseError> Parse(JsonElement block, string line, string path)
    {
        if (block.ValueKind != JsonValueKind.Object)
        {
            return JsonReading.Invalid(path + "[]", "expected an object.", line);
        }

        if (!block.RequireString("type", line, path + "[].type").TryGet(out var type, out var typeError))
        {
            return typeError;
        }

        var typed = type switch
        {
            "text" => (ContentBlock?)Text(block),
            "thinking" => Thinking(block),
            "redacted_thinking" => RedactedThinking(block),
            "tool_use" => ToolUse(block),
            "tool_result" => ToolResult(block, line, path),
            "image" => Image(block),
            "document" => Document(block, line, path),
            "search_result" => SearchResult(block, line, path),
            "tool_reference" => ToolReference(block),
            "browser_state" => BrowserState(block),
            "server_tool_use" => ServerToolUse(block),
            "web_search_tool_result" => WebSearchToolResult(block),
            "web_fetch_tool_result" => WebFetchToolResult(block, line, path),
            "advisor_tool_result" => AdvisorToolResult(block),
            "code_execution_tool_result" => CodeExecutionToolResult(block),
            "bash_code_execution_tool_result" => BashCodeExecutionToolResult(block),
            "text_editor_code_execution_tool_result" => TextEditorCodeExecutionToolResult(block),
            "tool_search_tool_result" => ToolSearchToolResult(block),
            "mcp_tool_use" => McpToolUse(block),
            "mcp_tool_result" => McpToolResult(block, line, path),
            "container_upload" => ContainerUpload(block),
            "compaction" => Compaction(block),
            "fallback" => Fallback(block),
            _ => null,
        };

        return typed ?? new UnknownContentBlock(type, block.Clone());
    }

    private static TextBlock? Text(JsonElement block)
    {
        if (!block.TryProperty("text", out var text)
            || text.ValueKind != JsonValueKind.String
            || !ContentBlockPartParser.TryCitations(block, out var citations)
            || !ContentBlockPartParser.TryCacheControl(block, out var cacheControl))
        {
            return null;
        }

        return new TextBlock(text.GetString() ?? string.Empty, citations) { CacheControl = cacheControl };
    }

    private static ThinkingBlock? Thinking(JsonElement block) =>
        block.StringOrNull("thinking") is { } thinking && block.StringOrNull("signature") is { } signature
            ? new ThinkingBlock(thinking, signature)
            : null;

    private static RedactedThinkingBlock? RedactedThinking(JsonElement block) =>
        block.StringOrNull("data") is { } data ? new RedactedThinkingBlock(data) : null;

    private static ToolUseBlock? ToolUse(JsonElement block)
    {
        if (block.ToolUseIdOrNull("id") is not { } id
            || block.ToolNameOrNull("name") is not { } name
            || block.ElementOrNull("input") is not { } input
            || !ContentBlockPartParser.TryCaller(block, out var caller)
            || !ContentBlockPartParser.TryCacheControl(block, out var cacheControl))
        {
            return null;
        }

        return new ToolUseBlock(id, name, ToolTypes.ReadInput(name, input), input)
        {
            Caller = caller,
            ToolsetName = block.StringOrNull("toolset_name"),
            CacheControl = cacheControl,
        };
    }

    private static ServerToolUseBlock? ServerToolUse(JsonElement block)
    {
        if (block.ToolUseIdOrNull("id") is not { } id
            || block.StringOrNull("name") is not { } name
            || block.ElementOrNull("input") is not { } input
            || !ContentBlockPartParser.TryCaller(block, out var caller)
            || !ContentBlockPartParser.TryCacheControl(block, out var cacheControl))
        {
            return null;
        }

        return new ServerToolUseBlock(id, new ServerToolName(name), input)
        {
            Caller = caller,
            CacheControl = cacheControl,
        };
    }

    private static McpToolUseBlock? McpToolUse(JsonElement block) =>
        block.ToolUseIdOrNull("id") is { } id
        && block.ToolNameOrNull("name") is { } name
        && block.StringOrNull("server_name") is { } server
        && block.ElementOrNull("input") is { } input
            ? new McpToolUseBlock(id, name, server, input)
            : null;

    private static ToolResultBlock? ToolResult(JsonElement block, string line, string path)
    {
        if (block.ToolUseIdOrNull("tool_use_id") is not { } id
            || !ContentBlockPartParser.TryCacheControl(block, out var cacheControl))
        {
            return null;
        }

        IReadOnlyList<ContentBlock> content = [];
        if (block.TryProperty("content", out var value))
        {
            if (!ParseContent(value, line, path + "[].content").TryGet(out var parsed, out _))
            {
                return null;
            }

            content = parsed;
        }

        bool? isError = null;
        if (block.TryProperty("is_error", out var flag))
        {
            if (flag.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            {
                return null;
            }

            isError = flag.ValueKind == JsonValueKind.True;
        }

        return new ToolResultBlock(id, content, isError)
        {
            ToolsetName = block.StringOrNull("toolset_name"),
            CacheControl = cacheControl,
        };
    }

    private static McpToolResultBlock? McpToolResult(JsonElement block, string line, string path)
    {
        if (block.ToolUseIdOrNull("tool_use_id") is not { } id
            || block.BoolOrNull("is_error") is not { } isError
            || !block.TryProperty("content", out var value)
            || !ParseContent(value, line, path + "[].content").TryGet(out var content, out _))
        {
            return null;
        }

        return new McpToolResultBlock(id, content, isError);
    }

    private static ImageBlock? Image(JsonElement block)
    {
        if (!block.TryProperty("source", out var source)
            || ContentBlockPartParser.ImageSource(source) is not { } read
            || !ContentBlockPartParser.TryImageTransformations(block, out var transformations)
            || !ContentBlockPartParser.TryCacheControl(block, out var cacheControl))
        {
            return null;
        }

        return new ImageBlock(read) { Transformations = transformations, CacheControl = cacheControl };
    }

    // A document block also stands on its own inside a web fetch result, so the set of server tool
    // results reads it here.
    public static DocumentBlock? Document(JsonElement block, string line, string path)
    {
        if (block.ValueKind != JsonValueKind.Object
            || !block.TryProperty("source", out var source)
            || ContentBlockPartParser.DocumentSource(source, line, path) is not { } read
            || !ContentBlockPartParser.TryCitationsConfiguration(block, out var citations)
            || !ContentBlockPartParser.TryCacheControl(block, out var cacheControl))
        {
            return null;
        }

        return new DocumentBlock(read)
        {
            Title = block.StringOrNull("title"),
            Context = block.StringOrNull("context"),
            Citations = citations,
            CacheControl = cacheControl,
        };
    }

    private static SearchResultBlock? SearchResult(JsonElement block, string line, string path)
    {
        if (!block.TryProperty("content", out var value)
            || !ParseContent(value, line, path + "[].content").TryGet(out var content, out _)
            || block.StringOrNull("source") is not { } source
            || block.StringOrNull("title") is not { } title
            || !ContentBlockPartParser.TryCitationsConfiguration(block, out var citations)
            || !ContentBlockPartParser.TryCacheControl(block, out var cacheControl))
        {
            return null;
        }

        return new SearchResultBlock(content, source, title) { Citations = citations, CacheControl = cacheControl };
    }

    private static ToolReferenceBlock? ToolReference(JsonElement block) =>
        block.StringOrNull("tool_name") is { } name
        && ContentBlockPartParser.TryCacheControl(block, out var cacheControl)
            ? new ToolReferenceBlock(name) { CacheControl = cacheControl }
            : null;

    private static BrowserStateBlock? BrowserState(JsonElement block)
    {
        if (!block.TryProperty("tabs", out var value)
            || !ContentBlockPartParser.TryTabs(value, out var tabs)
            || !ContentBlockPartParser.TryStateChanges(block, out var changes)
            || !ContentBlockPartParser.TryCacheControl(block, out var cacheControl))
        {
            return null;
        }

        return new BrowserStateBlock(tabs, changes) { CacheControl = cacheControl };
    }

    private static WebSearchToolResultBlock? WebSearchToolResult(JsonElement block)
    {
        if (block.ToolUseIdOrNull("tool_use_id") is not { } id
            || !block.TryProperty("content", out var value)
            || ServerToolResultParser.WebSearch(value) is not { } content
            || !ContentBlockPartParser.TryCaller(block, out var caller)
            || !ContentBlockPartParser.TryCacheControl(block, out var cacheControl))
        {
            return null;
        }

        return new WebSearchToolResultBlock(id, content) { Caller = caller, CacheControl = cacheControl };
    }

    private static WebFetchToolResultBlock? WebFetchToolResult(JsonElement block, string line, string path)
    {
        if (block.ToolUseIdOrNull("tool_use_id") is not { } id
            || !block.TryProperty("content", out var value)
            || ServerToolResultParser.WebFetch(value, line, path) is not { } content
            || !ContentBlockPartParser.TryCaller(block, out var caller)
            || !ContentBlockPartParser.TryCacheControl(block, out var cacheControl))
        {
            return null;
        }

        return new WebFetchToolResultBlock(id, content) { Caller = caller, CacheControl = cacheControl };
    }

    private static AdvisorToolResultBlock? AdvisorToolResult(JsonElement block) =>
        block.ToolUseIdOrNull("tool_use_id") is { } id
        && block.TryProperty("content", out var value)
        && ServerToolResultParser.Advisor(value) is { } content
            ? new AdvisorToolResultBlock(id, content)
            : null;

    private static CodeExecutionToolResultBlock? CodeExecutionToolResult(JsonElement block)
    {
        if (block.ToolUseIdOrNull("tool_use_id") is not { } id
            || !block.TryProperty("content", out var value)
            || ServerToolResultParser.CodeExecution(value) is not { } content
            || !ContentBlockPartParser.TryCacheControl(block, out var cacheControl))
        {
            return null;
        }

        return new CodeExecutionToolResultBlock(id, content) { CacheControl = cacheControl };
    }

    private static BashCodeExecutionToolResultBlock? BashCodeExecutionToolResult(JsonElement block)
    {
        if (block.ToolUseIdOrNull("tool_use_id") is not { } id
            || !block.TryProperty("content", out var value)
            || ServerToolResultParser.BashCodeExecution(value) is not { } content
            || !ContentBlockPartParser.TryCacheControl(block, out var cacheControl))
        {
            return null;
        }

        return new BashCodeExecutionToolResultBlock(id, content) { CacheControl = cacheControl };
    }

    private static TextEditorCodeExecutionToolResultBlock? TextEditorCodeExecutionToolResult(JsonElement block)
    {
        if (block.ToolUseIdOrNull("tool_use_id") is not { } id
            || !block.TryProperty("content", out var value)
            || ServerToolResultParser.TextEditorCodeExecution(value) is not { } content
            || !ContentBlockPartParser.TryCacheControl(block, out var cacheControl))
        {
            return null;
        }

        return new TextEditorCodeExecutionToolResultBlock(id, content) { CacheControl = cacheControl };
    }

    private static ToolSearchToolResultBlock? ToolSearchToolResult(JsonElement block)
    {
        if (block.ToolUseIdOrNull("tool_use_id") is not { } id
            || !block.TryProperty("content", out var value)
            || ServerToolResultParser.ToolSearch(value) is not { } content
            || !ContentBlockPartParser.TryCacheControl(block, out var cacheControl))
        {
            return null;
        }

        return new ToolSearchToolResultBlock(id, content) { CacheControl = cacheControl };
    }

    private static ContainerUploadBlock? ContainerUpload(JsonElement block) =>
        block.StringOrNull("file_id") is { } file
        && ContentBlockPartParser.TryCacheControl(block, out var cacheControl)
            ? new ContainerUploadBlock(file) { CacheControl = cacheControl }
            : null;

    // Every member of a compaction block may be absent or null: a failed compaction carries no
    // summary, and the block still stands.
    private static CompactionBlock Compaction(JsonElement block) =>
        new()
        {
            Content = block.StringOrNull("content"),
            EncryptedContent = block.StringOrNull("encrypted_content"),
            Signature = block.StringOrNull("signature"),
        };

    private static FallbackBlock? Fallback(JsonElement block) =>
        block.TryProperty("from", out var from)
        && ContentBlockPartParser.FallbackModel(from) is { } declining
        && block.TryProperty("to", out var to)
        && ContentBlockPartParser.FallbackModel(to) is { } taking
        && block.TryProperty("trigger", out var trigger)
        && ContentBlockPartParser.FallbackTrigger(trigger) is { } cause
            ? new FallbackBlock(declining, taking, cause)
            : null;
}
