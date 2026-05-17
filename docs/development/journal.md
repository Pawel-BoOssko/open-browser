# Development Journal

A loose, chronological list of changes. Most recent first.

---

## 2026-05-18

- **added:** Git-based versioning — build number = `git rev-list --count HEAD`, hash = `git rev-parse --short HEAD`, date with dots. Shown in window title.
- **added:** Human delay (20s base + truncated normal distribution: mean=22s, std=11s, min=0, max=50s). Applied in `SendTextToChatAsync` — covers both success and error paths.
- **added:** `docs/development/` and `docs/runtime/` — new documentation structure.
- **added:** `docs/README.md` — docs index with rules.
- **added:** README in every docs subfolder with purpose, creator, date, and rules.
- **added:** Rule: when you create a folder, you must add a README with purpose/creator/date/rules. Enter a folder → read its README first.
- **moved:** 10 historical docs files to `old-files/`.
- **change:** Cleaned up `D:\projects\` — old test repos moved to `old-files/`.

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
