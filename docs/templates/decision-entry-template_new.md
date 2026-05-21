# Decision Entry Template

Purpose:
Defines the format for architecture and project-governance decisions recorded by developer agents after discussion with the owner/architect.

Depends on:
- docs/templates/document-header-template_new.md
- docs/agent/architecture-decisions_new.md
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/documentation-update-checklist_new.md

Impacts:
- docs/agent/architecture-decisions_new.md
- docs/agent/current-state_new.md
- docs/agent/development-rules_new.md
- docs/agent/coding-invariants_new.md
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
Update this file when the required format for recorded project decisions changes.

Latest:
- Decisions must record what was decided, why, what it affects, and what must not be reintroduced.
- A decision entry is not a transcript. It is the current project rule resulting from the discussion.
- Newest decisions should stay near the top of architecture-decisions_new.md.

## Required decision entry

```text
## YYYY-MM-DD — <short decision title>

Decision:
<what is now decided>

Reason:
<why this decision was made>

Applies to:
- <component, module, executor, docs area, workflow>

Impacts:
- <documents or runtime areas that must be checked>

Do not reintroduce:
- <old approach or rejected assumption, if relevant>

Status:
accepted
```

## Usage rule

Use this template when the owner/architect makes or confirms a project decision that affects architecture, documentation governance, module boundaries, command behavior, executor behavior, runtime behavior, or future development rules.