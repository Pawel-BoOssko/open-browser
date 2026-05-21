# OpenBrowser Handoff for AntiGravity - 2026-05-21

## Read this first

The authoritative entrypoint for repository rules is:

- docs/agent/READ_BEFORE_WORK_new.md

Do not treat older README, handoff, test-plan, design, or development documents as source of truth unless the new documentation explicitly points to them as active operational references.

## Current repository state

Recent documentation governance work is complete and committed. The current documentation system includes:

- docs/agent/READ_BEFORE_WORK_new.md: mandatory entrypoint for developer agents;
- docs/agent/current-state_new.md: current repository truth;
- docs/agent/development-rules_new.md: developer workflow rules;
- docs/agent/coding-invariants_new.md: technical invariants;
- docs/agent/commit-rules_new.md: commit gate;
- docs/agent/folder-policy_new.md plus FOLDER.md files: folder governance;
- docs/feedback/legacy-docs-map_new.md: map of old documentation to current targets.

All old tracked markdown documentation files have a legacy preamble. If an old file conflicts with new docs, use the new docs.

## Important correction: temporary rules are not architecture

During the prior CloudCode work, a temporary instruction was used to stop uncontrolled feature expansion. That instruction must not be treated as a permanent architecture decision.

Do not preserve or reintroduce these as permanent project rules:

- the prior temporary feature-freeze instruction;
- the prior numbered stop-gap rule used to stop uncontrolled expansion;
- the prior instruction about a separate working copy;
- any path-specific separate-instance rule as a current architecture requirement.

The real standing rule is narrower: keep work scoped to the requested task, do not introduce unrelated features, and record real architecture decisions in docs/agent/architecture-decisions_new.md.

## Dirty working tree to inspect

At handoff time, the following non-documentation items may remain outside the committed documentation work:

- releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/PortalScanner.js: modified scanner code from the portal-document-download work;
- releases/BridgeBrowserAlpha0/downloads/*: runtime/download output, not source by default;
- test_download.html: likely old test artifact.

Do not assume these belong in the next commit. Inspect and classify them first.

## AntiGravity task 1: inventory and separate project directions

Run a fresh repository inventory and determine how to separate two directions conceptually and technically:

1. OpenBrowser: general local browser/runtime for LLM-assisted work;
2. Otwarty Portal Sądowy: possible separate project for automatic document download from Portal Informacyjny Sądów Powszechnych.

This handoff does not decide how to split them. AntiGravity should inspect the repository, code, docs, current dirty files, and user goals, then propose the split.

Important: do not move PortalScanner.js, do not create a new project, and do not rename products until the split plan is explicit and accepted.

## AntiGravity task 2: plan user runnable build for Paweł

Prepare only a plan for a clearly distinguishable user-runnable build for Paweł, created on explicit owner request.

The plan should address:

- separate name or launch identity;
- separate output folder or clearly distinguishable build path;
- how to avoid confusing user-running instance with active development instance;
- whether the build can be based on current development code;
- what command or script should create it.

Do not implement this plan until accepted.

## Portal scanner factual status from prior work

Current portal scanner direction is about automatic document download from Portal Informacyjny Sądów Powszechnych.

Known reported facts from the prior handoff:

- current-page row expansion was reported as working;
- source and PDF downloads per document were reported as partially or mostly working;
- WebView2 DownloadStarting saves downloaded files;
- auto-pagination was reported as the blocker;
- Angular Material paginator did not respond to the attempted click/event-dispatch sequence;
- download menu items may render in an overlay outside the table row, so row-scoped selectors may be unreliable.

Treat these as investigation notes, not final architecture.

## What not to do next

- Do not use old docs as primary truth before reading docs/agent/READ_BEFORE_WORK_new.md;
- do not revive HST_TOOLS or payload64 references;
- do not turn temporary stop-gap instructions into permanent rules;
- do not mix PortalScanner code cleanup with documentation-only commits;
- do not commit downloaded court/portal files unless there is an explicit evidence-retention decision.

## Suggested first AntiGravity prompt

Read docs/agent/READ_BEFORE_WORK_new.md, then read this handoff. Perform an inventory-only pass. Do not modify code. Classify the dirty working tree, identify temporary-rule pollution if any remains, and propose a clean plan for separating OpenBrowser from the portal-document-download direction and for designing a user-runnable build process for Paweł. Do not implement the split or build process until accepted.

---

## Critical fix applied 2026-05-21 (commit df3fb73)

**Problem:** OpenBrowser became extremely slow — executor hanging, envelope detection failing, app.ndjson growing to 476 MB within hours.

**Root cause:** `NetworkLogger.OnLoadingFinished` was calling `Network.getResponseBody` for EVERY network request and:
1. Logging the full body (JavaScript files from chatgpt.com SPA = hundreds of MB)
2. Forwarding bodies to `ResponseExtractor.AddRaw("cdp_response_body")` — causing thousands of false `extraction_final` events

**Fix:** Changed `NetworkLogger.cs:88-90` to log only `bodyLength` metadata instead of full body, and removed the `_extractor.AddRaw()` call for CDP response bodies. WebSocket frames and other CDP events are unaffected.

**Verification:** After fix, app.ndjson stays small (no JS file bodies), extractor no longer receives CDP noise, envelope detection works reliably.

## Additional maintenance note

`PipelineRawDump.cs` was deleted from the working tree (unstaged). It was restored via `git restore`. If build fails with "PipelineRawDump does not exist", restore it.

## gpt-5-5-thinking channel bug (identified 2026-05-21, not yet fixed)

**Problem:** Envelopes not detected when using gpt-5-5-thinking model. HST commands work but PS/PY commands fail.

**Root cause:** `ResponseExtractor.cs` line 414:
```csharp
if (!channel.Equals("final", StringComparison.OrdinalIgnoreCase)) return;
```
gpt-5-5-thinking sends final messages with `channel: null` instead of `channel: "final"`. The extractor silently discards them. Only messages with explicit `channel: "final"` are processed.

**Fix:** One line change — accept null/empty channel in addition to "final". Messages with explicit non-final channels (e.g. `channel: "thinking"`) will still be filtered.

---

## EMERGENCY NOTES — added here because proper location unknown

> **Ta informacja została dopisana w trybie awaryjnym, w złym miejscu i należy ją umieścić w dobrym miejscu. Autor (Cloud Code) nie wiedział jakie to dobre miejsce. Agent obsługujący: umieść to w odpowiednim pliku docelowym.**

### Emergency item 1: Start prompt wording

Start prompt (`config/start-prompt.txt`) currently says "Start by discovering your environment. Send this as your first action: HST_HELP". This is too prescriptive — it forces the model to use HST_HELP even when it already knows the environment. Should be reworded to: "If you don't know how to use OpenBridge, or if you're unsure what to do, use HST_HELP. If you already know the environment, proceed directly to your task."

### Emergency item 2: Help text rotation / randomization

The model receives identical help text every time (HST_HELP, PS_HELP, PY_HELP return the same output). This is problematic when the model is stuck in a retry loop — it gets bombarded with identical information. Solution: rotate or randomize segment order in help outputs, or add variations so consecutive calls don't return verbatim identical text.

---

## Backlog / Next Work Items for AntiGravity

1. Implement help command routing in code

   Make OpenBridge help commands display the documentation files defined by the current docs:

   - HST_HELP reads docs/runtime/hst-help_new.md;
   - PS_HELP reads docs/executors/PS/help_new.md;
   - PY_HELP reads docs/executors/PY/help_new.md;
   - LINKEDIN_HELP reads tools/linkedin/README.md;
   - GITHUB_HELP reads tools/github/README.md;
   - N8N_HELP reads tools/n8n/README.md;
   - WEB_HELP reads tools/web/README.md;
   - CLAUDE_HELP reads tools/claude/README.md.

   Each help response must start by stating the exact source file it displays.

2. Investigate OpenBridge text transport and encoding

   Use docs/feedback/antigravity-text-transport-task_new.md as the task definition.

   Investigate the full text transport path in both directions:

   - model -> OpenBridge -> executor;
   - executor -> OpenBridge -> model.

   This is an architecture-level transport problem, not a local PowerShell workaround task.

3. Review the LinkedIn tool implementation

   Inspect tools/linkedin and related code paths.

   Determine:
   - what is implemented;
   - what scripts are actually used;
   - how results are returned to the model;
   - whether image-posting code belongs in the current tool structure;
   - what should be documented, changed, removed, or stabilized.

4. Perform code inventory

   Classify the current codebase by real function, not by folder name alone:

   - executors;
   - tools;
   - runtime modules;
   - UI code;
   - diagnostics;
   - tests;
   - working artifacts;
   - external-service integrations.

5. Separate OpenBrowser/OpenBridge from portal-specific work conceptually

   Identify which parts belong to the general OpenBrowser/OpenBridge runtime and which parts belong to the portal-document-download direction.

   Do not move or refactor code yet. Produce an inventory and separation plan first.

6. Plan the separate "Otwarty Portal Sądowy" direction

   Prepare a plan for treating automatic document download from Portal Informacyjny Sądów Powszechnych as a separate project direction.

   The plan should explain what should eventually belong there and how to keep it separate from the main OpenBrowser/OpenBridge runtime.

7. Plan a user-runnable build process for Paweł

   Propose a process for producing a clearly distinguishable working build for Paweł.

   Requirements:
   - created only on explicit request;
   - may be based on the current development branch;
   - clearly distinguishable from development execution;
   - suitable for real work, not necessarily a perfectly stable release.

8. Verify documentation integration with runtime behavior

   Check whether the application actually uses the updated documentation files for help output.

   The goal is to eliminate hardcoded stale help text and make runtime help match the current documentation.

9. Review and classify the current dirty working tree

   Classify remaining untracked or modified files, especially:
   - releases/BridgeBrowserAlpha0/downloads/*;
   - test_download.html;
   - tools/linkedin/__pycache__/;
   - tools/linkedin/exports/*;
   - tools/linkedin/linkedin_post_image.py;
   - handoff.md if still modified.

   Decide what is a working artifact, generated output, real code change, documentation change, or cleanup candidate.

---

## CRITICAL — OpenBrowser practically unusable with gpt-5-5-thinking (2026-05-21)

### Priority

**This is the most urgent issue.** Higher than any feature, portal scanner, documentation, or anything else. OpenBrowser's core function — executing commands from the model — is broken.

### Symptoms observed

- Model's response appears in the chat UI but the envelope is not detected by OpenBridge.
- Status bar shows "OK" (previous command completed), output panel shows stale previous result.
- Sometimes the first command (HST_HELP) works, but subsequent PS/PY commands fail silently.
- Sometimes HST commands work but PS/PY commands do not.
- Appears to work once after Start Prompt injection, then stops.
- User reports: "OpenBrowser jest praktycznie bezużyteczny."

### Difficulty of reproduction

Intermittent. The same envelope sometimes works, sometimes doesn't. No clear pattern for when it fails vs succeeds. The failure rate is high enough to make work impossible.

### Changes attempted (all committed)

1. **CDP body logging disabled** (commit df3fb73) — `NetworkLogger.cs` was flooding the extractor with JavaScript file bodies (476 MB log files). Fixed by logging only bodyLength instead of full body, and removing the `_extractor.AddRaw()` call for CDP response bodies. Result: logs stay small, extractor no longer gets CDP noise. But the core problem persists.

2. **Channel filter relaxed** (commit 0ee5977) — `ResponseExtractor.cs` line 414 required `channel == "final"`. gpt-5-5-thinking sends messages with `channel: null`. Fixed to accept null/empty channel, only rejecting explicitly non-final channels (like "thinking"). Result: first two responses work now, but subsequent ones still fail.

3. **SSE stream capture from fetch** (commit e0f8d19) — gpt-5-5-thinking delivers responses via SSE through `window.fetch`. PageTap did not read response bodies — it only saw the request start/end. Added: response cloning + SSE parsing for all conversation fetch requests (GET and POST). SSE records forwarded to extractor via `page_fetch_sse_record`, completion via `page_fetch_sse_done` triggering `Finish()`. WebViewMessageHandler updated to route these events. Result: no change in behavior observed by user.

