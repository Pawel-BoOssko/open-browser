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
