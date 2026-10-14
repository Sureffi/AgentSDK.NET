# AgentSDK.NET

An unofficial Claude Agent SDK for .NET, not made or endorsed by Anthropic. It drives the `claude` binary over
stream-json on the account that binary is logged into — there is no
Messages API call anywhere in the tree, and no API key.

One namespace, `Claude.Agent`. A client, a live session, a second client
over the transcripts its sessions keep on disk; options for how the binary
is reached and options for what one call asks of it, hooks and a
permission callback as delegates on the second; in-process MCP tools;
models the library builds off the wire and a caller reads, which
`ClaudeAgentModelFactory` builds for a caller's own tests, and a typed
input and output for the binary's tools, which a caller reads and hands
back. The shape is the Azure SDK design guidelines for .NET.

## install

The package is not on nuget. `dotnet pack` at the root produces
`Claude.Agent.<version>.nupkg` for a folder feed, and a project
reference works the same; either way the generic host is the
consumer's, the library does not bring it in:

    <PackageReference Include="Claude.Agent" Version="3.0.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.12" />

One line is the whole bootstrap:

    builder.Services.AddClaudeAgentClient(options => options.Defaults.Model = "haiku");

`AddClaudeAgentClient` sits in `Microsoft.Extensions.DependencyInjection`,
beside `AddHostedService`. It registers `ClaudeAgentClient` and
`ClaudeSessionClient`, validates the options when the host starts, and
touches nothing on disk. Without a container,
`new ClaudeAgentClient(new ClaudeAgentClientOptions { ... })` is the
same client. The library references the BCL and `Microsoft.Extensions.*`,
nothing else.

## the one example

Usings: `Claude.Agent`, and `Microsoft.Extensions.DependencyInjection`
and `Microsoft.Extensions.Hosting` for the host.

    builder.Services.AddClaudeAgentClient(options =>
    {
        options.Defaults.SystemPrompt = AgentSystemPrompt.FromText("You are Mint. Answer in one short paragraph.");
        options.Defaults.MaxTurns = 1;
        options.Defaults.Model = "haiku";
    });

A `BackgroundService` takes `ClaudeAgentClient` and streams one turn:

    await foreach (var message in client.QueryAsync("What is a mint?", stoppingToken))
    {
        if (message is AssistantMessage { Text.Length: > 0 } assistant)
        {
            Console.WriteLine(assistant.Text);
        }

        if (message is ResultMessage { IsError: true } result)
        {
            Console.Error.WriteLine(result.Result ?? result.Subtype.ToString());
        }
    }

That is `samples/Mint`, minus the switch it prints the other message
types with and its `--resume`, which passes `ContinueLatest` on the
call. `QueryAsync` hands back the run at once and starts the binary
behind it. Enumerated, the run yields every message and ends after the
result, with a `ToolCallMessage` after each message that starts or
closes a tool call; `await run.WaitForCompletionAsync()` alone returns
the same messages as an `AgentTrace` instead, the result last, with the
run's tool calls, its text and `GetStructuredOutput<T>()` as views over
them. A run is read one of those two ways, and a second enumeration
throws. Every message read off the binary carries `Raw`, the line as the
binary wrote it; one the library assembles carries none.
The binary runs on the account it is logged into, in a configuration
directory of the client's own (see sessions on disk), so the answer is
that account's and the user's own settings and `CLAUDE.md` do not reach
it; `SettingSources = []` starts it bare.

A query's stdin stays open until the result that ends its turn, and
past it while a background agent or workflow the turn started is in
flight, so `run.InterruptAsync()` reaches the binary all the while; a
background shell does not hold it open past the result. At that result
the session closes: stdin is closed, the process is awaited to its
exit and what it wrote as it shut down is read; only then is the result
handed out, last. What the process wrote after it comes before it, and a
tool call still open is closed unanswered before it. When the run's
completion is there the process is gone. A binary that stays after its
stdin closes is ended by the close sequence, which delays the result by
up to `ProcessGracePeriod`, twice when SIGTERM does not end it; the run
completes with its trace all the same. A loop left before the stream
ended — a `break`, an exception — interrupts the turn, and is left once
the run has ended with the interrupted trace, which
`WaitForCompletionAsync` then returns; a loop read to its end
interrupts nothing. The call's token, given up before the turn's
result, closes the session and ends the run cancelled; once that result
is in, it stops nothing and the run completes.

## per call

`ClaudeAgentClientOptions` is how the binary is reached: `ExecutablePath`,
`EnvironmentVariables`, `ProcessGracePeriod`, `MaxMessageSize`,
`StandardError` and `ConfigurationDirectory`. Everything
asked of the binary is `ClaudeAgentSessionOptions`, and `QueryAsync` and
`StartSessionAsync` take one per call, so one registered client serves
calls that differ:

    var asked = new ClaudeAgentSessionOptions
    {
        Model = job.Model,
        WorkingDirectory = job.Directory,
        SystemPrompt = AgentSystemPrompt.FromText(job.Prompt),
        Tools = ["Read", "Write"],
        OutputSchema = schema,
        MaxTurns = 20,
        MaxBudgetUsd = 1.50m,
        Effort = Effort.High,
        PermissionCallback = guard.DecideAsync,
    };
    asked.AddHook(HookEvent.PreToolUse, null, guard.BeforeToolAsync);

    await foreach (var message in client.QueryAsync(prompt, asked, cancellationToken)) { ... }

`options.Defaults` on the client's options is one of the same type, the
part that binds from configuration, and a call is layered over it member
by member as the call is made. A scalar or a selection: the call's value
wins, null takes the default, and a `false` on the call beats a `true`
default. Anything that accumulates: the call adds to the defaults, and a
default always holds. Three accumulate:

- hooks: the defaults' run and the call's run too, the defaults' first;
- `McpServers` and `Agents`, by key: the call's entries are added, and
  one under a default's name replaces that entry;
- `AllowedTools`, `DisallowedTools`, `AdditionalDirectories`,
  `PluginDirectories` and `Betas`: the defaults' entries, then the call's
  that are not among them.

A call cannot drop what a default accumulated; a host that needs that
makes a second client. `Tools` and `SettingSources` are selections, so
`[]` is a value, none. `PermissionCallback` with `PermissionPromptTool`,
and `Skills` with `AllSkills`, are one selection each: a call that sets
either side replaces the default's pair, and a call that sets neither
keeps it. Which session it is — `SessionId`, `ResumeSessionId`,
`ContinueLatest`, `Fork`, `ResumeAtMessageId`, `DroppedTurnPromptId` —
comes from the call alone, and a default that names one is refused when
the client is made. The options are copied as the call is made.

No tools is `Tools = []`. It goes out as `--tools ""`, and the binary
started that way lists no tool of its own in its init frame, only what
an MCP server brought. `Tools = null` takes the default, and with no
default the binary's own set; `Tools = ["Read"]` is that one tool.
`AllowedTools` and `DisallowedTools` are permission rules over whatever
tools there are, not the tool list.

`Skills` names skills the binary may run without asking, each going out
among the allow rules as `Skill(<name>)`, the name written as the binary
writes a rule's content: each `\` doubled and a `\` before each `(` and
`)`, which the binary undoes as it reads the rule back. `AllSkills`
allows the `Skill` tool whole. Neither names a setting source: with
`SettingSources` null no `--setting-sources` goes out, and the binary
reads the user, project and local settings and finds the skills in the
configuration directory's `skills` and the project's `.claude/skills`. A
name the binary would not read back out of its rule as the one skill is
an `ArgumentException`: blank, holding a NUL or an unpaired surrogate,
splitting the rule at a comma or space after a `)`, `*`, a leading `/`,
a `:*` or ` *` ending, or whitespace at an end.

## structured output

`OutputSchema` is a JSON schema the answer must satisfy, handed in as
it stands. `SetOutputSchema<T>()` takes one from a type through the
platform's `JsonSchemaExporter`, off the contract the serializer options
hold for it or off a source-generated `JsonTypeInfo<T>`, and the answer
reads back as the type through the same contract:

    asked.SetOutputSchema<Verdict>();
    var trace = await client.QueryAsync(prompt, asked, cancellationToken).WaitForCompletionAsync();
    var verdict = trace.GetStructuredOutput<Verdict>();

The binary makes the schema the input schema of a `StructuredOutput`
tool, and the API takes an object there and nothing else: a root of
`["object", "null"]`, which the exporter writes for any class, is refused
with an error result. So the root is `"object"`, for a class, a struct
and a `Nullable<T>` of one; where the root was nullable, a reference
back to it, `{"$ref": "#"}`, is made `{"anyOf": [{"$ref": "#"},
{"type": "null"}]}` as the root is reached, so a list item or a
dictionary value the type reads as null is one the schema admits. A
member's `[Description]`, on the property or the constructor parameter,
or else its type's, is its `description`; `[MaxLength]`, `[StringLength]`
and `[Length]` are a string's `maxLength` and an array's `maxItems`, the
least of them when there are several, and on a byte array, written as
base64, they count bytes, so `maxLength` is the base64 length of that
many; `[ItemMaxLength]`, this library's own, on a list of strings is
each item's `maxLength`, counted the same way on a list of byte arrays,
and writes nothing on any other member; only the schema reads it; an
attribute on the member an override overrides is the override's; every
object the contract reads member by member and that
keeps no extension data has `"additionalProperties": false`. The
exporter writes the rest itself: an enum's names where the contract
writes it as strings, and `required` for the `required` members, those
marked `[JsonRequired]` and the constructor parameters without a
default. A `JsonSchemaExporterOptions` handed in applies its settings,
and its `TransformSchemaNode` runs over each node after the library's
work. A schema whose root is then not an object — a number's, a
string's, a list's — or has `anyOf`, `oneOf` or `allOf` at its root, as
a polymorphic type's has, which the binary's own note says the API does
not accept, is an `ArgumentException`, before any process starts.

An `OutputSchema` handed in is held to the same rule, in the same words
but for what names it: a root whose type is not `"object"`, a nullable
`["object", "null"]` among them, or that has `anyOf`, `oneOf` or `allOf`
at it, is an `ArgumentException` naming `OutputSchema` — on the defaults
when the client is made, on a call as it is made — before any process
starts. The root is read off the element as it is held and nothing is
parsed again, so a schema read with comments or trailing commas allowed,
or nested deeper than a reader's default of 64, is judged by its root
like any other, and a root that repeats a key by its last `type`, as the
binary's `JSON.parse` reads it, and by any combinator it names. JSON
`null` is no schema, and nothing goes out.

The trace names the type a schema was taken from: `AnswerType` is `T`
after `SetOutputSchema<T>()`, and null for a schema handed in or none.
Through the layering it follows the schema that wins.

## evidence as a type

`AskAsync<TEvidence, TAnswer>` is one question over typed evidence, and
hands back the run `QueryAsync` does. The answer's schema is `TAnswer`'s,
taken as `SetOutputSchema<T>()` takes it, and the evidence is written
into the prompt off the same contract:

    var run = client.AskAsync<SiteEvidence, Verdict>(task, evidence, asked, grammar, schemaOptions, cancellationToken);
    var trace = await run.WaitForCompletionAsync(cancellationToken);
    var verdict = trace.GetStructuredOutput<Verdict>(grammar);

The prompt is the task as given, then the evidence's schema, then the
evidence, a blank line apart:

    <task>

    <evidence_schema>
    {schema, indented}
    </evidence_schema>

    <evidence>
    {evidence, indented}
    </evidence>

Both are JSON written through the serializer options handed in, or
`JsonSerializerOptions.Default`. They are indented two spaces with line
feeds. A character is escaped only where JSON needs it: a quotation
mark, a backslash, a control character or a lone surrogate. So `ä`, `&`,
`<`, a no-break space and an emoji reach the model as they are. The evidence's schema comes from
the same exporter as the answer's, with the `JsonSchemaExporterOptions`
applied to both. A member's `[Description]` is therefore in what the
model reads. The evidence's root is any type the contract writes, a
list included. The system prompt stays the caller's. A twin overload
takes the two `JsonTypeInfo` a source generator wrote. The trace's
`EvidenceType` is `TEvidence` and its `AnswerType` is `TAnswer`.

These are thrown at the call, before any process starts:

- An `ArgumentNullException` for no evidence or no contract.
- An `ArgumentException` for a blank task.
- An `ArgumentException` naming `exporterOptions` when its transform
  makes the evidence schema's root null.
- An `ArgumentException` naming `sessionOptions` when the call or the
  client's defaults already carry an `OutputSchema`. JSON `null` is no
  schema and is not refused.
- What System.Text.Json throws taking either schema or writing the
  evidence, as it threw it.

A session takes no evidence.

## a live session

    await using var session = await client.StartSessionAsync(cancellationToken);

    await foreach (var message in session.SendAsync("Say: one", cancellationToken))
    {
        ...                                         // ends after the turn's ResultMessage
    }

    await session.SetModelAsync("sonnet", cancellationToken);
    await session.SetPermissionModeAsync(PermissionMode.AcceptEdits, cancellationToken);
    var usage = await session.GetContextUsageAsync(cancellationToken);
    var servers = await session.GetMcpStatusAsync(cancellationToken);
    await session.InterruptAsync(cancellationToken);

`SendAsync` hands back the prompt's run at once. One prompt is in
flight at a time: the next waits in the session, in call order, until
the run before it has ended, and its token takes it out while it waits.
The binary names each prompt in `CommandLifecycleMessage` frames, and a
run holds its prompt's lifecycle frames and every message from the one
that says the prompt started to the turn's result, the frame that says
the prompt is done placed before the result, which is last.
`ReceiveMessagesAsync` yields what no run reads, as it comes: a turn the
binary opens by itself, the frames a verb brings between turns, what
the binary writes between a prompt's `queued` and its `started`. 2.1.276
is the first version recorded naming prompts; on a binary that does not
name them, a run takes no turn and no prompt after the first is
written. A loop over a run left before its stream ended interrupts the
turn, or withdraws a prompt still waiting, and is left once the run has
ended; the session serves its next prompt. Disposing the session closes
it: stdin is closed, the process awaited and ended when it stays, and
what it wrote until then routed as any output is; a run whose result
has not come by then fails for the close. A process
that exits non-zero at a close its caller began is no failure of the
session's: `ReceiveMessagesAsync` ends as it would have, and a prompt
sent after the close reports the closed session, as the verbs do.

`session.InterruptAsync()` is written whenever it is called, a turn
under way or not, and returns once the binary has answered it; a turn
under way ends with its result, aborted. A run's `InterruptAsync` is
written only while that run's turn is under way: once the run's result
is in, it sends nothing.
Sent between turns while a background shell a turn started was still
running, the session's interrupt was answered and stopped nothing on
2.1.283: the shell ran to its end, and the binary then opened a turn of
its own on the shell's notification, which `ReceiveMessagesAsync`
yields.

`RewindFilesAsync`, `StopTaskAsync`, `ReconnectMcpServerAsync`
and `ToggleMcpServerAsync` are the rest of the verbs, and `CloseAsync`
closes without disposing; `session.Id` is what the binary calls the
session and `session.ServerInfo` what its first frame said. `ForkAsync`
and
`ResumeAsync` start a new session from this one's id, asked what this
one was. Which session a start continues is on the call's options:

    var resumed = await client.StartSessionAsync(new ClaudeAgentSessionOptions { ResumeSessionId = id }, cancellationToken);
    var latest = client.QueryAsync("And again?", new ClaudeAgentSessionOptions { ContinueLatest = true }, cancellationToken);

## failure

Everything throws. `ClaudeAgentException` is what the binary, the
process around it or a session refused, and `ErrorCode` names the kind:
`CliNotFound`, `WorkingDirectoryNotFound`, `Connection`, `Process`,
`Control`, `Parse`, `CannotRewind`, `SessionNotFound`, `SessionExists`,
`Fork`. `ExitCode` is the binary's when it
exited, `Messages` what the failed run or stream had handed out, and
`Result` the last of them when it is the result the binary wrote.
A run throws where it stands, from its stream and from its completion.
A query that has the result that ends its turn completes with its trace
however the process exits after it; one whose output ends before that
result fails. A reply that carries a verdict reports
it: `RewindFilesAsync` throws `CannotRewind` when the binary answers
`canRewind:false`. An option the binary would refuse, and a
configuration directory that is the user's own or the one the login is
read from, is an
`ArgumentException`: the client's options and its defaults when the
client is made, and the call's options over them as the call is made,
before any process starts. Under a container the first is checked at
host start and arrives as an `OptionsValidationException` with the same
message. Reading structured output as a type, and taking a schema from
one, throw what System.Text.Json throws, as it threw it; a type whose
schema is not an object at its root, or has `anyOf`, `oneOf` or `allOf`
there as a polymorphic type's has, is an `ArgumentException`, and so is
an `OutputSchema` handed in with such a root.

## hooks and permissions

Both are delegates on `ClaudeAgentSessionOptions`, a call's or the
defaults':

    asked.AddHook(HookEvent.PreToolUse, "Read", (input, cancellationToken) =>
        Task.FromResult(new HookOutput { Decision = PermissionDecision.Allow, DecisionReason = "reads are fine" }));

    asked.PermissionCallback = (request, cancellationToken) =>
        Task.FromResult(request.ToolCall.Name == "Write"
            ? ToolPermissionResult.Deny("not from here")
            : ToolPermissionResult.Allow());

The matcher is the binary's — a tool name, a pattern, or null for every
call. `HookEvent` names the events the binary declares, `PreToolUse`,
`PostToolUse`, `PostToolUseFailure`, `UserPromptSubmit`, `Stop`,
`SubagentStart`, `SubagentStop`, `PreCompact`, `Notification` and
`PermissionRequest` among them, and takes an event it does not name as
it stands, which is registered and called like any other. The input
arrives as its event's own type over `HookInput` — a `PreToolUse` is a
`PreToolUseHookInput`, with the tool call on it — and an event with no
type, or an input missing a member its event's type requires, is an
`UnknownHookInput`: its event's name, the members every input shares,
and the input as the binary wrote it, `Raw`. A `Stop` or `SubagentStop`
input lists the work the binary still runs in the background, one type
a kind: a `ShellStopHookBackgroundTask` with its command, a
`SubagentStopHookBackgroundTask` with its agent type, and any other
kind an `UnknownStopHookBackgroundTask`, its label and the entry as the
binary wrote it. The permission
callback is asked when no rule and no hook has answered. A session with
one logs a warning as it starts when its options leave the callback
calls it will not be asked about: under `PermissionMode.DontAsk` the
binary denies what it would ask, and under `BypassPermissions`, or for a
tool an `AllowedTools` rule names whole (`Skill`, under `AllSkills`), it
allows the calls no ask rule or safety check holds back.
`request.Suggestions` is what the binary says would stop it asking, and
`Allow(updatedPermissions: request.Suggestions)` takes it up on that.
A suggestion is one type per kind of change over `PermissionUpdate`,
and a caller writes one with `PermissionUpdate.AddRules`,
`ReplaceRules`, `RemoveRules`, `SetMode`, `AddDirectories` or
`RemoveDirectories`, each naming last where the binary keeps the
change; one the library cannot read is an `UnknownPermissionUpdate`,
which names no destination and goes back as it came.
A callback that is still running when its request is cancelled sees its
token cancelled.

## configuration

The client options bind from `IConfiguration`:

    builder.Services.AddClaudeAgentClient(builder.Configuration.GetSection("ClaudeAgent"));

    {
      "ClaudeAgent": {
        "ProcessGracePeriod": "00:00:03",
        "Defaults": {
          "SystemPrompt": { "Text": "You are Mint." },
          "Model": "haiku",
          "PermissionMode": "acceptEdits",
          "McpServers": {
            "hive": { "Type": "stdio", "Command": "hive-server", "Args": [ "--stdio" ] }
          }
        }
      }
    }

A closed set takes its wire name (`acceptEdits`, `high`, `project`), and
one this library does not name binds as it stands and is sent as it
stands. A value that cannot be mapped — an unknown MCP kind, an empty
mode — fails the options with the configuration key that carried it, at
host start.
A delegate, `StandardError` and an in-process MCP server have no
configuration shape and stay code-only, and which session a call
is has no default to bind.
`Claude.Agent.Tests/fixtures/appsettings.json` sets every kind at once.

## usage

`Usage`, on a `ResultMessage` the turn's sum and on an
`AssistantMessage` the count as the message started, serializes with
`JsonSerializer.Serialize` and reads back with
`JsonSerializer.Deserialize<Usage>` under default options, or options
that refuse a member the contract does not name, the same member for
member, so a caller can keep what it was handed. It is
written under the names the binary writes the usage with
(`input_tokens`, `cache_creation`, `inference_geo`, `iterations`, ...),
an iteration's kind and a fallback credit's outcome under `type` as the
binary writes them, and read as the family's and not as a member of the
kind, a service tier, a speed and a reason as their names,
and the unknown iteration or outcome with the JSON it was read from
under `raw`. The default of a service tier, a speed or a reason, which
names nothing, is written as the empty string and reads back as the
default, and a value the empty string names reads back as the default
too: a string cannot keep the two apart. No other model reads back this
way.

## the binary's tools

A tool call the binary makes is one object, `ToolCall`: its name, its
id, a typed `Input`, and once the answer comes a typed `Output`; its
`Status` is started until then, and answered, failed or unanswered
after. It is what a hook and the permission callback are asked about,
what a `ToolCallMessage` carries as the run passes it, and what
`trace.ToolCalls` holds when the run has ended:

    asked.AddHook(HookEvent.PreToolUse, "Bash", (input, cancellationToken) =>
        Task.FromResult(input is PreToolUseHookInput { ToolCall.Input: BashToolInput bash }
            && bash.Command.StartsWith("rm ", StringComparison.Ordinal)
                ? new HookOutput { Decision = PermissionDecision.Deny, DecisionReason = "no removals" }
                : new HookOutput()));

The same typed input is on the `ToolUseBlock` in the assistant message
it was written in, and the typed output on the `UserMessage` that
answers it. A tool has a type when the reference SDK declares it, a
recording founds its name, and the declaration gives what it carries a
shape; an MCP server's tool, whose declaration leaves what it sends and
what it hands back open, has none. A string a tool hands back is a
string, whatever it holds: `RemoteTrigger` sends a service a body the
declaration leaves open, which a create, an update and a
create_webhook_trigger input carry raw as `Body`, a JSON object the
binary refuses those actions without, and a run input carries as `Body`
when the call sends one, null when it sends none; it hands back the
service's answer as JSON text, which
`RemoteTriggerToolOutput.Json` holds as the string it came as, beside
the HTTP status and the binary's summary. A tool the library has no
type for, and a call whose JSON does not fit the type, leave the view
null beside `RawInput`, which is always there, and `RawOutput`, there
when the binary wrote an output beside the call's result. An output the binary writes in kinds is
one type a kind under an abstract one, each with only its own members:
a Read's by what it read, a Grep's by its mode, a Write's
`WriteCreateToolOutput` for a file made and `WriteUpdateToolOutput`
for one replaced; a kind with no type leaves the view null too. A
caller builds one of these
inputs to change a call: `HookOutput.UpdatedInput` and
`ToolPermissionResult.Allow(updatedInput:)` take it;
`HookOutput.RawUpdatedInput` and `Allow(rawUpdatedInput:)` carry the
JSON as the caller writes it.

## in-process tools

A method carrying `[McpTool]` is a tool the binary can call, served in
this process over the control channel:

    internal sealed class Calc
    {
        [McpTool("add", "Add two numbers")]
        public static double Add(double a, double b) => a + b;
    }

    builder.Services.AddClaudeTools<Calc>("calc");
    builder.Services.AddClaudeAgentClient(options => options.Defaults.AllowedTools.Add("mcp__calc__add"));

`AddClaudeTools<T>` registers `T` and puts the server on the defaults;
without a container it is
`McpServers["calc"] = McpServerConfiguration.InProcess(new Calc())`, on
the defaults or on a call. It stays for every call: a call that names
servers of its own has them beside it.
On the wire a tool is named `mcp__<server>__<tool>`. Arguments bind
against the schema the server advertises — primitives, enums by name,
arrays, nested records. A missing or mistyped argument comes back as an
error result the model reads, and so does an exception the method
throws; neither is a protocol error. A method that returns `ToolResult`
says text, error or image itself. A method that takes a
`CancellationToken` sees it cancelled when the binary gives the call up,
as it does on an interrupt, and the call is not answered: the binary has
already answered it itself.

## sessions on disk

A client's sessions run in a configuration directory of its own:
`ClaudeAgentClientOptions.ConfigurationDirectory`, or
`~/.config/claude-agent` when that is null — `.config/claude-agent`
under the user's profile directory, on every platform. The client
resolves it to a full path as it is constructed, and refuses it when it
is the user's own directory: `CLAUDE_CONFIG_DIR` as the host inherited
it, else `~/.claude`. An inherited `CLAUDE_CONFIG_DIR` that is the
configuration directory itself is an outer SDK session's, handed down
to a host that session started, and not the user's: the user's
directory is then the one `CLAUDE_SECURESTORAGE_CONFIG_DIR` as the host
inherited it names, else `~/.claude`. It is refused too when it is the
directory the login is read from, below. Directories are compared as they
resolve on disk, every symbolic link in either followed, an ancestor's
included. Before the first process the transport creates it, with mode
0700 off Windows, and a directory it cannot create is `Connection`.
Every process
it starts, the version probe included, is told `CLAUDE_CONFIG_DIR`, the
configuration directory, over whatever the host inherited, and
`EnvironmentVariables` naming it or `CLAUDE_SECURESTORAGE_CONFIG_DIR` is
refused.

The login is the user's own, read where the user's own `claude` reads
it, and nothing of it is copied. The child is told
`CLAUDE_SECURESTORAGE_CONFIG_DIR`: the value the host inherited, as it
stands, set to nothing or not; else the inherited `CLAUDE_CONFIG_DIR` as
it stands, when that is the user's directory; else the variable set to
nothing. The binary reads the login from the directory that value names,
`~/.claude` when it is set to nothing, and names the credential store by
the value: set to nothing, as the user's own `claude` names it with
neither variable set, and otherwise by a hash of the text, as the user's
own `claude` names it by a hash of its `CLAUDE_CONFIG_DIR`. A refresh of
the login writes it back there.

Probed on 2.1.283 with haiku, the configuration directory set and
`CLAUDE_SECURESTORAGE_CONFIG_DIR` set to nothing: the binary read the
login from `~/.claude/.credentials.json`, answered, and left the file as
it was. Probed on 2.1.283 with haiku and the configuration directory set: no
user `CLAUDE.md`, user settings, user skill or user-scope MCP server
(`.claude.json`) reached the session; the built-in skills and agents,
and the account's claude.ai connectors and skills, did. The binary
wrote `.claude.json`, `backups/`, `projects/`, `sessions/`, `state/`,
`plugins/synced` and `skills/synced` into the configuration directory,
and its MCP logs under `~/.cache/claude-cli-nodejs`. A consumer that
wants settings, skills, plugins or memory in its sessions puts them in
the configuration directory.

The transcripts are the binary's own JSONL, under
`<configuration directory>/projects/<cwd, sanitised>/<session id>.jsonl`,
and resume, continue and fork are the binary's own, from there.
`ClaudeSessionClient` reads and edits them — from the container, or
`client.GetSessionClient()`:

    var sessions = await sessionClient.GetSessionsAsync(
        new SessionQueryOptions { WorkingDirectory = Environment.CurrentDirectory, Limit = 1 }, cancellationToken);   // newest first
    var last = sessions[0];                                                                                         // SessionInfo
    var messages = await sessionClient.GetMessagesAsync(last.SessionId, cancellationToken: cancellationToken);

    await sessionClient.RenameSessionAsync(last.SessionId, "the mint", cancellationToken: cancellationToken);
    var forked = await sessionClient.ForkSessionAsync(last.SessionId, cancellationToken: cancellationToken);           // a new session id

`ForkSessionAsync` copies a session's conversation, or its part up to
`upToMessageId`, into a new transcript beside it under a new session id,
as the binary's `/branch` writes one. Each copy keeps the uuid, the
timestamp and every key of the line it copies but `sessionKind`, and
takes the new session id, the copy before it as its parent,
`isSidechain` false and `forkedFrom`, which names the source session
and the message. After the copies come the source's `atis-latch` line
under the new id, when the source carries one, and then the two lines
the binary writes for a title,
`{"type":"custom-title","customTitle":…,"sessionId":…}` and
`{"type":"agent-name","agentName":…,"sessionId":…}`. The title is
the one given, its runs of whitespace made one space and its ends
trimmed; without one it is the fork's first prompt, by the rule a
listing's first prompt follows, with its runs of whitespace made one
space and cut at 100 characters, or `Branched conversation` when there is
none, and then ` (Branch)`, or ` (Branch 2)`, ` (Branch 3)` and on when
a session of the project directory already carries that title. A
session with nothing to copy, or with no message `upToMessageId`, is a
`ClaudeAgentException` with `Fork`.

`GetSessionsAsync` lists a directory's sessions (and its git worktrees',
unless `IncludeWorktrees` is off), or every project's when no directory
is named. `GetSessionAsync`, `GetSubagentsAsync`,
`GetSubagentMessagesAsync`, `TagSessionAsync` and `DeleteSessionAsync`
are the rest of what it does in the configuration directory. A read of
a session that is not there answers null, or an empty list. A rename, a
tag, a delete or a fork of one is a `ClaudeAgentException` with
`SessionNotFound`, naming where it was looked for. `GetSessionAsync`
also answers null for a session the listing leaves out, one a line at
its transcript's head marks a sidechain and one with nothing to
summarise it by — no title, no last prompt or summary line, no first
prompt — which a rename, a tag and a delete find all the same.

A working directory handed to the session client names its project
directory as the binary names its own, by realpath: every symbolic link
in it followed, an ancestor's included, up to 40 of them. A path
realpath refuses — a component that is not there, more links than that
— is taken as spelled. A line is read as the binary's `JSON.parse`
reads it: half a surrogate pair escaped without its other half reads as
U+FFFD, and a line nested up to 1000 deep is read; one that still does
not parse is passed over. A title or a tag handed in with such a half
is written escaped, as `JSON.stringify` writes it, and reads back so.

`ImportSessionAsync` is the one crossing, one way: it copies one session
from the user's directory, as resolved above, into the configuration
directory, under the project directory of the same name, so a session started with its id in
`ResumeSessionId` takes it up. The transcript is copied byte for byte,
and with `includeSubagents`, on by default, every file under the
session's `subagents/` directory; off Windows the copies are made 0600
and the directories made for them, from the configuration directory
down, 0700. The user's directory is only
read. A session the user's directory does not hold is `SessionNotFound`,
and one any of whose files is already in the configuration directory is
`SessionExists`, with nothing written. A copy that fails or is cancelled
part-way removes, before it throws, the files it made, the one it was
writing included, and the directories it made that nothing else has
been put in since, so the import can be asked again.

## the process

The binary is a child process: `claude --output-format stream-json
--input-format stream-json ...`, stdin and stdout piped, stderr piped
only when `options.StandardError` is set. A `WorkingDirectory` that does
not exist is `WorkingDirectoryNotFound` before the binary is looked
for. `ExecutablePath` names the binary; otherwise it is the first
`claude` on `PATH`, else the native installer's launcher
`~/.local/bin/claude`, else the local npm install's `local/claude` under
the user's directory (as sessions on disk resolves it): where the
binary's own installers put it and its own doctor looks. On Windows it
is `claude.exe`, on `PATH` or in `~/.local/bin`. A global npm install is
found on `PATH` or not at all. When no place holds it the start is
`CliNotFound`, naming where it looked. A `.bat` or `.cmd` on Windows is
`Connection`: cmd.exe would run it.

The binary writes each message to stdout as one `JSON.stringify`d
object with `\n` after it, and stdout is cut into lines there and
nowhere else, decoded as UTF-8 as the bytes arrive; a line is read as
soon as its `\n` is. Every line goes to the codec as it was written;
one that does not decode is rejected with a warning where it stands,
and the stream goes on. Text no `\n` ended when stdout closes is one
more line, the same. A line longer than `MaxMessageSize` ends the
session as `Process`, whether or not its `\n` has come. stderr, when
piped, is free text: it goes to `StandardError` as it arrives, decoded
as UTF-8 with no character broken between reads, neither cut into lines
nor bounded, and it is read to its end whatever the writer does; a
write that throws is logged at debug.

Close is stdin EOF, a grace, SIGTERM, the same grace, SIGKILL of the
direct child; one that has to signal it is logged as a warning, and
stderr, when piped, is read to its end within one more grace.
`options.ProcessGracePeriod` is that grace, five seconds
by default; the version probe (`claude -v` before every start, which
logs a warning for a binary before 2.1.276) tears down by the same
value. A host that exits without closing a session — returns from
`Main`, dies on an unhandled exception, is stopped by SIGTERM through
the generic host's lifetime — still runs that step on every live
process from the exit handlers, so nothing is orphaned. The child's
environment is the host's without `CLAUDECODE`, plus
`CLAUDE_CODE_ENTRYPOINT=sdk-dotnet`, and `options.EnvironmentVariables`
over that. Over all of it: `CLAUDE_AGENT_SDK_VERSION`, the assembly's
own; `CLAUDE_CONFIG_DIR` and `CLAUDE_SECURESTORAGE_CONFIG_DIR`, as
sessions on disk says; `PWD`, the working directory when one is named;
`CLAUDE_CODE_ENABLE_SDK_FILE_CHECKPOINTING=true` under
`EnableFileCheckpointing`. When a W3C `Activity` is current its id goes
in `TRACEPARENT` and its state in `TRACESTATE`, and a `TRACESTATE` the
host inherited goes; either is left alone when the options name it.

## fixtures

`Claude.Agent.Tests/fixtures/` is the binary's stdout, the SDK's stdin
and the argv, recorded from the real binary by `Claude.Agent.Recorder`
through the library's own transport and scrubbed of the machine and
the account that recorded them; its README says what each fixture
holds, what the binary never emitted and how that was established.
`fixtures/transcripts/` is the transcript directory those runs left
behind, the recorder keeping a run's transcripts where its fixture asks,
scrubbed the same way. The tests replay the frames through a
socket transport, and a `claude` that only leaves a mark sits first on
`PATH` while they run, so the test project never starts the real one.
`fake/claude` and `mcp/echo-server` in the test output are two C#
apphosts: a binary that misbehaves on cue, for the process adapter's
edges, and the stdio MCP server the binary was recorded talking to.

## known wrong

- the Windows paths — Kill in place of SIGTERM on close, the append
  without O_APPEND — are written and unrun; there is no Windows machine
  here.
- `claude` must already be installed and logged in. there is no API key
  path to fall back to.
- net10.0 only, and not on nuget.
- `ErrorCode` is a string and the library exports no constants for it.
- `ReconnectMcpServerAsync` and `ToggleMcpServerAsync` on an in-process
  server: before the server is connected the binary answers `Server not
  found`; once it is, a reconnect and a toggle on are refused with `SDK
  servers should be handled in print.ts`, and a toggle off succeeds and
  leaves it connected. On a stdio server they succeed. All are recorded.
- the transcript directory's names are sanitised over UTF-16 code units,
  as the binary's JavaScript does. NFC normalisation of paths is a no-op
  in this tree (InvariantGlobalization), and a test pins that.
- the configuration directory and its login were probed on Linux alone;
  there is no macOS machine here. The name of the credential store the
  binary reads on macOS, the keychain's, is read off its JavaScript.
- a host that handles no signal dies where it stands on SIGTERM, and the
  exit handlers that reap `claude` die with it.

## developing

    dotnet test

`dotnet run --project Claude.Agent.Recorder -- OUT CWD [fixture ...]`
records fixtures, and `dotnet run --project samples/Mint` runs the
sample; both start the real binary and spend real usage on the account
it is logged into. `samples/Mint -- --resume` continues the newest
session of the current directory. The test project does neither.
