using System.Text.Json;

namespace Claude.Agent;

// The Agent tool's input and output. The output is a union on `status`, one type a variant. The
// completed variant carries three keys the declaration does not have — harnessNoteCount,
// harnessTailCount and harnessSectionHash — which the task and hooks-more recordings found; its
// usage iterations and its blocks' citations are untyped in the declaration and are left to the raw
// beside the view.
internal static class AgentToolParser
{
    public static AgentToolInput? ReadInput(JsonElement raw)
    {
        if (!raw.Text("description", out var description) || !raw.Text("prompt", out var prompt))
        {
            return null;
        }

        return new AgentToolInput(
            description,
            prompt,
            raw.TextOrNull("subagent_type"),
            raw.TextOrNull("model"),
            raw.FlagOrNull("run_in_background"),
            raw.TextOrNull("name"),
            raw.TextOrNull("team_name"),
            raw.SetOrNull("mode", value => new PermissionMode(value)),
            raw.SetOrNull("isolation", value => new AgentIsolation(value)));
    }

    public static void WriteInput(Utf8JsonWriter writer, AgentToolInput input)
    {
        writer.WriteStartObject();
        writer.WriteString("description", input.Description);
        writer.WriteString("prompt", input.Prompt);
        writer.Text("subagent_type", input.SubagentType);
        writer.Text("model", input.Model);
        writer.Flag("run_in_background", input.RunInBackground);
        writer.Text("name", input.Name);
        writer.Text("team_name", input.TeamName);
        writer.Text("mode", input.Mode?.ToString());
        writer.Text("isolation", input.Isolation?.ToString());
        writer.WriteEndObject();
    }

    public static AgentToolOutput? ReadOutput(JsonElement raw) =>
        raw.TextOrNull("status") switch
        {
            "completed" => AsCompleted(raw),
            "async_launched" => AsAsyncLaunched(raw),
            "remote_launched" => AsRemoteLaunched(raw),
            _ => null,
        };

    public static void WriteOutput(Utf8JsonWriter writer, AgentToolOutput output)
    {
        switch (output)
        {
            case AgentCompletedToolOutput completed:
                writer.WriteStartObject();
                writer.WriteString("status", "completed");
                writer.WriteString("agentId", completed.AgentId);
                writer.Text("agentType", completed.AgentType);
                writer.Items("content", completed.Content, WriteBlock, whenEmpty: true);
                writer.Text("resolvedModel", completed.ResolvedModel);
                writer.Texts("modelsUsed", completed.ModelsUsed);
                writer.WriteNumber("totalToolUseCount", completed.TotalToolUseCount);
                writer.Milliseconds("totalDurationMs", completed.TotalDuration);
                writer.WriteNumber("totalTokens", completed.TotalTokens);
                writer.WriteStartObject("usage");
                WriteUsage(writer, completed.Usage);
                writer.WriteEndObject();
                writer.Nested("toolStats", completed.ToolStatistics, WriteStatistics);
                writer.WriteString("prompt", completed.Prompt);
                writer.Text("worktreePath", completed.WorktreePath);
                writer.Text("worktreeBranch", completed.WorktreeBranch);
                writer.Count("harnessNoteCount", completed.HarnessNoteCount);
                writer.Count("harnessTailCount", completed.HarnessTailCount);
                writer.Text("harnessSectionHash", completed.HarnessSectionHash);
                writer.WriteEndObject();
                break;
            case AgentAsyncLaunchedToolOutput launched:
                writer.WriteStartObject();
                writer.WriteString("status", "async_launched");
                writer.Flag("isAsync", launched.IsAsync);
                writer.WriteString("agentId", launched.AgentId);
                writer.WriteString("description", launched.Description);
                writer.Text("resolvedModel", launched.ResolvedModel);
                writer.Texts("modelsUsed", launched.ModelsUsed);
                writer.WriteString("prompt", launched.Prompt);
                writer.WriteString("outputFile", launched.OutputFile);
                writer.Flag("canReadOutputFile", launched.CanReadOutputFile);
                writer.WriteEndObject();
                break;
            case AgentRemoteLaunchedToolOutput remote:
                writer.WriteStartObject();
                writer.WriteString("status", "remote_launched");
                writer.WriteString("taskId", remote.TaskId);
                writer.WriteString("sessionUrl", remote.SessionUrl);
                writer.WriteString("description", remote.Description);
                writer.WriteString("prompt", remote.Prompt);
                writer.WriteString("outputFile", remote.OutputFile);
                writer.WriteEndObject();
                break;
            default:
                throw new NotSupportedException($"No Agent output of type {output.GetType().Name} can be written.");
        }
    }

    private static AgentCompletedToolOutput? AsCompleted(JsonElement raw)
    {
        if (!raw.Text("agentId", out var agentId)
            || !raw.Items("content", AsBlock, out var content)
            || !raw.Count("totalToolUseCount", out var toolUseCount)
            || !raw.Milliseconds("totalDurationMs", out var duration)
            || !raw.Count("totalTokens", out var totalTokens)
            || !raw.Object("usage", out var rawUsage)
            || !raw.Text("prompt", out var prompt)
            || !raw.TextsOrEmpty("modelsUsed", out var modelsUsed)
            || !raw.Nested("toolStats", AsStatistics, out var statistics))
        {
            return null;
        }

        if (AsUsage(rawUsage) is not { } usage)
        {
            return null;
        }

        return new AgentCompletedToolOutput(
            agentId,
            content,
            toolUseCount,
            duration,
            totalTokens,
            usage,
            prompt,
            raw.TextOrNull("agentType"),
            raw.TextOrNull("resolvedModel"),
            modelsUsed,
            statistics,
            raw.TextOrNull("worktreePath"),
            raw.TextOrNull("worktreeBranch"),
            raw.CountOrNull("harnessNoteCount"),
            raw.CountOrNull("harnessTailCount"),
            raw.TextOrNull("harnessSectionHash"));
    }

    private static AgentAsyncLaunchedToolOutput? AsAsyncLaunched(JsonElement raw)
    {
        if (!raw.Text("agentId", out var agentId)
            || !raw.Text("description", out var description)
            || !raw.Text("prompt", out var prompt)
            || !raw.Text("outputFile", out var outputFile)
            || !raw.TextsOrEmpty("modelsUsed", out var modelsUsed))
        {
            return null;
        }

        return new AgentAsyncLaunchedToolOutput(
            agentId,
            description,
            prompt,
            outputFile,
            raw.FlagOrNull("isAsync"),
            raw.TextOrNull("resolvedModel"),
            modelsUsed,
            raw.FlagOrNull("canReadOutputFile"));
    }

    private static AgentRemoteLaunchedToolOutput? AsRemoteLaunched(JsonElement raw)
    {
        if (!raw.Text("taskId", out var taskId)
            || !raw.Text("sessionUrl", out var sessionUrl)
            || !raw.Text("description", out var description)
            || !raw.Text("prompt", out var prompt)
            || !raw.Text("outputFile", out var outputFile))
        {
            return null;
        }

        return new AgentRemoteLaunchedToolOutput(taskId, sessionUrl, description, prompt, outputFile);
    }

    private static AgentToolTextBlock? AsBlock(JsonElement raw) =>
        raw.Text("text", out var text) ? new AgentToolTextBlock(text) : null;

    private static void WriteBlock(Utf8JsonWriter writer, AgentToolTextBlock block)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "text");
        writer.WriteString("text", block.Text);
        writer.WriteEndObject();
    }

    private static AgentToolUsage? AsUsage(JsonElement raw)
    {
        if (!raw.Count("input_tokens", out var inputTokens)
            || !raw.Count("output_tokens", out var outputTokens)
            || !raw.Nested("server_tool_use", AsServerToolUse, out var serverToolUse)
            || !raw.Nested("cache_creation", AsCacheCreation, out var cacheCreation)
            || !raw.Nested("output_tokens_details", AsTokenDetails, out var details))
        {
            return null;
        }

        return new AgentToolUsage(
            inputTokens,
            outputTokens,
            raw.CountOrNull("cache_creation_input_tokens"),
            raw.CountOrNull("cache_read_input_tokens"),
            serverToolUse,
            raw.SetOrNull("service_tier", value => new ServiceTier(value)),
            cacheCreation,
            raw.TextOrNull("inference_geo"),
            raw.SetOrNull("speed", value => new InferenceSpeed(value)),
            details);
    }

    private static void WriteUsage(Utf8JsonWriter writer, AgentToolUsage usage)
    {
        writer.WriteNumber("input_tokens", usage.InputTokens);
        writer.WriteNumber("output_tokens", usage.OutputTokens);
        writer.CountOrNull("cache_creation_input_tokens", usage.CacheCreationInputTokens);
        writer.CountOrNull("cache_read_input_tokens", usage.CacheReadInputTokens);
        writer.NestedOrNull("server_tool_use", usage.ServerToolUse, WriteServerToolUse);
        writer.TextOrNull("service_tier", usage.ServiceTier?.ToString());
        writer.NestedOrNull("cache_creation", usage.CacheCreation, WriteCacheCreation);
        writer.Text("inference_geo", usage.InferenceGeo);
        writer.Text("speed", usage.Speed?.ToString());
        writer.Nested("output_tokens_details", usage.OutputTokenDetails, WriteTokenDetails);
    }

    private static AgentServerToolUse? AsServerToolUse(JsonElement raw) =>
        raw.Count("web_search_requests", out var searches) && raw.Count("web_fetch_requests", out var fetches)
            ? new AgentServerToolUse(searches, fetches)
            : null;

    private static void WriteServerToolUse(Utf8JsonWriter writer, AgentServerToolUse use)
    {
        writer.WriteNumber("web_search_requests", use.WebSearchRequests);
        writer.WriteNumber("web_fetch_requests", use.WebFetchRequests);
    }

    private static AgentCacheCreation? AsCacheCreation(JsonElement raw) =>
        raw.Count("ephemeral_1h_input_tokens", out var hour) && raw.Count("ephemeral_5m_input_tokens", out var minutes)
            ? new AgentCacheCreation(hour, minutes)
            : null;

    private static void WriteCacheCreation(Utf8JsonWriter writer, AgentCacheCreation creation)
    {
        writer.WriteNumber("ephemeral_1h_input_tokens", creation.EphemeralOneHourInputTokens);
        writer.WriteNumber("ephemeral_5m_input_tokens", creation.EphemeralFiveMinuteInputTokens);
    }

    private static AgentOutputTokenDetails? AsTokenDetails(JsonElement raw) =>
        new(raw.CountOrNull("thinking_tokens"));

    private static void WriteTokenDetails(Utf8JsonWriter writer, AgentOutputTokenDetails details) =>
        writer.CountOrNull("thinking_tokens", details.ThinkingTokens);

    private static AgentToolStatistics? AsStatistics(JsonElement raw)
    {
        if (!raw.Count("readCount", out var readCount)
            || !raw.Count("searchCount", out var searchCount)
            || !raw.Count("bashCount", out var bashCount)
            || !raw.Count("editFileCount", out var editFileCount)
            || !raw.Count("linesAdded", out var linesAdded)
            || !raw.Count("linesRemoved", out var linesRemoved)
            || !raw.Count("otherToolCount", out var otherToolCount))
        {
            return null;
        }

        return new AgentToolStatistics(
            readCount,
            searchCount,
            bashCount,
            editFileCount,
            linesAdded,
            linesRemoved,
            otherToolCount,
            raw.CountOrNull("frameCount"));
    }

    private static void WriteStatistics(Utf8JsonWriter writer, AgentToolStatistics statistics)
    {
        writer.WriteNumber("readCount", statistics.ReadCount);
        writer.WriteNumber("searchCount", statistics.SearchCount);
        writer.WriteNumber("bashCount", statistics.BashCount);
        writer.WriteNumber("editFileCount", statistics.EditFileCount);
        writer.WriteNumber("linesAdded", statistics.LinesAdded);
        writer.WriteNumber("linesRemoved", statistics.LinesRemoved);
        writer.WriteNumber("otherToolCount", statistics.OtherToolCount);
        writer.Count("frameCount", statistics.FrameCount);
    }
}
