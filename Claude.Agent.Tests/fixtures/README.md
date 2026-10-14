# fixtures

The binary's stdout, the SDK's stdin and the argv, recorded once per
fixture from `claude` with the transport tee'd by the recorder
(`../../Claude.Agent.Recorder`), under the neutral cwd
`/tmp/claude-agent-fixtures/cwd` (`notes.txt`: two lines; `echo-server`:
the stdio MCP server the test build puts at `mcp/echo-server`). The
tests replay them through the socket adapter and never start `claude`.

    dotnet run --project Claude.Agent.Recorder -- [--executable PATH] OUT CWD [fixture ...]

Forty-three fixtures, four binary versions: suggestions and agents are
2.1.274; schema-type, schema-nullable and background-interrupt are
2.1.283; ask is 2.1.295; the other thirty-seven are 2.1.276. The init frame's
claude_code_version says which. mcp-reconnect, mcp-toggle and stop-task
never prompt and so have no init frame; the first two name 2.1.276 in
the MCP clientInfo, and stop-task names no version and was recorded
with them. Every initialize reply lists fifty-six commands but the three
2.1.283 ones and ask's, which list fifty-seven. The recorder wrote every fixture,
and `Architecture/TreeTests.cs` holds that it names every fixture on
disk.

The recorder names haiku for every session it starts, and every fixture
is haiku; each replay test gives the options its fixture was recorded
with, but schema-nullable's, whose nullable root the library refuses
before any process starts: its replay asks for WordReport's schema as
the type gives it, and since the schema goes out on argv alone, what it
writes is the recorded stdin line for line. The recorder's sessions
run in the client's default configuration directory,
`~/.config/claude-agent`, on the recording account's login, and the
transcripts it keeps are copied from there. Recording one again changes its ids, its texts and its frames,
and the tests that pin them with it.

Thirteen were recorded through a session that writes each prompt with a
uuid of its own (background-interrupt, cancel, client, compact,
continuation, conversation-reset, mcp-cancel, payloads, rewind-files,
sdk-mcp-client, stdio-mcp, task and task-background): their stdin
carries it on every prompt, and their stdout names it wherever the
binary does (command_lifecycle, the stamps of the prompt's turn, a
replayed user message). 2.1.276 is the first
version recorded naming prompts; on a binary that does not name them, a
session's run takes no turn and no prompt after the first is written.
Such a recording is played by what its lines name (`Replay/Script.cs`),
as one sequence: a line waits for every prompt named at or before it,
so the rest of a turn the binary opens by itself waits for a prompt
queued in it (continuation:21, :53), and only the lines ahead of every
prompt's name wait for none. A control request of the binary waits for
the lines the client wrote ahead of its answer in the recorded stdin,
and may be moved past answers to get there (sdk-mcp-client plays the
initialize reply before the in-process server's initialize
mcp_message). What a client's control request made the binary do waits
for that request: its cancel of its own pending request, when the
interrupt's answer comes right after it (cancel:9, :23), and the frames
between turns right before an answer (task:17-19, stop_task's); a frame
right after an answer is the answered request's (client:12, the status
set_permission_mode announces) and waits for no later one. Every
recorded prompt uuid in a played line becomes the one the client wrote
for the prompt at that place.

Record from a login shell. Inside a Claude Code session the child
inherits that session's `ANTHROPIC_DEFAULT_*_MODEL` overrides, and
`--model haiku` can then name a model the binary refuses.

The recorder scrubs each file it writes (`FixtureScrub.cs`): the recording
home to `/home/user`; every address, wherever it stands, to
`user@example.com`; and the values under three keys, spaced or not and
however deep a string nests them — `email` to that address, which the
initialize reply's account block and the session_context attachment inside
a transcript both carry, `organization` to `user@example.com's
Organization`, and `atis` to the empty string, which the atis-latch line
of the kept transcripts carries. `Architecture/TreeTests.cs`
holds the scrub against every spelling and against every fixture, which it
leaves as it is.

Replay proves a fixture's stdin line for line after request ids and the
prompts' uuids are masked (`Fixture.Anonymised`; a prompt's uuid becomes
its place among the prompts, so the member must be where the recording
has it): the fixture server compares what the client wrote with the
recording as it closes, and the run ends by failing
on a fixture no replay wrote the whole stdin of
(`Architecture/Tripwire.cs`). A replay that registers other hooks,
answers another way than the recording, or drives it in another form
says `stdinDiffers` at the call.
fork-parent, fork and resume are the standing exception: they
were recorded through a one-shot query whose user frame wrote
`session_id` before the message, and the tests that replay them
(`Sessions/VerbReplayTests.cs`) drive a client session, which writes it
after `parent_tool_use_id` and a uuid after it; their prompts carry no
uuid and no line names one, so the session's run takes no turn and the
turn is read from what no run reads. Those replays
assert argv and session ids;
the two stdin lines of each are pinned through the codec instead, in
`Wire/FrameCodecEncodeTests.cs`.

Every argv is checked against `CommandLine.Build`: thirty-seven in
`Wire/CommandLineFixtureTests.cs`, fork-parent in
`Sessions/VerbReplayTests.cs`, stdio-mcp and sdk-mcp-client in
`Mcp/StdioMcpReplayTests.cs`, suggestions in
`Sessions/SuggestionsReplayTests.cs`, agents in
`Sessions/AgentsReplayTests.cs`, ask in `Sessions/AskTests.cs`, rebuilt
from the options the ask made.

## what each fixture holds

    trivial              one turn: init, rate_limit_event, the assistant's thinking frame and its
                         text, result
    tool-use             a Read: assistant tool_use, user tool_result, the answer
    hooks                SDK hooks: UserPromptSubmit, PreToolUse, PostToolUse, Stop through hook_callback;
                         can_use_tool registered and never asked (the Read was allowed)
    streaming            --include-partial-messages: every stream_event kind of one turn
    client               a turn on a live session, set_model, set_permission_mode and the
                         system status announcing the mode, get_context_usage,
                         get_mcp_status (the login's claude.ai connector), a second turn,
                         then a third and an interrupt: result error_during_execution,
                         the prompt's lifecycle cancelled
    sdk-mcp              the in-process bridge: four mcp_message requests around one tool call
    can-use-tool         Write not allowed, no hook: the binary asks, with a setMode suggestion
    mcp-reconnect        reconnect on a connected in-process server: refused, "SDK servers
                         should be handled in print.ts"; mcp_status still lists it connected
    mcp-toggle           toggle off on a connected in-process server: success with no body,
                         and mcp_status still lists it connected; toggle on: refused, the
                         same error
    stop-task            stop_task for an id the binary does not know: success
    rewind-files         file checkpointing with replayed user messages, the prompt's
                         carrying the uuid the prompt was written with; canRewind on the reply
    fork-parent          the turn the next two resume
    fork                 --resume --fork-session: a new session id
    resume               --resume: the parent's session id
    task                 a background shell (task_started local_bash, background_tasks_changed),
                         stop_task on it (task_updated killed, task_notification stopped), then
                         a foreground subagent (task_started local_agent, task_updated completed,
                         task_notification completed with usage)
    task-background      a background subagent that reads three times: three task_progress with
                         last_tool_name, the turn's result while it is in flight, then the
                         terminal frames and a continuation turn the binary opens on its own
    hook-events          --include-hook-events with two settings hooks beside an SDK hook:
                         hook_started and hook_response for the settings hooks only
    hooks-more           PostToolUseFailure, SubagentStart, SubagentStop, PermissionRequest
                         through hook_callback; can_use_tool for the Write
    compact              /compact as a prompt: PreCompact (trigger manual), two system status,
                         a fresh init, compact_boundary, two user frames and a result with
                         num_turns 0; then a second turn
    conversation-reset   /clear as a prompt: conversation_reset, a fresh init, a result with
                         num_turns 0, then a second turn
    cancel               an interrupt while a hook_callback is pending, then while a
                         can_use_tool is pending: control_cancel_request for each,
                         permission_denials on the second result, and each prompt's
                         lifecycle cancelled after its result
    denied               Write offered, nothing allowed, no handler: system permission_denied,
                         a tool_result with is_error, permission_denials on the result
    defer                a PreToolUse hook answering defer: deferred_tool_use on the result;
                         the assistant thought first (a thinking block)
    structured           --json-schema with thinking enabled: the StructuredOutput tool_use,
                         structured_output on the result
    schema-type          --json-schema taken by SetOutputSchema<T> from WordReport
                         (../../Claude.Agent.Recorder/WordReport.cs): its root "object",
                         descriptions, maxLength, maxItems, an enum's names, a nested object,
                         additionalProperties false on both; the StructuredOutput tool_use and
                         structured_output on the result
    schema-nullable      schema-type's schema with nothing changed but the root's type,
                         ["object", "null"]: the API refuses the request, and the binary writes
                         the refusal as an assistant message and an error result. the library
                         refuses that root, so the recorder asks for schema-type's and hands
                         the transport the nullable one in its place, past the refusal
    ask                  AskAsync<WordEvidence, WordReport>
                         (../../Claude.Agent.Recorder/WordEvidence.cs): the prompt is the
                         task, the evidence's schema with its descriptions and the evidence,
                         each in its tag, "pöllö" unescaped in the text; --json-schema is
                         schema-type's; the StructuredOutput tool_use and structured_output
                         on the result, read back as WordReport. its initialize reply is the
                         one that carries capabilities (["ui_surface_v1"]) and feedback_mode
                         ({"kind": "post"}), which no earlier version wrote
    web-search           WebSearch runs as a client tool: tool_use and tool_result, not
                         server_tool_use
    stdio-mcp            a real stdio server (../mcp/echo-server, the C# one) under
                         --strict-mcp-config, which keeps the login's own connectors out
                         of the status: mcp_status connected with its serverInfo and one
                         tool, reconnect and toggle answered with success and no body,
                         status disabled after the toggle, then the tool call: the
                         assistant's tool_use and the user's tool_result, the JSON-RPC to
                         the server never crossing the wire
    suggestions          three asks, nothing allowed: a Bash command (addRules for
                         localSettings), a Read outside the cwd (addRules for the session)
                         and a Write outside it (setMode and addDirectories)
    agents               one agent declared on the initialize, naming the echo server in
                         mcpServers, under --strict-mcp-config: the init lists it, the
                         Agent tool starts it (task_started local_agent), a system
                         notification says its server was blocked, its mcp__echo__echo
                         call goes through the session's own echo server all the same,
                         task_notification completed, then a continuation turn: two init
                         frames, two results
    sdk-mcp-client       the in-process bridge on a client session: mcp_status, answered
                         before the in-process server's handshake finished, lists no sdk
                         server, only the login's claude.ai connector, pending;
                         reconnect and toggle answer "Server not found: calc",
                         then the four mcp_message requests around one tool call
    defaults             no --tools: the init lists the thirty-three tools 2.1.276 offers by
                         default, Glob, Grep and TodoWrite not among them, and the login's
                         claude.ai connector pending; one turn, a thinking block, then the text
    tools                nine built-in tools asked and allowed, with PreToolUse, PostToolUse
                         and PostToolUseFailure SDK hooks through hook_callback: Glob, Grep, two
                         Reads in one message (one API message, two assistant frames), Write,
                         Edit, NotebookEdit, WebFetch, Bash, then a Read of a missing file:
                         PostToolUseFailure and a tool_result with is_error. 2.1.276 did not
                         offer TodoWrite, and the model said so and skipped it. the init lists
                         the login's claude.ai connector and its tools beside the nine
    task-tools           the task tools on a background shell: Bash with run_in_background
                         (background_tasks_changed, task_started local_bash), TaskOutput without
                         blocking (retrieval_status not_ready, the task running), TaskStop
                         (background_tasks_changed empty, task_updated killed,
                         task_notification stopped, then its tool_result)
    payloads             a client session with an in-process and a stdio server under
                         --strict-mcp-config: the initialize reply with the in-process server's
                         handshake around it, one turn, get_context_usage, mcp_status with both
                         connected and one tool each (echo source dynamic, calc source sdk),
                         set_permission_mode answered with the mode, then a system status frame
                         carrying it
    continuation         four prompts on a session with Bash allowed, each written once the
                         run before it ended: a background shell and "started"; the binary's
                         own turn when the shell ends, during which "Say: two" is queued and
                         then started after that turn's result; the shell again, asking for
                         `wc -l notes.txt` once it ends; the binary's own turn, into which
                         "Say: four" is folded after its first tool round: one reply,
                         "counted" and "four"
    reads                six Reads with the three tool-event SDK hooks: a text file by offset and
                         limit, whole, and again unchanged (file_unchanged); a PNG (image, and an
                         image block in the tool_result); a PDF whole (pdf, a text and a document
                         block) and by page (parts, a text and the page as a JPEG image block).
                         dot.png and page.pdf are the recorder's, written and deleted around the run
    edits                Glob under a directory; Grep files_with_matches, then count with an input
                         the binary refuses before any hook (the model wrote "i" for "-i") and
                         again with -i, then content with -n, -B and head_limit; Write that
                         creates and Write that updates; Edit with replace_all and Edit finding
                         nothing (tool_use_error, no hook); Bash with a timeout, a grep that finds
                         nothing (returnCodeInterpretation), a command that exits 2
                         (PostToolUseFailure) and one a PreToolUse hook denies
    thinking             --max-thinking-tokens 2048 and no display flag: the thinking block's
                         text is empty and its signature is not, then the text answer
    mcp-cancel           an interrupt while an in-process tool waits: the binary sends
                         notifications/cancelled naming the tools/call's JSON-RPC id and no
                         control_cancel_request, answers the call itself with a rejected
                         tool_result and never waits for the tools/call's reply; result
                         error_during_execution, terminal_reason aborted_tools
    background-stop      a Stop hook while a background Bash runs: background_tasks lists it,
                         type "shell", status "running"
    background-interrupt a background Bash (sleep 30) and the turn's result, then the
                         session's interrupt with no turn under way: its answer, and nothing
                         after it until the shell ends on its own, then the turn the binary
                         opens itself on the shell's notification

Three more things live here beside the fixtures:

    transcripts/         the CLAUDE_CONFIG_DIR the recording runs wrote, scrubbed the same
                         way. projects/-tmp-claude-agent-fixtures-cwd/ holds 23 session
                         transcripts and, under four of them, 4 subagent transcripts with
                         a .meta.json each. 19 are 2.1.261 runs that prompted (fork-parent
                         and resume shared one, conversation-reset had two), every fixture
                         of which has been recorded again since; the twentieth is the
                         discarded denied run below. the recorder kept the last three
                         (`Recording.KeepTranscripts`), on 2.1.276: a run with no fixture
                         whose foreground subagent reads notes.txt, with its subagent's
                         (dd69d1c6-e13f-4d37-8e72-8684030b744f); tagged's, a run with no
                         fixture: a session tagged
                         through the session client and resumed to read a file of forty
                         thousand characters, after which the binary wrote the tag line again
                         itself; and renamed's, a run with no fixture: a turn, then the
                         prompt "/rename kept title", after which the binary wrote a
                         custom-title line and an agent-name line. no other fixture's run
                         kept one. the session tests reach it through
                         Sessions/Transcripts.cs
    appsettings.json     one file exercising every kind the options binder reads, for
                         DependencyInjection/ConfigurationBindingTests.cs
    wire-names.tsv       the list: a set and a name a line, the literals of the declaration
                         files of @anthropic-ai/claude-agent-sdk 0.3.276 (claudeCodeVersion
                         2.1.276) and of the API types it imports from @anthropic-ai/sdk
                         0.126.0, set by set. tool names are on it only as values of other
                         sets: `Bash` under exit-plan-mode-prompt-tool, and the API's server
                         tools (advisor, bash_code_execution, code_execution,
                         text_editor_code_execution, tool_search_tool_bm25,
                         tool_search_tool_regex, web_fetch, web_search) under
                         server-tool-name. a tool's typed view rests on a recording's init
                         list all the same; the founding scan matches a name by its value,
                         so it does not hold that. the next version's
                         declarations diffed against it list what changed on the wire. a
                         parser branch on one of its names is founded
                         (Architecture/TreeTests.cs). one name on it the library does not
                         name back: the api-key-source that is the environment variable a key
                         is set in. nothing that ships here may spell it
                         (TreeTests.NothingThatShipsNamesAnApiKeyItsHeaderOrTheApisHost), so
                         ApiKeySource names the other eight and reads that one as a source it
                         does not name, IsDefined false. no recording carries it — every
                         recorded apiKeySource is `none` — and one made on a machine with the
                         variable set would fail Architecture/RecordedFixtureTests.cs

## what the recordings settled

    a denial          permission_denied arrives after the assistant's tool_use and before the
                      user frame the binary synthesises for it (is_error, non_execution_kind
                      user-rejected); the turn goes on and the result repeats the call in
                      permission_denials with its full tool_input
    a cancel          control_cancel_request lands before the interrupt's own control_response,
                      which is still_queued, empty;
                      the cancelled tool comes back as a rejected tool_result and a
                      "[Request interrupted by user for tool use]" user frame; the result is
                      error_during_execution with terminal_reason aborted_tools. a hook
                      cancelled before it answers leaves permission_denials empty; a
                      can_use_tool cancelled before it answers fills it; the prompt's
                      lifecycle closes cancelled after the result
    /clear            an ordinary user message, not a control request. conversation_reset
                      keeps the old session_id; new_conversation_id is not the session_id the
                      next init announces; the result has num_turns 0, no stop_reason and no
                      terminal_reason, and turns count from zero again. conversation_reset
                      comes after the prompt's started and before its init
    /compact          also a user message. PreCompact fires with trigger manual, between a
                      system status compacting and one with compact_result success, all
                      after the prompt's started and before a fresh init; then
                      compact_boundary (compact_metadata: trigger manual, pre_tokens,
                      post_tokens, cumulative_dropped_tokens, duration_ms;
                      logical_parent_uuid), a synthetic user frame carrying the summary the
                      conversation goes on from, and a replayed one carrying the command's
                      local-command-stdout; the result has num_turns 0. compact_boundary is
                      a CompactBoundaryMessage, its metadata and the branch it hangs off
                      typed on it
    tasks             task_started carries is_backgrounded for a local_bash task and a
                      local_agent task, and subagent_type, spawn_depth and prompt for the
                      local_agent alone, each typed on its kind's message. a
                      local_bash task does not hold the turn open; a foreground local_agent
                      does until its terminal frame. stop_task's three frames come before its
                      reply: background_tasks_changed (empty), task_updated killed,
                      task_notification stopped, and then success with an empty body
                      (task.stdout.jsonl:17-20). a backgrounded subagent's frames stream on
                      the parent's stdout with parent_tool_use_id; when it ends the binary
                      opens a continuation turn of its own: a second init and a second result
                      with no prompt from this side
    hook events       hook_name is "Event:matcher"; hook_response carries outcome, exit_code,
                      output, stdout and stderr. not a recording: samples/Mint, run on the
                      account's own settings with neither --setting-sources nor
                      --include-hook-events, still received hook_started and hook_response
                      for the account's two SessionStart hooks, so the flag is not what
                      gates them for a settings hook; no fixture pins it
    Read              a second Read of a file unchanged since the first answers file_unchanged
                      and no content. a PDF read whole is a pdf output and a document block;
                      read by page it is a parts output whose tool_use_result leaves out the
                      rendered pages the PostToolUse hook's tool_response carries
                      (reads.stdout.jsonl:34-35); the run has the page as the tool_result's
                      image block
    a tool that       a Grep with a key its schema lacks and an Edit that finds nothing are
    fails             refused before any hook: a tool_result with is_error, its content
                      wrapped in <tool_use_error>, and a string tool_use_result. a command
                      that exits non-zero fires PostToolUseFailure. a PreToolUse deny ends the
                      call there, its reason the tool_result, and no hook after it
    a subagent's      the .meta.json the binary writes beside a subagent's transcript, one for
    metadata          each of the four kept; the transcript files never carry it
    a tag             the binary reads the last tag line within its tail window when it appends
                      a session's metadata again, and writes it again when none is in the last
                      32 KB (tagged: the session client's line at 22, the binary's at 35)
    stdio mcp         a stdio server is connected before the first prompt; after toggle off
                      it is still listed, disabled, without serverInfo or tools; the init of
                      the next turn lists its tool again after toggle on. the sdk server's
                      initialize mcp_message is written before the initialize reply, with
                      protocolVersion 2025-11-25; notifications/initialized has no JSON-RPC
                      id and still waits for a control_response

    haiku             every recording that prompts thinks before it answers: an assistant
                      frame with one thinking block whose text is empty and whose
                      signature is not, ahead of the frame that carries the text or the
                      tool_use. schema-nullable is the one exception: no model answered,
                      and its one assistant frame is the binary's own, carrying the API's
                      refusal. mcp-reconnect, mcp-toggle and stop-task never prompt and
                      have no assistant frame. system thinking_tokens frames come in some
                      turns, ahead of that turn's thinking frame, and in none of client,
                      conversation-reset, defaults, fork, fork-parent, mcp-cancel,
                      payloads, resume, schema-nullable, sdk-mcp and trivial. in cancel, compact and
                      web-search the first thinking frame has none ahead of it and a later
                      one does; background-stop's one comes after its result, in the turn
                      the binary opens itself, where the recording stops. a ThinkingBlock
                      and a ThinkingTokensMessage. thinking asked for a budget and named no
                      display, as this SDK does unless it is asked to omit the thinking:
                      the text is empty there too. whether a display that summarizes
                      would fill it is not recorded; this SDK has no way to ask for one
    a prompt's        a prompt written with a uuid is named by the binary in
    lifecycle         command_lifecycle frames (command_uuid, state, uuid, session_id):
                      queued once read, started where its turn's frames begin, completed
                      after its result, cancelled after an interrupted one (cancel,
                      client). no recording without prompt uuids has one. a turn a
                      prompt opens itself opens with its started and then system init,
                      the conversation_reset or status frames of /clear and /compact
                      between the two; between queued and started the binary may write a
                      rate_limit_event or an in-process server's tools/list request. a
                      turn the binary opens by itself opens with system init and no
                      lifecycle frame (continuation.stdout.jsonl:20, :50). a prompt
                      written during that turn is queued in it and started after its
                      result (continuation:21, 27), or folded into it after a tool round:
                      the turn's init at 50, queued at 53, the tool round's user frame at
                      54, started at 55 with no init after it, completed at 60 before the
                      result at 61, which answers both. the stamps (user_message_uuid,
                      user_message_uuids) are on the thinking_tokens frames, the first
                      top-level assistant message and the result of a turn a prompt opens
                      itself, on those after a folded prompt's started (continuation:56-58,
                      61), on no frame of a turn the binary opens by itself before that
                      (20-26, 50-54), and on the result only of /clear and /compact
    an MCP cancel     an interrupt while an in-process tool call is pending is sent to the
                      server as MCP's notifications/cancelled, params requestId the
                      tools/call's id and reason "AbortError: remote-cancel"
                      (mcp-cancel.stdout.jsonl:12), and not as a control_cancel_request:
                      the recorded stdin acknowledges the notification and never answers
                      the tools/call. the call closes as a rejected tool_result and the
                      result is error_during_execution with aborted_tools
    an interrupt      background-interrupt, 2.1.283: written after a turn's result and its
    between turns     completed frame while the shell the turn started in the background
                      still slept, it was answered success with still_queued empty
                      (background-interrupt.stdout.jsonl:17) and stopped nothing. no frame
                      followed the answer until the shell ended on its own, 29 seconds
                      later in the recorder's log and by task_updated's end_time against
                      the first result's request_sent_wall_ms and duration_ms; then
                      background_tasks_changed empty, task_updated completed and
                      task_notification completed, exit code 0 (:18-20), and a turn the
                      binary opened itself on the notification, system init to a result
                      success (:21-26)
    in-process        ahead of the server's connection each verb is answered "Server not
    mcp verbs         found: calc" (sdk-mcp-client). once it is connected, reconnect and
                      toggle on are refused "SDK servers should be handled in print.ts",
                      and toggle off answers success with no body and leaves it listed
                      connected (mcp-reconnect, mcp-toggle)
    an agent's        an entry of mcpServers is a server's name. under --strict-mcp-config
    mcpServers        the binary blocks it for the agent and says so in a system
                      notification ("bee agent MCP server blocked by --strict-mcp-config:
                      echo", agents.stdout.jsonl); the agent's tools still reach the
                      session's server of that name
    a schema's root   --json-schema is the input schema of the StructuredOutput tool the
                      binary offers, and the API takes an object there and nothing else
                      (2.1.283). under ["object", "null"] it refuses the request, 400
                      "tools.9.custom.input_schema.type: Input should be 'object'", and the
                      binary writes the refusal itself: an assistant frame whose model is
                      <synthetic>, with error unknown, is_api_error_message and that text,
                      then a result with subtype success, is_error, api_error_status 400,
                      terminal_reason api_error and no structured_output
                      (schema-nullable.stdout.jsonl:3-4). the same schema under "object" is
                      answered (schema-type): the request that carried its descriptions,
                      maxLength, maxItems, enum and nested additionalProperties false was
                      taken, and the answer fit it

## what the binary did not emit, and how that was established

2.1.276 unless a line names another version.

    hook_started, hook_response   not for an SDK callback hook. hook-events registered an SDK
                                  PreToolUse hook beside the two settings hooks; the SDK hook
                                  fired through hook_callback in the same recording and no
                                  event names it. hook_name is "Event:matcher" for a settings
                                  hook
    a denial by disallowedTools   none. one run, by hand, on 2.1.261: the denied prompt with
                                  tools=["Write"] and disallowed_tools=["Write"]. the tool was
                                  never offered to the model, which answered in text. that run
                                  was discarded for the auto-deny one above; the recorder has
                                  no disallowed-tools fixture. on disk: the run's transcript,
                                  transcripts/projects/-tmp-claude-agent-fixtures-cwd/
                                  8a1c5516-dd2e-4951-878f-f9eeb6ed54ad.jsonl, the same prompt
                                  at 2026-09-10T23:48:51 with no tool_use and a text answer.
                                  not on disk: its stdout, so "permission_denials empty" has
                                  no artifact
    server_tool_use               not for WebSearch, which is a client tool here (web-search).
    advisor_tool_result           no run reaches it; its block is typed on the declaration
                                  alone, and no recording carries one
    replaceRules, removeRules,    never suggested (2.1.274 and 2.1.276). suggestions (2.1.274)
    removeDirectories             asks three times and the binary offers addRules, setMode and
                                  addDirectories; the other recordings offer setMode alone.
                                  what it suggests only adds. the three that take away are
                                  what a caller sends back in updatedPermissions, which ReplyWriter
                                  writes; the parser reads them because a suggestion has the
                                  same shape. unreached, not proven absent
    userSettings, projectSettings never a suggestion's destination (2.1.274 and 2.1.276): one
                                  localSettings and seven session across every recording
    plan, bypassPermissions,      never a setMode suggestion's mode (2.1.274 and 2.1.276): the
    dontAsk, auto                 five recorded say acceptEdits, one in suggestions, one each in
                                  can-use-tool and cancel, two in hooks-more. default is
                                  recorded, as the init frame's permissionMode, not as a
                                  suggestion
    ask, deny, defer as a rule's  never a suggested rule's behavior (2.1.274): the two recorded,
    behavior                      both in suggestions, say allow. defer is recorded as a hook's
                                  answer, which this SDK writes and the binary does not
    PreCompact trigger auto       one PreCompact, trigger manual (compact). the automatic one
                                  needs a context no fixture fills. unreached
    Notification hook             registered in hooks-more, never fired; the permission for the
                                  Write went to can_use_tool. unreached, not proven absent.
                                  PreCompact sat in the same initialize and never fired there
                                  either; it fired in compact
    a stopped turn's background   two recordings list background work in a hook's
    tasks                         background_tasks: background-stop's Stop hook its background
                                  Bash, type "shell", status "running"
                                  (background-stop.stdout.jsonl:11), and hooks-more's SubagentStop
                                  its backgrounded subagent, type "subagent", status "running",
                                  with agent_type (hooks-more.stdout.jsonl:18). hooks' Stop hook
                                  lists none. the binary 2.1.276's table and filter are the
                                  source of the rest. a task's type is its kind's label:
                                  local_agent "subagent",
                                  local_workflow "workflow", local_bash "shell", monitor_mcp and
                                  monitor_ws "monitor", mcp_task "MCP task", in_process_teammate
                                  "teammate", dream "dream", auto_mode_scan "auto-mode scan",
                                  remote_agent "cloud session"; a kind the table has no label
                                  for is written as its own name. a task is listed only while
                                  its status is running or pending and it is not held in the
                                  foreground. StopHookBackgroundTaskType names the nine labels.
                                  a task is one type a kind whose members a recording founds:
                                  ShellStopHookBackgroundTask with command (background-stop),
                                  SubagentStopHookBackgroundTask with agent_type (hooks-more).
                                  the reference's declaration says in comments that server and
                                  tool come beside monitor and MCP task and name beside workflow;
                                  the list carries no comment, and no recording lists those
                                  kinds. every label but shell and subagent is an
                                  UnknownStopHookBackgroundTask, the entry as the binary wrote it
    task_progress                 not for every subagent: none for the foreground one in task
                                  or the backgrounded one in hooks-more; three for
                                  task-background's backgrounded one, and one for agents'
                                  backgrounded one
                                  (2.1.274, agents.stdout.jsonl:18, its bee's mcp__echo__echo).
                                  no Bash task emitted one
    result.errors                 recorded, but never the API-error variant: a string array on
                                  the four interrupt results (cancel.stdout.jsonl:13 and :27,
                                  client.stdout.jsonl:29, mcp-cancel.stdout.jsonl:16), one
                                  "[ede_diagnostic] ..." each
    content-replacement           only the interactive terminal host creates the state that
                                  writes it, and only under the remote flag
                                  tengu_hawthorn_steeple; the stream-json path the recorder
                                  drives never creates it. two runs by the recorder, each a
                                  subagent answering five results over the per-message
                                  budget, one with the flag forced through
                                  CLAUDE_INTERNAL_FC_OVERRIDES, wrote none; neither is kept.
                                  the fork carries no such line
    progress                      2.1.276 and 2.1.282: dropped before any transcript write; no
                                  transcript on this machine carries one
    malformed frames              the binary writes none; Wire/FrameCodecRejectionTests.cs
                                  hand-writes them because nothing else can
    a failed or needs-auth        no recording; McpServerStatus.Error and the failed and
    MCP server                    needs-auth statuses stand on this line alone. pending is
                                  recorded for the login's claude.ai connector, in an init of
                                  each of client, compact, conversation-reset, defaults, fork,
                                  fork-parent, resume, schema-nullable, schema-type,
                                  streaming, structured, thinking, trivial and web-search and in
                                  sdk-mcp-client's mcp_status;
                                  disabled once (stdio-mcp's
                                  mcp_status after its toggle).
                                  Status is the wire's string, so no branch reads them
