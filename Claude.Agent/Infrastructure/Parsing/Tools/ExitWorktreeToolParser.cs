using System.Text.Json;

namespace Claude.Agent;

// The ExitWorktree tool's input and output. The action is which type each is, keep or remove, and
// an action the declaration does not name reads as null beside the raw. No recording calls this
// tool: the shape stands on the declaration, and on the binary's own build of the output, which
// names a terminal session only when it keeps the worktree and counts what it threw away only when
// it removes it.
internal static class ExitWorktreeToolParser
{
    public static ExitWorktreeToolInput? ReadInput(JsonElement raw) =>
        raw.TextOrNull("action") switch
        {
            "keep" => new ExitWorktreeKeepToolInput(),
            "remove" => new ExitWorktreeRemoveToolInput(raw.FlagOrNull("discard_changes")),
            _ => null,
        };

    public static ExitWorktreeToolOutput? ReadOutput(JsonElement raw)
    {
        if (!raw.Text("originalCwd", out var originalWorkingDirectory)
            || !raw.Text("worktreePath", out var worktreePath)
            || !raw.Text("message", out var message))
        {
            return null;
        }

        return raw.TextOrNull("action") switch
        {
            "keep" => new ExitWorktreeKeepToolOutput(
                originalWorkingDirectory,
                worktreePath,
                message,
                raw.TextOrNull("worktreeBranch"),
                raw.TextOrNull("tmuxSessionName")),
            "remove" => new ExitWorktreeRemoveToolOutput(
                originalWorkingDirectory,
                worktreePath,
                message,
                raw.TextOrNull("worktreeBranch"),
                raw.CountOrNull("discardedFiles"),
                raw.CountOrNull("discardedCommits")),
            _ => null,
        };
    }

    public static void WriteInput(Utf8JsonWriter writer, ExitWorktreeToolInput input)
    {
        writer.WriteStartObject();
        switch (input)
        {
            case ExitWorktreeKeepToolInput:
                writer.WriteString("action", "keep");
                break;
            case ExitWorktreeRemoveToolInput remove:
                writer.WriteString("action", "remove");
                writer.Flag("discard_changes", remove.DiscardChanges);
                break;
            default:
                throw new NotSupportedException($"No ExitWorktree input of type {input.GetType().Name} can be written.");
        }

        writer.WriteEndObject();
    }

    public static void WriteOutput(Utf8JsonWriter writer, ExitWorktreeToolOutput output)
    {
        writer.WriteStartObject();
        switch (output)
        {
            case ExitWorktreeKeepToolOutput keep:
                writer.WriteString("action", "keep");
                WriteShared(writer, keep);
                writer.Text("tmuxSessionName", keep.TmuxSessionName);
                break;
            case ExitWorktreeRemoveToolOutput remove:
                writer.WriteString("action", "remove");
                WriteShared(writer, remove);
                writer.Count("discardedFiles", remove.DiscardedFileCount);
                writer.Count("discardedCommits", remove.DiscardedCommitCount);
                break;
            default:
                throw new NotSupportedException($"No ExitWorktree output of type {output.GetType().Name} can be written.");
        }

        writer.WriteEndObject();
    }

    private static void WriteShared(Utf8JsonWriter writer, ExitWorktreeToolOutput value)
    {
        writer.WriteString("originalCwd", value.OriginalWorkingDirectory);
        writer.WriteString("worktreePath", value.WorktreePath);
        writer.WriteString("message", value.Message);
        writer.Text("worktreeBranch", value.WorktreeBranch);
    }
}
