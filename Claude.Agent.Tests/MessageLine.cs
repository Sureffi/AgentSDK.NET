using System.Text.Json;

namespace Claude.Agent.Tests;

internal static class MessageLine
{
    // The line a message was read from; every message read from a recording has one.
    public static JsonElement Line(this AgentMessage message) =>
        message.Raw ?? throw new AssertFailedException($"the {message.GetType().Name} carries no line.");

    // The messages the binary wrote, without the tool-call snapshots this side adds among them.
    public static List<AgentMessage> Written(this IEnumerable<AgentMessage> messages) =>
        [.. messages.Where(message => message is not ToolCallMessage)];

    // The text of a block's content: the concatenated text blocks.
    public static string Text(this IEnumerable<ContentBlock> content) =>
        string.Concat(content.OfType<TextBlock>().Select(block => block.Text));
}
