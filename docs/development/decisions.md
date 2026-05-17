# Architectural Decisions

Only active, binding decisions. One line + date per decision. Historical or superseded decisions are not stored here.

---

## Architecture

- **2026-05-16** OpenBridge is the system architecture; Open Browser is the application.
- **2026-05-16** Five layers: LLM → browser tab → tab process → Host → executor.
- **2026-05-16** UI shell and tab runtime are separate responsibilities.
- **2026-05-16** The source of truth for model responses is the stream/delta, not the DOM.
- **2026-05-17** Autonomous execution loop: LLM decides → executor executes → result returns to LLM. No human in the runtime loop.

## Envelopes

- **2026-05-17** Markers: `@@OPENBRIDGE_EXEC_BEGIN@@` and `@@OPENBRIDGE_EXEC_END@@`.
- **2026-05-17** Only the `PS` (PowerShell) command is accepted by the mapper.
- **2026-05-18** Parser takes the first envelope, ignores the rest.
- **2026-05-18** Every envelope attempt gets feedback to the LLM (result or `[OpenBridge] ...` error).

## Host and Executor

- **2026-05-17** `GeneralCommandExecutor` — the only active executor; launches any executable via `Process.Start`.
- **2026-05-17** `OpenBridgeHost` — shared singleton, not per-command. Has a concurrency lock (`_busy`).
- **2026-05-17** `IOpenBridgeCommandExecutor` — hot-swap interface for executors.
- **2026-05-18** Executor output is truncated at `MaxOutputChars` (default 50k) with a `[... truncated ...]` marker.

## UI and Interaction

- **2026-05-17** Result is injected into `#prompt-textarea.ProseMirror` + Send button click (`[data-testid='send-button']`).
- **2026-05-18** Human delay: 20s base + truncated normal distribution (mean=22s, std=11s, min=0, max=50s) before sending the response.

## Versioning

- **2026-05-18** Build number = `git rev-list --count HEAD`. Hash = `git rev-parse --short HEAD`. Date with dots.

## Safety

- **2026-05-16** Safety boundaries are at the OS and sandbox configuration level — not in runtime approval.
- **2026-05-16** `config/local/` in `.gitignore` — do not commit secrets.

## Open Decisions

- Format of `HST_*` commands (HELP, STATUS, CAPABILITIES) — unresolved.
- Cross-tab communication — unresolved.
- Watchdog/recovery — unresolved.
- Whether the approval panel UI should be removed entirely.
- Final implementation of linguistic response variants (humanizer).
