# Current State

Purpose:
Defines the current repository-level truth that developer agents must use before making changes or recommendations.

Depends on:
- docs/templates/document-header-template_new.md
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/development-rules_new.md
- docs/agent/coding-invariants_new.md
- docs/runtime/modules-index_new.md
- docs/agent/architecture-decisions_new.md
- docs/development/rules.md
- docs/development/decisions.md
- docs/development/journal.md
- docs/development/README.md
- docs/runtime/environment.md
- config/start-prompt.txt
- docs/design/session-rhythm.md
- docs/design/help-system.md

Impacts:
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/development-rules_new.md
- docs/agent/documentation-update-checklist_new.md
- docs/agent/commit-rules_new.md
- docs/runtime/start-prompt_new.md
- docs/runtime/hst-help_new.md
- docs/runtime/hst-status_new.md
- docs/runtime/modules-index_new.md
- docs/executors/*/index_new.md
- docs/modules/*/index_new.md

Update rule:
Update this file when implemented behavior, active architecture, available commands, active modules, active executors, runtime paths, build/test commands, RAW payload behavior, or verified system capabilities change.

Latest:
- 2026-10-18 16:50: Open Browser Lite SOURCE v1.60.0 and PBOX1 STAGED are separate from PBOX1 LIVE. Embedded assembly version includes exact UTC build second, rendered on title and toolbar as local Europe/Warsaw time. Code includes experimental opt-in resume supervisor for test chat only: DOM observations must be stable >=40s and >=3 samples, require verified High effort, clean composer and no tools/error; separate Luna Responses API requires OPENBROWSER_LUNA_MODEL and OPENAI_API_KEY configured in user's Windows environment, currently both absent on PBOX1. DPAPI resume ledger prevents uncertain-send retries. See docs/design/openbrowser-full-delivery-plan-v2-2026-10-18-1626.md and docs/openbrowser-checkpoint-2026-10-18-1643-v160.md. PBOX1 live former notes_dom_voice process should not be killed. Actual auto-resume E2E with a user-created 'testowy' chat still TODO; do not claim live supervision.
- 2026-10-18 15:52: Open Browser Lite iteration SOURCE/BUILD only (not LIVE): encrypted, crash-safe per-chat notes outbox (`LiteNotesOutbox.cs`, selftest PASS), safe navigation guard that refuses to leave chat while generating/draft/notes unsaved (`LiteTabSwitchGuard.cs`), headless adaptive warm-tab residency planner (0-2 warm per available memory, selftest PASS), metadata-only passive CDP network correlation (`LiteTransportDiagnostics.cs`, selftest PASS). UI buttons for outbox and initialization of network CDP NOT connected because tool safety blocked exact edits; see docs/Rejestr nieudanych komend.md entries OB-11..14. Full checkpoint docs/openbrowser-checkpoint-2026-10-18-1552.md. SATANAMD Debug build PASS, 0 errors, known MSB3277. PBOX1 live earlier release not interrupted.
- 2026-10-18 14:03: PBOX1 was fully unresponsive and rebooted at 13:53:23; post-reboot monitoring survived and resumed successfully at 14:01. Pre-reboot resource samples show severe *system-wide* memory pressure (274 MiB free at 13:45; >6 GiB pagefile occupied), with MS Edge, ChatGPT Desktop, Claude and WebView2 among consumers. Lite UI heartbeat continued through 13:52:54; this does not prove renderer/desktop responsiveness. No post-instrumentation WebView2 ProcessFailed recorded, CPU deltas of Lite renderer did not indicate a saturated single core. Scheduler transient 0x800710E0 around 13:59 resolved to success; on-battery task is currently disallowed. See docs/openbrowser-lite-incident-2026-10-18.md. Root cause not proven.
- 2026-10-18 13:44: Resource monitor is now installed on PBOX1 after the operator used schtasks.exe. Verified by LC MultiHost: Task Scheduler `OpenBrowser Lite Resource Monitor`, LastTaskResult=0, 2-minute cadence, latest JSONL samples present (3 lines). Runs while Windows user is logged in due to /IT. Post-reboot sample execution has not yet been verified. This supersedes the earlier failed task-registration attempt; see docs/openbrowser-lite-incident-2026-10-18.md.
- 2026-10-18: Added `scripts/monitor-lite-resources.ps1` plus hidden launcher `scripts/monitor-lite-resources-hidden.vbs` for independent read-only per-process CPU/memory snapshots. Manual execution on PBOX1 PASS, JSONL `resources-*.jsonl` exists. The initial Task Scheduler registration attempt timed out and was not installed at that point; this was resolved by the operator's later schtasks.exe registration (see latest entry above). Record in `docs/openbrowser-lite-incident-2026-10-18.md`.
- 2026-10-18: Diagnosed PBOX1 Lite `RenderProcessUnresponsive` under severe system-wide RAM pressure (16 GiB RAM, measured 85 MiB free, ~6.4 GiB pagefile in use). The prior Lite build had no persisted process error telemetry. Added `LiteEventLog.cs` and Lite UI instrumentation for WebView2 lifecycle, render failures, navigation outcomes and UI heartbeat; event metadata only, no chat contents/tokens. Debug build PASS and PBOX1 Release publish PASS; the new PBOX1 task and shortcuts launch `%LOCALAPPDATA%/Programs/OpenBrowserChatGPTLite_20261018_telemetry/BridgeBrowserAlpha0.exe`. Profile preserved; lifecycle/navigation/UI-heartbeat JSONL confirmed with WebView2 155.0.4283.60. SATANAMD Release output remains locked by its running PID 13384. See `docs/openbrowser-lite-incident-2026-10-18.md`.
- 2026-10-18: ChatGPT Lite preview is implemented in src/BridgeBrowserAlpha0/ChatGptLiteForm.cs. Launch via --chatgpt-lite, or scripts/launch-chatgpt-lite.ps1 after building Release. It is a single-window WebView2 with a dedicated user profile under %LOCALAPPDATA%/OpenBrowser/ChatGPT-Lite/Profile. It does not instantiate MainForm, OpenBridgeHost, PageTap, response interception, OpenBridge diagnostics or ConversationTrimmer; Lite has separate metadata-only event logs but does not capture or preserve conversations. SATANAMD interactive task 'OpenBrowser ChatGPT Lite' and a desktop shortcut were created for testing; ChatGPT login/navigation requires user verification.
- 2026-10-18: The relocated src/BridgeBrowserAlpha0/BridgeBrowserAlpha0.csproj and three smoke test csproj references were repaired. Debug and Release builds pass, 0 errors with known WindowsBase warning; Protocol, Host and ResponseExtractor smoke tests pass after re-running dotnet restore.
- 2026-10-18: Local SATANAMD working tree has an unfinished, staged relocation of 38 source files from releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0 to src/BridgeBrowserAlpha0. The old .csproj path no longer exists in this worktree. The moved .csproj has unresolved relative paths to assets. No build verification or runtime fixes are part of this housekeeping pass.
- 2026-10-18: Maintenance checkpoint and privacy exclusions are recorded in docs/maintenance-open-browser-2026-10-18.md. Runtime downloads remain on disk but are ignored by Git.
- Source migration from docs/development/*, docs/runtime/environment.md, config/start-prompt.txt, and design docs is reflected here.
- OpenBrowser project working directory is D:\projects\open-browser.
- Developer agents must begin technical work with git status --short.
- Current developer-agent documentation entrypoint is docs/agent/READ_BEFORE_WORK_new.md.
- Current module and executor registry is docs/runtime/modules-index_new.md.
- SessionRhythm is documented as planned/pending, not confirmed active implementation.
- RAW payload mechanism exists for long or quoting-sensitive executor payloads.
- Current Open Questions include cross-tab communication and watchdog/recovery.

## Current repository root

OpenBrowser/OpenBridge work is performed in:
- D:\projects\open-browser

Developer agents must not work outside this path unless explicitly instructed.

## Current active command model

Current OpenBridge command envelope version:
- version 001

Current Host commands:
- HST_HELP
- HST_STATUS

Current executors:
- PS
- PY

Related help commands:
- PS_HELP
- PY_HELP

Current tools reported by OpenBridge status may include:
- claude
- github
- linkedin
- local
- n8n
- web

## Current RAW payload model

Runtime environment documentation describes a RAW payload mechanism for commands that are long or fragile under JSON escaping.

Use RAW payload for:
- long scripts;
- quote-heavy commands;
- backslashes;
- JSON;
- markdown;
- OpenBridge-sensitive text.

Operational rule:
- RAW belongs inside the payload JSON string;
- RAW outside the JSON object makes the envelope invalid;
- RAW is preferred over manual JSON escaping for complex payloads.

## Current build and validation model

After every change to .cs files:
- run dotnet build.

Historical build command (no longer valid after the pending local source relocation):
- dotnet build D:\projects\open-browser\releases\BridgeBrowserAlpha0\src\BridgeBrowserAlpha0\BridgeBrowserAlpha0.csproj

Current local project file is src/BridgeBrowserAlpha0/BridgeBrowserAlpha0.csproj, but its asset references need correction before this path can be used as a validated build command.

After changes to OpenBridgeProtocol:
- dotnet run --project tests/OpenBridgeProtocolSmoke/OpenBridgeProtocolSmoke.csproj

After changes to OpenBridgeHost or executors:
- dotnet run --project tests/OpenBridgeHostSmoke/OpenBridgeHostSmoke.csproj

Known warning:
- MSB3277 WindowsBase can be ignored only if there are 0 errors.

## Current versioning model

Build identity uses:
- build number: git rev-list --count HEAD
- hash: git rev-parse --short HEAD
- date format: dot-separated date

Source evidence:
- docs/development/decisions.md
- docs/development/journal.md

## Current active documentation model

Developer agents must use repository documentation as current truth.

Key files:
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/development-rules_new.md
- docs/agent/coding-invariants_new.md
- docs/agent/documentation-update-checklist_new.md
- docs/agent/commit-rules_new.md
- docs/runtime/modules-index_new.md
- docs/feedback/field-notes_new.md

## Current module and executor status

Active:
- HST
- PS
- PY
- Downloads
- Humanizer
- PageTap

Existing documented area:
- ConversationTrimmer

Planned/pending design area:
- SessionRhythm

## Current verified capability: ChatGPT sandbox downloads

OpenBrowser currently has verified live ChatGPT sandbox file download handling.

Current flow:
- ChatGPT exposes a generated file in the conversation UI.
- PageTap detects the file control.
- PageTap triggers guarded activation.
- WebView2 DownloadStarting handles the browser download.
- The file is saved to the OpenBrowser downloads directory.
- HST_STATUS reports recent downloads.

Verified file:
- openbrowser_real_sandbox_test.txt

Verified content:
- Hello from live test

Verified events:
- page_sandbox_download_candidate
- page_sandbox_download_click
- download_starting
- download_completed

## Current active architecture constraints

Do not restore:
- old BridgeBrowserHelper
- old Cloud Code connector

Do not introduce without explicit approval or explicit task scope:
- new layers
- services
- managers
- registries
- factories
- dispatchers
- queues
- watchdog
- cross-tab
- multi-tab registry

Do not commit:
- config/local files
- secrets
- unrelated generated downloads

## Current open questions

Needs owner/architect decision:
- cross-tab communication;
- watchdog/recovery.

## Current security and folder evidence

Security and environment sources state:
- Open Browser is designed for local use and must not be exposed to the Internet.
- There is no authentication or network security layer for public exposure.
- Secrets, API keys, tokens, and credentials belong in config/local/.
- config/local/ is git-ignored and must not be committed.
- The previous application path was releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/; local sources are currently staged for relocation to src/BridgeBrowserAlpha0/.
- Local OpenBridgeHost code is currently under src/BridgeBrowserAlpha0/OpenBridgeHost/.
- Local OpenBridgeProtocol code is currently under src/BridgeBrowserAlpha0/OpenBridgeProtocol/.
- General command executor lives under OpenBridgeHost/GeneralCommand/.
- diagnostics/pipeline-output/ is used for pipeline diagnostic dumps.

Release/source evidence:
- releases/BridgeBrowserAlpha0 contains alpha release notes and changelog material.
- Historical release docs include ConversationTrimmer and older alpha context; active truth still comes from current *_new.md docs and current code.

## Current active tool integrations

Tool source migration identifies these active tool integration folders:
- tools/claude/: Claude Code launcher through DeepSeek Anthropic-compatible API.
- tools/github/: GitHub file publishing through Contents API, without local git.
- tools/linkedin/: LinkedIn posting through OAuth 2.0 and REST API.
- tools/n8n/: n8n workflow management through API and Supabase-related scripts.
- tools/web/: browser/web automation utilities for fetch, forms, and browser-related tasks.

Tool rules:
- Each tool folder owns its own README and working guide.
- Secrets belong in config/local/, never in tools/.
- Tool documentation should not be duplicated into Host or runtime help.
- HST_STATUS may report available tools, but detailed usage belongs in tools/* documentation.

## Current implementation evidence from handoff

Latest handoff sources identify these active implementation areas:
- PageTap.js detects ChatGPT download buttons, including button.behavior-btn, and dispatches the click sequence.
- BrowserTabRuntime.cs manages WebView lifecycle, PageTap, module loading, and WebView2 DownloadStarting handling.
- AppPaths.cs defines the Downloads path and EnsureAll creates it.
- OpenBridgeRuntimeApproval.cs participates in HST_STATUS recent downloads reporting.
- OpenBridgeHost.cs is a shared singleton with a concurrency lock.
- GeneralCommandExecutor.cs runs local processes, captures stdout/stderr, and truncates large output.
- IOpenBridgeCommandExecutor.cs is the hot-swap interface for executors.

Current downloads path evidence:
- AppPaths.Downloads currently resolves to releases/BridgeBrowserAlpha0/downloads/ according to handoff.md.

Historical do-not-restore evidence:
- Old markers are dead.
- ClaudeCodeExecutor and old CommandExecutor were removed and replaced by GeneralCommandExecutor.
- BridgeBrowserHelper must not be restored.
- Human approval panel was removed from runtime flow.

## Current verified test evidence

From test source migration:
- HST_STATUS has verified output areas: ID, build, working directory, Git status, tools, recent downloads section.
- PY executor works for basic Python execution and UTF-8 output according to later test results.
- RAW block inside payload is verified for PY, and invalid RAW placement outside JSON correctly fails.
- n8n workflow listing has passed in test sources.
- GitHub authentication check has passed in test sources.
- web_fetch.py was initially missing dependencies in older results, but later test results show it working.
- PS stdout may still corrupt Polish characters. Treat PS stdout UTF-8 as an open issue unless a later verification proves it fixed.
- Older sandbox download test failed in test-results-v4.md, but later live OpenBrowser tests verified ChatGPT sandbox downloads. Treat the old failure as historical unless reproduced.
## Current handoff for AntiGravity

Current handoff file: handoff.md.

The handoff is not a replacement for docs/agent/READ_BEFORE_WORK_new.md. It is a task handover for AntiGravity after the documentation cleanup.

Near-term AntiGravity work should be inventory and planning only:
- inspect dirty working tree, especially PortalScanner.js and runtime downloads;
- propose how to separate OpenBrowser from the portal-document-download direction, possibly under the user-facing idea name Otwarty Portal Sądowy;
- propose a procedure for creating a clearly distinguishable user-runnable build for Paweł on explicit owner request;
- do not implement the split or build procedure until accepted.


## Current repository caution
## Current folder policy

Folder governance is defined in docs/agent/folder-policy_new.md and local FOLDER.md files. New root folders require policy update or architect approval.


There are existing older docs and new *_new.md docs during the documentation-governance migration.
Developer agents must not assume old files are current if a *_new.md replacement exists and is part of the current documentation-governance work.

Historical or replaced content must not be treated as current truth unless migrated into active documentation.



<!-- COMPLETE_DOC_UPDATE_CURRENT_STATE_20260521 -->
## Current help and transport documentation state

Active model-facing help files:
- docs/runtime/hst-help_new.md: OpenBridge/HST help index, command frame, HST commands, tool help commands, RAW/Base64 framing;
- docs/executors/PS/help_new.md: PowerShell executor help;
- docs/executors/PY/help_new.md: Python executor help;
- docs/agent/help-command-routing_new.md: maps help commands to documentation files.

Active operational policy files:
- docs/agent/model-feedback-policy_new.md: where and when models record operational observations;
- docs/agent/external-tool-policy_new.md: how models treat external tools differently from local filesystem work;
- docs/feedback/antigravity-text-transport-task_new.md: architecture task for text transport and encoding.

Node.js may be available through the local Windows environment, but NODE is not an OpenBridge executor unless listed by implemented commands. Models must not infer a NODE executor from Node being installed.
<!-- /COMPLETE_DOC_UPDATE_CURRENT_STATE_20260521 -->
