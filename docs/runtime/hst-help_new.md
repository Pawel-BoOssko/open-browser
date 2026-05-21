# HST Help

Purpose:
Defines the help content for Host-level OpenBridge commands and points models to executor-specific help files.

Depends on:
- docs/templates/document-header-template_new.md
- docs/agent/current-state_new.md
- docs/agent/architecture-decisions_new.md
- docs/runtime/modules-index_new.md
- docs/templates/executor-docs-template_new.md
- docs/runtime/environment.md
- config/start-prompt.txt
- docs/design/help-system.md

Impacts:
- docs/runtime/start-prompt_new.md
- docs/runtime/hst-status_new.md
- docs/executors/HST/help_new.md
- docs/executors/*/help_new.md
- model self-discovery inside OpenBrowser

Update rule:
Update this file when Host commands, available executors, self-discovery rules, RAW payload rules, automatic bridge features, or help ownership rules change.

Latest:
- Source migration from docs/runtime/environment.md, config/start-prompt.txt, and docs/design/help-system.md is reflected here.
- HST_HELP describes Host commands and available executors only.
- HST_HELP must not duplicate full executor manuals.
- Executor-specific help belongs to executor help files and executor HELP commands.
- HST_HELP should tell the model that RAW payloads exist for longer or quote-heavy scripts.
- Active command markers must not be reproduced casually because the first valid block executes.

## Host commands

HST_HELP:
Shows Host-level commands, available executors, automatic bridge features, and where to ask for executor-specific help.

HST_STATUS:
Shows current system status, working directory, conversation identity, recent downloads, available tools, pending command, and last result.

## Available executors

PS:
PowerShell executor. Use PS_HELP for details.

PY:
Python executor. Use PY_HELP for details.

## Help ownership rule

Host help answers:
- what Host commands exist;
- what executors are available;
- which HELP command to call next;
- what automatic bridge features exist at a high level.

Executor help answers:
- what that executor does;
- what payload format it expects;
- what constraints and rules apply;
- what examples are safe and current.

## RAW payload rule

For commands longer than a few lines, or commands containing quotes, backslashes, JSON, markdown, or OpenBridge-sensitive text, use the runtime RAW payload mechanism described in docs/runtime/environment.md.

RAW content is written as plain text by the model and encoded by the system before JSON validation reaches the executor.

RAW markers must be inside the payload JSON string. If placed outside the JSON object, the envelope becomes invalid.

## Automatic OpenBrowser features

Generated files from ChatGPT sandbox are auto-detected and downloaded.
Use HST_STATUS to see recent downloads and current paths.

## Self-discovery path for models

Recommended order:
1. HST_HELP
2. HST_STATUS
3. executor HELP command, for example PS_HELP or PY_HELP
4. task-specific command