# PS Executor Index

Purpose:
Describes the PowerShell executor and its role in OpenBrowser/OpenBridge command execution.

Depends on:
- docs/templates/document-header-template_new.md
- docs/runtime/modules-index_new.md
- docs/templates/executor-docs-template_new.md
- docs/agent/coding-invariants_new.md
- docs/executors/PS/help_new.md
- docs/executors/PS/rules_new.md

Impacts:
- docs/executors/PS/help_new.md
- docs/executors/PS/rules_new.md
- docs/runtime/hst-help_new.md
- docs/runtime/modules-index_new.md
- docs/agent/coding-invariants_new.md

Update rule:
Update this file when PS executor responsibility, command ownership, payload expectations, output handling, timeout behavior, working directory behavior, or documentation path changes.

Latest:
- PS is the PowerShell executor.
- PS executes PowerShell payloads provided through OpenBridge command envelopes.
- PS must respect explicit UTF-8 rules when writing text files.
- PS-specific operational help belongs in docs/executors/PS/help_new.md.
- PS-specific invariants and limitations belong in docs/executors/PS/rules_new.md.

## Responsibility

PS owns execution of PowerShell commands from OpenBridge.

Current command namespace:
- PS
- PS_HELP

## Boundaries

PS does:
- execute PowerShell payloads;
- return command output, errors, and execution status;
- support local filesystem and shell diagnostics through PowerShell;
- write generated files when instructed.

PS does not:
- decide architecture;
- replace HST_STATUS;
- own Python execution;
- silently ignore encoding requirements.

## Related documents

- docs/executors/PS/help_new.md
- docs/executors/PS/rules_new.md
- docs/agent/coding-invariants_new.md
- docs/runtime/modules-index_new.md