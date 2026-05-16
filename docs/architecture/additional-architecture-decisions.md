# Approved OpenBridge / Open Browser Architecture Decisions

## 1. Purpose and core idea

1a. **OpenBridge** is the system architecture.

1b. **Open Browser** is the local browser application in which OpenBridge runs.

1c. The LLM runs on the model provider side, while OpenBridge gives it a local operating environment around a real browser session.

1d. Open Browser is intended to be a controlled, inspectable layer that can be developed by later models and by a human operator.

1e. Core idea: the LLM remains the reasoning engine, while the local system provides transport, state, diagnostics, communication and future tools.

---

## 2. Layer model

2a. The target architecture has five layers:

```text
LLM
→ browser tab / conversation
→ tab process
→ Host
→ executors / execution systems
```

2b. **LLM**: generates responses, decisions and commands.

2c. **Browser tab / conversation**: the interaction surface with the model.

2d. **Tab process**: locally handles one conversation.

2e. **Host**: routes commands, maintains registry, communicates with executors, handles diagnostics and system responses.

2f. **Executors**: execute specific operations, for example filesystem, shell, diagnostics, Python and integrations.

---

## 3. UI and tab runtime boundary

3a. UI shell and tab runtime are separate responsibilities.

3b. UI shell is responsible for:

```text
window
layout
controls
buttons
statuses
```

3c. Tab runtime is responsible for:

```text
WebView lifecycle
PageTap
JS modules
message handling
stream
local conversation state
communication with Host
```

3d. The future tab process should grow out of tab runtime logic, not out of the UI form.

3e. Current refactoring direction: progressively slim down `MainForm` and extract tab runtime into separate classes.

---

## 4. LLM identity

4a. Every LLM instance receives minimal identity.

4b. The minimal prompt contains:

```text
a name or an instruction to choose a name
OpenBridge ID
short information that the LLM runs inside OpenBridge / Open Browser
basic startup commands
```

4c. The LLM does not receive a large operational context upfront.

4d. The LLM reads environment details through self-discovery.

4e. For ChatGPT, the instance identifier is the `conversation_id` from the conversation URL.

4f. A name such as “Stefan” is a label, not a routing key.

4g. Minimal working prompt:

```text
Choose your human name.
Your OpenBridge ID is: <conversation_id>.
You are running inside OpenBridge / Open Browser.
Use HST_HELP, HST_STATUS and HST_CAPABILITIES to discover the environment.

To use them write a JSON in response:
{
  "version": "001",
  "command": "HST_HELP"
}
```

4h. The prompt is updated after the environment/protocol version number changes.

---

## 5. Self-discovery

5a. The LLM learns the environment through self-discovery commands.

5b. Basic Host startup commands:

```text
HST_HELP
HST_STATUS
HST_CAPABILITIES
```

5c. `HST_HELP` sends content from:

```text
docs/environment/README.md
docs/environment/executors.md
```

5d. `HST_CAPABILITIES` sends content from:

```text
docs/environment/capabilities.md
```

5e. `HST_STATUS` says what is enabled and what works. It is primarily a Host self-check and current system control mechanism.

5f. `HST_STATUS` is available to the LLM, but it is not the main self-discovery mechanism.

5g. Self-discovery responses should be complete, not shortened depending on payload size.

5h. Every executor has its own self-discovery metacommands, for example:

```text
FS_HELP
FS_STATUS
FS_CAPABILITIES

SH_HELP
SH_STATUS
SH_CAPABILITIES

PY_HELP
PY_STATUS
PY_CAPABILITIES
```

5i. `FS_HELP` is a question about the filesystem executor as a component.

5j. `FS` is an execution command to the filesystem.

---

## 6. Environment documentation

6a. The environment should be self-describing.

6b. Environment documents are stored in:

```text
docs/environment/
```

6c. Structure:

```text
docs/environment/
  README.md
  capabilities.md
  folders.md
  executors.md
  changes.md
```

6d. File meaning:

```text
README.md        general environment description
capabilities.md  current capabilities visible through CAPABILITIES
folders.md       folder descriptions and purposes
executors.md     executor descriptions and statuses
changes.md       dated environment changes
```

6e. Every folder should have a description of its purpose and rules.

6f. Minimal folder or function description contains:

```text
name
purpose
contents / capability
status
updated_at
```

6g. Function statuses:

```text
active
experimental
planned
disabled
```

6h. Significant environment changes should be dated.

---

## 7. DOM, stream and source of truth

7a. The source of truth for model responses is **stream / deltas**.

7b. Model responses are assembled from deltas/stream.

7c. The conversation DOM is not the source of truth.

7d. The DOM is not the operational data source.

7e. The DOM is a visual surface for the user.

7f. The system does not analyze, scroll or refer to old conversation history.

7g. If an operational context tail is needed, it means roughly the last:

```text
10-40 messages
```

7h. Older history is not a runtime resource.

---

## 8. Execution envelope

8a. The LLM can include an execution envelope in its response.

8b. The envelope is a fragment of the LLM message intended for OpenBridge.

8c. Final envelope markers:

```text
@@OPENBRIDGE_EXEC_BEGIN@@
...
@@OPENBRIDGE_EXEC_END@@
```

8d. One LLM response may contain one execution envelope.

8e. One envelope may contain one RAW block.

8f. The envelope contains JSON with metadata and simple payload.

8g. Minimal required JSON fields:

```json
{
  "version": "001",
  "command": "..."
}
```

8h. `version` is a three-character string representing the format version.

8i. A breaking compatibility change increases the version, for example:

```text
"001" → "002"
```

8j. `command` is the routing address.

8k. No separate addressing fields are added, such as:

```text
target
to
executor
recipient
```

8l. Unknown envelope fields are ignored and logged as warning.

---

## 9. Command and payload

9a. `command` indicates the executor or system layer.

9b. For execution executors, payload may use the executor's natural syntax.

9c. Simple operations do not have to be artificially split into separate parameters.

9d. Example principle:

```text
SH + payload = execute shell command
FS + payload = execute filesystem operation
PY + payload = execute Python code
```

9e. `payload` and `payload64` may both appear.

9f. Meaning:

```text
payload    = short instruction / mode / path / main command
payload64  = large auxiliary content
```

9g. Example:

```json
{
  "version": "001",
  "command": "FS",
  "payload": "write docs/example.md",
  "payload64": "..."
}
```

9h. Executor metacommands are separate:

```text
FS_HELP
FS_STATUS
FS_CAPABILITIES
```

9i. `FS_HELP` is not a filesystem operation, but a question to the filesystem executor as a component.

---

## 10. RAW block and Base64

10a. Long or syntactically risky content goes into a RAW block.

10b. Final RAW block markers:

```text
<<<OPENBRIDGE:RAW_PAYLOAD:BEGIN>>>
...
<<<OPENBRIDGE:RAW_PAYLOAD:END>>>
```

10c. The RAW block is detected by the envelope pre-parser.

10d. The pre-parser encodes the RAW block into one-line standard Base64.

10e. After encoding RAW, `payload64` is created.

10f. Only then does JSON go to validation and routing.

10g. The RAW block may contain text, Markdown, JSON, code, quotes, backslashes and special characters.

10h. Base64 must be standard and one-line.

---

## 11. Envelope processing order

11a. Order:

```text
1. find EXEC:BEGIN
2. find EXEC:END
3. check that there is at most one envelope
4. detect RAW block if present
5. encode RAW to one-line Base64
6. replace RAW pointer with payload64
7. parse JSON through System.Text.Json.JsonDocument.Parse
8. validate OpenBridge fields
9. route by command
10. return result or error
```

11b. JSON is parsed with a standard library.

11c. In .NET, use:

```text
System.Text.Json.JsonDocument.Parse
```

11d. A JSON error should return diagnostic information:

```text
line number
byte position in line
path
message
```

---

## 12. System responses to LLM

12a. The LLM does not receive raw JSON as the response.

12b. A technical result object exists internally.

12c. The LLM receives a result message wrapped in neutral linguistic noise.

12d. Wrapping must be semantically equivalent across variants.

12e. Wrapping must not add additional substantive information.

12f. The system response wrapping mechanism should use multiple linguistic variants.

12g. Variants are randomized from lists or sets of lists.

12h. The goal is varied, natural-looking communication.

12i. “Humanizer” is only an example name for the mechanism, not an architectural or implementation name.

12j. Technical statuses:

```text
ok
error
timeout
fallback
```

---

## 13. Errors

13a. A technical error must contain:

```text
status
layer
error_code
message
```

13b. `details` is optional.

13c. Example technical structure:

```json
{
  "version": "001",
  "command": "FS",
  "status": "error",
  "layer": "executor",
  "error_code": "FILE_NOT_FOUND",
  "message": "File not found.",
  "details": "docs/missing.md"
}
```

13d. Each layer is responsible for its own errors and reverses them to the layer above.

13e. No layer leaves silence.

---

## 14. Timeouts

14a. `timeout_ms` is not a parameter of the LLM envelope.

14b. Timeout belongs to the calling layer toward the layer below.

14c. Principle:

```text
tab process measures Host timeout
Host measures executor timeout
executor measures timeout of lower process if it starts one
```

14d. Timeouts are configured in the layer, executor or execution mechanism definition.

14e. Timeout response must contain at least:

```text
status
layer
command
message
elapsed_ms
timeout_limit_ms
```

14f. The LLM receives only a neutrally wrapped timeout message.

14g. Instructions on how to interpret timeouts and what to do after the first/subsequent timeout are in `HELP` / `CAPABILITIES`, not in the timeout message itself.

14h. After a timeout, the operation is closed for the layer above.

14i. A late response from the lower layer is ignored in a controlled way.

14j. At this stage, the system does not start automatic diagnostics or recovery after timeouts.

---

## 15. Host

15a. Host is the central system process.

15b. Host is responsible for:

```text
routing
registry
communication with executors
diagnostics
system status
controlled responses
```

15c. Host is on the command execution path.

15d. Host does not guess command intent.

15e. Host routes by `command`.

15f. Host maintains the LLM instance registry.

15g. Host blocks duplicate active conversations with the same `conversation_id`.

---

## 16. LLM instance registry

16a. The communication target between models is a specific LLM instance.

16b. For ChatGPT, the instance is identified by `conversation_id` from the conversation URL.

16c. Host maps `conversation_id` to the technical place of execution.

16d. The LLM addresses another instance by `conversation_id`.

16e. Instance name is not a routing key.

16f. There must be a command that lets the LLM query which model/conversation instances are available.

16g. Final command name for this is not decided.

---

## 17. Cross-tab

17a. Cross-tab communication is synchronous from the perspective of the asking instance.

17b. Cross-tab communication addresses a specific LLM instance by `conversation_id`.

17c. If the target instance is currently generating a response, the question is not injected during generation.

17d. After the current response finishes, the question may be passed in a controlled wrapper.

17e. The target instance may answer, refuse or defer if handling the question risks losing its own context.

17f. After delivering the answer, Host informs the responding instance that the answer was delivered and it may continue.

17g. Cross-tab command names such as `XTB_ASK` / `XTB_REPLY` are not approved.

17h. The exact cross-tab command model requires a separate decision.

---

## 18. Tab process

18a. The tab process handles one model conversation / one tab.

18b. It is responsible for:

```text
stream and delta handling
response assembly
local conversation state
page message handling
detecting and passing envelopes
communication with Host
passing Host responses back to the conversation
```

18c. The tab process keeps the current conversation/tab state in its own memory.

18d. The tab process measures Host timeout.

18e. The tab process returns an error to the LLM if it cannot communicate with Host.

---

## 19. Executors

19a. The executor model will be recreated after auditing the working execution pattern from the previous mechanism.

19b. Executor type is not guessed before that pattern is audited.

19c. Executor receives a normalized command:

```text
command
payload
payload64, if present
operation_id
```

19d. Executor returns a result object with status:

```text
ok/error/timeout
result or error
execution time
layer/error source
```

19e. Minimal first executor set:

```text
HST metacommands
FS
SH
DIA
```

19f. `PY` is to be considered later or immediately, but is not a condition of the first Host.

19g. `FS` and `SH` differ functionally:

```text
FS: file operations as a domain, for example read, write, list, copy, move, mkdir, delete, stat
SH: run a system command and return stdout/stderr/exit code
```

---

## 20. State and progress tracking

20a. The component that performs a work stage records stage start, end and result itself.

20b. Current state is stored in the memory of the owning process/component.

20c. The tab process keeps conversation/tab state.

20d. Host keeps routing, command, registry and execution state.

20e. Executor keeps operation state if the operation is ongoing.

20f. State change history is written to append-only event log.

20g. Host can query components for their current state.

20h. Watchdog reads state and last-change time, but is not the state owner.

20i. At this stage, no database is used for current state.

---

## 21. Event log

21a. Event log file format: `NDJSON`, one JSON per line.

21b. Minimal record fields:

```text
ts
component
operation_id
event
status
message
```

21c. Optional fields:

```text
command
duration_ms
error_code
details
```

21d. Logs are append-only.

21e. Required logical events:

```text
stage start
stage success end
stage error end
timeout
command passed between layers
result received from lower layer
```

21f. `STATUS` reads current component state, it does not reconstruct state from the log.

---

## 22. Log retention

22a. Logs are daily.

22b. Retention:

```text
14 days or until manual cleanup
```

22c. Maximum size of a single file:

```text
50 MB
```

22d. After the limit is exceeded, rotation occurs.

22e. No compression or database is planned for logs.

---

## 23. Time representation

23a. Internal time must be unambiguous.

23b. Preferred format: ISO 8601.

23c. Logs, timeouts, statuses and events use concrete timestamps.

23d. Protocols and runtime state do not use natural-language descriptions such as “tomorrow”, “in a moment” or “later”.

---

## 24. Watchdog

24a. Watchdog reads state and time of last change.

24b. Watchdog is not the state owner.

24c. Exact watchdog/recovery mechanism requires further analysis.

24d. At this stage, automatic diagnostics after timeout are not designed.

---

## 25. Roadmap order

25a. First close the boundary:

```text
UI shell ↔ tab runtime
```

25b. Only after that design and build:

```text
Host
registry
command routing
executors
cross-tab
watchdog
```

25c. Current refactors move toward extracting tab runtime from UI.

---

## 26. Runtime command approval

26a. OpenBridge envelopes detected in runtime must not execute automatically.

26b. A valid envelope detection creates a pending command candidate, not an execution order.

26c. Operator approval is mandatory before Host execution.

26d. First runtime execution integration must use DryRun only. Process mode from runtime requires a later explicit decision.

26e. Result delivery to the LLM conversation is not approved yet. The first runtime milestone stops at operator-visible result.

26f. Automatic WebView result injection is not approved yet.

26g. Every Process-mode CC operation requires individual operator approval. No batch approval. No "remember this choice."

26h. Envelopes inside code blocks, with parse errors, or in multiple-envelope responses must not execute.

26i. The observer remains passive. Execution is triggered by the runtime approval layer, not by the observer.

---

# Open decisions

1. Exact timeout configuration model in layer/executor definitions.
2. Executor type after auditing the working pattern.
3. Final command for listing available LLM instances.
4. Final cross-tab commands.
5. Watchdog/recovery details.
6. Exact implementation of linguistic response variants for system responses.
