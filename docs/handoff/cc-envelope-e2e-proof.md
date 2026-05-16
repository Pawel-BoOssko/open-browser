# CC Envelope End-to-End Proof

Status: proven locally — not runtime integration
Date: 2026-05-16

---

## 1. Status

The isolated CC envelope path has been proven end-to-end with real Claude Code through ignored local configuration. The full pipeline — from raw EXEC envelope text to structured result — works correctly outside of the WebView runtime.

No browser message triggered this execution. No runtime component was involved. This was a manual local probe only.

---

## 2. Proven flow

```
EXEC envelope text
  → OpenBridgeEnvelopeParser (parse envelope, extract command CC, payload)
    → OpenBridgeHostCommandMapper (map to HostCommandRequest)
      → OpenBridgeHost (validate command, working directory, assign operation_id,
                        enforce single-operation lock, enforce timeout)
        → ClaudeCodeExecutor in Process mode
          → System.Diagnostics.Process
            → claude -p "Say hello from OpenBridge CC envelope path. Return one short sentence."
          ← stdout captured, exit code 0
        ← HostCommandResult { Status = Ok, DurationMs = 7002, ExitCode = 0 }
      ← structured result returned to caller
    ← probe prints parse/map/host/executor/result status
  ← PASS
```

---

## 3. What was executed

- **Local config used**: `config/local/claude-code-executor.local.json` (ignored, not committed) — configured with `Mode: "Process"`, `ExecutablePath: "claude"`, `ArgumentsTemplate: "-p \"{prompt}\""`, `DefaultTimeoutMs: 720000`
- **Probe used**: `tools/local/CcEnvelopeEndToEndProbe/` (ignored, not committed)
- **Dry-run mode tested first**: `dotnet run -- --dry-run` — confirmed parser, mapper, host, and dry-run executor all work without real Claude
- **Real Claude invoked manually once**: with the hello prompt, no dangerous flags
- **WebView/runtime was NOT involved**: ResponseExtractor, OpenBridgeEnvelopeObserver, BrowserTabRuntime, WebViewMessageHandler, PageTap.js — none were loaded or called
- **No browser message caused execution**: the envelope text was constructed in the probe, not extracted from a live LLM response

---

## 4. Result

The real Claude Code invocation produced:

```
Hello from the OpenBridge CC envelope path — where OpenBridge envelopes flow into Claude host requests.
```

Full structured result:

| Field | Value |
|---|---|
| Status | Ok |
| OperationId | 0482d64021bb |
| DurationMs | 7002 |
| ExitCode | 0 |
| ErrorCode | - |
| Message | Process completed successfully. |
| Stdout preview | (as above) |

---

## 5. What remains intentionally not connected

These existing runtime components are intentionally not connected to Host or executor:

- **ResponseExtractor** — assembles stream/deltas, writes answer files, calls `OpenBridgeEnvelopeObserver.Observe()`. No Host integration.
- **OpenBridgeEnvelopeObserver** — passive: parses envelopes, logs results. Does not execute commands.
- **BrowserTabRuntime** — owns WebView lifecycle, injects PageTap, navigates to chatgpt.com. Knows nothing about Host.
- **WebViewMessageHandler** — handles WebMessageReceived events, feeds ResponseExtractor. Knows nothing about Host.
- **PageTap.js** — injected JavaScript for HTTP interception. Unchanged.
- **WebView result injection** — no mechanism exists to deliver Host results back to the LLM conversation. Not designed yet.

---

## 6. Runtime integration gates

Before connecting the runtime (ResponseExtractor / OpenBridgeEnvelopeObserver) to Host for automatic CC command execution, these questions must be decided or implemented:

1. **Approval model** — How does the user/operator approve CC command execution from LLM output? Must the operator click a button? Is there a confirmation dialog?
2. **Result delivery** — How does the structured HostCommandResult become a message in the WebView conversation? System message injection? A dedicated response format?
3. **Execution trigger** — Is the first runtime integration manual-confirmation only (operator must approve each CC command), or is there an auto-execute mode?
4. **Pending command display** — How does the user see that a CC command has been detected and is waiting for approval? Status bar? Dialog? Separate panel?
5. **Long-running operations** — How does the UI indicate that a CC operation is in progress (could be up to 720 seconds)? Progress indicator? Busy state?
6. **Timeout behavior** — Default 720000 ms (12 minutes). Is this shown to the user? Can it be adjusted per-operation?
7. **Output truncation** — Default 50000 chars. Is the user made aware of truncation? Can they request full output?
8. **Logging and redaction** — All command execution must be logged to NDJSON with redacted prompts. Is this done in Host, in runtime, or both?
9. **Dry-run first** — Should the first runtime integration execute only DryRun (no real Claude) until the approval flow is validated?
10. **Accidental execution prevention** — How to prevent the system from executing CC commands found in examples, quoted text, or historical conversation? Is there a freshness check? A confirmation step?

---

## 7. Hard non-goals

The following must NOT be implemented in or alongside the first runtime integration:

- No automatic execution from WebView without explicit user approval
- No generic `SH` (shell) executor
- No registry of LLM instances or `conversation_id` mapping
- No cross-tab communication (`XTB_*`)
- No watchdog or recovery processes
- No state machine for tab or operation lifecycle
- No plugin loader or executor auto-discovery
- No permanent `--dangerously-skip-permissions` scripts
- No package installation
- No git push

---

## 8. Recommended next milestone

**Docs-only design: runtime command approval.**

Create a design document at `docs/architecture/runtime-command-approval-design.md` that answers the gates from section 6 without writing code. It should define:

- The user-facing approval flow for CC commands detected in LLM output
- The result delivery mechanism (how Host output reaches the conversation)
- The confirmation / rejection UX
- How the system distinguishes real commands from quoted examples
- What the operator sees during pending, executing, completed, and errored states

This is a design document — not implementation. No runtime code changes. No Host-to-runtime wiring.
