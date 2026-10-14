using System.Text.Json;

namespace Claude.Agent;

// The EnterWorktree tool's input and output. No recording calls this tool: the shape stands on the
// declaration alone, and a JSON that does not fit it reads as null beside the raw.
internal static class EnterWorktreeToolParser
{
    public static EnterWorktreeToolInput? ReadInput(JsonElement raw) =>
        new EnterWorktreeToolInput(raw.TextOrNull("name"), raw.TextOrNull("path"));

    public static EnterWorktreeToolOutput? ReadOutput(JsonElement raw)
    {
        if (!raw.Text("worktreePath", out var worktreePath)
            || !raw.Text("message", out var message))
        {
            return null;
        }

        return new EnterWorktreeToolOutput(worktreePath, message, raw.TextOrNull("worktreeBranch"));
    }

    public static void WriteInput(Utf8JsonWriter writer, EnterWorktreeToolInput input)
    {
        writer.WriteStartObject();
        WriteEnterWorktreeToolInput(writer, input);
        writer.WriteEndObject();
    }

    private static void WriteEnterWorktreeToolInput(Utf8JsonWriter writer, EnterWorktreeToolInput value)
    {
        writer.Text("name", value.Name);
        writer.Text("path", value.Path);
    }

    public static void WriteOutput(Utf8JsonWriter writer, EnterWorktreeToolOutput output)
    {
        writer.WriteStartObject();
        WriteEnterWorktreeToolOutput(writer, output);
        writer.WriteEndObject();
    }

    private static void WriteEnterWorktreeToolOutput(Utf8JsonWriter writer, EnterWorktreeToolOutput value)
    {
        writer.WriteString("worktreePath", value.WorktreePath);
        writer.WriteString("message", value.Message);
        writer.Text("worktreeBranch", value.WorktreeBranch);
    }
}
