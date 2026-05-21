# Module Documentation Template

Purpose:
Defines the required documentation structure for every OpenBrowser/OpenBridge runtime module.

Depends on:
- docs/templates/document-header-template_new.md
- docs/runtime/modules-index_new.md
- docs/agent/documentation-update-checklist_new.md

Impacts:
- docs/runtime/modules-index_new.md
- docs/modules/*/index_new.md
- docs/modules/*/help_new.md
- docs/modules/*/rules_new.md
- docs/agent/documentation-update-checklist_new.md

Update rule:
Update this file when the required documentation structure for runtime modules changes.

Latest:
- Every documented runtime module has one folder under docs/modules/<ModuleName>/.
- Every documented runtime module folder contains exactly these base files: index_new.md, help_new.md, rules_new.md.
- The module registry stores only module name, type, status, and docs path.
- Common module documentation rules live here, not in the registry.

## Required module folder

```text
docs/modules/<ModuleName>/
  index_new.md
  help_new.md
  rules_new.md
```

## index_new.md

Purpose:
Describes what the module is, what responsibility it owns, where it fits in OpenBrowser/OpenBridge, and which other docs must be checked when the module changes.

Must contain:
- document header;
- module responsibility;
- module boundaries;
- related files or runtime areas;
- commands or events exposed by the module, if any;
- dependencies and impacts.

## help_new.md

Purpose:
Contains help content that can be exposed to a model or developer agent when this module has a help command or user-visible instruction.

Must contain:
- document header;
- concise operational help;
- commands, events, or usage instructions if applicable;
- links to rules_new.md when behavior has constraints.

## rules_new.md

Purpose:
Contains module-specific rules, invariants, limitations, and update obligations.

Must contain:
- document header;
- module-specific rules;
- known limitations;
- documentation update triggers;
- cross-document consistency notes.

## Module registry rule

When a module folder is created, removed, renamed, activated, deactivated, or reclassified, update:

- docs/runtime/modules-index_new.md