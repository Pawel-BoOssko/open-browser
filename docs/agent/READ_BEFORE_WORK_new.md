# Read Before Work

Purpose:
Defines the mandatory entrypoint for every developer agent before code changes, documentation changes, commit recommendations, final reports, or architecture-impacting work.

Depends on:
- docs/templates/document-header-template_new.md
- docs/agent/current-state_new.md
- docs/agent/development-rules_new.md
- docs/agent/coding-invariants_new.md
- docs/agent/documentation-update-checklist_new.md
- docs/agent/commit-rules_new.md
- docs/feedback/field-notes_new.md

Impacts:
- docs/agent/documentation-update-checklist_new.md
- docs/agent/commit-rules_new.md
- docs/agent/current-state_new.md
- docs/agent/coding-invariants_new.md
- docs/feedback/field-notes_new.md
- all developer-agent work performed in this repository

Update rule:
Update this file when the mandatory pre-work sequence for developer agents changes.

Latest:
- This file is the single trigger document for developer agents.
- The owner/architect can point an agent to this file instead of repeating project rules manually.
- Reading this file must start the documentation and consistency check chain.
- A developer agent must not recommend COMMIT before applying the checks listed here.

## Required pre-work sequence

Before starting any code change, documentation change, architecture-impacting task, final report, or commit recommendation, read and apply:

1. docs/agent/current-state_new.md
2. docs/agent/development-rules_new.md
3. docs/agent/coding-invariants_new.md
4. docs/agent/documentation-update-checklist_new.md
5. docs/agent/commit-rules_new.md
6. docs/feedback/field-notes_new.md
7. docs/agent/folder-policy_new.md

## Required response after reading

State briefly:

- what applies to the current task;
- whether any invariant may be affected;
- whether documentation may need updating;
- whether field notes contain relevant open observations;
- whether the task can proceed.

## Commit gate

Before recommending COMMIT, explicitly report:

- Documentation impact: yes/no
- Documentation files updated: list or none
- Invariants checked: yes
- Field notes checked: yes
- Commit allowed by commit-rules: yes/no

If any answer is missing, do not recommend COMMIT.

<!-- COMPLETE_DOC_UPDATE_REQUIRED_DOCS_20260521 -->
## Additional required documentation after 2026-05-21 help and transport update

Before work that touches OpenBridge command behavior, help output, executors, tools, external services, folder policy, documentation governance, or model-facing instructions, also read:

8. docs/agent/help-command-routing_new.md
9. docs/agent/model-feedback-policy_new.md
10. docs/agent/external-tool-policy_new.md

For text transport and encoding architecture work, also read:

11. docs/feedback/antigravity-text-transport-task_new.md

These documents are part of the active developer-agent documentation set.
<!-- /COMPLETE_DOC_UPDATE_REQUIRED_DOCS_20260521 -->
