using System.Text.Json;
using System.Text.Json.Nodes;

namespace Claude.Agent.Recorder;

// The forty-three fixtures, one method each, and two runs that keep a transcript and no frames. The
// binary recorded is the one named, never one found on the path, so no other install takes its
// place; every run is under the neutral cwd, in the client's default configuration directory.
internal sealed class Fixtures(Recording recording, string cwd, string executable)
{
    private Dictionary<string, Func<Task>>? _byName;

    public Task RunAsync(string name)
    {
        _byName ??= Table();
        if (!_byName.TryGetValue(name, out var fixture))
        {
            throw new ArgumentException($"no fixture named {name}", nameof(name));
        }

        return fixture();
    }

    private Dictionary<string, Func<Task>> Table() => new(StringComparer.Ordinal)
    {
        ["trivial"] = TrivialAsync,
        ["tool-use"] = ToolUseAsync,
        ["hooks"] = HooksAsync,
        ["streaming"] = StreamingAsync,
        ["client"] = ClientAsync,
        ["sdk-mcp"] = SdkMcpAsync,
        ["can-use-tool"] = CanUseToolAsync,
        ["mcp-reconnect"] = McpReconnectAsync,
        ["mcp-toggle"] = McpToggleAsync,
        ["stop-task"] = StopTaskAsync,
        ["rewind-files"] = RewindFilesAsync,
        ["fork-parent"] = ForkParentAsync,
        ["fork"] = ForkAsync,
        ["resume"] = ResumeAsync,
        ["task"] = TaskAsync,
        ["hook-events"] = HookEventsAsync,
        ["conversation-reset"] = ConversationResetAsync,
        ["cancel"] = CancelAsync,
        ["denied"] = DeniedAsync,
        ["stdio-mcp"] = StdioMcpAsync,
        ["sdk-mcp-client"] = SdkMcpClientAsync,
        ["hooks-more"] = HooksMoreAsync,
        ["defer"] = DeferAsync,
        ["structured"] = StructuredAsync,
        ["schema-type"] = SchemaTypeAsync,
        ["schema-nullable"] = SchemaNullableAsync,
        ["ask"] = AskAsync,
        ["web-search"] = WebSearchAsync,
        ["task-background"] = TaskBackgroundAsync,
        ["compact"] = CompactAsync,
        ["suggestions"] = SuggestionsAsync,
        ["agents"] = AgentsAsync,
        ["defaults"] = DefaultsAsync,
        ["tools"] = ToolsAsync,
        ["task-tools"] = TaskToolsAsync,
        ["payloads"] = PayloadsAsync,
        ["continuation"] = ContinuationAsync,
        ["reads"] = ReadsAsync,
        ["edits"] = EditsAsync,
        ["tagged"] = TaggedAsync,
        ["thinking"] = ThinkingAsync,
        ["mcp-cancel"] = McpCancelAsync,
        ["background-stop"] = BackgroundStopAsync,
        ["background-interrupt"] = BackgroundInterruptAsync,
        ["renamed"] = RenamedAsync,
    };

    // A recording proves the wire, not the answer, so every fixture runs on the cheapest model that
    // drives the tools, and every argv names it.
    private const string Model = "haiku";

    // What a fixture asks goes on the call, as a consumer's does; the client knows only where the
    // binary is.
    private ClaudeAgentSessionOptions Base(int maxTurns = 1, Effort? effort = null) => new()
    {
        WorkingDirectory = cwd,
        SettingSources = [],
        Model = Model,
        MaxTurns = maxTurns,
        Effort = effort,
    };

    private ClaudeAgentClient Client() =>
        new(
            new ClaudeAgentClientOptions { ExecutablePath = executable },
            null,
            Transports(),
            null);

    private SubprocessTransportFactory Transports() =>
        new(new FrameCodec(), new CliLocator(), null, recording.Tap);

    private async Task QueryAsync(string name, ClaudeAgentSessionOptions options, string prompt, Action<AgentMessage>? each = null)
    {
        recording.Begin(name);
        await Drain(Client().QueryAsync(prompt, options), name, each);
    }

    // Where the sessions' transcripts are: the configuration directory every client here runs in.
    private void KeepTranscripts(string session) =>
        recording.KeepTranscripts(Client().Options.ConfigurationDirectory!, cwd, session);

    private async Task WithSessionAsync(string name, ClaudeAgentSessionOptions options, Func<ClaudeAgentSession, Task> body)
    {
        recording.Begin(name);
        await using var session = await Client().StartSessionAsync(options);
        await body(session);
    }

    private static async Task Drain(IAsyncEnumerable<AgentMessage> messages, string label, Action<AgentMessage>? each = null, CancellationToken cancellationToken = default)
    {
        await foreach (var message in messages.WithCancellation(cancellationToken))
        {
            Report(message, label);
            each?.Invoke(message);
        }
    }

    // Read whatever the binary sends in the next few seconds; nothing is a full answer. A prompt's
    // run is read first, then what no run reads, for the whole of the time.
    private static async Task DrainFor(ClaudeAgentSession session, int seconds, string label, ClaudeAgentRun? run = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        try
        {
            if (run is not null)
            {
                await foreach (var message in run.WithCancellation(timeout.Token))
                {
                    Report(message, label);
                }
            }

            await foreach (var message in session.ReceiveMessagesAsync(timeout.Token))
            {
                Report(message, label);
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
        }
    }

    private static void Report(AgentMessage message, string label)
    {
        switch (message)
        {
            case AssistantMessage assistant:
                foreach (var text in assistant.Content.Select(Text).OfType<string>())
                {
                    Console.WriteLine($"  [{label}] '{text[..Math.Min(80, text.Length)]}'");
                }

                break;
            case ResultMessage result:
                Console.WriteLine($"  [{label}] result {result.Subtype} turns={result.TurnCount} cost={result.TotalCostUsd} denials={result.PermissionDenials.Count}");
                break;
            case TaskMessage task:
                Console.WriteLine($"  [{label}] {task.Subtype} task={task.TaskId}");
                break;
            case ConversationResetMessage reset:
                Console.WriteLine($"  [{label}] conversation reset {reset.NewConversationId}");
                break;
            case SystemMessage system:
                Console.WriteLine($"  [{label}] system {system.Subtype}");
                break;
            default:
                break;
        }
    }

    private static string? Text(ContentBlock block) => block switch
    {
        TextBlock text => text.Text,
        ThinkingBlock thinking => thinking.Thinking,
        _ => null,
    };

    private static void Say(string label, object? value) => Console.WriteLine($"  {label}: {value}");

    private static Task<HookOutput> Done() => Task.FromResult(new HookOutput());

    // The tool a hook input is about, for the recorder's own log line.
    private static string Tool(HookInput input) => input switch
    {
        PreToolUseHookInput pre => pre.ToolCall.Name,
        PostToolUseHookInput post => post.ToolCall.Name,
        PostToolUseFailureHookInput failed => failed.ToolCall.Name,
        PermissionRequestHookInput asked => asked.ToolCall.Name,
        PermissionDeniedHookInput denied => denied.ToolCall.Name,
        _ => string.Empty,
    };

    private static JsonElement Json(string text)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static string Names(IReadOnlyList<McpServerStatus> status) =>
        string.Join(", ", status.Select(server => $"{server.Name}={server.Status}"));

    private Task TrivialAsync() =>
        QueryAsync("trivial", Base(), "Reply with exactly one word: pong");

    private Task ToolUseAsync()
    {
        var options = Base(3, Effort.Low);
        options.Tools = ["Read"];
        options.AllowedTools.Add("Read");
        return QueryAsync("tool-use", options, "Read notes.txt in the working directory and reply with its first line only.");
    }

    private async Task HooksAsync()
    {
        var seen = new List<string>();
        var options = Base(3, Effort.Low);
        options.Tools = ["Read"];
        options.IncludeHookEvents = true;
        options.PermissionCallback = (request, _) =>
        {
            seen.Add($"can_use_tool {request.ToolCall.Name}");
            return Task.FromResult(ToolPermissionResult.Allow());
        };
        options.AddHook(HookEvent.PreToolUse, "Read", (input, _) =>
        {
            seen.Add($"pre {Tool(input)}");
            return Task.FromResult(new HookOutput { Decision = PermissionDecision.Allow, DecisionReason = "fixture allows" });
        });
        options.AddHook(HookEvent.PostToolUse, null, (input, _) =>
        {
            seen.Add($"post {Tool(input)}");
            return Task.FromResult(new HookOutput { AdditionalContext = "fixture saw the read" });
        });
        options.AddHook(HookEvent.UserPromptSubmit, null, (_, _) =>
        {
            seen.Add("prompt");
            return Task.FromResult(new HookOutput { AdditionalContext = "the file is small" });
        });
        options.AddHook(HookEvent.Stop, null, (_, _) =>
        {
            seen.Add("stop");
            return Done();
        });
        await QueryAsync("hooks", options, "Read notes.txt in the working directory and reply with its second line only.");
        Say("hooks seen", string.Join(", ", seen));
    }

    private async Task StreamingAsync()
    {
        var options = Base();
        options.IncludePartialMessages = true;
        options.SystemPrompt = AgentSystemPrompt.FromText("Answer in one short sentence.");
        var events = 0;
        await QueryAsync("streaming", options, "What is a mint?", each: message => events += message is StreamEventMessage ? 1 : 0);
        Say("stream events", events);
    }

    private Task ClientAsync()
    {
        var options = Base(1, Effort.Low);
        options.SystemPrompt = AgentSystemPrompt.Preset("Be terse.");
        return WithSessionAsync("client", options, async session =>
        {
            Say("server info commands", string.Join(", ", session.ServerInfo?.Commands.Select(command => command.Name) ?? []));
            await Drain(session.SendAsync("Say: one"), "client/1");
            await session.SetModelAsync(Model);
            await session.SetPermissionModeAsync(PermissionMode.AcceptEdits);
            var usage = await session.GetContextUsageAsync();
            Say("context usage categories", string.Join(", ", usage.Categories.Select(category => category.Name)));
            Say("mcp status", Names(await session.GetMcpStatusAsync()));
            await Drain(session.SendAsync("Say: two"), "client/2");
            var interrupted = false;
            await foreach (var message in session.SendAsync("Count slowly from 1 to 200, one number per line, no other text."))
            {
                if (message is AssistantMessage && !interrupted)
                {
                    interrupted = true;
                    await session.InterruptAsync();
                }

                if (message is ResultMessage result)
                {
                    Say("interrupted result", $"{result.Subtype} terminal_reason={result.TerminalReason} stop={result.StopReason}");
                    break;
                }
            }
        });
    }

    private ClaudeAgentSessionOptions WithCalc(int maxTurns)
    {
        var options = Base(maxTurns, Effort.Low);
        options.Tools = [];
        options.McpServers["calc"] = McpServerConfiguration.InProcess(new Calc());
        options.AllowedTools.Add("mcp__calc__add");
        return options;
    }

    private Task SdkMcpAsync() =>
        QueryAsync("sdk-mcp", WithCalc(3), "Use the add tool to add 2 and 3, then reply with just the number.");

    // No hook pre-allows, so the binary asks. Write is not in AllowedTools.
    private async Task CanUseToolAsync()
    {
        var asked = new List<string>();
        var options = Base(3, Effort.Low);
        options.Tools = ["Write"];
        options.PermissionCallback = (request, _) =>
        {
            asked.Add(request.ToolCall.Name);
            return Task.FromResult(ToolPermissionResult.Allow());
        };
        await QueryAsync("can-use-tool", options, "Create a file named hello.txt in the working directory containing exactly: hello");
        Say("asked", string.Join(", ", asked));
    }

    // The binary connects an in-process server as the session starts and answers "Server not found"
    // to a verb that comes before the connection is done, so the verbs wait a moment first. The wait
    // writes nothing.
    private static Task Connected() => Task.Delay(TimeSpan.FromSeconds(3));

    private Task McpReconnectAsync() =>
        WithSessionAsync("mcp-reconnect", WithCalc(1), async session =>
        {
            await Connected();
            await Verb("reconnect", session.ReconnectMcpServerAsync("calc"));
            Say("status", Names(await session.GetMcpStatusAsync()));
        });

    private Task McpToggleAsync() =>
        WithSessionAsync("mcp-toggle", WithCalc(1), async session =>
        {
            await Connected();
            await Verb("toggle off", session.ToggleMcpServerAsync("calc", false));
            Say("status", Names(await session.GetMcpStatusAsync()));
            await Verb("toggle on", session.ToggleMcpServerAsync("calc", true));
        });

    // A verb the binary answers or refuses; either answer is the recording.
    private static async Task Verb(string name, Task verb)
    {
        try
        {
            await verb;
            Say(name, "ok");
        }
        catch (ClaudeAgentException e)
        {
            Say($"{name} refused", $"{e.ErrorCode}: {e.Message}");
        }
    }

    // No task runs; the binary answers a stop_task for an id it does not know.
    private Task StopTaskAsync() =>
        WithSessionAsync("stop-task", Base(1, Effort.Low), async session =>
        {
            try
            {
                await session.StopTaskAsync("task_00000000");
                Say("stop", "ok");
            }
            catch (ClaudeAgentException e)
            {
                Say("stop refused", $"{e.ErrorCode}: {e.Message}");
            }
        });

    // File checkpointing on, user messages replayed so a uuid comes back to rewind to.
    private Task RewindFilesAsync()
    {
        var options = Base(3, Effort.Low);
        options.Tools = ["Write"];
        options.AllowedTools.Add("Write");
        options.EnableFileCheckpointing = true;
        options.ReplayUserMessages = true;
        return WithSessionAsync("rewind-files", options, async session =>
        {
            string? checkpoint = null;
            await Drain(session.SendAsync("Create a file named rewound.txt in the working directory containing exactly: before"), "rewind-files", message =>
            {
                if (message is UserMessage { Uuid: { } uuid } && checkpoint is null)
                {
                    checkpoint = uuid;
                }
            });
            Say("checkpoint", checkpoint);
            await session.RewindFilesAsync(checkpoint!);
            Say("rewind", "ok");
        });
    }

    private Task ForkParentAsync() =>
        QueryAsync("fork-parent", Base(effort: Effort.Low), "Reply with exactly one word: pong");

    private Task ForkAsync()
    {
        var options = Base(effort: Effort.Low);
        options.ResumeSessionId = recording.SessionIdOf("fork-parent");
        options.Fork = true;
        return QueryAsync("fork", options, "Reply with exactly the word you replied last time.");
    }

    private Task ResumeAsync()
    {
        var options = Base(effort: Effort.Low);
        options.ResumeSessionId = recording.SessionIdOf("fork-parent");
        return QueryAsync("resume", options, "Reply with exactly the word you replied last time.");
    }

    // A background shell task started by the model and stopped by the SDK; then a subagent run to its end.
    private Task TaskAsync()
    {
        var options = Base(3, Effort.Low);
        options.Tools = ["Bash", "Agent"];
        options.AllowedTools.Add("Bash");
        options.AllowedTools.Add("Agent");
        return WithSessionAsync("task", options, async session =>
        {
            var started = new List<string>();
            await Drain(session.SendAsync("Use the Bash tool with run_in_background set to true to run: sleep 300. Then reply with exactly the word: started"), "task/1", message =>
            {
                if (message is TaskStartedMessage { TaskId: { } id })
                {
                    started.Add(id);
                }
            });
            Say("task ids", string.Join(", ", started));
            if (started.Count > 0)
            {
                await session.StopTaskAsync(started[0]);
                Say("stop", "ok");
                await DrainFor(session, 8, "task/stop");
            }

            await Drain(session.SendAsync("Use the Agent tool to launch one subagent whose only instruction is: reply with the single word pong. Then reply with exactly the subagent's answer."), "task/2");
            await DrainFor(session, 4, "task/after");
        });
    }

    // IncludeHookEvents with a settings hook beside an SDK hook: what the binary emits as system frames.
    private async Task HookEventsAsync()
    {
        var seen = new List<string>();
        var options = Base(3, Effort.Low);
        options.Tools = ["Read"];
        options.AllowedTools.Add("Read");
        options.IncludeHookEvents = true;
        options.Settings = """{"hooks": {"PreToolUse": [{"matcher": "Read", "hooks": [{"type": "command", "command": "exit 0"}]}], "PostToolUse": [{"matcher": "Read", "hooks": [{"type": "command", "command": "exit 0"}]}]}}""";
        options.AddHook(HookEvent.PreToolUse, "Read", (input, _) =>
        {
            seen.Add($"pre {Tool(input)}");
            return Done();
        });
        await QueryAsync("hook-events", options, "Read notes.txt in the working directory and reply with its first line only.");
        Say("hooks seen", string.Join(", ", seen));
    }

    // /clear between two turns of one client session.
    private Task ConversationResetAsync() =>
        WithSessionAsync("conversation-reset", Base(1, Effort.Low), async session =>
        {
            await Drain(session.SendAsync("Say: one"), "reset/1");
            await DrainFor(session, 10, "reset/clear", session.SendAsync("/clear"));
            await Drain(session.SendAsync("Say: two"), "reset/2");
        });

    // An interrupt while a hook callback is pending, then while the permission callback is pending.
    private async Task CancelAsync()
    {
        var outcome = new List<string>();
        var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var asked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var options = Base(3, Effort.Low);
        options.Tools = ["Read", "Write"];
        options.AddHook(HookEvent.PreToolUse, "Read", async (_, ct) =>
        {
            fired.TrySetResult();
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
            catch (OperationCanceledException)
            {
                outcome.Add("hook cancelled");
                throw;
            }

            outcome.Add("hook ran out");
            return new HookOutput();
        });
        options.PermissionCallback = async (_, ct) =>
        {
            asked.TrySetResult();
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
            catch (OperationCanceledException)
            {
                outcome.Add("permission cancelled");
                throw;
            }

            outcome.Add("permission ran out");
            return ToolPermissionResult.Allow();
        };
        await WithSessionAsync("cancel", options, async session =>
        {
            async Task InterruptWhen(Task signal, string label)
            {
                await signal;
                await Task.Delay(TimeSpan.FromSeconds(1));
                Say("interrupting during", label);
                await session.InterruptAsync();
            }

            async Task Bounded(ClaudeAgentRun run, string label)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                try
                {
                    await Drain(run, label, cancellationToken: timeout.Token);
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                {
                    Say("no result within 60s after the interrupt", label);
                }
            }

            var run = session.SendAsync("Read notes.txt in the working directory and reply with its first line only.");
            var waiter = InterruptWhen(fired.Task, "hook");
            await Bounded(run, "cancel/hook");
            await waiter;
            run = session.SendAsync("Create a file named cancelled.txt in the working directory containing exactly: no");
            waiter = InterruptWhen(asked.Task, "permission");
            await Bounded(run, "cancel/permission");
            await waiter;
            await DrainFor(session, 3, "cancel/after");
        });
        Say("outcome", string.Join(", ", outcome));
    }

    // Write in the tool set, not in AllowedTools, and no permission callback: the binary denies the call
    // by its own rule (a disallowed tool is never offered to the model, so that path records no denial).
    private Task DeniedAsync()
    {
        var options = Base(2, Effort.Low);
        options.Tools = ["Write"];
        return QueryAsync("denied", options, "Create a file named denied.txt in the working directory containing exactly: no. If you cannot, reply with exactly the word: denied");
    }

    // A real stdio server: reconnect, status, toggle off and on, then a tool call end to end. Only the
    // configured server, so the login's own connectors stay out of the status and the init frame.
    private Task StdioMcpAsync()
    {
        var options = Base(3, Effort.Low);
        options.Tools = [];
        options.McpServers["echo"] = McpServerConfiguration.Stdio(Path.Combine(cwd, "echo-server"));
        options.OnlyConfiguredMcpServers = true;
        options.AllowedTools.Add("mcp__echo__echo");
        return WithSessionAsync("stdio-mcp", options, async session =>
        {
            Say("status", Names(await session.GetMcpStatusAsync()));
            await session.ReconnectMcpServerAsync("echo");
            Say("reconnect", "ok");
            await session.ToggleMcpServerAsync("echo", false);
            Say("toggle off", "ok");
            Say("status", Names(await session.GetMcpStatusAsync()));
            await session.ToggleMcpServerAsync("echo", true);
            Say("toggle on", "ok");
            await Drain(session.SendAsync("Use the echo tool with the text pong, then reply with exactly what it returned."), "stdio-mcp");
        });
    }

    // The in-process bridge through the same verbs, then a tool call, in one session.
    private Task SdkMcpClientAsync() =>
        WithSessionAsync("sdk-mcp-client", WithCalc(3), async session =>
        {
            Say("status", Names(await session.GetMcpStatusAsync()));
            try
            {
                await session.ReconnectMcpServerAsync("calc");
                Say("reconnect", "ok");
            }
            catch (ClaudeAgentException e)
            {
                Say("reconnect refused", $"{e.ErrorCode}: {e.Message}");
            }

            try
            {
                await session.ToggleMcpServerAsync("calc", false);
                Say("toggle off", "ok");
            }
            catch (ClaudeAgentException e)
            {
                Say("toggle refused", $"{e.ErrorCode}: {e.Message}");
            }

            await Drain(session.SendAsync("Use the add tool to add 2 and 3, then reply with just the number."), "sdk-mcp-client");
        });

    // The hook inputs the first hooks run never reached: PostToolUseFailure, SubagentStart, SubagentStop,
    // PermissionRequest, Notification, PreCompact. One turn: a Read that fails, a subagent, a tool that needs asking.
    private async Task HooksMoreAsync()
    {
        var seen = new List<string>();
        var options = Base(6, Effort.Low);
        options.Tools = ["Read", "Agent", "Write"];
        options.AllowedTools.Add("Read");
        options.AllowedTools.Add("Agent");
        options.PermissionCallback = (request, _) =>
        {
            seen.Add($"can_use_tool {request.ToolCall.Name}");
            return Task.FromResult(ToolPermissionResult.Allow());
        };
        foreach (var hookEvent in new[] { HookEvent.PostToolUseFailure, HookEvent.SubagentStart, HookEvent.SubagentStop, HookEvent.PermissionRequest, HookEvent.Notification, HookEvent.PreCompact })
        {
            options.AddHook(hookEvent, null, (input, _) =>
            {
                seen.Add($"{hookEvent} {input.Event} {Tool(input)}");
                return Done();
            });
        }

        await QueryAsync("hooks-more", options, "Do these three things in order, each with one tool call: read the file missing.txt in the working directory (it does not exist; carry on when the read fails); launch one subagent with the Agent tool whose only instruction is to reply pong; write a file named asked.txt containing exactly: yes. Then reply with exactly the word: done");
        Say("hooks seen", string.Join(", ", seen));
    }

    // A PreToolUse hook that answers defer: the run stops and the result carries deferred_tool_use.
    private Task DeferAsync()
    {
        var options = Base(3, Effort.Low);
        options.Tools = ["Read"];
        options.AllowedTools.Add("Read");
        options.AddHook(HookEvent.PreToolUse, "Read", (_, _) => Task.FromResult(new HookOutput { Decision = PermissionDecision.Defer }));
        return QueryAsync("defer", options, "Read notes.txt in the working directory and reply with its first line only.");
    }

    // A json_schema output format with thinking enabled: the StructuredOutput tool_use and structured_output
    // on the result.
    private async Task StructuredAsync()
    {
        var options = Base(1, Effort.Medium);
        options.Thinking = ThinkingOptions.WithBudget(1024);
        options.OutputSchema = Json("""{"type": "object", "properties": {"word": {"type": "string"}, "letters": {"type": "integer"}}, "required": ["word", "letters"], "additionalProperties": false}""");
        recording.Begin("structured");

        // The run is held rather than drained through the helper, so the answer is read where a
        // caller reads it: off the trace, not off a second spelling of the result's own key.
        var run = Client().QueryAsync("Think briefly, then answer: the word is pong; how many letters does it have?", options);
        await Drain(run, "structured", message =>
        {
            if (message is AssistantMessage assistant)
            {
                Say("blocks", string.Join(", ", assistant.Content.Select(block => block.GetType().Name)));
            }
        });

        Say("structured", (await run.WaitForCompletionAsync()).GetStructuredOutput<JsonElement>());
    }

    private const string SchemaPrompt = "Report on the word: pong";

    // The schema SetOutputSchema<T> takes from a type, its root an object: the StructuredOutput
    // tool_use and structured_output on the result, read back as the type.
    private async Task SchemaTypeAsync()
    {
        var options = Base(2, Effort.Low);
        options.SetOutputSchema<WordReport>();
        recording.Begin("schema-type");

        var run = Client().QueryAsync(SchemaPrompt, options);
        await Drain(run, "schema-type");
        var answer = (await run.WaitForCompletionAsync()).GetStructuredOutput<WordReport>();
        Say("schema-type", answer is null ? "no answer" : $"{answer.Word} {answer.Letters} {answer.PartOfSpeech} {string.Concat(answer.Spelling)}");
    }

    // The same schema with its root made nullable, as the platform's exporter writes a class's, and
    // nothing else changed: what the binary does with a root that is not an object alone. The library
    // refuses that root before any process starts, so the call asks for the schema as the type gives
    // it, and the transport is handed the nullable one in its place, past the refusal.
    private async Task SchemaNullableAsync()
    {
        var options = Base(2, Effort.Low);
        options.SetOutputSchema<WordReport>();
        var schema = JsonNode.Parse(options.OutputSchema!.Value.GetRawText())!.AsObject();
        schema["type"] = new JsonArray("object", "null");
        var nullable = Json(schema.ToJsonString());
        recording.Begin("schema-nullable");

        var client = new ClaudeAgentClient(
            new ClaudeAgentClientOptions { ExecutablePath = executable },
            null,
            new SessionChangingTransportFactory(Transports(), session => session.OutputSchema = nullable),
            null);
        await Drain(client.QueryAsync(SchemaPrompt, options), "schema-nullable");
    }

    // An ask: the evidence written into the prompt off its type, the answer's schema WordReport's,
    // the answer read back as WordReport off the trace, and the trace naming both types.
    private async Task AskAsync()
    {
        var options = Base(2, Effort.Low);
        recording.Begin("ask");

        var run = Client().AskAsync<WordEvidence, WordReport>(WordEvidence.AskTask, WordEvidence.Recorded, options);
        await Drain(run, "ask");
        var trace = await run.WaitForCompletionAsync();
        var answer = trace.GetStructuredOutput<WordReport>();
        Say("ask", $"{trace.EvidenceType?.Name} -> {trace.AnswerType?.Name}: " + (answer is null ? "no answer" : $"{answer.Word} {answer.Letters} {answer.PartOfSpeech} {string.Concat(answer.Spelling)}"));
    }

    // WebSearch: a client-side tool_use WebSearch and a user tool_result, not a server_tool_use block.
    private Task WebSearchAsync()
    {
        var options = Base(2, Effort.Low);
        options.Tools = ["WebSearch"];
        options.AllowedTools.Add("WebSearch");
        return QueryAsync("web-search", options, "Search the web for: what year was the Finnish city of Oulu founded. Reply with the year only.", each: message =>
        {
            if (message is AssistantMessage assistant)
            {
                Say("blocks", string.Join(", ", assistant.Content.Select(block => block.GetType().Name)));
            }
        });
    }

    // A subagent in the background that uses tools: task_progress frames while it runs, the result before it
    // ends, and the notification after.
    private Task TaskBackgroundAsync()
    {
        var options = Base(3, Effort.Low);
        options.Tools = ["Agent", "Read"];
        options.AllowedTools.Add("Agent");
        options.AllowedTools.Add("Read");
        return WithSessionAsync("task-background", options, async session =>
        {
            await Drain(session.SendAsync("Use the Agent tool with run_in_background set to true to launch one subagent whose instruction is: read notes.txt in the working directory three separate times with the Read tool, then reply pong. Do not wait for it; reply immediately with exactly the word: started"), "task-background/1");
            await DrainFor(session, 45, "task-background/after");
        });
    }

    // /compact between two turns with a PreCompact hook registered.
    private async Task CompactAsync()
    {
        var seen = new List<string>();
        var options = Base(1, Effort.Low);
        options.AddHook(HookEvent.PreCompact, null, (input, _) =>
        {
            seen.Add($"PreCompact {(input is PreCompactHookInput compact ? compact.Trigger.ToString() : string.Empty)}");
            return Done();
        });
        await WithSessionAsync("compact", options, async session =>
        {
            await Drain(session.SendAsync("Say: one"), "compact/1");
            await DrainFor(session, 40, "compact/compact", session.SendAsync("/compact"));
            await Drain(session.SendAsync("Say: two"), "compact/2");
        });
        Say("hooks seen", string.Join(", ", seen));
    }

    // The permission suggestions the first recordings never reached: a Bash command, a Read outside the cwd
    // and a Write outside it, none allowed, so the binary asks for each and says what rule or directory would
    // stop it asking. CWD/../outside/notes.txt is a one-line file.
    private async Task SuggestionsAsync()
    {
        var asked = new List<string>();
        var options = Base(5, Effort.Low);
        options.Tools = ["Bash", "Read", "Write"];
        options.PermissionCallback = (request, _) =>
        {
            asked.Add($"{request.ToolCall.Name}: {string.Join(" + ", request.Suggestions.Select(suggestion => suggestion.GetType().Name))}");
            return Task.FromResult(ToolPermissionResult.Allow());
        };
        var outside = Path.GetFullPath(Path.Combine(cwd, "..", "outside"));
        await QueryAsync("suggestions", options, $"Do these three things in order, each with one tool call: run the shell command `git init --bare hive.git` with the Bash tool; read the file {outside}/notes.txt with the Read tool; write a file {outside}/asked.txt containing exactly: yes. Then reply with exactly the word: done");
        Say("asked", string.Join("; ", asked));
    }

    // An agent declared by this process, in the initialize request: the init frame lists it, the Agent tool
    // starts it by name, and it calls the stdio server it names.
    private Task AgentsAsync()
    {
        var options = Base(3, Effort.Low);
        options.Tools = ["Agent"];
        options.AllowedTools.Add("Agent");
        options.AllowedTools.Add("mcp__echo__echo");
        options.McpServers["echo"] = McpServerConfiguration.Stdio(Path.Combine(cwd, "echo-server"));
        options.OnlyConfiguredMcpServers = true;
        options.Agents["bee"] = new AgentDefinition(
            "Echoes a word through the echo tool and reports what came back.",
            "You are bee. Call the echo tool with the text you are given, then reply with exactly what it returned.")
        {
            Tools = ["mcp__echo__echo"],
            Model = Model,
            McpServers = ["echo"],
            MaxTurns = 3,
        };
        return QueryAsync("agents", options, "Use the Agent tool with subagent_type bee and the prompt: pong. Then reply with exactly the subagent's answer.");
    }

    // The binary as it ships: no --tools, so the init frame lists the tools it offers by default, and
    // one turn that needs none.
    private Task DefaultsAsync() =>
        QueryAsync("defaults", Base(), "Reply with exactly one word: ok");

    // The built-in tools that read and write files, run the shell, fetch the web and keep the todo list,
    // each asked for once, with a hook on each tool event, so a call is recorded as the stream carries it
    // and as the hooks see it. 2.1.276 offers no TodoWrite: the model says so and skips it, and eight of
    // the nine are called. Two reads go in one message; the last read names a file that does not exist.
    // draft.txt and cells.ipynb are the fixture's own, and gone afterwards.
    private async Task ToolsAsync()
    {
        string[] tools = ["Glob", "Grep", "Read", "Write", "Edit", "NotebookEdit", "TodoWrite", "WebFetch", "Bash"];
        var draft = Path.Combine(cwd, "draft.txt");
        var notebook = Path.Combine(cwd, "cells.ipynb");
        File.WriteAllText(notebook, """{"cells": [{"cell_type": "code", "execution_count": null, "id": "a1", "metadata": {}, "outputs": [], "source": ["print(1)"]}], "metadata": {}, "nbformat": 4, "nbformat_minor": 5}""");
        var seen = new List<string>();
        try
        {
            var options = Base(16, Effort.Low);
            options.Tools = tools;
            foreach (var tool in tools)
            {
                options.AllowedTools.Add(tool);
            }

            foreach (var hookEvent in new[] { HookEvent.PreToolUse, HookEvent.PostToolUse, HookEvent.PostToolUseFailure })
            {
                options.AddHook(hookEvent, null, (input, _) =>
                {
                    seen.Add($"{input.Event} {Tool(input)}");
                    return Done();
                });
            }

            await QueryAsync("tools", options, "Do these steps in order, one tool call each unless a step says otherwise, without explaining: 1. Glob the pattern *.txt. 2. Grep the pattern line in notes.txt with output mode content. 3. In one message, Read notes.txt and Read cells.ipynb together. 4. Write draft.txt containing exactly: alpha. 5. Edit draft.txt replacing alpha with beta. 6. NotebookEdit cells.ipynb: replace the source of the first cell with print(2). 7. TodoWrite a list with one item, content check the draft, status completed. 8. WebFetch https://example.com with the prompt: what is the page title? 9. Run the shell command `wc -l notes.txt` with Bash. 10. Read missing.txt. Then reply with exactly the word: done");
        }
        finally
        {
            File.Delete(draft);
            File.Delete(notebook);
        }

        Say("hooks seen", string.Join(", ", seen));
    }

    // The task tools around a background shell: Bash starts it, TaskOutput reads it without waiting, and
    // TaskStop ends it.
    private Task TaskToolsAsync()
    {
        string[] tools = ["Bash", "TaskOutput", "TaskStop"];
        var options = Base(6, Effort.Low);
        options.Tools = tools;
        foreach (var tool in tools)
        {
            options.AllowedTools.Add(tool);
        }

        return QueryAsync("task-tools", options, "Do these steps in order, one tool call each, without explaining: 1. Run the shell command `sleep 30` with Bash in the background. 2. Call TaskOutput on the task it started, without blocking, with a timeout of 1000. 3. Stop that task with TaskStop. Then reply with exactly the word: done");
    }

    // What a session says about itself on the binary as it ships: the initialize reply, then around one
    // turn a context usage, the status of an in-process and a stdio server, and a permission mode set.
    private Task PayloadsAsync()
    {
        var options = Base(1, Effort.Low);
        options.Tools = [];
        options.McpServers["calc"] = McpServerConfiguration.InProcess(new Calc());
        options.McpServers["echo"] = McpServerConfiguration.Stdio(Path.Combine(cwd, "echo-server"));
        options.OnlyConfiguredMcpServers = true;
        return WithSessionAsync("payloads", options, async session =>
        {
            Say("server info commands", string.Join(", ", session.ServerInfo?.Commands.Select(command => command.Name) ?? []));
            await Drain(session.SendAsync("Reply with exactly one word: ok"), "payloads");
            var usage = await session.GetContextUsageAsync();
            Say("context usage categories", string.Join(", ", usage.Categories.Select(category => category.Name)));
            Say("mcp status", Names(await session.GetMcpStatusAsync()));
            await session.SetPermissionModeAsync(PermissionMode.AcceptEdits);
            Say("mode", "acceptEdits");
        });
    }

    // A prompt sent while the binary runs a turn it opened itself. Twice a background shell ends
    // after its turn's result and the binary opens a turn of its own to report it: the first time a
    // prompt goes out as the notification arrives, the second as that turn makes its first tool
    // call. Every prompt carries a uuid, so the frames say which turn took which prompt.
    private Task ContinuationAsync()
    {
        var options = Base(4, Effort.Low);
        options.Tools = ["Bash"];
        options.AllowedTools.Add("Bash");
        return WithSessionAsync("continuation", options, async session =>
        {
            await Drain(session.SendAsync("Run the shell command `sleep 5` with Bash in the background. Then reply with exactly the word: started"), "continuation/1");
            await Until(session, message => message is TaskNotificationMessage, "continuation/own");
            await DrainFor(session, 45, "continuation/2", session.SendAsync("Say: two"));

            await Drain(session.SendAsync("Run the shell command `sleep 5` with Bash in the background. Then reply with exactly the word: started. When it has finished, run the shell command `wc -l notes.txt` with Bash, then reply with exactly the word: counted"), "continuation/3");
            await Until(session, message => message is AssistantMessage assistant && assistant.Content.Any(block => block is ToolUseBlock), "continuation/own");
            await DrainFor(session, 45, "continuation/4", session.SendAsync("Say: four"));
        });
    }

    // Read what no run reads until a message matches, for a minute at most.
    private static async Task Until(ClaudeAgentSession session, Func<AgentMessage, bool> match, string label)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await foreach (var message in session.ReceiveMessagesAsync(timeout.Token))
            {
                Report(message, label);
                if (match(message))
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            Say("nothing matched within 60s", label);
        }
    }

    // Read as a caller meets it: a text file by offset and limit, the same file whole and then again
    // unchanged, an image, whose tool result carries the image itself, and a PDF whole and by page,
    // with a hook on each tool event. dot.png and page.pdf are the fixture's own, and gone afterwards.
    private async Task ReadsAsync()
    {
        var image = Path.Combine(cwd, "dot.png");
        var document = Path.Combine(cwd, "page.pdf");
        File.WriteAllBytes(image, Convert.FromBase64String(Dot));
        File.WriteAllBytes(document, OnePagePdf());
        var seen = new List<string>();
        try
        {
            var options = Base(10, Effort.Low);
            options.Tools = ["Read"];
            options.AllowedTools.Add("Read");
            foreach (var hookEvent in new[] { HookEvent.PreToolUse, HookEvent.PostToolUse, HookEvent.PostToolUseFailure })
            {
                options.AddHook(hookEvent, null, (input, _) =>
                {
                    seen.Add($"{input.Event} {Tool(input)}");
                    return Done();
                });
            }

            await QueryAsync("reads", options, "Do these steps in order, one tool call each, without explaining: 1. Read notes.txt from line 2 with a limit of 1 line. 2. Read notes.txt. 3. Read notes.txt again. 4. Read dot.png. 5. Read page.pdf. 6. Read page.pdf with pages 1. Then reply with exactly the word: done");
        }
        finally
        {
            File.Delete(image);
            File.Delete(document);
        }

        Say("hooks seen", string.Join(", ", seen));
    }

    // The tools that search and change files, and the shell, as a caller meets them: Glob under a
    // directory, Grep in its other two modes and with its flags, Write over a file that is there, Edit
    // replacing every match and Edit finding nothing, Bash with a timeout, a command that finds nothing,
    // one that fails, and one a PreToolUse hook denies as a caller's guard does. draft.txt is the
    // fixture's own, and gone afterwards.
    private async Task EditsAsync()
    {
        string[] tools = ["Glob", "Grep", "Write", "Edit", "Bash"];
        var draft = Path.Combine(cwd, "draft.txt");
        var seen = new List<string>();
        try
        {
            var options = Base(16, Effort.Low);
            options.Tools = tools;
            foreach (var tool in tools)
            {
                options.AllowedTools.Add(tool);
            }

            options.AddHook(HookEvent.PreToolUse, null, (input, _) =>
            {
                seen.Add($"{input.Event} {Tool(input)}");
                return Task.FromResult(
                    input is PreToolUseHookInput { ToolCall.Input: BashToolInput bash } && bash.Command.Contains("touch", StringComparison.Ordinal)
                        ? new HookOutput { Decision = PermissionDecision.Deny, DecisionReason = "guarded: no new files" }
                        : new HookOutput());
            });
            foreach (var hookEvent in new[] { HookEvent.PostToolUse, HookEvent.PostToolUseFailure })
            {
                options.AddHook(hookEvent, null, (input, _) =>
                {
                    seen.Add($"{input.Event} {Tool(input)}");
                    return Done();
                });
            }

            await QueryAsync("edits", options, $"Do these steps in order, one tool call each, without explaining: 1. Glob the pattern *.txt in the directory {cwd}. 2. Grep the pattern line with output mode files_with_matches. 3. Grep the pattern LINE case-insensitively with output mode count. 4. Grep the pattern second in notes.txt with output mode content, with line numbers, one line of context before each match, and a head limit of 1. 5. Write draft.txt containing exactly: a a. 6. Write draft.txt containing exactly: a a a. 7. Edit draft.txt replacing every a with b. 8. Edit draft.txt replacing zzz with y. 9. Run the shell command `wc -l notes.txt` with Bash with a timeout of 30000. 10. Run the shell command `grep zzz notes.txt` with Bash. 11. Run the shell command `ls nowhere` with Bash. 12. Run the shell command `touch guarded.txt` with Bash. Then reply with exactly the word: done");
        }
        finally
        {
            File.Delete(draft);
            File.Delete(Path.Combine(cwd, "guarded.txt"));
        }

        Say("hooks seen", string.Join(", ", seen));
    }

    // No fixture's frames: a transcript in which the binary writes a tag of its own. A session is
    // tagged through the session client, whose line the binary reads back on a resume; the resumed
    // turn reads a file of forty thousand characters, so that line is further back than the binary
    // looks for it, and it writes the tag again after the turn. long.txt is the run's own, and gone
    // afterwards. The transcript is kept; the two runs are not recorded.
    private async Task TaggedAsync()
    {
        var file = Path.Combine(cwd, "long.txt");
        File.WriteAllLines(file, Enumerable.Range(1, 800).Select(line => $"line {line:D4} " + new string('x', 40)));
        try
        {
            string? session = null;
            await Drain(Client().QueryAsync("Reply with exactly one word: pong", Base(effort: Effort.Low)), "tagged/1", message => session ??= (message as SystemInitMessage)?.SessionId);
            var tagged = session ?? throw new InvalidOperationException("tagged has no init frame");
            await Client().GetSessionClient().TagSessionAsync(tagged, "kept", cwd);
            var resumed = Base(3, Effort.Low);
            resumed.ResumeSessionId = tagged;
            resumed.Tools = ["Read"];
            resumed.AllowedTools.Add("Read");
            await Drain(Client().QueryAsync("Read long.txt whole, in one call. Then reply with exactly the word: read", resumed), "tagged/2");
            KeepTranscripts(tagged);
        }
        finally
        {
            File.Delete(file);
        }
    }

    // Extended thinking on, with a budget: whether haiku writes a thinking block's text, which every
    // other recording carries empty.
    private Task ThinkingAsync()
    {
        var options = Base();
        options.Thinking = ThinkingOptions.WithBudget(2048);
        return QueryAsync(
            "thinking",
            options,
            "A train leaves at 14:35 and arrives at 17:10. How many minutes is the trip? Think it through, then reply with just the number.",
            each: message =>
            {
                foreach (var thinking in (message as AssistantMessage)?.Content.OfType<ThinkingBlock>() ?? [])
                {
                    Say("thinking characters", thinking.Thinking.Length);
                }
            });
    }

    // An interrupt while an in-process MCP tool is still answering: whether the binary cancels the
    // mcp_message request it waits on, and how the turn ends. The tool waits until it is cancelled.
    private async Task McpCancelAsync()
    {
        var slow = new Slow();
        var options = Base(3, Effort.Low);
        options.Tools = [];
        options.McpServers["slow"] = McpServerConfiguration.InProcess(slow);
        options.AllowedTools.Add("mcp__slow__wait");
        await WithSessionAsync("mcp-cancel", options, async session =>
        {
            var run = session.SendAsync("Call the wait tool once. Then reply with exactly the word: waited");
            var interrupting = Task.Run(async () =>
            {
                await slow.Called.Task.WaitAsync(TimeSpan.FromSeconds(60));
                await Task.Delay(TimeSpan.FromSeconds(1));
                await run.InterruptAsync();
                Say("interrupt", "sent");
            });
            using var patience = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            await Drain(run, "mcp-cancel", cancellationToken: patience.Token);
            await interrupting;
            await Task.Delay(TimeSpan.FromSeconds(1));
            Say("tool", slow.Outcome);
        });
    }

    // A Stop hook while a background shell task is in flight: the binary lists the task on the hook's
    // input. The query ends at its result; the task completes after it, the binary opens a turn of
    // its own, and the recording stops two frames into that turn.
    private async Task BackgroundStopAsync()
    {
        var seen = new List<string>();
        var options = Base(3, Effort.Low);
        options.Tools = ["Bash"];
        options.AllowedTools.Add("Bash");
        options.AddHook(HookEvent.Stop, null, (input, _) =>
        {
            if (input is StopHookInput stop)
            {
                seen.Add(string.Join(",", stop.BackgroundTasks.Select(task => $"{task.TaskType}/{task.Status}")));
            }

            return Done();
        });
        await QueryAsync("background-stop", options, "Use the Bash tool with run_in_background set to true to run: sleep 5. Then reply with exactly the word: started");
        Say("stop hooks saw", string.Join(" | ", seen));
    }

    // The session's interrupt between turns while a background shell the first turn started still
    // sleeps: the interrupt is written once that turn's run has ended, and what the binary sends in
    // the next 45 seconds, past the shell's own end had it not been stopped, is the recording. The
    // log gives each message the seconds since the interrupt was answered.
    private Task BackgroundInterruptAsync()
    {
        var options = Base(3, Effort.Low);
        options.Tools = ["Bash"];
        options.AllowedTools.Add("Bash");
        return WithSessionAsync("background-interrupt", options, async session =>
        {
            await Drain(session.SendAsync("Use the Bash tool with run_in_background set to true to run: sleep 30. Then reply with exactly the word: started"), "background-interrupt/1");
            await session.InterruptAsync();
            var answered = System.Diagnostics.Stopwatch.StartNew();
            Say("interrupt", "answered");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            try
            {
                await foreach (var message in session.ReceiveMessagesAsync(timeout.Token))
                {
                    Say($"+{answered.Elapsed.TotalSeconds:F1}s", message.GetType().Name);
                    Report(message, "background-interrupt/after");
                }
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
            }
        });
    }

    // No fixture's frames: a transcript in which the binary writes a custom-title line of its own,
    // through the /rename command it runs outside an interactive terminal. The transcript is kept;
    // the runs are not recorded.
    private async Task RenamedAsync()
    {
        string? id = null;
        await using var session = await Client().StartSessionAsync(Base(effort: Effort.Low));
        await Drain(session.SendAsync("Reply with exactly one word: pong"), "renamed/1", message => id ??= message.SessionId);
        await DrainFor(session, 15, "renamed/rename", session.SendAsync("/rename kept title"));
        KeepTranscripts(id ?? throw new InvalidOperationException("renamed has no session id"));
    }

    private sealed class Slow
    {
        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Outcome { get; private set; } = "not called";

        [McpTool("wait", "Waits until it is told to stop")]
        public async Task<string> Wait(CancellationToken cancellationToken)
        {
            Outcome = "waiting";
            Called.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return "done";
            }
            catch (OperationCanceledException)
            {
                Outcome = "cancelled";
                throw;
            }
        }
    }

    // A one-pixel PNG.
    private const string Dot = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    // One page saying "one page", its cross-reference table counted off the bytes before it.
    private static byte[] OnePagePdf()
    {
        const string content = "BT /F1 18 Tf 20 40 Td (one page) Tj ET";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ];
        var text = new System.Text.StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(text.Length);
            text.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var table = text.Length;
        text.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            text.Append($"{offset:D10} 00000 n \n");
        }

        text.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{table}\n%%EOF\n");
        return System.Text.Encoding.ASCII.GetBytes(text.ToString());
    }

    private sealed class Calc
    {
        [McpTool("add", "Add two numbers")]
        public static double Add(double a, double b) => a + b;
    }

    // Changes what a session asks after the library has validated it, as the transport is made: how a
    // fixture drives the binary with what the library refuses.
    private sealed class SessionChangingTransportFactory(ITransportFactory inner, Action<ClaudeAgentSessionOptions> change)
        : ITransportFactory
    {
        public ITransport Create(ClaudeAgentClientOptions options, ClaudeAgentSessionOptions session, TransportNeeds needs)
        {
            change(session);
            return inner.Create(options, session, needs);
        }
    }
}
