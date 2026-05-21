# Development Rules

Purpose:
Defines how developer agents must work in the OpenBrowser/OpenBridge repository so that technical work leaves the project understandable, documented, testable, and safe for the next agent.

Depends on:
- docs/templates/document-header-template_new.md
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/coding-invariants_new.md
- docs/agent/documentation-update-checklist_new.md
- docs/agent/commit-rules_new.md
- docs/feedback/field-notes_new.md
- docs/development/rules.md
- docs/development/decisions.md
- docs/development/journal.md
- docs/development/README.md

Impacts:
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/documentation-update-checklist_new.md
- docs/agent/commit-rules_new.md
- docs/agent/current-state_new.md
- docs/agent/coding-invariants_new.md
- all developer-agent work performed in this repository

Update rule:
Update this file when required developer-agent workflow, build/test expectations, prohibited actions, documentation duties, or architecture decision handling changes.

Latest:
- Source migration from docs/development/rules.md, decisions.md, journal.md, README.md is reflected here.
- The developer agent must start from current repository state, not memory.
- The developer agent must run git status before work and must understand dirty state before changing files.
- The developer agent is responsible for technical execution and for keeping project knowledge usable by the next developer agent.
- The owner/architect makes architecture decisions; the developer agent records and applies them.
- The developer agent must check documentation impact after changes, not only code correctness.

## Core working rules

1. Work only inside D:\projects\open-browser.
2. Always begin technical work with git status --short.
3. Do not ignore a dirty repository. If there are existing unstaged or untracked changes, identify them before adding new work.
4. Read docs/agent/READ_BEFORE_WORK_new.md when beginning work, before final reports, and before recommending COMMIT.
5. Keep changes scoped to the requested task.
6. Do not reintroduce rejected or stale approaches unless explicitly instructed by the owner/architect.
7. Do not treat old chat history as a source of truth when current repository documents exist.
8. If behavior changes, update the relevant documentation.
9. If a new repeated technical rule appears, add or update a coding invariant.
10. If operational use reveals a problem or missing capability, record or triage it in docs/feedback/field-notes_new.md.
11. If a decision affects architecture or project governance, record it in docs/agent/architecture-decisions_new.md.
12. Before recommending COMMIT, follow docs/agent/commit-rules_new.md.


## Command simplicity rule

Prefer simple, direct, single-purpose commands.

Use a small sequence:
- inspect;
- write;
- verify.

Do not replace a simple mkdir, dir, copy, Get-Content, Set-Content, or Get-Item command with a large defensive PowerShell block unless the task actually requires it.

If payload escaping becomes fragile, use RAW payload rather than adding complex quoting or escaping logic.
## Build and validation rules

After every change to .cs files:
- run dotnet build.

Known warning:
- MSB3277 WindowsBase can be ignored only if there are 0 errors.

After changes to OpenBridgeProtocol:
- run dotnet run --project tests/OpenBridgeProtocolSmoke/OpenBridgeProtocolSmoke.csproj.

After changes to OpenBridgeHost or executors:
- run dotnet run --project tests/OpenBridgeHostSmoke/OpenBridgeHostSmoke.csproj.

Full build command:
- dotnet build D:\projects\open-browser\releases\BridgeBrowserAlpha0\src\BridgeBrowserAlpha0\BridgeBrowserAlpha0.csproj

## Commit rules summary

Detailed commit rules live in docs/agent/commit-rules_new.md.

Working rules:
- keep commits small, one logical package per commit;
- do not amend other people's commits;
- do not use --no-verify;
- do not commit files from config/local/;
- never run git push unless explicitly instructed.

## Removed feature rule

When a feature is removed, every trace of that removed feature must be erased from the working tree.

This includes:
- code;
- docs;
- handoffs;
- journals;
- test plans;
- README files;
- comments;
- config;
- variable names;
- file names.

Do not keep deprecated comments, old markers, commented-out code, _old suffixes, or explanatory ghosts inside active files.
Git history is the record of what was removed.

## Prohibitions

Never do these without explicit user approval:
- run git push;
- install packages;
- restore the old BridgeBrowserHelper;
- restore the old Cloud Code connector;
- create new layers, services, managers, registries, factories, dispatchers, or queues;
- design for unapproved future mechanisms such as watchdog, cross-tab, or multi-tab registry unless the task explicitly requires it;
- work outside D:\projects\open-browser;
- insert emoji into code;
- leave traces of removed features in active files.

## Architecture decision rule

When an architecture decision is needed:
- stop;
- present the decision clearly;
- wait for the owner/architect.

After a decision is made:
- record it in docs/agent/architecture-decisions_new.md;
- update affected docs listed in Impacts;
- do not silently convert unresolved architecture questions into implementation choices.

## Documentation responsibility

The developer agent must leave the project in a state where another developer agent can continue work from repository knowledge without reconstructing history from chat.

Relevant source documents for this migration batch:
- docs/development/rules.md
- docs/development/decisions.md
- docs/development/journal.md
- docs/development/README.md