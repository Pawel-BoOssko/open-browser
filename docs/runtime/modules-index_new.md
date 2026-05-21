# Modules Index

Purpose:
Provides the single registry of documented OpenBrowser/OpenBridge modules, executors, command namespaces, and active tool integrations.

Depends on:
- docs/templates/document-header-template_new.md
- docs/agent/documentation-update-checklist_new.md
- docs/templates/module-docs-template_new.md
- docs/templates/executor-docs-template_new.md
- docs/agent/current-state_new.md
- docs/design/session-rhythm.md
- docs/design/help-system.md

Impacts:
- docs/runtime/hst-help_new.md
- docs/runtime/hst-status_new.md
- docs/runtime/start-prompt_new.md
- docs/executors/*/index_new.md
- docs/modules/*/index_new.md
- docs/agent/current-state_new.md
- tool integration documentation

Update rule:
Update this file when a documented module, executor, command namespace, tool integration, status, or docs path is added, removed, renamed, or reclassified.

Latest:
- Active tool integrations are reflected here.
- This file is only a registry.
- It stores module/tool name, type, status, and docs path.
- It must not store common documentation rules or full tool manuals.
- Common module and executor documentation rules live in templates and documentation checklists.
- Detailed tool instructions live in tools/* README and working-guide files.
- SessionRhythm is planned/pending according to design sources, not confirmed active implementation.

## Registry fields

Module name:
Short module, executor, command namespace, or tool integration name.

Type:
One of: executor, runtime-module, host-command-namespace, adapter, tool-integration.

Status:
Short operational status, for example: active, planned, existing, experimental.

Docs path:
Path to the module, executor, or tool documentation folder.

## Registry

| Module name | Type | Status | Docs path |
|---|---|---|---|
| HST | host-command-namespace | active | docs/executors/HST/ |
| PS | executor | active | docs/executors/PS/ |
| PY | executor | active | docs/executors/PY/ |
| ConversationTrimmer | runtime-module | existing | docs/modules/ConversationTrimmer/ |
| Downloads | runtime-module | active | docs/modules/Downloads/ |
| Humanizer | runtime-module | active | docs/modules/Humanizer/ |
| PageTap | runtime-module | active | docs/modules/PageTap/ |
| SessionRhythm | runtime-module | planned | docs/modules/SessionRhythm/ |
| Claude | tool-integration | active | tools/claude/ |
| GitHub | tool-integration | active | tools/github/ |
| LinkedIn | tool-integration | active | tools/linkedin/ |
| n8n | tool-integration | active | tools/n8n/ |
| Web | tool-integration | active | tools/web/ |

## Registry rule

The registry lists documented modules, executors, command namespaces, and active tool integrations.
It does not define their common documentation structure and does not duplicate tool manuals.

Common structure belongs in:
- docs/templates/module-docs-template_new.md
- docs/templates/executor-docs-template_new.md
- docs/agent/documentation-update-checklist_new.md

Tool usage belongs in:
- tools/*/README.md
- tools/*/*_WORKING_GUIDE.md