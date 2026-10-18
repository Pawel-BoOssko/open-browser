# Folder Policy

Purpose: Defines how folders in the OpenBrowser/OpenBridge repository are created, named, documented, and validated.

Depends on:
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/current-state_new.md
- docs/agent/development-rules_new.md
- docs/agent/coding-invariants_new.md
- docs/agent/commit-rules_new.md

Impacts:
- FOLDER.md
- local FOLDER.md files
- scripts/check-agent-docs.ps1
- all future folder creation in the repository

Update rule: Update this file when root folder allowlist, local FOLDER.md format, inheritance rules, or folder-validation behavior changes.

Latest:
- 2026-10-18: src/ is an existing local source relocation root; scripts/check-agent-docs.ps1 now recognizes it and requires src/FOLDER.md. This change records the current worktree layout, without approving or completing the code migration.
- Folder policy is part of developer-agent governance.
- Every folder must be covered by its own FOLDER.md or inherited parent policy.
- New root folders require policy update or architect approval.

## Core rule

Every folder must have an applicable policy: local FOLDER.md or inherited parent FOLDER.md.

## Local FOLDER.md rule

Use FOLDER.md at responsibility boundaries, not mechanically in every child folder.

Required sections: Purpose, Allowed, Forbidden, Child folder rule, Update rule, Parent policy.

## Commit rule

DO NOT COMMIT if a change introduces an undocumented root folder or a new responsibility boundary without folder-policy coverage.
