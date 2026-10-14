namespace Claude.Agent.Tests.Wire;

// The content block set, read off hand-written lines. Every kind but text, thinking, tool_use and
// tool_result is declared and never recorded (the fixtures' README says which), so these lines are
// this tree's own, written from the declared shape: the kinds are the wire's, the values are ours.
// What each test holds is in its name; the recorded kinds are held by the replays beside this file.
[TestClass]
public sealed class ContentBlockDecodeTests
{
    [TestMethod]
    public void TheTextBlockCarriesEveryKindOfCitationAndItsCacheBreakpoint()
    {
        var block = (TextBlock)Block(
            """
            {"type":"text","text":"Tea steeps for three minutes.","cache_control":{"type":"ephemeral","ttl":"1h"},
             "citations":[
               {"type":"char_location","cited_text":"three minutes","document_index":0,"document_title":"Brewing","file_id":"file_01","start_char_index":12,"end_char_index":25},
               {"type":"page_location","cited_text":"three minutes","document_index":1,"document_title":null,"file_id":null,"start_page_number":4,"end_page_number":5},
               {"type":"content_block_location","cited_text":"three minutes","document_index":2,"document_title":"Notes","file_id":null,"start_block_index":1,"end_block_index":3},
               {"type":"web_search_result_location","cited_text":"three minutes","encrypted_index":"idx_01","title":"How to brew","url":"https://example.com/brew"},
               {"type":"search_result_location","cited_text":"three minutes","search_result_index":2,"source":"kettle-notes","start_block_index":0,"end_block_index":1,"title":null}]}
            """);

        Assert.AreEqual("Tea steeps for three minutes.", block.Text);
        Assert.AreEqual(CacheControlTimeToLive.OneHour, block.CacheControl!.TimeToLive);
        Assert.AreEqual(5, block.Citations.Count);

        var character = (CharacterLocationCitation)block.Citations[0];
        Assert.AreEqual("three minutes", character.CitedText);
        Assert.AreEqual(0, character.DocumentIndex);
        Assert.AreEqual("Brewing", character.DocumentTitle);
        Assert.AreEqual("file_01", character.FileId);
        Assert.AreEqual(12, character.StartCharacterIndex);
        Assert.AreEqual(25, character.EndCharacterIndex);

        var page = (PageLocationCitation)block.Citations[1];
        Assert.AreEqual(4, page.StartPageNumber);
        Assert.AreEqual(5, page.EndPageNumber);
        Assert.IsNull(page.DocumentTitle);
        Assert.IsNull(page.FileId);

        var nested = (ContentBlockLocationCitation)block.Citations[2];
        Assert.AreEqual(1, nested.StartBlockIndex);
        Assert.AreEqual(3, nested.EndBlockIndex);

        var searched = (WebSearchResultLocationCitation)block.Citations[3];
        Assert.AreEqual("idx_01", searched.EncryptedIndex);
        Assert.AreEqual("https://example.com/brew", searched.Url);
        Assert.AreEqual("How to brew", searched.Title);

        var result = (SearchResultLocationCitation)block.Citations[4];
        Assert.AreEqual(2, result.SearchResultIndex);
        Assert.AreEqual("kettle-notes", result.Source);
        Assert.IsNull(result.Title);
    }

    [TestMethod]
    public void ACitationOfAKindTheLibraryHasNoTypeForIsKeptWholeAsTheUnknown()
    {
        var block = (TextBlock)Block(
            """
            {"type":"text","text":"steeped","citations":[{"type":"kettle_location","cited_text":"steeped","shelf":3}]}
            """);

        var unknown = (UnknownCitation)block.Citations.Single();
        Assert.AreEqual("kettle_location", unknown.Type);
        Assert.AreEqual(3, unknown.Raw.GetProperty("shelf").GetInt32());
    }

    [TestMethod]
    public void TheToolUseBlockCarriesItsCallerItsToolsetAndItsCacheBreakpoint()
    {
        var direct = (ToolUseBlock)Block(
            """
            {"type":"tool_use","id":"toolu_01","name":"Read","input":{"file_path":"/home/user/notes.txt"},
             "caller":{"type":"direct"},"toolset_name":"files","cache_control":{"type":"ephemeral"}}
            """);

        Assert.AreEqual("toolu_01", direct.Id);
        Assert.AreEqual("Read", direct.Name);
        Assert.IsInstanceOfType<DirectToolCaller>(direct.Caller);
        Assert.AreEqual("files", direct.ToolsetName);
        Assert.IsNull(direct.CacheControl!.TimeToLive);

        var early = (ToolUseBlock)Block(
            """
            {"type":"tool_use","id":"toolu_02","name":"Read","input":{},"caller":{"type":"code_execution_20250825","tool_id":"srvtoolu_01"}}
            """);
        Assert.AreEqual("srvtoolu_01", ((CodeExecutionToolCaller20250825)early.Caller!).ToolId);

        var late = (ToolUseBlock)Block(
            """
            {"type":"tool_use","id":"toolu_03","name":"Read","input":{},"caller":{"type":"code_execution_20260120","tool_id":"srvtoolu_02"}}
            """);
        Assert.AreEqual("srvtoolu_02", ((CodeExecutionToolCaller20260120)late.Caller!).ToolId);

        var unnamed = (ToolUseBlock)Block(
            """
            {"type":"tool_use","id":"toolu_04","name":"Read","input":{},"caller":{"type":"kettle","tool_id":"srvtoolu_03"}}
            """);
        Assert.AreEqual("kettle", ((UnknownToolCaller)unnamed.Caller!).Type);
    }

    // Every recorded tool_use carries a caller, and every one of them is the model itself.
    [TestMethod]
    public void TheRecordedToolUseBlocksAreCalledByTheModelItself()
    {
        var calls = Recorded.Messages<AssistantMessage>("tools")
            .SelectMany(message => message.Content)
            .OfType<ToolUseBlock>()
            .ToArray();

        Assert.AreNotEqual(0, calls.Length);
        foreach (var call in calls)
        {
            Assert.IsInstanceOfType<DirectToolCaller>(call.Caller, call.Name);
            Assert.IsNull(call.ToolsetName, call.Name);
            Assert.IsNull(call.CacheControl, call.Name);
        }
    }

    // No recorded text block carries citations: the declaration says the member is always there on
    // the response side, and the binary at 2.1.259 through 2.1.276 never wrote it.
    [TestMethod]
    public void NoRecordedTextBlockCarriesACitation()
    {
        var text = Recorded.Messages<AssistantMessage>("tools")
            .SelectMany(message => message.Content)
            .OfType<TextBlock>()
            .ToArray();

        Assert.AreNotEqual(0, text.Length);
        Assert.IsTrue(text.All(block => block.Citations.Count == 0));
    }

    [TestMethod]
    public void TheServerToolUseBlockNamesTheToolTheApiRunsAndKeepsItsArgumentsRaw()
    {
        var block = (ServerToolUseBlock)Block(
            """
            {"type":"server_tool_use","id":"srvtoolu_01","name":"web_search","input":{"query":"how to brew tea"},"caller":{"type":"direct"}}
            """);

        Assert.AreEqual("srvtoolu_01", block.Id);
        Assert.AreEqual(ServerToolName.WebSearch, block.Name);
        Assert.IsTrue(block.Name.IsDefined);
        Assert.AreEqual("how to brew tea", block.RawInput.GetProperty("query").GetString());
        Assert.IsInstanceOfType<DirectToolCaller>(block.Caller);

        var unnamed = (ServerToolUseBlock)Block(
            """
            {"type":"server_tool_use","id":"srvtoolu_02","name":"kettle_search","input":{}}
            """);
        Assert.AreEqual("kettle_search", unnamed.Name.ToString());
        Assert.IsFalse(unnamed.Name.IsDefined);
    }

    [TestMethod]
    public void TheImageBlockCarriesEachSourceKindItsFormatAndItsTransformations()
    {
        var encoded = (ImageBlock)Block(
            """
            {"type":"image","source":{"type":"base64","media_type":"image/png","data":"AA=="},"transformations":{"oversized_image":"downsize"}}
            """);
        var source = (Base64ImageSource)encoded.Source;
        Assert.AreEqual("AA==", source.Data);
        Assert.AreEqual(ImageMediaType.Png, source.MediaType);
        Assert.AreEqual(ImageOversizedHandling.Downsize, encoded.Transformations!.OversizedImage);

        var fetched = (ImageBlock)Block("""{"type":"image","source":{"type":"url","url":"https://example.com/pixel.png"}}""");
        Assert.AreEqual("https://example.com/pixel.png", ((UrlImageSource)fetched.Source).Url);
        Assert.IsNull(fetched.Transformations);

        var uploaded = (ImageBlock)Block("""{"type":"image","source":{"type":"file","file_id":"file_01"}}""");
        Assert.AreEqual("file_01", ((FileImageSource)uploaded.Source).FileId);

        var unnamed = (ImageBlock)Block("""{"type":"image","source":{"type":"kettle","data":"AA=="}}""");
        Assert.AreEqual("kettle", ((UnknownImageSource)unnamed.Source).Type);

        var format = (ImageBlock)Block("""{"type":"image","source":{"type":"base64","media_type":"image/tiff","data":"AA=="}}""");
        Assert.AreEqual("image/tiff", ((Base64ImageSource)format.Source).MediaType.ToString());
        Assert.IsFalse(((Base64ImageSource)format.Source).MediaType.IsDefined);
    }

    [TestMethod]
    public void TheDocumentBlockCarriesEachSourceKindItsTitleItsContextAndItsCitationSetting()
    {
        var encoded = (DocumentBlock)Block(
            """
            {"type":"document","source":{"type":"base64","media_type":"application/pdf","data":"AA=="},
             "title":"Brewing","context":"the kitchen's tea notes","citations":{"enabled":true}}
            """);
        Assert.AreEqual(DocumentMediaType.Pdf, ((Base64DocumentSource)encoded.Source).MediaType);
        Assert.AreEqual("Brewing", encoded.Title);
        Assert.AreEqual("the kitchen's tea notes", encoded.Context);
        Assert.AreEqual(true, encoded.Citations!.Enabled);

        var plain = (DocumentBlock)Block("""{"type":"document","source":{"type":"text","media_type":"text/plain","data":"steep"}}""");
        Assert.AreEqual("steep", ((TextDocumentSource)plain.Source).Data);
        Assert.AreEqual(DocumentMediaType.PlainText, ((TextDocumentSource)plain.Source).MediaType);
        Assert.IsNull(plain.Citations);

        var built = (DocumentBlock)Block(
            """
            {"type":"document","source":{"type":"content","content":[{"type":"text","text":"steep"},{"type":"image","source":{"type":"file","file_id":"file_01"}}]}}
            """);
        var content = ((ContentDocumentSource)built.Source).Content;
        Assert.AreEqual(2, content.Count);
        Assert.IsInstanceOfType<TextBlock>(content[0]);
        Assert.IsInstanceOfType<ImageBlock>(content[1]);

        var whole = (DocumentBlock)Block("""{"type":"document","source":{"type":"content","content":"steep"}}""");
        Assert.AreEqual("steep", ((TextBlock)((ContentDocumentSource)whole.Source).Content.Single()).Text);

        var fetched = (DocumentBlock)Block("""{"type":"document","source":{"type":"url","url":"https://example.com/brew.pdf"}}""");
        Assert.AreEqual("https://example.com/brew.pdf", ((UrlDocumentSource)fetched.Source).Url);

        var uploaded = (DocumentBlock)Block("""{"type":"document","source":{"type":"file","file_id":"file_02"}}""");
        Assert.AreEqual("file_02", ((FileDocumentSource)uploaded.Source).FileId);

        var unnamed = (DocumentBlock)Block("""{"type":"document","source":{"type":"kettle"}}""");
        Assert.AreEqual("kettle", ((UnknownDocumentSource)unnamed.Source).Type);
    }

    [TestMethod]
    public void TheSearchResultBlockCarriesTheBlocksItsSourceAndItsTitle()
    {
        var block = (SearchResultBlock)Block(
            """
            {"type":"search_result","source":"kettle-notes","title":"Brewing","content":[{"type":"text","text":"steep"}],"citations":{"enabled":false}}
            """);

        Assert.AreEqual("kettle-notes", block.Source);
        Assert.AreEqual("Brewing", block.Title);
        Assert.AreEqual("steep", ((TextBlock)block.Content.Single()).Text);
        Assert.AreEqual(false, block.Citations!.Enabled);
    }

    [TestMethod]
    public void TheToolResultBlockReadsTheBlocksNestedInItAndItsToolsetName()
    {
        var block = (ToolResultBlock)Block(
            """
            {"type":"tool_result","tool_use_id":"toolu_01","is_error":false,"toolset_name":"files","content":[
              {"type":"text","text":"steep"},
              {"type":"image","source":{"type":"file","file_id":"file_01"}},
              {"type":"document","source":{"type":"url","url":"https://example.com/brew.pdf"}},
              {"type":"search_result","source":"kettle-notes","title":"Brewing","content":[{"type":"text","text":"steep"}]},
              {"type":"tool_reference","tool_name":"Kettle","cache_control":{"type":"ephemeral","ttl":"5m"}},
              {"type":"browser_state","tabs":[]}]}
            """);

        Assert.AreEqual("toolu_01", block.ToolUseId);
        Assert.AreEqual(false, block.IsError);
        Assert.AreEqual("files", block.ToolsetName);
        Assert.AreEqual(6, block.Content.Count);
        Assert.IsInstanceOfType<TextBlock>(block.Content[0]);
        Assert.IsInstanceOfType<ImageBlock>(block.Content[1]);
        Assert.IsInstanceOfType<DocumentBlock>(block.Content[2]);
        Assert.IsInstanceOfType<SearchResultBlock>(block.Content[3]);
        Assert.IsInstanceOfType<ToolReferenceBlock>(block.Content[4], out var reference);
        Assert.AreEqual("Kettle", reference.ToolName);
        Assert.AreEqual(CacheControlTimeToLive.FiveMinutes, reference.CacheControl!.TimeToLive);
        Assert.IsInstanceOfType<BrowserStateBlock>(block.Content[5]);
    }

    [TestMethod]
    public void TheBrowserStateBlockCarriesEveryTabAndEveryChangeTheCallMade()
    {
        var block = (BrowserStateBlock)Block(
            """
            {"type":"browser_state","tabs":[
               {"tab_id":"tab_01","title":"Brewing","url":"https://example.com/brew","active":true},
               {"tab_id":"tab_02","title":"","url":""}],
             "state_changes":[
               {"type":"tab_opened","tab_id":"tab_02"},
               {"type":"download_started","download_id":"dl_01","url":"https://example.com/notes.pdf"},
               {"type":"download_completed","download_id":"dl_01","url":"https://example.com/notes.pdf","path":"/home/user/notes.pdf","size_bytes":4096},
               {"type":"download_failed","download_id":"dl_02","url":"https://example.com/gone.pdf","error":"refused"},
               {"type":"tab_closed","tab_id":"tab_01"}]}
            """);

        Assert.AreEqual(2, block.Tabs.Count);
        Assert.AreEqual("tab_01", block.Tabs[0].TabId);
        Assert.AreEqual("Brewing", block.Tabs[0].Title);
        Assert.AreEqual(true, block.Tabs[0].Active);
        Assert.IsNull(block.Tabs[1].Active);

        Assert.AreEqual(5, block.StateChanges.Count);
        Assert.AreEqual("tab_02", ((TabOpenedBrowserStateChange)block.StateChanges[0]).TabId);
        Assert.AreEqual("dl_01", ((DownloadStartedBrowserStateChange)block.StateChanges[1]).DownloadId);
        var completed = (DownloadCompletedBrowserStateChange)block.StateChanges[2];
        Assert.AreEqual("/home/user/notes.pdf", completed.Path);
        Assert.AreEqual(4096, completed.SizeBytes);
        Assert.AreEqual("refused", ((DownloadFailedBrowserStateChange)block.StateChanges[3]).Error);
        Assert.AreEqual("tab_closed", ((UnknownBrowserStateChange)block.StateChanges[4]).Type);

        var quiet = (BrowserStateBlock)Block("""{"type":"browser_state","tabs":[]}""");
        Assert.AreEqual(0, quiet.Tabs.Count);
        Assert.AreEqual(0, quiet.StateChanges.Count);
    }

    [TestMethod]
    public void TheWebSearchToolResultReadsItsResultsAndTheCodeItFailedWith()
    {
        var found = (WebSearchToolResultBlock)Block(
            """
            {"type":"web_search_tool_result","tool_use_id":"srvtoolu_01","caller":{"type":"direct"},"content":[
              {"type":"web_search_result","encrypted_content":"kept_01","title":"How to brew","url":"https://example.com/brew","page_age":"2 days"},
              {"type":"web_search_result","encrypted_content":"kept_02","title":"Kettles","url":"https://example.com/kettles","page_age":null}]}
            """);

        Assert.AreEqual("srvtoolu_01", found.ToolUseId);
        Assert.AreEqual(2, found.Content.Count);
        var first = (WebSearchResult)found.Content[0];
        Assert.AreEqual("kept_01", first.EncryptedContent);
        Assert.AreEqual("How to brew", first.Title);
        Assert.AreEqual("https://example.com/brew", first.Url);
        Assert.AreEqual("2 days", first.PageAge);
        Assert.IsNull(((WebSearchResult)found.Content[1]).PageAge);

        var failed = (WebSearchToolResultBlock)Block(
            """
            {"type":"web_search_tool_result","tool_use_id":"srvtoolu_02","content":{"type":"web_search_tool_result_error","error_code":"query_too_long"}}
            """);
        var code = ((WebSearchToolResultErrorContent)failed.Content.Single()).ErrorCode;
        Assert.AreEqual(WebSearchErrorCode.QueryTooLong, code);
        Assert.IsTrue(code.IsDefined);
    }

    [TestMethod]
    public void TheWebFetchToolResultReadsThePageItFetchedAndTheCodeItFailedWith()
    {
        var fetched = (WebFetchToolResultBlock)Block(
            """
            {"type":"web_fetch_tool_result","tool_use_id":"srvtoolu_01","content":{"type":"web_fetch_result",
              "url":"https://example.com/brew","retrieved_at":"2026-09-18T10:11:12Z",
              "content":{"type":"document","title":"Brewing","citations":{"enabled":true},"source":{"type":"text","media_type":"text/plain","data":"steep"}}}}
            """);

        var page = (WebFetchResult)fetched.Content.Single();
        Assert.AreEqual("https://example.com/brew", page.Url);
        Assert.AreEqual(new DateTimeOffset(2026, 9, 18, 10, 11, 12, TimeSpan.Zero), page.RetrievedAt);
        Assert.AreEqual("Brewing", page.Content.Title);
        Assert.AreEqual("steep", ((TextDocumentSource)page.Content.Source).Data);

        var failed = (WebFetchToolResultBlock)Block(
            """
            {"type":"web_fetch_tool_result","tool_use_id":"srvtoolu_02","content":{"type":"web_fetch_tool_result_error","error_code":"url_not_allowed"}}
            """);
        Assert.AreEqual(WebFetchErrorCode.UrlNotAllowed, ((WebFetchToolResultErrorContent)failed.Content.Single()).ErrorCode);
    }

    [TestMethod]
    public void TheAdvisorToolResultReadsItsAnswerItsKeptAnswerAndTheCodeItFailedWith()
    {
        var answered = (AdvisorToolResultBlock)Block(
            """
            {"type":"advisor_tool_result","tool_use_id":"srvtoolu_01","content":{"type":"advisor_result","text":"steep it longer","stop_reason":"end_turn"}}
            """);
        var answer = (AdvisorResult)answered.Content.Single();
        Assert.AreEqual("steep it longer", answer.Text);
        Assert.AreEqual(StopReason.EndTurn, answer.StopReason);

        var kept = (AdvisorToolResultBlock)Block(
            """
            {"type":"advisor_tool_result","tool_use_id":"srvtoolu_02","content":{"type":"advisor_redacted_result","encrypted_content":"kept_01","stop_reason":null}}
            """);
        var redacted = (AdvisorRedactedResult)kept.Content.Single();
        Assert.AreEqual("kept_01", redacted.EncryptedContent);
        Assert.IsNull(redacted.StopReason);

        var failed = (AdvisorToolResultBlock)Block(
            """
            {"type":"advisor_tool_result","tool_use_id":"srvtoolu_03","content":{"type":"advisor_tool_result_error","error_code":"model_not_found"}}
            """);
        Assert.AreEqual(AdvisorErrorCode.ModelNotFound, ((AdvisorToolResultErrorContent)failed.Content.Single()).ErrorCode);
    }

    [TestMethod]
    public void TheCodeExecutionToolResultReadsWhatTheCodeWroteWhatItKeptAndTheCodeItFailedWith()
    {
        var ran = (CodeExecutionToolResultBlock)Block(
            """
            {"type":"code_execution_tool_result","tool_use_id":"srvtoolu_01","content":{"type":"code_execution_result",
              "return_code":0,"stderr":"","stdout":"3 minutes","content":[{"type":"code_execution_output","file_id":"file_01"}]}}
            """);
        var output = (CodeExecutionResult)ran.Content.Single();
        Assert.AreEqual(0, output.ReturnCode);
        Assert.AreEqual("3 minutes", output.StandardOutput);
        Assert.AreEqual(string.Empty, output.StandardError);
        Assert.AreEqual("file_01", output.Content.Single().FileId);

        var kept = (CodeExecutionToolResultBlock)Block(
            """
            {"type":"code_execution_tool_result","tool_use_id":"srvtoolu_02","content":{"type":"encrypted_code_execution_result",
              "return_code":1,"stderr":"boiled over","encrypted_stdout":"kept_01","content":[]}}
            """);
        var encrypted = (EncryptedCodeExecutionResult)kept.Content.Single();
        Assert.AreEqual(1, encrypted.ReturnCode);
        Assert.AreEqual("kept_01", encrypted.EncryptedStandardOutput);
        Assert.AreEqual("boiled over", encrypted.StandardError);
        Assert.AreEqual(0, encrypted.Content.Count);

        var failed = (CodeExecutionToolResultBlock)Block(
            """
            {"type":"code_execution_tool_result","tool_use_id":"srvtoolu_03","content":{"type":"code_execution_tool_result_error","error_code":"execution_time_exceeded"}}
            """);
        Assert.AreEqual(
            CodeExecutionErrorCode.ExecutionTimeExceeded,
            ((CodeExecutionToolResultErrorContent)failed.Content.Single()).ErrorCode);
    }

    [TestMethod]
    public void TheBashCodeExecutionToolResultReadsWhatTheCommandWroteAndTheCodeItFailedWith()
    {
        var ran = (BashCodeExecutionToolResultBlock)Block(
            """
            {"type":"bash_code_execution_tool_result","tool_use_id":"srvtoolu_01","content":{"type":"bash_code_execution_result",
              "return_code":2,"stderr":"no kettle","stdout":"","content":[{"type":"bash_code_execution_output","file_id":"file_01"}]}}
            """);
        var output = (BashCodeExecutionResult)ran.Content.Single();
        Assert.AreEqual(2, output.ReturnCode);
        Assert.AreEqual("no kettle", output.StandardError);
        Assert.AreEqual("file_01", output.Content.Single().FileId);

        var failed = (BashCodeExecutionToolResultBlock)Block(
            """
            {"type":"bash_code_execution_tool_result","tool_use_id":"srvtoolu_02","content":{"type":"bash_code_execution_tool_result_error","error_code":"output_file_too_large"}}
            """);
        Assert.AreEqual(
            BashCodeExecutionErrorCode.OutputFileTooLarge,
            ((BashCodeExecutionToolResultErrorContent)failed.Content.Single()).ErrorCode);
    }

    [TestMethod]
    public void TheTextEditorCodeExecutionToolResultReadsItsViewItsNewFileItsEditAndItsFailure()
    {
        var viewed = (TextEditorCodeExecutionToolResultBlock)Block(
            """
            {"type":"text_editor_code_execution_tool_result","tool_use_id":"srvtoolu_01","content":{"type":"text_editor_code_execution_view_result",
              "content":"steep","file_type":"text","num_lines":1,"start_line":1,"total_lines":9}}
            """);
        var view = (TextEditorCodeExecutionViewResult)viewed.Content.Single();
        Assert.AreEqual("steep", view.Content);
        Assert.AreEqual(TextEditorFileType.Text, view.FileType);
        Assert.AreEqual(1, view.LineCount);
        Assert.AreEqual(1, view.StartLine);
        Assert.AreEqual(9, view.TotalLines);

        var created = (TextEditorCodeExecutionToolResultBlock)Block(
            """
            {"type":"text_editor_code_execution_tool_result","tool_use_id":"srvtoolu_02","content":{"type":"text_editor_code_execution_create_result","is_file_update":true}}
            """);
        Assert.IsTrue(((TextEditorCodeExecutionCreateResult)created.Content.Single()).IsFileUpdate);

        var edited = (TextEditorCodeExecutionToolResultBlock)Block(
            """
            {"type":"text_editor_code_execution_tool_result","tool_use_id":"srvtoolu_03","content":{"type":"text_editor_code_execution_str_replace_result",
              "lines":["steep","pour"],"new_lines":2,"new_start":4,"old_lines":1,"old_start":4}}
            """);
        var replaced = (TextEditorCodeExecutionStringReplaceResult)edited.Content.Single();
        CollectionAssert.AreEqual(new[] { "steep", "pour" }, replaced.Lines.ToArray());
        Assert.AreEqual(2, replaced.NewLineCount);
        Assert.AreEqual(4, replaced.NewStartLine);
        Assert.AreEqual(1, replaced.OldLineCount);
        Assert.AreEqual(4, replaced.OldStartLine);

        var failed = (TextEditorCodeExecutionToolResultBlock)Block(
            """
            {"type":"text_editor_code_execution_tool_result","tool_use_id":"srvtoolu_04","content":{"type":"text_editor_code_execution_tool_result_error",
              "error_code":"file_not_found","error_message":"no such file"}}
            """);
        var failure = (TextEditorCodeExecutionToolResultErrorContent)failed.Content.Single();
        Assert.AreEqual(TextEditorCodeExecutionErrorCode.FileNotFound, failure.ErrorCode);
        Assert.AreEqual("no such file", failure.ErrorMessage);
    }

    [TestMethod]
    public void TheToolSearchToolResultReadsTheToolsItFoundAndTheCodeItFailedWith()
    {
        var found = (ToolSearchToolResultBlock)Block(
            """
            {"type":"tool_search_tool_result","tool_use_id":"srvtoolu_01","content":{"type":"tool_search_tool_search_result",
              "tool_references":[{"type":"tool_reference","tool_name":"Kettle"},{"type":"tool_reference","tool_name":"Timer"}]}}
            """);
        var references = ((ToolSearchToolSearchResult)found.Content.Single()).ToolReferences;
        CollectionAssert.AreEqual(new[] { "Kettle", "Timer" }, references.Select(reference => reference.ToolName).ToArray());

        var failed = (ToolSearchToolResultBlock)Block(
            """
            {"type":"tool_search_tool_result","tool_use_id":"srvtoolu_02","content":{"type":"tool_search_tool_result_error","error_code":"unavailable","error_message":null}}
            """);
        var failure = (ToolSearchToolResultErrorContent)failed.Content.Single();
        Assert.AreEqual(ToolSearchErrorCode.Unavailable, failure.ErrorCode);
        Assert.IsNull(failure.ErrorMessage);
    }

    [TestMethod]
    public void TheMcpToolUseAndResultBlocksNameTheServerAndCarryItsAnswer()
    {
        var call = (McpToolUseBlock)Block(
            """
            {"type":"mcp_tool_use","id":"mcptoolu_01","name":"brew","server_name":"kettle","input":{"minutes":3}}
            """);
        Assert.AreEqual("mcptoolu_01", call.Id);
        Assert.AreEqual("brew", call.Name);
        Assert.AreEqual("kettle", call.ServerName);
        Assert.AreEqual(3, call.RawInput.GetProperty("minutes").GetInt32());

        var answer = (McpToolResultBlock)Block(
            """
            {"type":"mcp_tool_result","tool_use_id":"mcptoolu_01","is_error":false,"content":[{"type":"text","text":"steeping"}]}
            """);
        Assert.AreEqual("mcptoolu_01", answer.ToolUseId);
        Assert.IsFalse(answer.IsError);
        Assert.AreEqual("steeping", ((TextBlock)answer.Content.Single()).Text);

        var whole = (McpToolResultBlock)Block(
            """
            {"type":"mcp_tool_result","tool_use_id":"mcptoolu_02","is_error":true,"content":"no kettle"}
            """);
        Assert.IsTrue(whole.IsError);
        Assert.AreEqual("no kettle", ((TextBlock)whole.Content.Single()).Text);
    }

    [TestMethod]
    public void TheRedactedThinkingContainerUploadCompactionAndFallbackBlocksAreRead()
    {
        var kept = (RedactedThinkingBlock)Block("""{"type":"redacted_thinking","data":"kept_01"}""");
        Assert.AreEqual("kept_01", kept.Data);

        var uploaded = (ContainerUploadBlock)Block("""{"type":"container_upload","file_id":"file_01","cache_control":{"type":"ephemeral"}}""");
        Assert.AreEqual("file_01", uploaded.FileId);
        Assert.IsNotNull(uploaded.CacheControl);

        var compacted = (CompactionBlock)Block(
            """
            {"type":"compaction","content":"the kettle boiled","encrypted_content":"kept_02","signature":"sig_01"}
            """);
        Assert.AreEqual("the kettle boiled", compacted.Content);
        Assert.AreEqual("kept_02", compacted.EncryptedContent);
        Assert.AreEqual("sig_01", compacted.Signature);

        var failedCompaction = (CompactionBlock)Block("""{"type":"compaction","content":null,"encrypted_content":null}""");
        Assert.IsNull(failedCompaction.Content);
        Assert.IsNull(failedCompaction.EncryptedContent);
        Assert.IsNull(failedCompaction.Signature);

        var handedOver = (FallbackBlock)Block(
            """
            {"type":"fallback","from":{"model":"a-model"},"to":{"model":"another-model"},"trigger":{"type":"refusal","category":"general_harms"}}
            """);
        Assert.AreEqual("a-model", handedOver.From.Model);
        Assert.AreEqual("another-model", handedOver.To.Model);
        Assert.AreEqual(RefusalCategory.GeneralHarms, handedOver.Trigger.Category);

        var unnamedCategory = (FallbackBlock)Block(
            """
            {"type":"fallback","from":{"model":"a-model"},"to":{"model":"another-model"},"trigger":{"type":"refusal","category":"kettle"}}
            """);
        Assert.AreEqual("kettle", unnamedCategory.Trigger.Category.ToString());
        Assert.IsFalse(unnamedCategory.Trigger.Category!.Value.IsDefined);
    }

    // The degrade the page asks for, block by block: a kind the library names whose members do not
    // fit its type is the unknown block, kept whole, and the message still reads.
    [TestMethod]
    public void ABlockOfAKindTheLibraryNamesThatDoesNotFitItsTypeIsTheUnknownBlock()
    {
        string[] lines =
        [
            """{"type":"image","transformations":{}}""",
            """{"type":"image","source":"a file"}""",
            """{"type":"redacted_thinking"}""",
            """{"type":"container_upload","file_id":"file_01","cache_control":"5m"}""",
            """{"type":"text","text":"steep","citations":{"enabled":true}}""",
            """{"type":"tool_use","id":"","name":"Read","input":{}}""",
            """{"type":"tool_use","id":"toolu_01","name":"Read","input":{},"caller":7}""",
            """{"type":"mcp_tool_use","id":"mcptoolu_01","name":"brew","input":{}}""",
            """{"type":"web_search_tool_result","tool_use_id":"srvtoolu_01","content":"nothing"}""",
            """{"type":"browser_state","tabs":[{"tab_id":"tab_01"}]}""",
            """{"type":"browser_state","tabs":[],"state_changes":{}}""",
            """{"type":"fallback","from":{"model":"a-model"},"to":{"model":"another-model"}}""",
            """{"type":"search_result","source":"kettle-notes","title":"Brewing"}""",
        ];

        foreach (var line in lines)
        {
            var block = Block(line);
            Assert.IsInstanceOfType<UnknownContentBlock>(block, line);
            Assert.AreEqual(line, ((UnknownContentBlock)block).Raw.GetRawText(), line);
        }
    }

    // A part of a server tool's result that the library has no type for is the unknown of that
    // tool's own set, not the unknown block: the block around it still reads.
    [TestMethod]
    public void APartOfAServerToolsResultTheLibraryHasNoTypeForIsTheUnknownOfThatToolsSet()
    {
        var search = (WebSearchToolResultBlock)Block(
            """{"type":"web_search_tool_result","tool_use_id":"srvtoolu_01","content":{"type":"kettle_result","shelf":3}}""");
        Assert.AreEqual("kettle_result", ((UnknownWebSearchToolResultContent)search.Content.Single()).Type);

        var fetch = (WebFetchToolResultBlock)Block(
            """{"type":"web_fetch_tool_result","tool_use_id":"srvtoolu_02","content":{"type":"kettle_result"}}""");
        Assert.AreEqual("kettle_result", ((UnknownWebFetchToolResultContent)fetch.Content.Single()).Type);

        var advisor = (AdvisorToolResultBlock)Block(
            """{"type":"advisor_tool_result","tool_use_id":"srvtoolu_03","content":{"type":"kettle_result"}}""");
        Assert.AreEqual("kettle_result", ((UnknownAdvisorToolResultContent)advisor.Content.Single()).Type);

        var code = (CodeExecutionToolResultBlock)Block(
            """{"type":"code_execution_tool_result","tool_use_id":"srvtoolu_04","content":{"type":"kettle_result"}}""");
        Assert.AreEqual("kettle_result", ((UnknownCodeExecutionToolResultContent)code.Content.Single()).Type);

        var shell = (BashCodeExecutionToolResultBlock)Block(
            """{"type":"bash_code_execution_tool_result","tool_use_id":"srvtoolu_05","content":{"type":"kettle_result"}}""");
        Assert.AreEqual("kettle_result", ((UnknownBashCodeExecutionToolResultContent)shell.Content.Single()).Type);

        var editor = (TextEditorCodeExecutionToolResultBlock)Block(
            """{"type":"text_editor_code_execution_tool_result","tool_use_id":"srvtoolu_06","content":{"type":"kettle_result"}}""");
        Assert.AreEqual("kettle_result", ((UnknownTextEditorCodeExecutionToolResultContent)editor.Content.Single()).Type);

        var tools = (ToolSearchToolResultBlock)Block(
            """{"type":"tool_search_tool_result","tool_use_id":"srvtoolu_07","content":{"type":"kettle_result"}}""");
        Assert.AreEqual("kettle_result", ((UnknownToolSearchToolResultContent)tools.Content.Single()).Type);
    }

    // A part of a kind the library names whose members do not fit is the unknown of its set too: the
    // same degrade, one level down.
    [TestMethod]
    public void APartOfAServerToolsResultThatDoesNotFitItsKindIsTheUnknownOfThatToolsSet()
    {
        var search = (WebSearchToolResultBlock)Block(
            """{"type":"web_search_tool_result","tool_use_id":"srvtoolu_01","content":{"type":"web_search_result","title":"Brewing"}}""");
        Assert.AreEqual("web_search_result", ((UnknownWebSearchToolResultContent)search.Content.Single()).Type);

        var code = (CodeExecutionToolResultBlock)Block(
            """{"type":"code_execution_tool_result","tool_use_id":"srvtoolu_02","content":{"type":"code_execution_result","return_code":0,"stderr":"","stdout":""}}""");
        Assert.AreEqual("code_execution_result", ((UnknownCodeExecutionToolResultContent)code.Content.Single()).Type);

        var fetch = (WebFetchToolResultBlock)Block(
            """{"type":"web_fetch_tool_result","tool_use_id":"srvtoolu_03","content":{"type":"web_fetch_result","url":"https://example.com/brew"}}""");
        Assert.AreEqual("web_fetch_result", ((UnknownWebFetchToolResultContent)fetch.Content.Single()).Type);
    }

    // One block, read off a user frame: the parser reads the same set on either side of the wire, so
    // a block declared on the assistant side alone reads here too.
    private static ContentBlock Block(string json)
    {
        var line = $$"""{"type":"user","message":{"role":"user","content":[{{json}}]},"session_id":"s-1"}""";
        var decoded = new FrameCodec().Decode(line.ReplaceLineEndings(string.Empty));
        Assert.IsTrue(decoded.IsOk, Recorded.Reason(decoded));
        Assert.IsInstanceOfType<MessageFrame>(decoded.Value, out var frame);
        Assert.IsInstanceOfType<UserMessage>(frame.Message, out var user);
        return user.Content.Single();
    }
}
