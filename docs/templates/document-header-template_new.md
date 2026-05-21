# Document Header Template

Purpose:
Defines the mandatory header format used at the top of every active OpenBrowser/OpenBridge documentation file.

Depends on:
- docs/agent/documentation-update-checklist_new.md
- docs/agent/commit-rules_new.md
- docs/templates/module-docs-template_new.md
- docs/templates/executor-docs-template_new.md
- docs/templates/decision-entry-template_new.md
- docs/templates/field-note-template_new.md

Impacts:
- all active OpenBrowser/OpenBridge documentation files

Update rule:
Update this file when the mandatory document header format changes.

Latest:
- Active documentation files use this header:
  - Purpose
  - Depends on
  - Impacts
  - Update rule
  - Latest
- Do not use Status, Draft, Deprecated or Archived states.
- In this project, a documentation file exists and is active, or it is removed.
- Depends on and Impacts must be maintained bidirectionally.
- Newest and most important information must stay near the top of each document.

## Required header

```text
Purpose:
<why this document exists>

Depends on:
- <documents this document derives from>

Impacts:
- <documents that must be checked when this document changes>

Update rule:
<when this document must be updated>

Latest:
- <newest/current information first>
```

## Field meanings

### Purpose
One sentence explaining why the document exists.

### Depends on
Documents that define or constrain this document.

### Impacts
Documents that must be checked after this document changes.

### Update rule
The condition that triggers an update of this document.

### Latest
Newest decisions, rules or changes. Keep newest information first.