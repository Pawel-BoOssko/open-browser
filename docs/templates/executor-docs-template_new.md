# Executor Documentation Template

Purpose:
Defines the required documentation structure for every OpenBrowser/OpenBridge executor or command namespace.

Depends on:
- docs/templates/document-header-template_new.md
- docs/runtime/modules-index_new.md
- docs/agent/documentation-update-checklist_new.md

Impacts:
- docs/runtime/modules-index_new.md
- docs/executors/*/index_new.md
- docs/executors/*/help_new.md
- docs/executors/*/rules_new.md
- docs/agent/documentation-update-checklist_new.md
- docs/agent/coding-invariants_new.md

Update rule:
Update this file when the required documentation structure for executors or command namespaces changes.

Latest:
- Every documented executor or command namespace has one folder under docs/executors/<ExecutorName>/.
- Every documented executor folder contains exactly these base files: index_new.md, help_new.md, rules_new.md.
- Executor-specific rules belong in rules_new.md.
- Help returned by executor HELP commands should be based on help_new.md.

## Required executor folder

```text
docs/executors/<ExecutorName>/
  index_new.md
  help_new.md
  rules_new.md
```

## index_new.md

Purpose:
Describes what the executor or command namespace is responsible for, which commands it owns, and how it fits into OpenBridge command routing.

Must contain:
- document header;
- executor responsibility;
- command namespace or command list;
- input and output expectations;
- related runtime areas;
- dependencies and impacts.

## help_new.md

Purpose:
Contains help content that can be returned by the executor help command, for example PS_HELP, PY_HELP, HST_HELP, FS_HELP, DL_HELP, TAB_HELP, or LOG_HELP.

Must contain:
- document header;
- concise operational help;
- supported commands;
- expected payload format;
- key constraints;
- pointer to rules_new.md for executor-specific rules.

## rules_new.md

Purpose:
Contains executor-specific invariants, safety rules, limitations, encoding rules, timeout expectations, output rules, and documentation update obligations.

Must contain:
- document header;
- executor-specific invariants;
- known limitations;
- output and error conventions;
- documentation update triggers;
- cross-document consistency notes.

## Executor registry rule

When an executor folder is created, removed, renamed, activated, deactivated, or reclassified, update:

- docs/runtime/modules-index_new.md

## Help ownership rule

Host help describes Host commands and available executors.
Executor help describes that executor.
Do not keep full executor help inside Host help.