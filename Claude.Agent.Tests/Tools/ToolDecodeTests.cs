using System.Text.Json;

namespace Claude.Agent.Tests.Tools;

// The shapes the declaration carries that no recording of this library's fixtures ever reached: the
// Read variants beside text and notebook, an Agent launched on the remote, a Write that updated, the
// diff a file tool carries against the repository, the keys a Bash output only has when it ran a git
// command, a Grep's page and its answer when nothing matched, and the rest. Every JSON here is hand-written against the
// declared shape, and every one of these tests would fail on a parser that read the key by another
// name or dropped it.
[TestClass]
public sealed class ToolDecodeTests
{
    [TestMethod]
    public void AReadOfAnImageCarriesItsMediaTypeAndBothPairsOfDimensions()
    {
        var output = Output<ReadImageToolOutput>("Read", """
            {"type":"image","file":{"base64":"iVBORw0KGgo=","type":"image/png","originalSize":18244,
             "dimensions":{"originalWidth":1920,"originalHeight":1080,"displayWidth":960,"displayHeight":540}}}
            """);

        Assert.AreEqual("iVBORw0KGgo=", output.File.Base64);
        Assert.AreEqual(ImageMediaType.Png, output.File.MediaType);
        Assert.AreEqual(18244L, output.File.OriginalSize);
        Assert.IsNotNull(output.File.Dimensions);
        Assert.AreEqual(1920L, output.File.Dimensions.OriginalWidth);
        Assert.AreEqual(1080L, output.File.Dimensions.OriginalHeight);
        Assert.AreEqual(960L, output.File.Dimensions.DisplayWidth);
        Assert.AreEqual(540L, output.File.Dimensions.DisplayHeight);
    }

    [TestMethod]
    public void AReadOfADocumentCarriesItsBytesAndAReadOfItsPagesCarriesOneImagePerPage()
    {
        var document = Output<ReadPdfToolOutput>("Read", """
            {"type":"pdf","file":{"filePath":"/work/paper.pdf","base64":"JVBERi0=","originalSize":90112}}
            """);
        Assert.AreEqual("/work/paper.pdf", document.File.FilePath);
        Assert.AreEqual("JVBERi0=", document.File.Base64);
        Assert.AreEqual(90112L, document.File.OriginalSize);

        var parts = Output<ReadPartsToolOutput>("Read", """
            {"type":"parts","file":{"filePath":"/work/paper.pdf","originalSize":90112,"count":2,"outputDir":"/work/pages"},
             "firstPage":3,
             "pages":[{"base64":"/9j/4AAQ","mediaType":"image/jpeg"},
                      {"base64":"","mediaType":"image/jpeg","error":"the page holds no text layer"}]}
            """);
        Assert.AreEqual(2L, parts.File.PageCount);
        Assert.AreEqual("/work/pages", parts.File.OutputDirectory);
        Assert.AreEqual(3L, parts.FirstPage);
        Assert.AreEqual(2, parts.Pages.Count);
        Assert.AreEqual(ImageMediaType.Jpeg, parts.Pages[0].MediaType);
        Assert.IsNull(parts.Pages[0].Error);
        Assert.AreEqual("the page holds no text layer", parts.Pages[1].Error);
    }

    [TestMethod]
    public void AReadOfAFileTheModelAlreadyHasCarriesWhyItWasNotSentAgain()
    {
        var output = Output<ReadFileUnchangedToolOutput>("Read", """
            {"type":"file_unchanged","file":{"filePath":"/work/notes.txt"},"source":"seeded"}
            """);

        Assert.AreEqual("/work/notes.txt", output.File.FilePath);
        Assert.AreEqual(FileUnchangedSource.Seeded, output.Source);

        var bare = Output<ReadFileUnchangedToolOutput>("Read", """
            {"type":"file_unchanged","file":{"filePath":"/work/notes.txt"}}
            """);
        Assert.IsNull(bare.Source);
    }

    [TestMethod]
    public void AReadOfTextCarriesTheStoresPlaceAndWhetherTheTokenCapCutIt()
    {
        var output = Output<ReadTextToolOutput>("Read", """
            {"type":"text",
             "file":{"filePath":"/work/long.txt","content":"the first line\n","numLines":1,"startLine":1,
                     "totalLines":94000,"truncatedByTokenCap":true},
             "artifactRead":{"slug":"long-txt","ver":"4"}}
            """);

        Assert.AreEqual(94000L, output.File.TotalLineCount);
        Assert.AreEqual(true, output.File.TruncatedByTokenCap);
        Assert.IsNotNull(output.ArtifactRead);
        Assert.AreEqual("long-txt", output.ArtifactRead.Slug);
        Assert.AreEqual("4", output.ArtifactRead.Version);
    }

    [TestMethod]
    public void AReadInputCarriesTheWindowAndThePageRange()
    {
        var input = Input<ReadToolInput>("Read", """
            {"file_path":"/work/paper.pdf","offset":120,"limit":40,"pages":"2-5"}
            """);

        Assert.AreEqual("/work/paper.pdf", input.FilePath);
        Assert.AreEqual(120L, input.Offset);
        Assert.AreEqual(40L, input.Limit);
        Assert.AreEqual("2-5", input.Pages);
    }

    [TestMethod]
    public void AWriteThatUpdatedAFileCarriesItsPatchItsOriginalAndItsDiffAgainstTheRepository()
    {
        var output = Output<WriteUpdateToolOutput>("Write", """
            {"type":"update","filePath":"/work/notes.txt","content":"beta\n","originalFile":"alpha\n",
             "structuredPatch":[{"oldStart":1,"oldLines":1,"newStart":1,"newLines":1,"lines":["-alpha","+beta"]}],
             "gitDiff":{"filename":"notes.txt","status":"modified","additions":1,"deletions":1,"changes":2,
                        "patch":"@@ -1 +1 @@\n-alpha\n+beta\n","repository":"the-repo"},
             "userModified":false,"staged":true}
            """);

        Assert.AreEqual("alpha\n", output.OriginalFile);
        Assert.AreEqual(1, output.StructuredPatch.Count);
        Assert.AreEqual(1L, output.StructuredPatch[0].OldStart);
        CollectionAssert.AreEqual(new[] { "-alpha", "+beta" }, output.StructuredPatch[0].Lines.ToArray());
        Assert.IsNotNull(output.GitDiff);
        Assert.AreEqual("notes.txt", output.GitDiff.FileName);
        Assert.AreEqual(GitDiffStatus.Modified, output.GitDiff.Status);
        Assert.AreEqual(2L, output.GitDiff.Changes);
        Assert.AreEqual("the-repo", output.GitDiff.Repository);
        Assert.AreEqual(false, output.UserModified);
        Assert.AreEqual(true, output.Staged);
    }

    [TestMethod]
    public void AnEditCarriesWhetherItReplacedEveryOccurrenceAndWhatTheUserChangedUnderIt()
    {
        var input = Input<EditToolInput>("Edit", """
            {"file_path":"/work/notes.txt","old_string":"alpha","new_string":"beta","replace_all":true}
            """);
        Assert.AreEqual(true, input.ReplaceAll);

        var output = Output<EditToolOutput>("Edit", """
            {"filePath":"/work/notes.txt","oldString":"alpha","newString":"beta","originalFile":"alpha\n",
             "structuredPatch":[{"oldStart":1,"oldLines":1,"newStart":1,"newLines":1,"lines":["-alpha","+beta"]}],
             "userModified":true,"replaceAll":false,
             "gitDiff":{"filename":"notes.txt","status":"added","additions":1,"deletions":0,"changes":1,"patch":"@@"},
             "staged":false}
            """);

        Assert.IsTrue(output.UserModified);
        Assert.IsFalse(output.ReplaceAll);
        Assert.AreEqual("alpha\n", output.OriginalFile);
        Assert.IsNotNull(output.GitDiff);
        Assert.AreEqual(GitDiffStatus.Added, output.GitDiff.Status);
        Assert.IsNull(output.GitDiff.Repository);
        Assert.AreEqual(false, output.Staged);
    }

    [TestMethod]
    public void ABashInputCarriesItsTimeoutAsASpanAndABashOutputTheSpanItTimedOutAfter()
    {
        var input = Input<BashToolInput>("Bash", """
            {"command":"sleep 5","timeout":120000,"description":"wait","run_in_background":true,
             "dangerouslyDisableSandbox":false}
            """);
        Assert.AreEqual(TimeSpan.FromMinutes(2), input.Timeout);
        Assert.AreEqual(true, input.RunInBackground);
        Assert.AreEqual(false, input.DangerouslyDisableSandbox);

        var output = Output<BashToolOutput>("Bash", """
            {"stdout":"","stderr":"","interrupted":true,"isImage":false,"timedOutAfterMs":2000,
             "backgroundTaskId":"bregtd04n","backgroundedByUser":true,"backgroundCwdHint":"/work",
             "backgroundEndsWithFinalResponse":false,"returnCodeInterpretation":"the command was killed",
             "noOutputExpected":true,"rawOutputPath":"/tmp/raw","persistedOutputPath":"/tmp/kept",
             "persistedOutputSize":4096,"staleReadFileStateHint":"notes.txt moved under you",
             "ghRateLimitHint":"one request left"}
            """);
        Assert.AreEqual(TimeSpan.FromSeconds(2), output.TimedOutAfter);
        Assert.AreEqual("bregtd04n", output.BackgroundTaskId);
        Assert.AreEqual("/work", output.BackgroundWorkingDirectoryHint);
        Assert.AreEqual(4096L, output.PersistedOutputSize);
        Assert.AreEqual("one request left", output.GitHubRateLimitHint);
        Assert.IsNull(output.GitOperation);
    }

    [TestMethod]
    public void ABashThatRanAGitCommandCarriesTheCommitThePushTheBranchAndThePullRequest()
    {
        var output = Output<BashToolOutput>("Bash", """
            {"stdout":"","stderr":"","interrupted":false,
             "gitOperation":{"commit":{"sha":"0f1e2d3c","kind":"cherry-picked","branch":"main"},
                             "push":{"branch":"main"},
                             "branch":{"ref":"refs/heads/run/tools","action":"rebased"},
                             "pr":{"number":31,"action":"auto-merge-enabled","url":"https://example.com/pull/31"}}}
            """);

        var operation = output.GitOperation;
        Assert.IsNotNull(operation);
        Assert.AreEqual("0f1e2d3c", operation.Commit?.Sha);
        Assert.AreEqual(GitCommitAction.CherryPicked, operation.Commit?.Kind);
        Assert.AreEqual("main", operation.Commit?.Branch);
        Assert.AreEqual("main", operation.Push?.Branch);
        Assert.AreEqual("refs/heads/run/tools", operation.Branch?.Reference);
        Assert.AreEqual(GitBranchAction.Rebased, operation.Branch?.Action);
        Assert.AreEqual(31L, operation.PullRequest?.Number);
        Assert.AreEqual(GitPullRequestAction.AutoMergeEnabled, operation.PullRequest?.Action);
        Assert.AreEqual("https://example.com/pull/31", operation.PullRequest?.Url);
    }

    [TestMethod]
    public void AGrepInputCarriesTheFlagsTheWireNamesByOneLetter()
    {
        var input = Input<GrepToolInput>("Grep", """
            {"pattern":"the line","path":"/work","glob":"*.cs","output_mode":"files_with_matches",
             "-B":1,"-A":2,"-C":3,"context":4,"-n":true,"-i":false,"-o":true,"type":"cs",
             "head_limit":20,"offset":5,"multiline":true}
            """);

        Assert.AreEqual(GrepOutputMode.FilesWithMatches, input.OutputMode);
        Assert.AreEqual(1L, input.BeforeContextLines);
        Assert.AreEqual(2L, input.AfterContextLines);
        Assert.AreEqual(3L, input.SurroundingContextLines);
        Assert.AreEqual(4L, input.ContextLines);
        Assert.AreEqual(true, input.ShowLineNumbers);
        Assert.AreEqual(false, input.CaseInsensitive);
        Assert.AreEqual(true, input.MatchesOnly);
        Assert.AreEqual("cs", input.FileType);
        Assert.AreEqual(20L, input.HeadLimit);
        Assert.AreEqual(5L, input.Offset);
        Assert.AreEqual(true, input.Multiline);
    }

    // The mode is which type the output is, each with the keys the binary writes in that mode every
    // time: here a page of each, with the totals beyond the page.
    [TestMethod]
    public void AGrepOutputIsOneTypeAModeAndCarriesItsPageAndTheTotalsBeyondIt()
    {
        var files = Output<GrepFilesWithMatchesToolOutput>("Grep", """
            {"mode":"files_with_matches","filenames":["/work/b.cs"],"numFiles":1,"totalFiles":40,
             "appliedLimit":1,"appliedOffset":1}
            """);
        CollectionAssert.AreEqual(new[] { "/work/b.cs" }, files.FileNames.ToArray());
        Assert.AreEqual(1L, files.FileCount);
        Assert.AreEqual(40L, files.TotalFileCount);
        Assert.AreEqual(1L, files.AppliedLimit);
        Assert.AreEqual(1L, files.AppliedOffset);

        var lines = Output<GrepContentToolOutput>("Grep", """
            {"mode":"content","numFiles":0,"filenames":[],"content":"a.cs:3:the line\na.cs:9:the line",
             "numLines":2,"totalLines":900,"appliedLimit":2}
            """);
        Assert.AreEqual("a.cs:3:the line\na.cs:9:the line", lines.Content);
        Assert.AreEqual(2L, lines.LineCount);
        Assert.AreEqual(900L, lines.TotalLineCount);
        Assert.AreEqual(2L, lines.AppliedLimit);
        Assert.IsNull(lines.AppliedOffset);

        var counted = Output<GrepCountToolOutput>("Grep", """
            {"mode":"count","numFiles":2,"filenames":[],"content":"a.cs:7","numMatches":9,"appliedLimit":1}
            """);
        Assert.AreEqual("a.cs:7", counted.Content);
        Assert.AreEqual(2L, counted.FileCount);
        Assert.AreEqual(9L, counted.MatchCount);
        Assert.AreEqual(1L, counted.AppliedLimit);
        Assert.IsNull(counted.AppliedOffset);
    }

    // When the search target does not exist the binary answers early, in each mode's own shape with
    // nothing in it and fewer keys than a search that ran.
    [TestMethod]
    public void AGrepThatMatchedNothingIsStillTheTypeOfItsMode()
    {
        var files = Output<GrepFilesWithMatchesToolOutput>("Grep", """
            {"mode":"files_with_matches","filenames":[],"numFiles":0,"totalFiles":0}
            """);
        Assert.AreEqual(0, files.FileNames.Count);
        Assert.AreEqual(0L, files.TotalFileCount);

        var lines = Output<GrepContentToolOutput>("Grep", """
            {"mode":"content","numFiles":0,"filenames":[],"content":"","numLines":0,"totalLines":0}
            """);
        Assert.AreEqual(string.Empty, lines.Content);
        Assert.AreEqual(0L, lines.TotalLineCount);

        var counted = Output<GrepCountToolOutput>("Grep", """
            {"mode":"count","numFiles":0,"filenames":[],"content":"","numMatches":0}
            """);
        Assert.AreEqual(string.Empty, counted.Content);
        Assert.AreEqual(0L, counted.MatchCount);
    }

    // An output that names no mode is file names, as the binary reads its own. A mode the
    // declaration does not name, or a type without a key it requires, is null beside the raw.
    [TestMethod]
    public void AGrepOutputThatNamesNoModeIsFileNamesAndAModeUndeclaredIsNull()
    {
        var unnamed = Output<GrepFilesWithMatchesToolOutput>("Grep", """
            {"filenames":["/work/a.cs"],"numFiles":1,"totalFiles":1}
            """);
        Assert.AreEqual(1L, unnamed.TotalFileCount);

        Assert.IsNull(Read("Grep", """{"mode":"files_without_match","filenames":[],"numFiles":0,"totalFiles":0}"""));
        Assert.IsNull(Read("Grep", """{"mode":1,"filenames":[],"numFiles":0,"totalFiles":0}"""));
        Assert.IsNull(Read("Grep", """{"mode":"files_with_matches","filenames":["/work/a.cs"],"numFiles":1}"""));
        Assert.IsNull(Read("Grep", """{"mode":"content","numFiles":0,"filenames":[],"content":"the line","numLines":1}"""));
        Assert.IsNull(Read("Grep", """{"mode":"count","numFiles":1,"filenames":[],"numMatches":2}"""));
        Assert.IsNull(Read("Grep", """{"numFiles":0,"filenames":[],"content":"the line","numLines":1,"totalLines":1}"""));
    }

    [TestMethod]
    public void AGlobOutputCarriesWhetherTheListWasCutAndWhetherTheCountIsWhole()
    {
        var output = Output<GlobToolOutput>("Glob", """
            {"durationMs":17,"numFiles":2,"filenames":["/work/a.cs","/work/b.cs"],"truncated":true,
             "totalMatches":4096,"countIsComplete":false}
            """);

        Assert.AreEqual(TimeSpan.FromMilliseconds(17), output.Duration);
        Assert.IsTrue(output.Truncated);
        Assert.AreEqual(4096L, output.TotalMatchCount);
        Assert.AreEqual(false, output.CountIsComplete);
    }

    [TestMethod]
    public void ATaskStopNamesEitherTheTaskOrTheShellAndAnswersWithWhatItStopped()
    {
        var shell = Input<TaskStopToolInput>("TaskStop", """{"shell_id":"shell-3"}""");
        Assert.IsNull(shell.TaskId);
        Assert.AreEqual("shell-3", shell.ShellId);

        var output = Output<TaskStopToolOutput>("TaskStop", """
            {"message":"the task was stopped","task_id":"bregtd04n","task_type":"local_bash","command":"sleep 30"}
            """);
        Assert.AreEqual("the task was stopped", output.Message);
        Assert.AreEqual("local_bash", output.TaskType);
        Assert.AreEqual("sleep 30", output.Command);
    }

    [TestMethod]
    public void ATaskOutputCarriesTheExitCodeOfATaskThatEnded()
    {
        var output = Output<TaskOutputToolOutput>("TaskOutput", """
            {"retrieval_status":"not_ready",
             "task":{"task_id":"bregtd04n","task_type":"local_bash","status":"completed",
                     "description":"sleep 30","output":"done\n","exitCode":0}}
            """);

        Assert.AreEqual(TaskRetrievalStatus.NotReady, output.RetrievalStatus);
        Assert.AreEqual("completed", output.Task.Status);
        Assert.AreEqual(0L, output.Task.ExitCode);
    }

    [TestMethod]
    public void ANotebookEditCarriesTheModeItRanInAndWhatWentWrongWhereItDid()
    {
        var inserted = Input<NotebookEditToolInput>("NotebookEdit", """
            {"notebook_path":"/work/cells.ipynb","new_source":"the cell","cell_id":"a1",
             "cell_type":"markdown","edit_mode":"insert"}
            """);
        Assert.AreEqual(NotebookCellType.Markdown, inserted.CellType);
        Assert.AreEqual(NotebookEditMode.Insert, inserted.EditMode);
        Assert.AreEqual("a1", inserted.CellId);

        var output = Output<NotebookEditToolOutput>("NotebookEdit", """
            {"new_source":"","cell_type":"code","language":"csharp","edit_mode":"delete",
             "notebook_path":"/work/cells.ipynb","original_file":"{}","updated_file":"{}",
             "old_source":"the cell","cell_id":"a1","error":"the cell was already gone"}
            """);
        Assert.AreEqual(NotebookEditMode.Delete, output.EditMode);
        Assert.AreEqual(NotebookCellType.Code, output.CellType);
        Assert.AreEqual("csharp", output.Language);
        Assert.AreEqual("the cell", output.OldSource);
        Assert.AreEqual("the cell was already gone", output.Error);
    }

    [TestMethod]
    public void AWebFetchCarriesTheStoresPlaceAndWhetherThePageWasAlreadySeeded()
    {
        var output = Output<WebFetchToolOutput>("WebFetch", """
            {"bytes":51232,"code":200,"codeText":"OK","result":"the page says so","durationMs":1702,
             "url":"https://example.com/page","artifactRead":{"slug":"example-page","ver":"2","seeded":true}}
            """);

        Assert.AreEqual(51232L, output.Bytes);
        Assert.AreEqual(200L, output.Code);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1702), output.Duration);
        Assert.IsNotNull(output.ArtifactRead);
        Assert.AreEqual("example-page", output.ArtifactRead.Slug);
        Assert.AreEqual("2", output.ArtifactRead.Version);
        Assert.AreEqual(true, output.ArtifactRead.Seeded);
    }

    [TestMethod]
    public void AWebSearchInputCarriesTheDomainsItWasHeldTo()
    {
        var input = Input<WebSearchToolInput>("WebSearch", """
            {"query":"the query","allowed_domains":["good.example"],"blocked_domains":["bad.example","worse.example"]}
            """);

        CollectionAssert.AreEqual(new[] { "good.example" }, input.AllowedDomains.ToArray());
        CollectionAssert.AreEqual(new[] { "bad.example", "worse.example" }, input.BlockedDomains.ToArray());
    }

    [TestMethod]
    public void AnAgentLaunchedOnTheRemoteCarriesItsTaskAndTheSessionToWatchIt()
    {
        var output = Output<AgentRemoteLaunchedToolOutput>("Agent", """
            {"status":"remote_launched","taskId":"task-9","sessionUrl":"https://example.com/s/9",
             "description":"look it up","prompt":"find the line","outputFile":"/tasks/task-9.output"}
            """);

        Assert.AreEqual("task-9", output.TaskId);
        Assert.AreEqual("https://example.com/s/9", output.SessionUrl);
        Assert.AreEqual("/tasks/task-9.output", output.OutputFile);
    }

    [TestMethod]
    public void AnAgentThatFinishedCarriesItsToolCountsItsWorktreeAndTheModelsItUsed()
    {
        var output = Output<AgentCompletedToolOutput>("Agent", """
            {"status":"completed","agentId":"agent-7","prompt":"find the line","agentType":"bee",
             "content":[{"type":"text","text":"the answer"}],
             "totalToolUseCount":6,"totalDurationMs":14220,"totalTokens":8548,
             "usage":{"input_tokens":12,"output_tokens":340,"cache_creation_input_tokens":1900,
                      "cache_read_input_tokens":22000,"service_tier":"standard","speed":"standard",
                      "inference_geo":"not_available",
                      "server_tool_use":{"web_search_requests":2,"web_fetch_requests":1},
                      "cache_creation":{"ephemeral_1h_input_tokens":0,"ephemeral_5m_input_tokens":1900},
                      "output_tokens_details":{"thinking_tokens":64}},
             "resolvedModel":"claude-haiku-4-5","modelsUsed":["claude-haiku-4-5","claude-sonnet-4-5"],
             "toolStats":{"readCount":4,"searchCount":1,"bashCount":2,"editFileCount":3,
                          "linesAdded":40,"linesRemoved":9,"otherToolCount":1,"frameCount":12},
             "worktreePath":"/work/tree","worktreeBranch":"run/bee",
             "harnessNoteCount":2,"harnessTailCount":1,"harnessSectionHash":"9c1f"}
            """);

        Assert.AreEqual("agent-7", output.AgentId);
        Assert.AreEqual("bee", output.AgentType);
        Assert.AreEqual(6L, output.TotalToolUseCount);
        Assert.AreEqual(TimeSpan.FromMilliseconds(14220), output.TotalDuration);
        Assert.AreEqual("the answer", output.Content[0].Text);
        CollectionAssert.AreEqual(new[] { "claude-haiku-4-5", "claude-sonnet-4-5" }, output.ModelsUsed.ToArray());
        Assert.AreEqual("/work/tree", output.WorktreePath);
        Assert.AreEqual("run/bee", output.WorktreeBranch);
        Assert.AreEqual(2L, output.HarnessNoteCount);
        Assert.AreEqual(1L, output.HarnessTailCount);
        Assert.AreEqual("9c1f", output.HarnessSectionHash);

        var statistics = output.ToolStatistics;
        Assert.IsNotNull(statistics);
        Assert.AreEqual(4L, statistics.ReadCount);
        Assert.AreEqual(40L, statistics.LinesAdded);
        Assert.AreEqual(9L, statistics.LinesRemoved);
        Assert.AreEqual(12L, statistics.FrameCount);

        Assert.AreEqual(ServiceTier.Standard, output.Usage.ServiceTier);
        Assert.AreEqual(22000L, output.Usage.CacheReadInputTokens);
        Assert.AreEqual(2L, output.Usage.ServerToolUse?.WebSearchRequests);
        Assert.AreEqual(1L, output.Usage.ServerToolUse?.WebFetchRequests);
        Assert.AreEqual(1900L, output.Usage.CacheCreation?.EphemeralFiveMinuteInputTokens);
        Assert.AreEqual(64L, output.Usage.OutputTokenDetails?.ThinkingTokens);
        Assert.AreEqual("not_available", output.Usage.InferenceGeo);
    }

    [TestMethod]
    public void AnAgentInputCarriesTheTeamTheModeAndWhereItIsIsolated()
    {
        var input = Input<AgentToolInput>("Agent", """
            {"description":"look it up","prompt":"find the line","subagent_type":"bee","model":"haiku",
             "run_in_background":true,"name":"scout","team_name":"the hive","mode":"acceptEdits",
             "isolation":"worktree"}
            """);

        Assert.AreEqual("bee", input.SubagentType);
        Assert.AreEqual("haiku", input.Model);
        Assert.AreEqual("scout", input.Name);
        Assert.AreEqual("the hive", input.TeamName);
        Assert.AreEqual(PermissionMode.AcceptEdits, input.Mode);
        Assert.AreEqual(AgentIsolation.Worktree, input.Isolation);
    }

    // The other half of the claim: a shape that misfits degrades to null beside its raw and does not
    // throw, and does not come back as a view with a hole in it.
    [TestMethod]
    public void AShapeThatMisfitsIsNullAndNothingThrows()
    {
        // A variant this library has no type for.
        Assert.IsNull(Read("Read", """{"type":"hologram","file":{"filePath":"/work/notes.txt"}}"""));
        Assert.IsNull(Read("Write", """{"type":"append","filePath":"/work/notes.txt","content":""}"""));
        Assert.IsNull(Read("Agent", """{"status":"paused","agentId":"agent-7"}"""));

        // A required member missing.
        Assert.IsNull(Read("Read", """{"type":"pdf","file":{"filePath":"/work/paper.pdf"}}"""));
        Assert.IsNull(Read("Glob", """{"durationMs":3,"numFiles":1,"filenames":["/work/a.cs"]}"""));

        // A required member of the wrong JSON kind.
        Assert.IsNull(Read("Bash", """{"stdout":17,"stderr":"","interrupted":false}"""));
        Assert.IsNull(Read("WebFetch", """
            {"bytes":"51232","code":200,"codeText":"OK","result":"","durationMs":1,"url":"https://example.com"}
            """));

        // A nested shape that misfits takes the whole view with it.
        Assert.IsNull(Read("Write", """
            {"type":"update","filePath":"/work/notes.txt","content":"","structuredPatch":[{"oldStart":1}],"originalFile":"a"}
            """));

        // An output that is not an object at all.
        Assert.IsNull(Read("Read", "\"Error: File does not exist.\""));
        Assert.IsNull(Read("Bash", "null"));

        // A tool this library does not type.
        Assert.IsNull(Read("DesignSync", """{"stdout":"","stderr":"","interrupted":false}"""));
    }

    private static T Input<T>(string tool, string json)
        where T : ToolInput
    {
        using var document = JsonDocument.Parse(json);
        var read = ToolTypes.ReadInput(tool, document.RootElement);
        Assert.IsNotNull(read, json);
        return (T)read;
    }

    private static T Output<T>(string tool, string json)
        where T : ToolOutput
    {
        using var document = JsonDocument.Parse(json);
        var read = ToolTypes.ReadOutput(tool, document.RootElement);
        Assert.IsNotNull(read, json);
        return (T)read;
    }

    private static ToolOutput? Read(string tool, string json)
    {
        using var document = JsonDocument.Parse(json);
        return ToolTypes.ReadOutput(tool, document.RootElement);
    }
}
