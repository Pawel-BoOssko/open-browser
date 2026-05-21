# HST Executor Rules

Purpose:
Defines rules and constraints for Host-level HST commands and self-discovery behavior.

Depends on:
- docs/templates/document-header-template_new.md
- docs/executors/HST/index_new.md
- docs/executors/HST/help_new.md
- docs/runtime/hst-help_new.md
- docs/runtime/hst-status_new.md
- docs/runtime/start-prompt_new.md
- docs/agent/architecture-decisions_new.md
- docs/runtime/environment.md
- config/start-prompt.txt
- docs/design/help-system.md

Impacts:
- docs/executors/HST/index_new.md
- docs/executors/HST/help_new.md
- docs/runtime/hst-help_new.md
- docs/runtime/hst-status_new.md
- docs/runtime/start-prompt_new.md
- model self-discovery inside OpenBrowser

Update rule:
Update this file when HST command behavior, HST_HELP, HST_STATUS, self-discovery flow, active command marker behavior, RAW payload guidance, or Host-level command boundaries change.

Latest:
- Source migration from runtime environment, start prompt, and help-system design docs is reflected here.
- HST help must stay short and Host-focused.
- HST help points to executor-specific HELP commands instead of duplicating executor manuals.
- HST_STATUS reports runtime state and evidence, not full documentation.
- HST commands must support model self-discovery without loading full project context upfront.
- Active command markers execute; do not reproduce them unless intentionally executing.

## Rules

### HST-RULE-001: Host help stays Host-focused

HST_HELP describes Host commands, available executors, automatic bridge features, and the next help command to call.
It must not become a full manual for PS, PY, or future executors.

### HST-RULE-002: Executor help belongs to executors

Executor-specific help belongs in executor help files and executor HELP commands.
Examples:
- PS details belong in PS_HELP and docs/executors/PS/help_new.md.
- PY details belong in PY_HELP and docs/executors/PY/help_new.md.

### HST-RULE-003: HST_STATUS is status, not help

HST_STATUS should report current runtime state, paths, identity, recent downloads, available tools, pending command, and last result.
It should not duplicate manuals or long architectural explanations.

### HST-RULE-004: Self-discovery starts small

A model entering OpenBrowser should start with HST_HELP and HST_STATUS, then call executor-specific HELP commands only when needed.

### HST-RULE-005: Host output must not leave the model without a controlled response

If a Host command cannot complete, it should return a clear error or timeout response rather than silence.

### HST-RULE-006: Active command markers are execution triggers

If a model writes a valid OpenBridge command envelope, the first valid block is executed.
There is no display-only mode for active command envelopes.
Do not reproduce command markers unless intentionally executing a command.

### HST-RULE-007: RAW payload belongs inside payload

For long or quoting-sensitive commands, use the RAW payload mechanism described in docs/runtime/environment.md.
RAW markers must be inside the payload JSON string. RAW outside the JSON object makes the envelope invalid.