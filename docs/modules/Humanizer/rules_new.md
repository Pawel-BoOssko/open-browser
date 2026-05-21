# Humanizer Module Rules

Purpose:
Defines rules, limitations, and update obligations for Humanizer response wording variation.

Depends on:
- docs/templates/document-header-template_new.md
- docs/modules/Humanizer/index_new.md
- docs/modules/Humanizer/help_new.md
- docs/agent/current-state_new.md

Impacts:
- docs/modules/Humanizer/index_new.md
- docs/modules/Humanizer/help_new.md
- docs/runtime/hst-help_new.md
- docs/runtime/start-prompt_new.md
- docs/agent/documentation-update-checklist_new.md
- model-facing system responses

Update rule:
Update this file when Humanizer wording rules, response wrapper behavior, timeout wording, error wording, or inter-model response style changes.

Latest:
- Humanizer may vary wording, but must preserve meaning.
- Humanizer must not hide errors, timeouts, failures, or missing capabilities.
- Humanizer must not add unsupported facts.
- Humanizer is not an architecture or routing component.

## Rules

### HUM-RULE-001: Preserve meaning

Humanizer may change wording, but it must not change the technical meaning of the underlying result.

### HUM-RULE-002: Do not invent facts

Humanizer must not add details that are not present in the original result, runtime state, command output, or documented behavior.

### HUM-RULE-003: Keep failures clear

Errors, timeouts, rejected commands, unsupported commands, and failed operations must remain explicit and understandable.

### HUM-RULE-004: Avoid hiding technical evidence

Humanizer must not remove important paths, command names, file names, error codes, timeout values, or status fields when they matter to the task.

### HUM-RULE-005: Use neutral operational wording

Humanizer wording should be natural but not theatrical, promotional, emotional, or misleading.

### HUM-RULE-006: Humanizer does not decide

Humanizer must not choose architecture, route commands, alter executor results, or decide whether an operation succeeded.

## Documentation triggers

Update Humanizer documentation when:
- wrapper wording rules change;
- timeout or error wording changes;
- inter-model communication wording changes;
- command result presentation changes;
- Humanizer boundaries change.