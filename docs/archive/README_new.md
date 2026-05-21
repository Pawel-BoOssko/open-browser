# Archive

Purpose:
Defines the rule for material moved outside active OpenBrowser/OpenBridge documentation.

Depends on:
- docs/templates/document-header-template_new.md
- docs/agent/architecture-decisions_new.md
- docs/agent/documentation-update-checklist_new.md

Impacts:
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/current-state_new.md
- docs/agent/documentation-update-checklist_new.md
- developer-agent repository navigation

Update rule:
Update this file when the rule for removed, replaced, or non-active project material changes.

Latest:
- Active documentation does not use Draft, Deprecated, or Archived statuses.
- A file in docs/archive is not active project truth.
- Developer agents must not read docs/archive as normal working context unless explicitly instructed.
- If archived material becomes relevant again, create or update an active documentation file instead of treating archive content as current.

## Archive rule

The archive folder may contain old, replaced, or non-active materials kept only for reference.

Archive content is not part of active documentation governance.

## Developer-agent rule

Do not use docs/archive as a source of current project truth.
Use active docs instead:
- docs/agent/current-state_new.md
- docs/agent/architecture-decisions_new.md
- docs/runtime/modules-index_new.md
- relevant executor or module documentation