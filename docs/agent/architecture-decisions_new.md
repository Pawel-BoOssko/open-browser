# Architecture Decisions

Purpose:
Records accepted architecture and project-governance decisions that developer agents must treat as current project rules.

Depends on:
- docs/templates/document-header-template_new.md
- docs/templates/decision-entry-template_new.md
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/current-state_new.md
- docs/agent/development-rules_new.md
- docs/development/decisions.md
- docs/development/journal.md
- docs/development/rules.md
- docs/feedback/field-notes_new.md

Impacts:
- docs/agent/current-state_new.md
- docs/agent/development-rules_new.md
- docs/agent/coding-invariants_new.md
- docs/agent/documentation-update-checklist_new.md
- docs/runtime/modules-index_new.md
- docs/runtime/hst-help_new.md
- docs/runtime/start-prompt_new.md
- docs/executors/*/index_new.md
- docs/executors/*/help_new.md
- docs/executors/*/rules_new.md
- docs/modules/*/index_new.md
- docs/modules/*/help_new.md
- docs/modules/*/rules_new.md

Update rule:
Update this file when the owner/architect accepts, rejects, changes, or clarifies an architecture or project-governance decision.

Latest:
- Source migration from docs/development/decisions.md, journal.md, rules.md is reflected here.
- This file stores accepted decisions, not discussion transcripts.
- Newest decisions stay near the top.
- Developer agents must not silently override unresolved architecture questions with implementation choices.
- Accepted decisions must be reflected in affected docs listed in Impacts.
- Open questions stay in the Open Questions section until accepted.

## Open Questions

## Accepted Decisions

## 2026-10-19 — Owner-run Open Browser wake (not ChatGPT reminders)

Owner replaces the previous opt-in-only book supervisor priority with a persistent, application-local single-chat development wake. The timer runs inside Open Browser Lite at ~25-minute intervals, tied to exactly one user-approved ChatGPT conversation by its URL in DPAPI local settings; no ChatGPT scheduled Tasks, no Windows hourly ChatGPT wake tasks, and no task packet selection by the watcher. Wake message merely reminds the same chat to read its own durable master plan and continue choosing work autonomously. User explicitly authorizes running this cycle unattended overnight. It is permitted only when the browser is running, the correct conversation is active, the last assistant response is stable, High is verified, there is no generation, user draft, or UI delivery error, and the prior answer has not already been prompted. Persist receipt before sending; no retries on uncertain delivery. Completion marker stops the watcher. Plan: docs/design/openbrowser-master-roadmap-v3-2026-10-19.md. This is distinct from the disallowed ChatGPT reminders: those remain disabled.


## 2026-10-18 18:28 — No ChatGPT scheduled development until operator revokes

Owner directly prohibited creating or running any ChatGPT reminder/automation. All 32 existing ChatGPT automations were found disabled. This overrides older documents referring to a recurring hourly helper; no future developer should re-enable it without a new explicit user instruction. Development takes place in the current operator-supervised chat only. Existing Windows Autosync and read-only resource-monitoring tasks are independent maintenance components, not ChatGPT reminder triggers. Source/test/staged/live remain separate; no automatic promotion of staged releases with failing test gates. Backup and audit evidence: docs/openbrowser-audit-recovery-2026-10-18-1828.md.


## 2026-10-18 | Build-time visible version and test-only Luna continuation supervisor

Decision: generate informational assembly version from fixed semantic version and true build UTC timestamp, show exact build date/time with Warsaw UTC offset in titlebar and first toolbar control, and preserve it when copying/restarting. Resume supervisor is OFF by default, can operate only in a conversation explicitly identified as 'testowy' (bookmarked local project or exact ChatGPT document title); only after 3 stable observations >=40s after tools/generation idle, no draft/error, High effort verified, configured external Luna API model and key, validated JSON decision and DPAPI once-only receipt. Avoid automatic retry when submission acknowledgement is uncertain. Never claim internal server delivery from the rendered reply. The model alias Luna is not an official API ID; real ID must be configured. Keep model API content out of repo/logs and prompt injection from observed text untrusted.

Evidence: docs/design/openbrowser-full-delivery-plan-v2-2026-10-18-1626.md, docs/openbrowser-checkpoint-2026-10-18-1643-v160.md; release needs browser E2E. User's requested High setting is a preference/target; tool blocks are not demonstrably caused by Instant.


## 2026-10-18 | Encrypted notes queue, no implicit web retries, memory-aware chat tabs

Decision: Keep per-chat operator notes in Windows user-scoped DPAPI storage and queue only new note fragments in an append-progress outbox with encrypted atomic replacements and a backup. Use irreversible delivery state transitions (`Queued` -> `Prepared` -> `DeliveryUncertain` -> `Confirmed`); never auto retry uncertain sends because ChatGPT may already have processed them. Preserve one live WebView2 until multi-WebView runtime is E2E tested; tab pool planner allows 0-2 warm tabs by free RAM but protects pending model generation/tool use/drafts/unsaved notes. Before navigating to another conversation check local notes and page busy state with a bounded probe. Passive network CDP telemetry must never capture request bodies, cookies, tokens, headers or complete URLs.

Status: queue + planner and guard built/tested in SOURCE; queue button and CDP attachment blocked and not active in LIVE. Ref: docs/openbrowser-checkpoint-2026-10-18-1552.md and docs/Rejestr nieudanych komend.md.


## 2026-10-18 | ChatGPT Lite critical event diagnostics preserve message privacy

Decision: Record WebView2 lifecycle failures, renderer hang reasons, navigation outcomes (host only) and UI heartbeat into local UTF-8 JSONL diagnostic files with bounded retention. Avoid capturing message bodies, full conversation URLs, passwords, cookies or tokens. Monitoring must not trigger automatic prompts, cancel model work, or reset the WebView profile. The Lite test shell remains separate from OpenBridge automation.

Reason: PBOX1 renderer became unresponsive while the ChatGPT response was not visible; without a separate durable event log we cannot distinguish renderer failure from server-side interruption.

Applies to: src/BridgeBrowserAlpha0/LiteEventLog.cs and ChatGptLiteForm.cs; docs/openbrowser-lite-incident-2026-10-18.md.

## 2026-10-18 | Lightweight ChatGPT test mode is separate from automation

Decision:
Provide an optional --chatgpt-lite startup mode in OpenBrowser. It uses a single WebView2 with basic navigation, separate local user profile and no OpenBridge command execution or response injection. Keep legacy MainForm behavior unchanged.

Reason:
The owner wants to test ChatGPT inside a lighter browser before developing independent task continuation and chat resilience.

Applies to:
- src/BridgeBrowserAlpha0/Program.cs and ChatGptLiteForm.cs
- Windows desktop launcher and scripts/launch-chatgpt-lite.ps1

Impacts:
- docs/agent/current-state_new.md
- docs/openbrowser-chatgpt-lite-2026-10-18.md
- the development build and smoke test references

Do not reintroduce:
- OpenBridge command execution or DOM interception into Lite by default
- treating the Lite preview as an implemented durable conversation archive or unattended task dispatcher

## 2026-05-20 | Documentation governance uses one trigger document

Decision:
docs/agent/READ_BEFORE_WORK_new.md is the single trigger document for developer agents.

Reason:
The owner/architect needs one file to point to when the agent must start the documentation and consistency check chain.

Applies to:
- developer-agent workflow
- documentation governance
- commit recommendation process

Impacts:
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/documentation-update-checklist_new.md
- docs/agent/commit-rules_new.md

Do not reintroduce:
- relying on long chat memory as the main source of project rules

## 2026-05-20 | Documentation files are active or removed

Decision:
Documentation files do not use draft, deprecated, or archived states in their header.

Reason:
The project should not keep stale documents as active candidates for model interpretation. If a documentation file is no longer valid, it should be removed or moved out of the active documentation path.

Applies to:
- all active documentation files
- document header template

Impacts:
- docs/templates/document-header-template_new.md
- docs/agent/documentation-update-checklist_new.md

Do not reintroduce:
- Status field with Draft, Deprecated, or Archived states

## 2026-05-20 | Module registry is only a registry

Decision:
docs/runtime/modules-index_new.md stores only module name, type, status, and docs path.

Reason:
Common documentation rules for modules and executors must live in templates and checklists, not inside every registry entry.

Applies to:
- docs/runtime/modules-index_new.md
- docs/templates/module-docs-template_new.md
- docs/templates/executor-docs-template_new.md

Impacts:
- docs/runtime/modules-index_new.md
- docs/agent/documentation-update-checklist_new.md

Do not reintroduce:
- storing common module documentation rules in the registry

## 2026-05-20 | Help ownership is split by namespace

Decision:
Host help describes Host commands and available executors. Executor help describes that executor.

Reason:
Host documentation should not become a large duplicated manual for every executor.

Applies to:
- HST_HELP
- PS_HELP
- PY_HELP
- future executor help commands

Impacts:
- docs/runtime/hst-help_new.md
- docs/executors/*/help_new.md
- docs/templates/executor-docs-template_new.md

Do not reintroduce:
- full executor manuals inside Host help

## 2026-05-18 | Build versioning uses Git count and hash

Decision:
Build number is based on git rev-list --count HEAD. Hash is based on git rev-parse --short HEAD. Date uses dot format.

Reason:
Runtime/build identity should be deterministic and visible.

Applies to:
- build/version display
- status/window title/version metadata

Impacts:
- docs/agent/current-state_new.md
- docs/runtime/hst-status_new.md

Source evidence:
- docs/development/decisions.md
- docs/development/journal.md

## 2026-05-18 | Cycle timeout guarantees model response

Decision:
Full execution cycle has a 120 second deadline so the LLM always receives a result or timeout fallback.

Reason:
The LLM must not be left without a controlled response.

Applies to:
- Host command execution
- executor timeout behavior
- model-facing error/timeout responses

Impacts:
- docs/executors/HST/rules_new.md
- docs/runtime/hst-status_new.md
- docs/modules/Humanizer/rules_new.md

Source evidence:
- docs/development/decisions.md

## 2026-05-18 | config/local is never committed

Decision:
config/local is ignored and must not be committed.

Reason:
Local config may contain machine-specific settings or secrets.

Applies to:
- Git workflow
- commit rules
- local configuration

Impacts:
- docs/agent/commit-rules_new.md
- docs/agent/development-rules_new.md

Source evidence:
- docs/development/decisions.md
- docs/development/rules.md

## 2026-05-18 | Humanizer wraps model-facing responses

Decision:
Humanizer provides random text wrapping for LLM/system responses while preserving meaning.

Reason:
Repeated mechanical responses should be less repetitive without changing technical result content.

Applies to:
- model-facing response wrapping
- timeout and result wording
- inter-model communication style

Impacts:
- docs/modules/Humanizer/index_new.md
- docs/modules/Humanizer/help_new.md
- docs/modules/Humanizer/rules_new.md

Source evidence:
- docs/development/journal.md

## 2026-05-18 | Always send feedback after process exit

Decision:
Even empty command output sends a model-facing process-exit feedback message.

Reason:
The model must receive a controlled response rather than silence.

Applies to:
- command execution result handling
- no-output command behavior
- Host/executor feedback

Impacts:
- docs/executors/HST/rules_new.md
- docs/executors/PS/rules_new.md
- docs/executors/PY/rules_new.md

Source evidence:
- docs/development/journal.md

## 2026-05-16 | Legacy BridgeBrowserHelper and Cloud Code connector removed

Decision:
The old BridgeBrowserHelper and old Cloud Code connector must not be restored.

Reason:
They were removed from active architecture and must not re-enter through stale references.

Applies to:
- active architecture
- developer-agent prohibitions
- removed-feature invariant

Impacts:
- docs/agent/coding-invariants_new.md
- docs/agent/development-rules_new.md

Do not reintroduce:
- old BridgeBrowserHelper
- old Cloud Code connector

Source evidence:
- docs/development/rules.md
- docs/development/journal.md

## Still unresolved from source decisions

- Cross-tab communication: unresolved.
- Watchdog/recovery: unresolved.
