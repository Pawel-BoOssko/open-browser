# Development Journal

A loose, chronological list of changes. Most recent first.

---

## 2026-05-18 (publication prep)

- **added:** `README.md`, `LICENSE` (MIT), `SECURITY.md` — first public-facing files
- **cleaned:** removed 6 tracked artifact files from repo (n8n exports, github exports, test artifacts, inter-model comms)
- **updated:** `.gitignore` — tool exports, comm files, test artifacts now ignored
- **idea:** Tool artifacts (n8n exports, github exports) need a dedicated location outside the repo — e.g., `D:\projects\open-browser-exports\` or a runtime-specific directory. Currently discarding them during publication prep.

## 2026-05-18 (LinkedIn)

- **added:** LinkedIn posting tools — `tools/linkedin/`. OAuth via official `linkedin-api-client`, posting via REST API.
- **fixed:** Token exchange — manual HTTP always failed with 401 "invalid_client". Official LinkedIn Python client works immediately.
- **learned:** App must be verified with a Company Page (Settings → Verify), not just have Products added.
- **learned:** Use `127.0.0.1` not `localhost` for redirect URI. Use API version `202604` (not `202605`). Both `LinkedIn-Version` and `X-RestLi-Protocol-Version` headers required.
- **learned:** Keep only ONE active client secret. Multiple secrets cause confusion.
- **first post:** Published successfully — `urn:li:share:7462137023722774529`.
- **docs:** `LINKEDIN_WORKING_GUIDE.md` updated with full troubleshooting checklist and lessons learned.

## 2026-05-18 (later)

- **added:** Humanizer — random text wrapping for all LLM responses. 4 slots × 16 variants = 65k+ combinations via Polish template. Same prefix never twice in a row. Applied in `SendTextToChatAsync`.
- **added:** Cycle timeout — 120s deadline on full execution cycle. On timeout: `[OpenBridge] Timeout: no response within 120s.` Late responses discarded via `_cycleClosed` flag.
- **added:** Always-send-feedback — even empty output sends `[OpenBridge] Process exited with code N. No output.`
- **fixed:** `_shouldFinish` bool replaced with `_pendingFinishCount` counter (Interlocked) — prevents lost Finish() calls when frames finish close together.
- **added:** `tools/n8n/` — 8 cleaned Python scripts for n8n API workflow management + Supabase queries. Secrets in `config/local/n8n/`. Docs: README + N8N_WORKING_GUIDE.md. Model can self-discover via HST_HELP.
- **added:** `tools/github/` — 5 cleaned Python scripts for GitHub Contents API publishing. Secrets in `config/local/github/`. Docs: README + GITHUB_WORKING_GUIDE.md.
- **added:** `tools/README.md` — index of all tools.
- **preserved:** Mozilla Web Extensions credentials → `config/local/mozilla/`.
- **added:** `docs/handoff/baseline-2026-05-18.md` — baseline handoff document.
- **updated:** Test plan v2 — 10 tests including humanizer variety check.

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
