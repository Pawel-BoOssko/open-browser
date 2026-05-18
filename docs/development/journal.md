# Development Journal

A loose, chronological list of changes. Most recent first.

---

## 2026-05-18 (later)

- **added:** Cycle timeout — 120s deadline on full execution cycle. On timeout: `[OpenBridge] Timeout: no response within 120s.` Late responses discarded via `_cycleClosed` flag.
- **added:** Always-send-feedback — even empty output sends `[OpenBridge] Process exited with code N. No output.`
- **added:** `docs/handoff/baseline-2026-05-18.md` — baseline handoff document.

## 2026-05-18 (early)

- **added:** `docs/runtime/environment.md` — the runtime contract for the LLM inside Open Browser (commands, envelope format, what to expect).
- **added:** HST_HELP metacommand — model can ask system what it can do, gets `environment.md` back.
- **added:** `docs/development/journal.md` — development journal (this file).
- **added:** `docs/development/rules.md` — working rules for dev agents.
- **added:** `docs/development/decisions.md` — active architectural decisions only.
- **added:** `docs/README.md` — docs index with rules.
- **added:** README in every docs subfolder: purpose, creator, date, rules. Rule: new folder → must add README. Enter folder → read README first.
- **added:** Git-based versioning — build number = `git rev-list --count HEAD`, hash = `git rev-parse --short HEAD`, date with dots. Shown in window title.
- **added:** Human delay (20s base + truncated normal distribution: mean=22s, std=11s, min=0, max=50s). Applied in `SendTextToChatAsync`.
- **moved:** 10 historical docs → `old-files/`. 8 more legacy files (architecture, handoff, capabilities) → `old-files/`. All knowledge preserved in `decisions.md` and `journal.md`.
- **cleaned:** Approval panel UI — removed Approve/Reject buttons, details textbox, copy prompt. Simplified to Output panel with result + copy output button.
- **cleaned:** `D:\projects\` — old test repos moved to `old-files/`.
- **migrated:** Core philosophy from `project-assumptions.md` → `decisions.md`. Knowledge preserved.

## 2026-05-17 (late)

- **added:** stdout/stderr truncation in `GeneralCommandExecutor` — `MaxOutputChars` (default 50k), `[... truncated ...]` marker.
- **added:** First envelope only — parser takes the first `@@OPENBRIDGE_EXEC_BEGIN@@`, ignores everything after `@@OPENBRIDGE_EXEC_END@@`.
- **added:** Error feedback to LLM — every envelope attempt gets a response (parse error, unsupported command, empty prompt, busy). `SendTextToChatAsync` sends `[OpenBridge] ...` to chat.
- **removed:** `CommandExecutor`, `CommandExecutorOptions`, `CommandExecutorOptionsLoader`, `CommandExecutorMode` — dead code from Cloud Code era.
- **removed:** `ApproveProcessAsync`, `IsProcessAvailable`, `ProcessAvailableMessage` from `OpenBridgeRuntimeApproval`.
- **change:** `OpenBridgeHost` — executor now required in constructor (no default).
- **change:** `OpenBridgeRuntimeApproval` — now takes `OpenBridgeHost` via constructor (shared Host).
- **change:** `ApproveDryRunAsync` renamed to `ExecutePendingAsync`.
- **change:** `GeneralCommandExecutor` — empty prompt validation (`PROMPT_EMPTY`).

## 2026-05-17 (early)

- **added:** Auto-execution — envelope detected → PS executed → result injected into `#prompt-textarea.ProseMirror` → Send clicked.
- **added:** Message ID dedup — `_observedMessageIds` in `ResponseExtractor.Finish()` prevents re-processing the same message.
- **added:** Observer scoped to new frames only — `GetCurrentAnswerTextForFrames(newFrames)` instead of full `GetCurrentAnswerText()`.
- **removed:** 15s cooldown — replaced by message ID dedup.
- **added:** Send button click — searches `[data-testid='send-button']`, then `button[aria-label*='Send']`, then `button svg`.
- **added:** App icon (ChatGPT PNG, loaded via `Bitmap.GetHicon()`).
- **added:** Build timestamp in window title.
- **removed:** Duplicate `_buildLabel` from status bar.
- **change:** Envelope markers from `<<<OPENBRIDGE:EXEC:BEGIN>>>` to `@@OPENBRIDGE_EXEC_BEGIN@@` (hard migration, no backward compatibility).
- **removed:** CC command (Cloud Code) — only PS accepted by mapper.
- **change:** `IClaudeCodeExecutor` → `IOpenBridgeCommandExecutor`.
- **change:** `ClaudeCodeExecutor` → `CommandExecutor` (later removed entirely).
- **added:** `GeneralCommandExecutor` — runs any executable via `System.Diagnostics.Process`.
- **UI cleanup:** Removed unused buttons, diagnostics panel; result text made selectable.
- **added:** `PipelineRawDump` — diagnostic dumps at every pipeline stage.
- **fixed:** CDP body routing to ResponseExtractor via `isCdpConversationBody` bypass.
- **fixed:** Recursive CDP body unwrap (loop up to 5 levels).
- **added:** `_sawPageStream` gate to block CDP data after WebSocket messages.

## 2026-05-16 and earlier

- Project structure: `MainForm`, `PageTap.js`, `ResponseExtractor`, `NetworkLogger`, `BrowserTabRuntime`, `WebViewMessageHandler`.
- OpenBridge Protocol: `OpenBridgeEnvelopeParser`, `OpenBridgeEnvelopeObserver`, `OpenBridgeHostCommandMapper`.
- `OpenBridgeHost`, `HostCommandRequest`, `HostCommandResult`, `HostErrorCodes`.
- Smoke tests: `OpenBridgeProtocolSmoke`, `OpenBridgeHostSmoke`, `ResponseExtractorSmoke`.
- Removed legacy `BridgeBrowserHelper` and Cloud Code connector.
- `config/local/` in `.gitignore`.
