# Runtime Command Approval Design

Status: design + DryRun-only implementation
Date: 2026-05-16 (design), 2026-05-16 (DryRun implementation)

---

## 1. Purpose

Runtime execution from LLM output requires an explicit, operator-controlled approval layer before any OpenBridge CC envelope can reach Host and trigger Claude Code execution. This document designs that layer.

The LLM may produce valid execution envelopes in its response stream. But a valid envelope is not a command until the operator approves it. This document defines the flow, UI requirements, and safety constraints that must exist before runtime-to-Host wiring is implemented.

---

## 2. Current proven path

The isolated (non-runtime) path has been proven and documented in `docs/handoff/cc-envelope-e2e-proof.md`:

```
EXEC envelope text
  → OpenBridgeEnvelopeParser (extracts command CC, payload)
    → OpenBridgeHostCommandMapper (maps to HostCommandRequest)
      → OpenBridgeHost (validates, assigns operation_id, enforces lock/timeout)
        → ClaudeCodeExecutor in Process mode (launches Claude Code via System.Diagnostics.Process)
          → structured HostCommandResult
```

A local manual probe (`tools/local/CcEnvelopeEndToEndProbe/`) successfully invoked real Claude Code through this path. The path works. It is not yet connected to the WebView runtime.

---

## 3. Why approval is mandatory

- **LLM output may contain examples** — the LLM might describe a command envelope or show one in documentation. The system must not execute it.
- **Quoted envelopes must not execute** — if the LLM quotes a previous command or discusses the format, the envelope markers are present but the intent is not execution.
- **Malicious or accidental envelopes** — an untrusted or misconfigured model could produce destructive commands. The operator must review before execution.
- **CC execution can modify the repository** — Claude Code has filesystem access within `D:\projects\open-browser`. Unreviewed execution could produce unwanted code changes.
- **Operator must see and approve** — the human operator is the authority. No command executes without their explicit consent.

---

## 4. Proposed runtime flow, first version

```
LLM response finishes (stream ends)
  → ResponseExtractor.Finish() calls OpenBridgeEnvelopeObserver.Observe()
    → observer detects valid CC envelope
      → observer does NOT execute — creates a pending command candidate
        → runtime presents PendingCommand state to operator
          → operator sees command summary in UI

Operator chooses:
  [Approve] → runtime creates HostCommandRequest via mapper
            → calls OpenBridgeHost.ExecuteAsync(request)
              → ClaudeCodeExecutor executes (DryRun or Process)
                → structured result returned to runtime
                  → runtime shows result to operator
  [Reject]  → runtime logs rejection
            → no Host call is made
```

**Key rule**: The observer remains passive. The runtime creates a candidate object. Host is called only after operator approval.

---

## 5. Approval UI requirements

A minimal UI component (panel, dialog, or status area) must present:

| Element | Content |
|---|---|
| Command | `CC` |
| Version | `001` |
| Prompt preview | First 200 characters of the prompt |
| Payload size | Total characters (payload + decoded payload64) |
| Working directory | `D:\projects\open-browser` (or configured path) |
| Timeout | 720000 ms (12 minutes) |
| Max output | 50000 characters |
| Execution mode | DryRun or Process |
| Process mode warning | If Process mode is configured, show a clear warning: "Process mode is enabled. Real Claude Code will be invoked." |

Controls:
- **Approve** button — primary action, executes the command
- **Reject** button — secondary action, discards the command
- **Copy details** button (optional) — copies command info to clipboard for review

**No automatic execution.** The default state is pending, not approved.

---

## 6. Rejection behavior

- Rejection is logged as an NDJSON event with `status: "approval_rejected"`, `command: "CC"`, and the operation timestamp.
- No Host call happens. No executor is invoked.
- The pending command is removed from UI state.
- Whether the LLM receives a rejection message is a separate decision (see section 8). For the first milestone, the LLM is not notified.

---

## 7. Execution behavior after approval

After operator approves:

1. Runtime maps the parsed envelope to `HostCommandRequest` via `OpenBridgeHostCommandMapper`
2. Runtime calls `OpenBridgeHost.ExecuteAsync(request)`
3. Host validates command, working directory, assigns `operation_id`, enforces single-operation lock
4. ClaudeCodeExecutor executes (DryRun or Process mode, depending on local config)
5. Timeout enforced at 720000 ms by default
6. Output truncated at 50000 characters by default
7. Structured result returned to runtime
8. Result logged as NDJSON events: `host_execution_started`, `host_execution_finished` / `host_execution_failed` / `host_execution_timeout`
9. UI shows final status, duration, exit code, stdout/stderr preview

During execution:
- The approve button is disabled (single-operation lock prevents duplicate submission)
- UI shows an "Executing..." status with elapsed time
- If timeout fires, UI shows timeout status with elapsed time and error details

---

## 8. Result delivery options

After execution completes, the structured result must be made available. Options, from simplest to most integrated:

| # | Option | Description | First milestone? |
|---|---|---|---|
| 1 | Operator-only display | Result shown in UI. Operator reads it and decides what to do. | Yes |
| 2 | Copy to clipboard | Operator can copy result text for manual paste into conversation. | Yes |
| 3 | Manual paste | Operator manually pastes result into the WebView input field. | Yes (manual) |
| 4 | Automatic WebView injection | System writes result into the WebView chat input via JavaScript. | No — not approved |
| 5 | Structured system response | System constructs a neutrally-wrapped system message and injects it as a conversation turn. | No — not designed |

**Decision**: The first runtime implementation milestone must stop at operator-visible result (option 1), with optional copy-to-clipboard (option 2). Automatic WebView injection and structured system responses are separate future decisions.

---

## 9. Accidental execution prevention

Hard rules that must be enforced in code:

- **Do not execute envelopes inside code blocks** unless future detection can reliably distinguish code blocks from genuine execution intent. For the first milestone, envelopes inside Markdown code fences (` ``` `) are considered examples.
- **Do not execute quoted examples** — if the envelope text appears to be part of a meta-discussion (the LLM talking about envelopes rather than issuing one), the system should be cautious. For the first milestone, every detected envelope requires manual approval regardless.
- **Do not execute if multiple envelopes exist** — the parser returns `MULTIPLE_ENVELOPES` error. Runtime must reject these automatically.
- **Do not execute if parser returns any error** — parse errors require operator attention. Auto-reject and log.
- **Require explicit operator approval for every Process-mode CC operation** — no batch approval. No "approve all." No "remember this choice." One approval per command.

---

## 10. Logging

Events to log when runtime integration is implemented (not now):

| Event | When |
|---|---|
| `envelope_detected` | Observer detects a valid CC envelope after stream finishes |
| `approval_pending` | A pending command is shown to the operator |
| `approval_accepted` | Operator clicks Approve |
| `approval_rejected` | Operator clicks Reject |
| `host_execution_started` | Host.ExecuteAsync is called |
| `host_execution_finished` | Host returns Ok result |
| `host_execution_failed` | Host returns Error result |
| `host_execution_timeout` | Host returns Timeout result |

All events use existing NDJSON format with `ts`, `component`, `event`, `status`, `operation_id`, `command`, and relevant details.

---

## 11. First implementation milestone after approval

A possible first code milestone that integrates runtime-to-Host through the approval layer, without real Claude Code execution:

1. **UI panel** — a simple approval panel in the existing WinForms UI (e.g., a panel below the diagnostics textbox) visible only when a pending command exists
2. **Pending command state** — a simple object holding the parsed envelope data and the mapped HostCommandRequest
3. **Approve/Reject buttons** — wired to Host (Approve) or discard (Reject)
4. **DryRun only** — the first runtime integration uses DryRun mode only. The local config's Process mode is ignored for runtime execution until explicitly approved later
5. **No WebView result injection** — result is shown in the UI panel only
6. **No registry, cross-tab, watchdog, state machine, or plugin loader**
7. **No generic SH executor**
8. **No automatic execution** — every command requires approval

---

## 12. Explicit non-goals

- No automatic execution from WebView
- No WebView DOM injection or system message delivery
- No generic SH (shell) executor
- No registry of LLM instances or `conversation_id` mapping
- No cross-tab communication (`XTB_*`)
- No watchdog or recovery processes
- No state machine for tab or operation lifecycle
- No plugin loader or executor auto-discovery
- No `BridgeBrowserHelper` or old helper infrastructure
- No `--dangerously-skip-permissions` scripts
- No package installation
- No git push
