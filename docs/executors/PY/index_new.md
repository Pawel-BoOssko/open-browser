# PY Executor Index

Purpose:
Describes the Python executor and its role in OpenBrowser/OpenBridge command execution.

Depends on:
- docs/templates/document-header-template_new.md
- docs/runtime/modules-index_new.md
- docs/templates/executor-docs-template_new.md
- docs/executors/PY/help_new.md
- docs/executors/PY/rules_new.md

Impacts:
- docs/executors/PY/help_new.md
- docs/executors/PY/rules_new.md
- docs/runtime/hst-help_new.md
- docs/runtime/modules-index_new.md
- docs/agent/coding-invariants_new.md

Update rule:
Update this file when PY executor responsibility, command ownership, payload expectations, output handling, timeout behavior, working directory behavior, or documentation path changes.

Latest:
- PY is the Python 3 executor.
- PY executes Python payloads provided through OpenBridge command envelopes.
- PY should be used when Python is a better fit than PowerShell, for example JSON validation, text processing, NDJSON analysis, and structured file operations.
- PY-specific operational help belongs in docs/executors/PY/help_new.md.
- PY-specific invariants and limitations belong in docs/executors/PY/rules_new.md.

## Responsibility

PY owns execution of Python 3 commands from OpenBridge.

Current command namespace:
- PY
- PY_HELP

## Boundaries

PY does:
- execute Python payloads;
- perform structured text and JSON processing;
- analyze logs and NDJSON;
- create or transform files when instructed;
- return output, errors, and execution status.

PY does not:
- decide architecture;
- replace HST_STATUS;
- own PowerShell execution;
- bypass documentation update rules.

## Related documents

- docs/executors/PY/help_new.md
- docs/executors/PY/rules_new.md
- docs/runtime/modules-index_new.md
- docs/agent/coding-invariants_new.md