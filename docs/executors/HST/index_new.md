# HST Executor Index

Purpose:
Describes the Host-level HST command namespace and its role in OpenBrowser/OpenBridge self-discovery, runtime status, and command routing boundaries.

Depends on:
- docs/templates/document-header-template_new.md
- docs/runtime/modules-index_new.md
- docs/runtime/hst-help_new.md
- docs/runtime/hst-status_new.md
- docs/templates/executor-docs-template_new.md
- docs/runtime/environment.md
- config/start-prompt.txt
- docs/design/help-system.md

Impacts:
- docs/executors/HST/help_new.md
- docs/executors/HST/rules_new.md
- docs/runtime/hst-help_new.md
- docs/runtime/hst-status_new.md
- docs/runtime/start-prompt_new.md
- docs/runtime/modules-index_new.md

Update rule:
Update this file when HST command ownership, Host command routing, Host self-discovery, HST_HELP, HST_STATUS, executor discovery, RAW payload guidance, or Host boundaries change.

Latest:
- Source migration from docs/runtime/environment.md, config/start-prompt.txt, and docs/design/help-system.md is reflected here.
- HST is documented as a Host command namespace.
- HST owns Host-level commands, not executor-specific manuals.
- HST_HELP exposes Host commands and available executors.
- HST_STATUS exposes current runtime state.
- HST is listed in docs/runtime/modules-index_new.md as host-command-namespace.

## Responsibility

HST owns Host-level discovery and status commands.

Current commands:
- HST_HELP
- HST_STATUS

Related help commands exposed by the system:
- PS_HELP
- PY_HELP

## Boundaries

HST does:
- describe Host commands;
- expose runtime status;
- point models to available executors;
- support model self-discovery;
- describe automatic bridge features at a high level.

HST does not:
- contain full PS or PY manuals;
- execute PowerShell or Python payloads;
- replace executor-specific HELP commands;
- replace repository documentation as source of project truth.

## Related documents

- docs/runtime/hst-help_new.md
- docs/runtime/hst-status_new.md
- docs/runtime/start-prompt_new.md
- docs/runtime/modules-index_new.md
- docs/executors/HST/help_new.md
- docs/executors/HST/rules_new.md
- docs/runtime/environment.md
- docs/design/help-system.md