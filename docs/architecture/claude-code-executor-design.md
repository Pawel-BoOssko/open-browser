# Claude Code Executor Design

Status: draft design document
Created: 2026-05-16
Commit: 062e793 (at time of writing)

---

## 1. Purpose

The highest-priority executor direction for OpenBridge is enabling Open Browser to delegate coding and repository work to a local AI coding agent — specifically Claude Code (Anthropic), Cloud Code, or a DeepSeek-backed agent — as an execution tool controlled through the OpenBridge protocol.

The LLM running inside Open Browser should be able to issue a command like:

```json
{
  "version": "001",
  "command": "CC",
  "payload": "Implement a health-check endpoint in src/api/health.py",
  "payload64": "<base64-encoded detailed prompt or file content>"
}
```

And receive back structured stdout/stderr, exit code, and result content — all within the safety boundaries of the local project.

This document defines the design for that path. It does not implement anything.

---

## 2. Architectural position

The intended execution path:

```
LLM in Open Browser (chatgpt.com conversation)
  → execution envelope in model response
    → ResponseExtractor assembles stream
      → OpenBridgeEnvelopeObserver detects envelope, calls parser
        → future minimal Host receives parsed command object
          → ClaudeCodeExecutor (or ShellExecutor wrapper)
            → Claude Code / Cloud Code / DeepSeek API process
          ← stdout, stderr, exit code, duration
        ← structured result object
      ← Host wraps result for LLM consumption
    ← tab runtime delivers response to WebView
  ← LLM sees system response in conversation
```

**Current reality**: Stage A is closed. The parser and passive observer exist. No Host exists. No executor exists. This document designs the Host + executor path specifically for the Claude Code use case.

---

## 3. Why this direction has priority

- **Antigravity credits are limited**. The current setup relies on a single LLM provider for both conversation and reasoning. Delegating longer coding workflows to a separate agent reduces credit consumption on the primary conversation model.
- **Claude Code / Cloud Code / DeepSeek can take over longer coding workflows**. These tools are purpose-built for multi-file edits, test runs, and iterative debugging — tasks that are awkward to do through manual copy/paste from a browser conversation.
- **Open Browser should orchestrate, not just observe**. The long-term vision is Open Browser as a controlled execution environment. The Claude Code executor is the first concrete use case that justifies the Host layer.
- **Immediate practical value**. Once wired, the LLM in Open Browser can ask a local coding agent to read files, run tests, apply fixes, and report results — all within the project boundary.

---

## 4. Minimal Host responsibilities for this path

The Host layer for the Claude Code path needs only these responsibilities. Do not build a general-purpose Host beyond what this path requires:

| Responsibility | Description |
|---|---|
| Receive parsed command | Accept the `OpenBridgeEnvelopeParseResult` from the observer (future handoff) |
| Assign `operation_id` | Generate unique ID for logging and timeout tracking |
| Validate working directory | Ensure the requested path is within `D:\projects\open-browser` (or explicitly allowed paths) |
| Call executor | Dispatch to `ClaudeCodeExecutor` with normalized input |
| Enforce timeout | Measure elapsed time, kill process on timeout, return structured timeout error |
| Collect stdout/stderr/result | Capture all output from the executor process |
| Return structured result | Build a result object conforming to the output contract |
| Log operation | Write NDJSON event log entries: operation start, end, error, timeout |

**Explicitly out of scope for this path**:
- Registry of LLM instances
- Cross-tab communication
- Watchdog / recovery
- Executor auto-discovery
- Command queuing or async execution
- General executor dispatch table (only one executor initially)

---

## 5. ClaudeCodeExecutor responsibilities

The executor wraps the local coding agent process. It is not the agent itself — it is the controlled execution boundary around it.

| Responsibility | Detail |
|---|---|
| Working directory control | Execute only within the project root unless a whitelisted path is specified. Default: `D:\projects\open-browser`. |
| Input prompt handling | Pass the LLM's `payload` (and `payload64` if present) as the coding task description. Handle prompt length limits. |
| Non-interactive execution | Prefer non-interactive flags (`--print`, `-p`, `--no-interactive`, or equivalent) so the agent does not block waiting for user input. |
| Timeout | Hard timeout enforced by Host. Executor may have its own softer timeout for sub-operations. Default proposal: 300s for coding tasks. |
| stdout/stderr capture | Capture both streams separately. Stream to log in real time if feasible, or collect fully on completion. |
| Exit code handling | Map exit code to status: `0` → `ok`, non-zero → map to appropriate `error_code`. |
| Response size limits | Truncate stdout and stderr at `max_output_chars` (proposed default: 50,000 characters). Log truncation event. |
| Log redaction | Pass output through existing `Redactor` before logging. |
| No uncontrolled permission bypass | Never pass `--dangerously-skip-permissions` or equivalent by default. Permissions are controlled at the OpenBridge Host layer, not at the executor wrapper. |

---

## 6. Invocation options

Four approaches to invoking Claude Code / Cloud Code / DeepSeek from a .NET process. None is chosen yet — this section compares them for decision.

### Option A: Direct CLI call

```
claude -p "Fix the null check in src/auth.py" --no-interactive
```

| Pros | Risks |
|---|---|
| Simplest path — call the existing CLI | CLI flags may change between versions |
| No additional infrastructure | Interactive prompts may block if flag support is incomplete |
| Works with any Claude Code-compatible backend | Shell injection risk if prompt is not sanitized |
| | Requires Claude Code to be installed and on PATH |

**Must verify**: `--print` / `-p` non-interactive behavior, exit code conventions, stdout format, whether the CLI ever prompts interactively despite flags.

### Option B: Wrapper PowerShell script

```
Invoke-ClaudeCode -Prompt "..." -Timeout 300 -MaxOutput 50000
```

| Pros | Risks |
|---|---|
| Isolates CLI quirks behind a script boundary | Extra layer to maintain and test |
| Can handle retries, env setup, credential checks | Script must be versioned and shipped with the project |
| Easier to test independently of .NET code | PowerShell-specific; not portable to Linux |

**Must verify**: script error handling, credential passing from environment, output encoding.

### Option C: Dedicated .NET executor process

A separate `BridgeBrowserClaudeCodeWorker` .NET project that wraps `System.Diagnostics.Process`.

| Pros | Risks |
|---|---|
| Full .NET control over process lifecycle | More code to write and maintain |
| Clean stdout/stderr separation via `ProcessStartInfo` | Separate project means separate build artifact |
| No shell interpretation of prompt content | Over-engineering for a single CLI call |

**Must verify**: process kill behavior on timeout, encoding (UTF-8 output), large output buffering.

### Option D: API-based call (DeepSeek / OpenRouter / Anthropic API)

Direct HTTP call to the model provider API instead of invoking a local CLI tool.

| Pros | Risks |
|---|---|
| No local CLI dependency | Requires API key management |
| Clean request/response model | No file access — must build tool-use loop |
| Works cross-platform | Reimplements what Claude Code already does |
| | Latency and cost per call |

**Must verify**: API availability, cost model, whether the API provides equivalent coding capabilities to the CLI.

### Recommendation for first implementation

Option A (direct CLI) or Option C (.NET Process wrapper) are the most practical. Option A is fastest to prototype. Option C is safer long-term. The decision should be made after a spike where both are tried against a real Claude Code installation.

---

## 7. Input and output contract

Draft technical contract. Not a protocol — a design sketch to guide future implementation.

### Input (Host → Executor)

```json
{
  "operation_id": "op_20260516_001",
  "working_directory": "D:\\projects\\open-browser",
  "prompt": "Fix the null check in src/auth.py",
  "mode": "non_interactive",
  "timeout_ms": 300000,
  "max_output_chars": 50000
}
```

| Field | Required | Description |
|---|---|---|
| `operation_id` | yes | Unique ID from Host for log correlation |
| `working_directory` | yes | Absolute path; must be within allowed roots |
| `prompt` | yes | The coding task from the LLM |
| `mode` | yes | `non_interactive` initially; `interactive` reserved for future |
| `timeout_ms` | yes | Hard timeout in milliseconds |
| `max_output_chars` | no | Default 50000 if omitted |

### Output (Executor → Host)

```json
{
  "status": "ok",
  "operation_id": "op_20260516_001",
  "duration_ms": 12450,
  "exit_code": 0,
  "stdout_preview": "Fixed null check in src/auth.py:42. Tests pass.",
  "stderr_preview": "",
  "error_code": null,
  "message": "Task completed successfully.",
  "stdout_full_truncated": false,
  "stderr_full_truncated": false
}
```

| Field | Required | Description |
|---|---|---|
| `status` | yes | `ok`, `error`, or `timeout` |
| `operation_id` | yes | Echoes input operation_id |
| `duration_ms` | yes | Wall-clock execution time |
| `exit_code` | no | Process exit code if available |
| `stdout_preview` | yes | Truncated stdout or summary |
| `stderr_preview` | yes | Truncated stderr or empty |
| `error_code` | no | Machine-readable error code if status is error |
| `message` | yes | Human-readable result or error description |
| `stdout_full_truncated` | no | True if stdout exceeded max_output_chars |
| `stderr_full_truncated` | no | True if stderr exceeded max_output_chars |

**This is a draft**. Field names, structure, and semantics may change during implementation.

---

## 8. Safety and control constraints

These constraints are non-negotiable for the Claude Code executor path. They apply regardless of which invocation option is chosen.

| Constraint | Enforcement |
|---|---|
| **No git push by default** | Host must not pass flags that enable pushing. Executor must not add `--push` or equivalent. |
| **No package install without explicit permission** | Prompt must not instruct the agent to install packages unless the LLM explicitly requests it and Host allows it. |
| **No work outside project root** | `working_directory` validated by Host. Executor must not `cd` outside allowed roots. |
| **No permanent `--dangerously-skip-permissions` scripts** | No `.bat` launchers, shell scripts, or config files that embed permission bypass. Permissions are granted per-operation by Host. |
| **Command execution must be logged** | Every executor invocation writes NDJSON log entries: start, end, timeout, error. Log includes operation_id, prompt (redacted), duration, exit code, and output preview. |
| **Large outputs truncated** | stdout and stderr capped at `max_output_chars`. Truncation is logged and flagged in the result. |
| **Timeouts mandatory** | Every operation has a hard timeout. No unbounded execution. Host kills the process on timeout. |
| **Prompt content logged redacted** | The prompt string is logged through the existing `Redactor` before writing to NDJSON. |

---

## 9. Relationship to existing parser

The OpenBridge protocol parser and passive observer already exist (Stage A artifacts). Their relationship to the future Claude Code executor:

- **`OpenBridgeEnvelopeParser`** — parses `<<<OPENBRIDGE:EXEC:BEGIN>>>` markers, extracts JSON, validates fields. No changes needed for the executor path. The parser already extracts `command`, `payload`, and `payload64`.
- **`OpenBridgeEnvelopeObserver`** — currently passive: calls parser, logs results, returns. No changes needed.
- **`ResponseExtractor.Finish()`** — currently calls `_observer.Observe(answer)`. This is the handoff point. No changes needed.
- **Future Host integration** — the observer must not execute commands directly. The observer returns a parse result. Only Host (after design approval and implementation) may take that result and dispatch to an executor.

**Rule**: ResponseExtractor and OpenBridgeEnvelopeObserver hand off parsed envelopes to Host only after Host design is approved and Host code exists. Until then, the observer remains passive.

---

## 10. Open decisions

These decisions must be resolved before writing executor code:

| # | Decision | Options |
|---|---|---|
| 1 | **Invocation method** | Direct CLI (Option A) vs .NET Process wrapper (Option C) vs API (Option D) |
| 2 | **Exact CLI command and flags** | Depends on whether Claude Code, Cloud Code, or DeepSeek CLI is the target. Flags differ per tool. |
| 3 | **Interactive approval handling** | What happens if the coding agent asks "Proceed? (y/n)" despite non-interactive flags? Timeout? Auto-reject? |
| 4 | **Timeout values** | 300s default for coding tasks? Different timeouts for different command types? |
| 5 | **Max output sizes** | 50,000 chars reasonable for preview? Should full output be available separately? |
| 6 | **First executor type** | Dedicated `CC` (ClaudeCode) executor or generic `SH` (shell) executor that can call any CLI? |
| 7 | **Result delivery to LLM** | How does the structured result become a message in the WebView conversation? System message injection? |
| 8 | **Credential management** | Where are API keys stored? Environment variables? How does Host pass them to the executor process? |
| 9 | **Working directory allowlist** | Only `D:\projects\open-browser`? Or a configurable list of allowed roots? |
| 10 | **Concurrency** | Can multiple operations run at once? Or is the executor single-operation? |

---

## 11. First implementation milestone after approval

A proposed first milestone that proves the command flow without running a real coding agent yet:

**Goal**: Prove that a parsed OpenBridge envelope can travel from observer → Host → executor → result → log, without touching a real Claude Code process.

**What to build**:

1. **Minimal Host stub** — a class that receives a parsed envelope, assigns operation_id, and calls an executor interface.
2. **Echo executor** — a fake executor that receives the input contract, waits N milliseconds, and returns a hardcoded structured result:

```json
{
  "status": "ok",
  "operation_id": "op_20260516_001",
  "duration_ms": 1500,
  "exit_code": 0,
  "stdout_preview": "[ECHO] Received prompt: Fix the null check in src/auth.py",
  "stderr_preview": "",
  "error_code": null,
  "message": "Echo executor: task would be dispatched to Claude Code."
}
```

3. **NDJSON log verification** — confirm that operation start, success, and result events appear in the run log.
4. **Smoke test** — a test that feeds a synthetic `HST_HELP` or `CC` envelope through the flow and asserts the log contains expected events.

**What this milestone does NOT do**:
- Does not call Claude Code or any real CLI
- Does not implement cross-tab, registry, or watchdog
- Does not inject responses into the WebView conversation
- Does not handle timeouts (echo executor always succeeds)

**After this milestone passes**, the team can decide whether to implement Option A (direct CLI), Option C (.NET wrapper), or Option D (API) as the first real executor.
