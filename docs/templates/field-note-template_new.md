# Field Note Template

Purpose:
Defines the format for operational observations written by the Operator or by the model working inside OpenBrowser.

Depends on:
- docs/templates/document-header-template_new.md
- docs/feedback/field-notes_new.md
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/documentation-update-checklist_new.md

Impacts:
- docs/feedback/field-notes_new.md
- docs/agent/documentation-update-checklist_new.md
- docs/agent/commit-rules_new.md
- developer-agent triage work

Update rule:
Update this file when the required format for field notes changes.

Latest:
- Field notes are not technical documentation.
- Field notes are raw operational observations from real use.
- The developer agent later triages field notes into bugs, tasks, documentation updates, architecture questions, or rejected items.
- Field notes should be short and written close to the moment when the observation happens.

## Required field note entry

```text
## YYYY-MM-DD HH:mm | <source> | <type> | <status>

Context:
<what the Operator or model was trying to do>

Observation:
<what happened or what was noticed>

Impact:
<why it matters, what work was blocked, slowed down, or made unclear>

Expected:
<what should happen instead, if known>

Links or files:
- <optional paths, logs, downloads, screenshots, commands>
```

## Allowed source values

- operator
- openbrowser-model
- developer-agent

## Allowed type values

- problem
- failure
- missing-capability
- confusing-instruction
- manual-workaround
- idea
- question

## Allowed status values

- new
- triaged
- converted-to-task
- needs-architect-decision
- done
- rejected

## Triage rule

A developer agent must check field notes before larger work and before recommending COMMIT when the change may relate to open observations.