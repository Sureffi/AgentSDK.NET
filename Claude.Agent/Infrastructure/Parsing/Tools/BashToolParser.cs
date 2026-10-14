using System.Text.Json;

namespace Claude.Agent;

// The Bash tool's input and output, and what the binary read off the repository the command ran in.
// The output's structuredContent is untyped in the declaration and is left to the raw beside the view.
internal static class BashToolParser
{
    public static BashToolInput? ReadInput(JsonElement raw)
    {
        if (!raw.Text("command", out var command))
        {
            return null;
        }

        return new BashToolInput(
            command,
            raw.MillisecondsOrNull("timeout"),
            raw.TextOrNull("description"),
            raw.FlagOrNull("run_in_background"),
            raw.FlagOrNull("dangerouslyDisableSandbox"));
    }

    public static void WriteInput(Utf8JsonWriter writer, BashToolInput input)
    {
        writer.WriteStartObject();
        writer.WriteString("command", input.Command);
        writer.Milliseconds("timeout", input.Timeout);
        writer.Text("description", input.Description);
        writer.Flag("run_in_background", input.RunInBackground);
        writer.Flag("dangerouslyDisableSandbox", input.DangerouslyDisableSandbox);
        writer.WriteEndObject();
    }

    public static BashToolOutput? ReadOutput(JsonElement raw)
    {
        if (!raw.Text("stdout", out var standardOutput)
            || !raw.Text("stderr", out var standardError)
            || !raw.Flag("interrupted", out var interrupted)
            || !raw.Nested("gitOperation", Operation, out var operation))
        {
            return null;
        }

        return new BashToolOutput(
            standardOutput,
            standardError,
            interrupted,
            raw.TextOrNull("rawOutputPath"),
            raw.FlagOrNull("isImage"),
            raw.TextOrNull("backgroundTaskId"),
            raw.FlagOrNull("backgroundedByUser"),
            raw.MillisecondsOrNull("timedOutAfterMs"),
            raw.TextOrNull("backgroundCwdHint"),
            raw.FlagOrNull("backgroundEndsWithFinalResponse"),
            raw.FlagOrNull("dangerouslyDisableSandbox"),
            raw.TextOrNull("returnCodeInterpretation"),
            raw.FlagOrNull("noOutputExpected"),
            raw.TextOrNull("persistedOutputPath"),
            raw.CountOrNull("persistedOutputSize"),
            raw.TextOrNull("staleReadFileStateHint"),
            raw.TextOrNull("ghRateLimitHint"),
            operation);
    }

    public static void WriteOutput(Utf8JsonWriter writer, BashToolOutput output)
    {
        writer.WriteStartObject();
        writer.WriteString("stdout", output.StandardOutput);
        writer.WriteString("stderr", output.StandardError);
        writer.Text("rawOutputPath", output.RawOutputPath);
        writer.WriteBoolean("interrupted", output.Interrupted);
        writer.Flag("isImage", output.IsImage);
        writer.Text("backgroundTaskId", output.BackgroundTaskId);
        writer.Flag("backgroundedByUser", output.BackgroundedByUser);
        writer.Milliseconds("timedOutAfterMs", output.TimedOutAfter);
        writer.Text("backgroundCwdHint", output.BackgroundWorkingDirectoryHint);
        writer.Flag("backgroundEndsWithFinalResponse", output.BackgroundEndsWithFinalResponse);
        writer.Flag("dangerouslyDisableSandbox", output.DangerouslyDisableSandbox);
        writer.Text("returnCodeInterpretation", output.ReturnCodeInterpretation);
        writer.Flag("noOutputExpected", output.NoOutputExpected);
        writer.Text("persistedOutputPath", output.PersistedOutputPath);
        writer.Count("persistedOutputSize", output.PersistedOutputSize);
        writer.Text("staleReadFileStateHint", output.StaleReadFileStateHint);
        writer.Text("ghRateLimitHint", output.GitHubRateLimitHint);
        writer.Nested("gitOperation", output.GitOperation, WriteOperation);
        writer.WriteEndObject();
    }

    private static BashGitOperation? Operation(JsonElement raw)
    {
        if (!raw.Nested("commit", Commit, out var commit)
            || !raw.Nested("push", Push, out var push)
            || !raw.Nested("branch", Branch, out var branch)
            || !raw.Nested("pr", PullRequest, out var pullRequest))
        {
            return null;
        }

        return new BashGitOperation(commit, push, branch, pullRequest);
    }

    private static void WriteOperation(Utf8JsonWriter writer, BashGitOperation operation)
    {
        if (operation.Commit is { } commit)
        {
            writer.WriteStartObject("commit");
            writer.WriteString("sha", commit.Sha);
            writer.WriteString("kind", commit.Kind.ToString());
            writer.Text("branch", commit.Branch);
            writer.WriteEndObject();
        }

        if (operation.Push is { } push)
        {
            writer.WriteStartObject("push");
            writer.WriteString("branch", push.Branch);
            writer.WriteEndObject();
        }

        if (operation.Branch is { } branch)
        {
            writer.WriteStartObject("branch");
            writer.WriteString("ref", branch.Reference);
            writer.WriteString("action", branch.Action.ToString());
            writer.WriteEndObject();
        }

        if (operation.PullRequest is { } pullRequest)
        {
            writer.WriteStartObject("pr");
            writer.WriteNumber("number", pullRequest.Number);
            writer.Text("url", pullRequest.Url);
            writer.WriteString("action", pullRequest.Action.ToString());
            writer.WriteEndObject();
        }
    }

    private static BashGitCommit? Commit(JsonElement raw) =>
        raw.Text("sha", out var sha) && raw.Text("kind", out var kind)
            ? new BashGitCommit(sha, kind, raw.TextOrNull("branch"))
            : null;

    private static BashGitPush? Push(JsonElement raw) =>
        raw.Text("branch", out var branch) ? new BashGitPush(branch) : null;

    private static BashGitBranch? Branch(JsonElement raw) =>
        raw.Text("ref", out var reference) && raw.Text("action", out var action)
            ? new BashGitBranch(reference, action)
            : null;

    private static BashGitPullRequest? PullRequest(JsonElement raw) =>
        raw.Count("number", out var number) && raw.Text("action", out var action)
            ? new BashGitPullRequest(number, action, raw.TextOrNull("url"))
            : null;
}
