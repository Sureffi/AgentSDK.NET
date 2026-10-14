using System.Text.Json;
using Claude.Agent.Tests.Replay;

namespace Claude.Agent.Tests.Tools;

// Every call of a typed tool in every recorded stdout, not only the four fixtures the tool replays
// drive: the tool_use block's input and, where the frame that answered it carried one, the
// tool_use_result. A call whose view is null fails, naming the fixture, the line and the tool. This
// is what reaches the Agent and WebSearch recordings, which no replay of this lane drives.
[TestClass]
public sealed class RecordedToolCallTests
{
    private sealed record Call(string Fixture, int Line, string Name, JsonElement Input);

    private sealed record Answer(string Fixture, int Line, string Name, JsonElement Output);

    [TestMethod]
    public void EveryRecordedCallOfATypedToolReadsAsItsTypedInput()
    {
        var calls = Calls().Where(call => ToolTypes.Names.Contains(call.Name)).ToArray();
        Assert.IsTrue(calls.Length >= 60, $"{calls.Length} calls of typed tools found, fewer than the 60 the recordings carry today");
        foreach (var call in calls)
        {
            Assert.IsNotNull(
                ToolTypes.ReadInput(call.Name, call.Input),
                $"{call.Fixture}.stdout.jsonl line {call.Line}: the {call.Name} input does not read as its type");
        }
    }

    [TestMethod]
    public void EveryRecordedOutputOfATypedToolReadsAsItsTypedOutput()
    {
        var answers = Answers().Where(answer => ToolTypes.Names.Contains(answer.Name)).ToArray();
        Assert.IsTrue(answers.Length >= 47, $"{answers.Length} answered calls of typed tools found, fewer than the 47 the recordings carry today");
        foreach (var answer in answers)
        {
            Assert.IsNotNull(
                ToolTypes.ReadOutput(answer.Name, answer.Output),
                $"{answer.Fixture}.stdout.jsonl line {answer.Line}: the {answer.Name} output does not read as its type");
        }
    }

    // The Agent tool, which no replay of this lane drives: both variants the recordings carry, with
    // the three keys the declaration lacks on the finished one. The finished subagent used no tool and
    // carries no count of what it did.
    [TestMethod]
    public void TheRecordedAgentOutputsCarryTheirMembersAndTheKeysTheDeclarationLacks()
    {
        var outputs = Answers()
            .Where(answer => answer.Name == "Agent")
            .Select(answer => (AgentToolOutput)ToolTypes.ReadOutput(answer.Name, answer.Output)!)
            .ToArray();

        var completed = outputs.OfType<AgentCompletedToolOutput>().ToArray();
        Assert.AreEqual(1, completed.Length);
        foreach (var one in completed)
        {
            Assert.AreNotEqual(string.Empty, one.AgentId);
            Assert.AreNotEqual(string.Empty, one.Prompt);
            Assert.IsTrue(one.TotalDuration > TimeSpan.Zero);
            Assert.IsTrue(one.TotalTokens > 0);
            Assert.AreEqual(1, one.Content.Count);
            Assert.AreNotEqual(string.Empty, one.Content[0].Text);
            Assert.AreEqual(0, one.ModelsUsed.Count);
            Assert.IsNull(one.WorktreePath);
            Assert.AreEqual(0, one.HarnessNoteCount);
            Assert.AreEqual(0, one.HarnessTailCount);
            Assert.IsNotNull(one.HarnessSectionHash);
            Assert.AreEqual(ServiceTier.Standard, one.Usage.ServiceTier);
            Assert.AreEqual("standard", one.Usage.Speed);
            Assert.IsTrue(one.Usage.InputTokens > 0);
            Assert.IsNotNull(one.Usage.CacheCreation);
            Assert.IsNotNull(one.Usage.ServerToolUse);
            Assert.AreEqual(0, one.Usage.ServerToolUse!.WebSearchRequests);
        }

        var finished = completed.Single();
        Assert.AreEqual("general-purpose", finished.AgentType);
        Assert.AreEqual(0, finished.TotalToolUseCount);
        Assert.IsNull(finished.ToolStatistics);

        var launched = outputs.OfType<AgentAsyncLaunchedToolOutput>().ToArray();
        Assert.AreEqual(3, launched.Length);
        foreach (var one in launched)
        {
            Assert.AreEqual(true, one.IsAsync);
            Assert.AreNotEqual(string.Empty, one.AgentId);
            Assert.AreNotEqual(string.Empty, one.Description);
            Assert.AreNotEqual(string.Empty, one.OutputFile);
            Assert.IsNotNull(one.CanReadOutputFile);
            Assert.IsNotNull(one.ResolvedModel);
        }

        var inputs = Calls()
            .Where(call => call.Name == "Agent")
            .Select(call => (AgentToolInput)ToolTypes.ReadInput(call.Name, call.Input)!)
            .ToArray();
        Assert.AreEqual(4, inputs.Length);
        Assert.IsTrue(inputs.All(input => input.Description.Length > 0 && input.Prompt.Length > 0));
        CollectionAssert.AreEquivalent(
            new string?[] { null, null, null, "bee" },
            inputs.Select(input => input.SubagentType).ToArray());
        CollectionAssert.AreEquivalent(
            new bool?[] { null, null, false, true },
            inputs.Select(input => input.RunInBackground).ToArray());
        Assert.IsTrue(inputs.All(input => input.Mode is null && input.Isolation is null && input.Model is null));
    }

    // The WebSearch tool, whose one recorded result carries both kinds of item its list holds.
    [TestMethod]
    public void TheRecordedWebSearchOutputCarriesBothKindsOfResult()
    {
        var answer = Answers().Single(one => one.Name == "WebSearch");
        var output = (WebSearchToolOutput)ToolTypes.ReadOutput(answer.Name, answer.Output)!;

        Assert.AreEqual(1, output.SearchCount);
        Assert.AreNotEqual(string.Empty, output.Query);
        Assert.IsTrue(output.Duration > TimeSpan.FromSeconds(2));
        Assert.AreEqual(2, output.Results.Count);

        var hit = (WebSearchHitResult)output.Results[0];
        Assert.IsTrue(hit.ToolUseId.StartsWith("srvtoolu_", StringComparison.Ordinal));
        Assert.IsTrue(hit.Content.Count > 0);
        Assert.IsTrue(hit.Content.All(link => link.Title.Length > 0 && link.Url.Length > 0));

        var text = (WebSearchTextResult)output.Results[1];
        Assert.AreNotEqual(string.Empty, text.Text);

        var input = (WebSearchToolInput)ToolTypes.ReadInput(answer.Name, Calls().Single(call => call.Name == "WebSearch").Input)!;
        Assert.AreEqual(output.Query, input.Query);
        Assert.AreEqual(0, input.AllowedDomains.Count);
        Assert.AreEqual(0, input.BlockedDomains.Count);
    }

    // Every tool_use block of every recorded stdout: the tool's name and the input the model wrote.
    private static IEnumerable<Call> Calls()
    {
        foreach (var (fixture, line, frame) in Frames())
        {
            if (!Content(frame, out var content))
            {
                continue;
            }

            foreach (var block in content.EnumerateArray())
            {
                if (block.ValueKind == JsonValueKind.Object
                    && Says(block, "type", "tool_use")
                    && block.TryGetProperty("name", out var name)
                    && name.ValueKind == JsonValueKind.String
                    && block.TryGetProperty("input", out var input))
                {
                    yield return new Call(fixture, line, name.GetString()!, input);
                }
            }
        }
    }

    // Every user frame that carried a tool's own output: its tool_use_result, under the name of the
    // call the frame's one tool result answers.
    private static IEnumerable<Answer> Answers()
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (fixture, line, frame) in Frames())
        {
            if (Content(frame, out var content))
            {
                foreach (var block in content.EnumerateArray().Where(block => block.ValueKind == JsonValueKind.Object))
                {
                    if (Says(block, "type", "tool_use")
                        && block.TryGetProperty("id", out var id)
                        && block.TryGetProperty("name", out var name))
                    {
                        names[$"{fixture}:{id.GetString()}"] = name.GetString()!;
                    }

                    if (Says(block, "type", "tool_result")
                        && frame.TryGetProperty("tool_use_result", out var result)
                        && result.ValueKind == JsonValueKind.Object
                        && block.TryGetProperty("tool_use_id", out var answered)
                        && names.TryGetValue($"{fixture}:{answered.GetString()}", out var called))
                    {
                        yield return new Answer(fixture, line, called, result);
                    }
                }
            }
        }
    }

    private static bool Says(JsonElement element, string key, string value) =>
        element.TryGetProperty(key, out var found)
        && found.ValueKind == JsonValueKind.String
        && found.GetString() == value;

    private static bool Content(JsonElement frame, out JsonElement content)
    {
        content = default;
        return frame.TryGetProperty("message", out var message)
            && message.ValueKind == JsonValueKind.Object
            && message.TryGetProperty("content", out content)
            && content.ValueKind == JsonValueKind.Array;
    }

    private static IEnumerable<(string Fixture, int Line, JsonElement Frame)> Frames()
    {
        foreach (var file in Directory.EnumerateFiles(Fixture.Root, "*.stdout.jsonl").Order(StringComparer.Ordinal))
        {
            var fixture = Path.GetFileName(file)[..^".stdout.jsonl".Length];
            var number = 0;
            foreach (var line in File.ReadLines(file))
            {
                number++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(line);
                yield return (fixture, number, document.RootElement.Clone());
            }
        }
    }
}
