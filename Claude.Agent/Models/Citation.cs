using System.Text.Json;

namespace Claude.Agent;

/// <summary>
/// What a span of text is cited from. Each kind the wire names is a type of its own; a kind this
/// library has no type for, or a citation that does not fit its kind's type, is an
/// <see cref="UnknownCitation"/>.
/// </summary>
public abstract class Citation
{
    private protected Citation()
    {
    }
}

/// <summary>A citation of a character range of a document.</summary>
public sealed class CharacterLocationCitation : Citation
{
    internal CharacterLocationCitation(string citedText, long documentIndex, long startCharacterIndex, long endCharacterIndex)
    {
        CitedText = citedText;
        DocumentIndex = documentIndex;
        StartCharacterIndex = startCharacterIndex;
        EndCharacterIndex = endCharacterIndex;
    }

    /// <summary>The text as it stands in the document.</summary>
    public string CitedText { get; }

    /// <summary>Which document of the request is cited, counted from zero.</summary>
    public long DocumentIndex { get; }

    /// <summary>The first character cited, counted from zero.</summary>
    public long StartCharacterIndex { get; }

    /// <summary>One past the last character cited.</summary>
    public long EndCharacterIndex { get; }

    /// <summary>The document's title; null when it has none.</summary>
    public string? DocumentTitle { get; internal init; }

    /// <summary>The file the document was uploaded as; null when it was not one, and on the request side, which never carries it.</summary>
    public string? FileId { get; internal init; }
}

/// <summary>A citation of a page range of a document.</summary>
public sealed class PageLocationCitation : Citation
{
    internal PageLocationCitation(string citedText, long documentIndex, long startPageNumber, long endPageNumber)
    {
        CitedText = citedText;
        DocumentIndex = documentIndex;
        StartPageNumber = startPageNumber;
        EndPageNumber = endPageNumber;
    }

    /// <summary>The text as it stands in the document.</summary>
    public string CitedText { get; }

    /// <summary>Which document of the request is cited, counted from zero.</summary>
    public long DocumentIndex { get; }

    /// <summary>The first page cited.</summary>
    public long StartPageNumber { get; }

    /// <summary>One past the last page cited.</summary>
    public long EndPageNumber { get; }

    /// <summary>The document's title; null when it has none.</summary>
    public string? DocumentTitle { get; internal init; }

    /// <summary>The file the document was uploaded as; null when it was not one, and on the request side, which never carries it.</summary>
    public string? FileId { get; internal init; }
}

/// <summary>A citation of a run of blocks in a document made of content blocks.</summary>
public sealed class ContentBlockLocationCitation : Citation
{
    internal ContentBlockLocationCitation(string citedText, long documentIndex, long startBlockIndex, long endBlockIndex)
    {
        CitedText = citedText;
        DocumentIndex = documentIndex;
        StartBlockIndex = startBlockIndex;
        EndBlockIndex = endBlockIndex;
    }

    /// <summary>The cited blocks' text, joined.</summary>
    public string CitedText { get; }

    /// <summary>Which document of the request is cited, counted from zero.</summary>
    public long DocumentIndex { get; }

    /// <summary>The first block cited, counted from zero.</summary>
    public long StartBlockIndex { get; }

    /// <summary>One past the last block cited.</summary>
    public long EndBlockIndex { get; }

    /// <summary>The document's title; null when it has none.</summary>
    public string? DocumentTitle { get; internal init; }

    /// <summary>The file the document was uploaded as; null when it was not one, and on the request side, which never carries it.</summary>
    public string? FileId { get; internal init; }
}

/// <summary>A citation of a page the API's web search found.</summary>
public sealed class WebSearchResultLocationCitation : Citation
{
    internal WebSearchResultLocationCitation(string citedText, string encryptedIndex, string url)
    {
        CitedText = citedText;
        EncryptedIndex = encryptedIndex;
        Url = url;
    }

    /// <summary>The text as it stands on the page.</summary>
    public string CitedText { get; }

    /// <summary>Which search result is cited, as an opaque value that goes back unchanged.</summary>
    public string EncryptedIndex { get; }

    /// <summary>The page's address.</summary>
    public string Url { get; }

    /// <summary>The page's title; null when it has none.</summary>
    public string? Title { get; internal init; }
}

/// <summary>A citation of a run of blocks in a search result a tool handed back.</summary>
public sealed class SearchResultLocationCitation : Citation
{
    internal SearchResultLocationCitation(string citedText, long searchResultIndex, string source, long startBlockIndex, long endBlockIndex)
    {
        CitedText = citedText;
        SearchResultIndex = searchResultIndex;
        Source = source;
        StartBlockIndex = startBlockIndex;
        EndBlockIndex = endBlockIndex;
    }

    /// <summary>The cited blocks' text, joined.</summary>
    public string CitedText { get; }

    /// <summary>Which search result of the request is cited, counted from zero and apart from the documents.</summary>
    public long SearchResultIndex { get; }

    /// <summary>Where the search result came from, as the block that carried it said.</summary>
    public string Source { get; }

    /// <summary>The first block cited, counted from zero.</summary>
    public long StartBlockIndex { get; }

    /// <summary>One past the last block cited.</summary>
    public long EndBlockIndex { get; }

    /// <summary>The search result's title; null when it has none.</summary>
    public string? Title { get; internal init; }
}

/// <summary>A citation of a kind this library has no type for, or one that does not fit its kind's type: its kind's name and the citation as written.</summary>
public sealed class UnknownCitation : Citation
{
    internal UnknownCitation(string type, JsonElement raw)
    {
        Type = type;
        Raw = raw;
    }

    /// <summary>The citation's <c>type</c>.</summary>
    public string Type { get; }

    /// <summary>The citation as the binary wrote it.</summary>
    public JsonElement Raw { get; }
}
