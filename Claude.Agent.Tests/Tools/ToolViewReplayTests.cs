using System.Collections.Immutable;
using System.Text.Json;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Tools;

// The typed views over the four recordings that call the built-in tools: through the run, where a
// tool call carries its input and, once it closes, its output; and through the hooks, where the same
// JSON arrives as a PreToolUse tool_input and a PostToolUse tool_response. Every recorded call of a
// tool this library types reads as its type and carries the recorded values.
[TestClass]
public sealed class ToolViewReplayTests
{
    private const string RecordedCwd = "/tmp/claude-agent-fixtures/cwd";

    private const string ToolsPrompt =
        "Do these steps in order, one tool call each unless a step says otherwise, without explaining: "
        + "1. Glob the pattern *.txt. 2. Grep the pattern line in notes.txt with output mode content. "
        + "3. In one message, Read notes.txt and Read cells.ipynb together. 4. Write draft.txt containing exactly: alpha. "
        + "5. Edit draft.txt replacing alpha with beta. "
        + "6. NotebookEdit cells.ipynb: replace the source of the first cell with print(2). "
        + "7. TodoWrite a list with one item, content check the draft, status completed. "
        + "8. WebFetch https://example.com with the prompt: what is the page title? "
        + "9. Run the shell command `wc -l notes.txt` with Bash. 10. Read missing.txt. "
        + "Then reply with exactly the word: done";

    private const string TaskToolsPrompt =
        "Do these steps in order, one tool call each, without explaining: 1. Run the shell command `sleep 30` with Bash "
        + "in the background. 2. Call TaskOutput on the task it started, without blocking, with a timeout of 1000. "
        + "3. Stop that task with TaskStop. Then reply with exactly the word: done";

    private const string ReadsPrompt =
        "Do these steps in order, one tool call each, without explaining: 1. Read notes.txt from line 2 with a limit of 1 line. "
        + "2. Read notes.txt. 3. Read notes.txt again. 4. Read dot.png. 5. Read page.pdf. 6. Read page.pdf with pages 1. "
        + "Then reply with exactly the word: done";

    private const string EditsPrompt =
        "Do these steps in order, one tool call each, without explaining: 1. Glob the pattern *.txt in the directory "
        + RecordedCwd + ". 2. Grep the pattern line with output mode files_with_matches. "
        + "3. Grep the pattern LINE case-insensitively with output mode count. "
        + "4. Grep the pattern second in notes.txt with output mode content, with line numbers, one line of context before each match, and a head limit of 1. "
        + "5. Write draft.txt containing exactly: a a. 6. Write draft.txt containing exactly: a a a. "
        + "7. Edit draft.txt replacing every a with b. 8. Edit draft.txt replacing zzz with y. "
        + "9. Run the shell command `wc -l notes.txt` with Bash with a timeout of 30000. "
        + "10. Run the shell command `grep zzz notes.txt` with Bash. 11. Run the shell command `ls nowhere` with Bash. "
        + "12. Run the shell command `touch guarded.txt` with Bash. Then reply with exactly the word: done";

    // The one-pixel PNG the reads fixture read.
    private const string Dot = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private static readonly string[] ToolsFixtureTools =
        ["Glob", "Grep", "Read", "Write", "Edit", "NotebookEdit", "TodoWrite", "WebFetch", "Bash"];

    private static readonly string[] EditsFixtureTools = ["Glob", "Grep", "Write", "Edit", "Bash"];

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    // The tools fixture is replayed once for this class: the two tests below read the same run, one
    // through the calls it paired and one through the hooks it answered.
    private static readonly Lazy<Task<ToolsReplay>> Tools = new(ReplayToolsAsync);

    private static readonly Lazy<Task<ToolsReplay>> Reads = new(() => ReplayAsync(
        "reads", ReadsPrompt, ["Read"], maxTurns: 10, _ => new HookOutput()));

    // The recorder's guard: a PreToolUse hook that denies the shell command that would make a file.
    private static readonly Lazy<Task<ToolsReplay>> Edits = new(() => ReplayAsync(
        "edits", EditsPrompt, EditsFixtureTools, maxTurns: 16, input =>
            input is PreToolUseHookInput { ToolCall.Input: BashToolInput bash } && bash.Command.Contains("touch", StringComparison.Ordinal)
                ? new HookOutput { Decision = PermissionDecision.Deny, DecisionReason = "guarded: no new files" }
                : new HookOutput()));

    [TestMethod]
    public async Task EveryCallOfTheToolsFixtureCarriesItsTypedInputAndOutputThroughTheRun()
    {
        var replay = await Tools.Value;
        var calls = replay.Closed;

        CollectionAssert.AreEqual(
            new[] { "Glob", "Grep", "Read", "Read", "Write", "Edit", "NotebookEdit", "WebFetch", "Bash", "Read" },
            calls.Select(call => call.Name).ToArray());

        var glob = (GlobToolInput)calls[0].Input!;
        Assert.AreEqual("*.txt", glob.Pattern);
        Assert.IsNull(glob.Path);
        var globOutput = (GlobToolOutput)calls[0].Output!;
        CollectionAssert.AreEqual(new[] { "notes.txt" }, globOutput.FileNames.ToArray());
        Assert.AreEqual(1, globOutput.FileCount);
        Assert.AreEqual(TimeSpan.FromMilliseconds(3), globOutput.Duration);
        Assert.IsFalse(globOutput.Truncated);
        Assert.AreEqual(1, globOutput.TotalMatchCount);
        Assert.AreEqual(true, globOutput.CountIsComplete);

        var grep = (GrepToolInput)calls[1].Input!;
        Assert.AreEqual("line", grep.Pattern);
        Assert.AreEqual($"{RecordedCwd}/notes.txt", grep.Path);
        Assert.AreEqual(GrepOutputMode.Content, grep.OutputMode);
        var grepOutput = (GrepContentToolOutput)calls[1].Output!;
        Assert.AreEqual("1:first line\n2:second line", grepOutput.Content);
        Assert.AreEqual(2, grepOutput.LineCount);
        Assert.AreEqual(2, grepOutput.TotalLineCount);
        Assert.IsNull(grepOutput.AppliedLimit);
        Assert.AreEqual(calls[1].RawOutput!.Value.GetRawText(), ToolTypes.WriteOutput(grepOutput).GetRawText());

        var read = (ReadToolInput)calls[2].Input!;
        Assert.AreEqual($"{RecordedCwd}/notes.txt", read.FilePath);
        Assert.IsNull(read.Offset);
        var text = (ReadTextToolOutput)calls[2].Output!;
        Assert.AreEqual($"{RecordedCwd}/notes.txt", text.File.FilePath);
        Assert.AreEqual("first line\nsecond line\n", text.File.Content);
        Assert.AreEqual(3, text.File.LineCount);
        Assert.AreEqual(1, text.File.StartLine);
        Assert.AreEqual(3, text.File.TotalLineCount);
        Assert.IsNull(text.File.TruncatedByTokenCap);
        Assert.IsNull(text.ArtifactRead);

        var notebookRead = (ReadToolInput)calls[3].Input!;
        Assert.AreEqual($"{RecordedCwd}/cells.ipynb", notebookRead.FilePath);
        var notebookOutput = (ReadNotebookToolOutput)calls[3].Output!;
        Assert.AreEqual($"{RecordedCwd}/cells.ipynb", notebookOutput.File.FilePath);
        Assert.AreEqual(1, notebookOutput.File.Cells.Count);
        Assert.AreEqual(NotebookCellType.Code, notebookOutput.File.Cells[0].CellType);
        Assert.AreEqual("print(1)", notebookOutput.File.Cells[0].Source);
        Assert.AreEqual("a1", notebookOutput.File.Cells[0].CellId);
        Assert.AreEqual("pyt" + "hon", notebookOutput.File.Cells[0].Language);

        var write = (WriteToolInput)calls[4].Input!;
        Assert.AreEqual($"{RecordedCwd}/draft.txt", write.FilePath);
        Assert.AreEqual("alpha", write.Content);
        var writeOutput = (WriteCreateToolOutput)calls[4].Output!;
        Assert.AreEqual($"{RecordedCwd}/draft.txt", writeOutput.FilePath);
        Assert.AreEqual("alpha", writeOutput.Content);
        Assert.AreEqual(0, calls[4].RawOutput!.Value.GetProperty("structuredPatch").GetArrayLength());
        Assert.AreEqual(JsonValueKind.Null, calls[4].RawOutput!.Value.GetProperty("originalFile").ValueKind);
        Assert.AreEqual(false, writeOutput.UserModified);
        Assert.IsNull(writeOutput.GitDiff);

        var edit = (EditToolInput)calls[5].Input!;
        Assert.AreEqual($"{RecordedCwd}/draft.txt", edit.FilePath);
        Assert.AreEqual("alpha", edit.OldString);
        Assert.AreEqual("beta", edit.NewString);
        Assert.AreEqual(false, edit.ReplaceAll);
        var editOutput = (EditToolOutput)calls[5].Output!;
        Assert.AreEqual("alpha", editOutput.OriginalFile);
        Assert.IsFalse(editOutput.UserModified);
        Assert.IsFalse(editOutput.ReplaceAll);
        var hunk = editOutput.StructuredPatch.Single();
        Assert.AreEqual(1, hunk.OldStart);
        Assert.AreEqual(1, hunk.OldLines);
        Assert.AreEqual(1, hunk.NewStart);
        Assert.AreEqual(1, hunk.NewLines);
        Assert.AreEqual(4, hunk.Lines.Count);
        Assert.AreEqual("-alpha", hunk.Lines[0]);
        Assert.AreEqual("+beta", hunk.Lines[2]);

        var notebookEdit = (NotebookEditToolInput)calls[6].Input!;
        Assert.AreEqual($"{RecordedCwd}/cells.ipynb", notebookEdit.NotebookPath);
        Assert.AreEqual("a1", notebookEdit.CellId);
        Assert.AreEqual("print(2)", notebookEdit.NewSource);
        Assert.IsNull(notebookEdit.EditMode);
        var notebookEditOutput = (NotebookEditToolOutput)calls[6].Output!;
        Assert.AreEqual("print(2)", notebookEditOutput.NewSource);
        Assert.AreEqual("print(1)", notebookEditOutput.OldSource);
        Assert.AreEqual(NotebookCellType.Code, notebookEditOutput.CellType);
        Assert.AreEqual(NotebookEditMode.Replace, notebookEditOutput.EditMode);
        Assert.AreEqual("a1", notebookEditOutput.CellId);
        Assert.AreEqual(string.Empty, notebookEditOutput.Error);
        Assert.AreEqual($"{RecordedCwd}/cells.ipynb", notebookEditOutput.NotebookPath);
        Assert.AreEqual(
            calls[6].RawOutput!.Value.GetProperty("language").GetString(),
            notebookEditOutput.Language);
        Assert.IsTrue(notebookEditOutput.UpdatedFile.Contains("print(2)", StringComparison.Ordinal));
        Assert.IsTrue(notebookEditOutput.OriginalFile.Contains("print(1)", StringComparison.Ordinal));

        var fetch = (WebFetchToolInput)calls[7].Input!;
        Assert.AreEqual("https://example.com", fetch.Url);
        Assert.AreEqual("what is the page title?", fetch.Prompt);
        var fetchOutput = (WebFetchToolOutput)calls[7].Output!;
        Assert.AreEqual(559, fetchOutput.Bytes);
        Assert.AreEqual(200, fetchOutput.Code);
        Assert.AreEqual("OK", fetchOutput.CodeText);
        Assert.AreEqual("The page title is \"Example Domain.\"", fetchOutput.Result);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1702), fetchOutput.Duration);
        Assert.AreEqual("https://example.com", fetchOutput.Url);
        Assert.IsNull(fetchOutput.ArtifactRead);

        var bash = (BashToolInput)calls[8].Input!;
        Assert.AreEqual($"wc -l {RecordedCwd}/notes.txt", bash.Command);
        Assert.IsNull(bash.Timeout);
        Assert.IsNull(bash.RunInBackground);
        var bashOutput = (BashToolOutput)calls[8].Output!;
        Assert.AreEqual($"2 {RecordedCwd}/notes.txt", bashOutput.StandardOutput);
        Assert.AreEqual(string.Empty, bashOutput.StandardError);
        Assert.IsFalse(bashOutput.Interrupted);
        Assert.AreEqual(false, bashOutput.IsImage);
        Assert.AreEqual(false, bashOutput.NoOutputExpected);
        Assert.IsNull(bashOutput.BackgroundTaskId);
        Assert.IsNull(bashOutput.GitOperation);

        // The tenth call failed. Its typed input still reads; it closes with no output at all, typed
        // or raw, and the tool_use_result the frame carried is a JSON string, which is no tool's
        // shape: ToolTypes reads it as null.
        var failed = calls[9];
        Assert.AreEqual(ToolCallStatus.Failed, failed.Status);
        Assert.AreEqual($"{RecordedCwd}/missing.txt", ((ReadToolInput)failed.Input!).FilePath);
        Assert.IsNull(failed.Output);
        Assert.IsNull(failed.RawOutput);
        Assert.AreEqual(
            $"File does not exist. Note: your current working directory is {RecordedCwd}.",
            failed.Error);

        using var refused = JsonDocument.Parse("\"Error: File does not exist.\"");
        Assert.IsNull(ToolTypes.ReadOutput("Read", refused.RootElement));
    }

    [TestMethod]
    public async Task EveryCallOfTheToolsFixtureCarriesItsTypedInputAndOutputThroughTheHooks()
    {
        var replay = await Tools.Value;

        CollectionAssert.AreEqual(
            new[]
            {
                "PreToolUse Glob", "PostToolUse Glob",
                "PreToolUse Grep", "PostToolUse Grep",
                "PreToolUse Read", "PostToolUse Read",
                "PreToolUse Read", "PostToolUse Read",
                "PreToolUse Write", "PostToolUse Write",
                "PreToolUse Edit", "PostToolUse Edit",
                "PreToolUse NotebookEdit", "PostToolUse NotebookEdit",
                "PreToolUse WebFetch", "PostToolUse WebFetch",
                "PreToolUse Bash", "PostToolUse Bash",
                "PreToolUse Read", "PostToolUseFailure Read",
            },
            replay.Hooks.Select(hook => $"{hook.Event} {hook.ToolName}").ToArray());

        foreach (var hook in replay.Hooks)
        {
            var input = ToolTypes.ReadInput(hook.ToolName!, hook.ToolInput!.Value);
            Assert.IsNotNull(input, $"{hook.Event} {hook.ToolName}: the hook's tool input does not read as its type");
            if (hook.ToolResponse is { } response)
            {
                Assert.IsNotNull(
                    ToolTypes.ReadOutput(hook.ToolName!, response),
                    $"{hook.Event} {hook.ToolName}: the hook's tool response does not read as its type");
            }
        }

        // A PostToolUse tool_response and the user frame's tool_use_result are the same object on
        // success: nine calls succeeded, and each hook's typed output equals the run's.
        var answered = replay.Closed.Where(call => call.Status == ToolCallStatus.Answered).ToArray();
        Assert.AreEqual(9, answered.Length);
        foreach (var call in answered)
        {
            var hook = replay.Hooks.Single(one => one.ToolUseId == call.Id && one.ToolResponse is not null);
            Assert.AreEqual(
                ToolTypes.WriteOutput(ToolTypes.ReadOutput(hook.ToolName!, hook.ToolResponse!.Value)!).GetRawText(),
                ToolTypes.WriteOutput(call.Output!).GetRawText(),
                $"{call.Name}: the hook's output and the run's differ");
        }

        // The failing Read's PostToolUseFailure carries the tool's input and no response at all.
        var failure = replay.Hooks.Single(hook => hook.Event == HookEvent.PostToolUseFailure);
        Assert.IsNull(failure.ToolResponse);
        Assert.AreEqual($"{RecordedCwd}/missing.txt", ((ReadToolInput)ToolTypes.ReadInput("Read", failure.ToolInput!.Value)!).FilePath);
    }

    // Read in each of its output shapes: a slice of a text file, the whole file, the same file again
    // unchanged, an image, a PDF whole and a PDF by page. The tool result a caller reads beside the
    // output carries the image, the document and the rendered page as blocks.
    [TestMethod]
    public async Task EveryCallOfTheReadsFixtureCarriesItsTypedInputAndOutputThroughTheRun()
    {
        var replay = await Reads.Value;
        var calls = replay.Closed;

        Assert.AreEqual(6, calls.Length);
        Assert.IsTrue(calls.All(call => call.Name == "Read" && call.Status == ToolCallStatus.Answered));

        var slice = (ReadToolInput)calls[0].Input!;
        Assert.AreEqual($"{RecordedCwd}/notes.txt", slice.FilePath);
        Assert.AreEqual(1, slice.Offset);
        Assert.AreEqual(1, slice.Limit);
        Assert.IsNull(slice.Pages);
        var sliced = ((ReadTextToolOutput)calls[0].Output!).File;
        Assert.AreEqual("first line", sliced.Content);
        Assert.AreEqual(1, sliced.LineCount);
        Assert.AreEqual(1, sliced.StartLine);
        Assert.AreEqual(3, sliced.TotalLineCount);

        var whole = ((ReadTextToolOutput)calls[1].Output!).File;
        Assert.AreEqual("first line\nsecond line\n", whole.Content);
        Assert.AreEqual(3, whole.LineCount);

        var unchanged = (ReadFileUnchangedToolOutput)calls[2].Output!;
        Assert.AreEqual($"{RecordedCwd}/notes.txt", unchanged.File.FilePath);
        Assert.IsNull(unchanged.Source);

        var image = ((ReadImageToolOutput)calls[3].Output!).File;
        Assert.AreEqual(Dot, image.Base64);
        Assert.AreEqual(ImageMediaType.Png, image.MediaType);
        Assert.AreEqual(70, image.OriginalSize);
        Assert.AreEqual(1, image.Dimensions!.OriginalWidth);
        Assert.AreEqual(1, image.Dimensions.DisplayHeight);
        var imageBlock = (ImageBlock)calls[3].Content.Single();
        Assert.AreEqual(Dot, ((Base64ImageSource)imageBlock.Source).Data);

        var pdf = ((ReadPdfToolOutput)calls[4].Output!).File;
        Assert.AreEqual($"{RecordedCwd}/page.pdf", pdf.FilePath);
        Assert.AreEqual(582, pdf.OriginalSize);
        StringAssert.StartsWith(pdf.Base64, "JVBERi0xLjQK");
        Assert.AreEqual(2, calls[4].Content.Count);
        Assert.AreEqual($"PDF file read: {RecordedCwd}/page.pdf (582 bytes)", ((TextBlock)calls[4].Content[0]).Text);
        var document = (Base64DocumentSource)((DocumentBlock)calls[4].Content[1]).Source;
        Assert.AreEqual(pdf.Base64, document.Data);
        Assert.AreEqual(DocumentMediaType.Pdf, document.MediaType);

        Assert.AreEqual("1", ((ReadToolInput)calls[5].Input!).Pages);
        var parts = (ReadPartsToolOutput)calls[5].Output!;
        Assert.AreEqual($"{RecordedCwd}/page.pdf", parts.File.FilePath);
        Assert.AreEqual(582, parts.File.OriginalSize);
        Assert.AreEqual(1, parts.File.PageCount);
        StringAssert.StartsWith(parts.File.OutputDirectory, "/home/user/.claude/projects/-tmp-claude-agent-fixtures-cwd/");
        Assert.AreEqual(1, parts.FirstPage);
        Assert.AreEqual(0, parts.Pages.Count);
        Assert.AreEqual(ImageMediaType.Jpeg, ((Base64ImageSource)((ImageBlock)calls[5].Content[1]).Source).MediaType);

        CollectionAssert.AreEqual(
            Enumerable.Range(0, 6).SelectMany(_ => new[] { "PreToolUse Read", "PostToolUse Read" }).ToArray(),
            replay.Hooks.Select(hook => $"{hook.Event} {hook.ToolName}").ToArray());
        AssertEachHookReadsAsItsTypeAndMatchesTheRun(replay, except: calls[5].Id);

        // The one output the hook and the run do not share: the PostToolUse response of the read by
        // page carries the rendered page, which the run has only as the tool result's image block.
        var pageHook = replay.Hooks.Single(hook => hook.ToolUseId == calls[5].Id && hook.ToolResponse is not null);
        var hookParts = (ReadPartsToolOutput)ToolTypes.ReadOutput("Read", pageHook.ToolResponse!.Value)!;
        var page = hookParts.Pages.Single();
        Assert.AreEqual(ImageMediaType.Jpeg, page.MediaType);
        Assert.IsNull(page.Error);
        Assert.AreEqual(
            ((Base64ImageSource)((ImageBlock)calls[5].Content[1]).Source).Data,
            page.Base64);
    }

    // The tools that search and change files, and the shell, past the tools fixture: Glob under a
    // directory, Grep in its other two modes and with its flags, Write over a file that is there, Edit
    // replacing every match, Bash with a timeout and with a code the binary reads as no match. Four
    // calls fail, each its own way: a Grep the binary refuses before any hook, an Edit that finds
    // nothing, a command that exits non-zero, and one a PreToolUse hook denies.
    [TestMethod]
    public async Task EveryCallOfTheEditsFixtureCarriesItsTypedInputAndOutputThroughTheRun()
    {
        var replay = await Edits.Value;
        var calls = replay.Closed;

        CollectionAssert.AreEqual(
            new[] { "Glob", "Grep", "Grep", "Grep", "Grep", "Write", "Write", "Edit", "Edit", "Bash", "Bash", "Bash", "Bash" },
            calls.Select(call => call.Name).ToArray());

        Assert.AreEqual(RecordedCwd, ((GlobToolInput)calls[0].Input!).Path);
        var glob = (GlobToolOutput)calls[0].Output!;
        CollectionAssert.AreEqual(new[] { "notes.txt" }, glob.FileNames.ToArray());
        Assert.AreEqual(TimeSpan.FromMilliseconds(8), glob.Duration);

        Assert.AreEqual(GrepOutputMode.FilesWithMatches, ((GrepToolInput)calls[1].Input!).OutputMode);
        var files = (GrepFilesWithMatchesToolOutput)calls[1].Output!;
        CollectionAssert.AreEqual(new[] { "notes.txt" }, files.FileNames.ToArray());
        Assert.AreEqual(1, files.FileCount);
        Assert.AreEqual(1, files.TotalFileCount);
        Assert.IsNull(files.AppliedLimit);

        // The binary refused this input for a key its schema does not know: the typed view reads
        // what it knows and leaves the flag unset, and the call closes failed.
        var refused = calls[2];
        Assert.AreEqual(ToolCallStatus.Failed, refused.Status);
        Assert.IsNull(((GrepToolInput)refused.Input!).CaseInsensitive);
        Assert.AreEqual("true", refused.RawInput.GetProperty("i").GetString());
        Assert.IsNull(refused.Output);
        StringAssert.StartsWith(refused.Error, "<tool_use_error>InputValidationError: Grep failed");

        var countInput = (GrepToolInput)calls[3].Input!;
        Assert.AreEqual(true, countInput.CaseInsensitive);
        Assert.AreEqual(GrepOutputMode.Count, countInput.OutputMode);
        var count = (GrepCountToolOutput)calls[3].Output!;
        Assert.AreEqual("notes.txt:2", count.Content);
        Assert.AreEqual(2, count.MatchCount);
        Assert.AreEqual(1, count.FileCount);

        var contentInput = (GrepToolInput)calls[4].Input!;
        Assert.AreEqual("notes.txt", contentInput.Path);
        Assert.AreEqual(true, contentInput.ShowLineNumbers);
        Assert.AreEqual(1, contentInput.BeforeContextLines);
        Assert.AreEqual(1, contentInput.HeadLimit);
        var content = (GrepContentToolOutput)calls[4].Output!;
        Assert.AreEqual("1-first line", content.Content);
        Assert.AreEqual(1, content.LineCount);
        Assert.AreEqual(2, content.TotalLineCount);
        Assert.AreEqual(1, content.AppliedLimit);
        Assert.IsNull(content.AppliedOffset);

        // Each mode's output written back is what the binary wrote, byte for byte: its key order, and
        // beside lines and counts the file count of 0 or the empty file names their types carry no
        // member for.
        foreach (var answered in new[] { calls[1], calls[3], calls[4] })
        {
            Assert.AreEqual(answered.RawOutput!.Value.GetRawText(), ToolTypes.WriteOutput(answered.Output!).GetRawText(), answered.Name);
        }

        Assert.IsInstanceOfType<WriteCreateToolOutput>(calls[5].Output);
        var update = (WriteUpdateToolOutput)calls[6].Output!;
        Assert.AreEqual("a a a", update.Content);
        Assert.AreEqual("a a", update.OriginalFile);
        CollectionAssert.AreEqual(
            new[] { "-a a", "\\ No newline at end of file", "+a a a", "\\ No newline at end of file" },
            update.StructuredPatch.Single().Lines.ToArray());

        // A file made, written back, is what the binary wrote byte for byte, the empty patch and the
        // null contents its type carries no member for among it; a file replaced is what the binary
        // wrote, its patch's lines as they are escaped aside.
        Assert.AreEqual(calls[5].RawOutput!.Value.GetRawText(), ToolTypes.WriteOutput(calls[5].Output!).GetRawText());
        Assert.IsTrue(JsonElement.DeepEquals(calls[6].RawOutput!.Value, ToolTypes.WriteOutput(calls[6].Output!)));

        Assert.AreEqual(true, ((EditToolInput)calls[7].Input!).ReplaceAll);
        var everyMatch = (EditToolOutput)calls[7].Output!;
        Assert.IsTrue(everyMatch.ReplaceAll);
        Assert.AreEqual("a a a", everyMatch.OriginalFile);
        Assert.AreEqual("+b b b", everyMatch.StructuredPatch.Single().Lines[2]);

        var notFound = calls[8];
        Assert.AreEqual(ToolCallStatus.Failed, notFound.Status);
        Assert.AreEqual(false, ((EditToolInput)notFound.Input!).ReplaceAll);
        Assert.IsNull(notFound.Output);
        Assert.AreEqual("<tool_use_error>String to replace not found in file.\nString: zzz</tool_use_error>", notFound.Error);

        var timed = (BashToolInput)calls[9].Input!;
        Assert.AreEqual("wc -l notes.txt", timed.Command);
        Assert.AreEqual(TimeSpan.FromMilliseconds(30000), timed.Timeout);
        Assert.AreEqual("2 notes.txt", ((BashToolOutput)calls[9].Output!).StandardOutput);

        var noMatch = (BashToolOutput)calls[10].Output!;
        Assert.AreEqual(string.Empty, noMatch.StandardOutput);
        Assert.AreEqual("No matches found", noMatch.ReturnCodeInterpretation);

        var exited = calls[11];
        Assert.AreEqual(ToolCallStatus.Failed, exited.Status);
        Assert.IsNull(exited.Output);
        Assert.AreEqual("Exit code 2\nls: cannot access 'nowhere': No such file or directory", exited.Error);

        var denied = calls[12];
        Assert.AreEqual(ToolCallStatus.Failed, denied.Status);
        Assert.AreEqual("touch guarded.txt", ((BashToolInput)denied.Input!).Command);
        Assert.IsNull(denied.Output);
        Assert.AreEqual("guarded: no new files", denied.Error);

        // The refused Grep and the Edit that found nothing never reach a hook; the denied command
        // reaches PreToolUse and no hook after it.
        CollectionAssert.AreEqual(
            new[]
            {
                "PreToolUse Glob", "PostToolUse Glob",
                "PreToolUse Grep", "PostToolUse Grep",
                "PreToolUse Grep", "PostToolUse Grep",
                "PreToolUse Grep", "PostToolUse Grep",
                "PreToolUse Write", "PostToolUse Write",
                "PreToolUse Write", "PostToolUse Write",
                "PreToolUse Edit", "PostToolUse Edit",
                "PreToolUse Bash", "PostToolUse Bash",
                "PreToolUse Bash", "PostToolUse Bash",
                "PreToolUse Bash", "PostToolUseFailure Bash",
                "PreToolUse Bash",
            },
            replay.Hooks.Select(hook => $"{hook.Event} {hook.ToolName}").ToArray());
        AssertEachHookReadsAsItsTypeAndMatchesTheRun(replay);
    }

    [TestMethod]
    public async Task EveryCallOfTheTaskToolsFixtureCarriesItsTypedInputAndOutputThroughTheRun()
    {
        var calls = await ReplayTaskToolsAsync();

        CollectionAssert.AreEqual(
            new[] { "Bash", "TaskOutput", "TaskStop" },
            calls.Select(call => call.Name).ToArray());

        var bash = (BashToolInput)calls[0].Input!;
        Assert.AreEqual("sleep 30", bash.Command);
        Assert.AreEqual(true, bash.RunInBackground);
        var bashOutput = (BashToolOutput)calls[0].Output!;
        Assert.AreEqual(string.Empty, bashOutput.StandardOutput);
        Assert.AreEqual("bregtd04n", bashOutput.BackgroundTaskId);
        Assert.IsFalse(bashOutput.Interrupted);

        var taskOutput = (TaskOutputToolInput)calls[1].Input!;
        Assert.AreEqual("bregtd04n", taskOutput.TaskId);
        Assert.IsFalse(taskOutput.Block);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1000), taskOutput.Timeout);
        var taskOutputOutput = (TaskOutputToolOutput)calls[1].Output!;
        Assert.AreEqual(TaskRetrievalStatus.NotReady, taskOutputOutput.RetrievalStatus);
        Assert.AreEqual("bregtd04n", taskOutputOutput.Task.TaskId);
        Assert.AreEqual("local_bash", taskOutputOutput.Task.TaskType);
        Assert.AreEqual("running", taskOutputOutput.Task.Status);
        Assert.AreEqual("sleep 30", taskOutputOutput.Task.Description);
        Assert.AreEqual(string.Empty, taskOutputOutput.Task.Output);
        Assert.IsNull(taskOutputOutput.Task.ExitCode);

        var stop = (TaskStopToolInput)calls[2].Input!;
        Assert.AreEqual("bregtd04n", stop.TaskId);
        Assert.IsNull(stop.ShellId);
        var stopOutput = (TaskStopToolOutput)calls[2].Output!;
        Assert.AreEqual("Successfully stopped task: bregtd04n (sleep 30)", stopOutput.Message);
        Assert.AreEqual("bregtd04n", stopOutput.TaskId);
        Assert.AreEqual("local_bash", stopOutput.TaskType);
        Assert.AreEqual("sleep 30", stopOutput.Command);
    }

    private sealed record Hook(HookEvent Event, string? ToolName, string? ToolUseId, JsonElement? ToolInput, JsonElement? ToolResponse);

    private sealed record ToolsReplay(ImmutableArray<ToolCall> Closed, ImmutableArray<Hook> Hooks);

    private static Task<ToolsReplay> ReplayToolsAsync() =>
        ReplayAsync("tools", ToolsPrompt, ToolsFixtureTools, maxTurns: 16, _ => new HookOutput());

    // A fixture replayed as its recorder ran it: the tools offered and allowed, and a hook on each of
    // the three tool events answering as the recorder's did.
    private static async Task<ToolsReplay> ReplayAsync(
        string fixture, string prompt, string[] tools, int maxTurns, Func<HookInput, HookOutput> answer)
    {
        var hooks = new List<Hook>();
        var gate = new Lock();
        await using var server = FixtureServer.Start(fixture);
        using var host = TestHost.Start(server, options =>
        {
            options.WorkingDirectory = RecordedCwd;
            options.SettingSources = [];
            options.Model = "haiku";
            options.MaxTurns = maxTurns;
            options.Effort = Effort.Low;
            options.Tools = [.. tools];
            foreach (var tool in tools)
            {
                options.AllowedTools.Add(tool);
            }

            foreach (var hookEvent in new[] { HookEvent.PreToolUse, HookEvent.PostToolUse, HookEvent.PostToolUseFailure })
            {
                options.AddHook(hookEvent, null, (input, _) =>
                {
                    lock (gate)
                    {
                        // The hook inputs are typed by their event, so the call is read off the
                        // event's own type; an event that carries none leaves the four null.
                        var call = input switch
                        {
                            PreToolUseHookInput pre => pre.ToolCall,
                            PostToolUseHookInput post => post.ToolCall,
                            PostToolUseFailureHookInput failed => failed.ToolCall,
                            PermissionRequestHookInput asked => asked.ToolCall,
                            _ => null,
                        };
                        hooks.Add(new Hook(input.Event, call?.Name, call?.Id, call?.RawInput, call?.RawOutput));
                    }

                    return Task.FromResult(answer(input));
                });
            }
        });

        var closed = await ClosedCallsAsync(host.Client.QueryAsync(prompt));
        await server.WaitForWrites(Fixture.Load(fixture).Stdin.Length, Patience);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        return new ToolsReplay(closed, [.. hooks]);
    }

    // Every hook's tool input, and response where it carries one, reads as its tool's type, and a
    // PostToolUse response is the output the run closed that call with, but for the one call named.
    private static void AssertEachHookReadsAsItsTypeAndMatchesTheRun(ToolsReplay replay, string? except = null)
    {
        foreach (var hook in replay.Hooks)
        {
            Assert.IsNotNull(
                ToolTypes.ReadInput(hook.ToolName!, hook.ToolInput!.Value),
                $"{hook.Event} {hook.ToolName}: the hook's tool input does not read as its type");
            if (hook.ToolResponse is not { } response)
            {
                continue;
            }

            var output = ToolTypes.ReadOutput(hook.ToolName!, response);
            Assert.IsNotNull(output, $"{hook.Event} {hook.ToolName}: the hook's tool response does not read as its type");
            var call = replay.Closed.Single(one => one.Id == hook.ToolUseId);
            if (call.Id == except)
            {
                continue;
            }

            Assert.AreEqual(
                ToolTypes.WriteOutput(call.Output!).GetRawText(),
                ToolTypes.WriteOutput(output).GetRawText(),
                $"{call.Name}: the hook's output and the run's differ");
        }
    }

    private static async Task<ImmutableArray<ToolCall>> ReplayTaskToolsAsync()
    {
        await using var server = FixtureServer.Start("task-tools");
        using var host = TestHost.Start(server, options =>
        {
            options.WorkingDirectory = RecordedCwd;
            options.SettingSources = [];
            options.Model = "haiku";
            options.MaxTurns = 6;
            options.Effort = Effort.Low;
            options.Tools = ["Bash", "TaskOutput", "TaskStop"];
            foreach (var tool in new[] { "Bash", "TaskOutput", "TaskStop" })
            {
                options.AllowedTools.Add(tool);
            }
        });

        var closed = await ClosedCallsAsync(host.Client.QueryAsync(TaskToolsPrompt));
        await server.WaitForWrites(2, Patience);
        Assert.IsNull(server.Failure, $"the fixture server failed: {server.Failure}");
        return closed;
    }

    // The closing snapshot of every call the run paired, in the order the calls started.
    private static async Task<ImmutableArray<ToolCall>> ClosedCallsAsync(ClaudeAgentRun run)
    {
        var trace = await run.WaitForCompletionAsync();
        return [.. trace.ToolCalls];
    }
}
