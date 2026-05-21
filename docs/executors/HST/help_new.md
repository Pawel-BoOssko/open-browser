# HST Executor Help

Purpose:
Defines help content for Host-level HST commands exposed inside OpenBrowser/OpenBridge.

Depends on:
- docs/templates/document-header-template_new.md
- docs/runtime/hst-help_new.md
- docs/runtime/hst-status_new.md
- docs/runtime/modules-index_new.md
- docs/templates/executor-docs-template_new.md
- docs/runtime/environment.md
- config/start-prompt.txt
- docs/design/help-system.md

Impacts:
- docs/runtime/hst-help_new.md
- docs/runtime/start-prompt_new.md
- model self-discovery inside OpenBrowser

Update rule:
Update this file when HST commands, Host-level help behavior, Host status behavior, Host self-discovery behavior, available executor listing, or RAW payload guidance changes.

Latest:
- Source migration from runtime environment, start prompt, and help-system design docs is reflected here.
- HST commands are Host-level commands.
- HST_HELP lists Host commands and available executors.
- HST_STATUS reports current runtime state.
- Executor-specific details belong in executor-specific HELP files.
- RAW payload guidance should be referenced for long or quoting-sensitive commands.

## HST commands

HST_HELP:
Shows Host-level commands, available executors, automatic bridge features, and next HELP commands to use.

HST_STATUS:
Shows runtime status, working directory, build/version, OpenBridge ID, Git status summary, available tools, recent downloads, pending command, and last result.

## Available executor help

Use executor-specific help for executor details:
- PS_HELP for PowerShell executor details.
- PY_HELP for Python executor details.

## RAW payload note

For long scripts, quote-heavy commands, markdown, JSON, backslashes, or OpenBridge-sensitive text, use the RAW payload mechanism described in docs/runtime/environment.md.

Do not casually reproduce active command markers in model responses because the first valid command block executes.

## Usage rule

Use HST_HELP first when entering OpenBrowser/OpenBridge.
Use HST_STATUS when current runtime state, paths, downloads, tools, pending command, last result, or identity are needed.
Use executor HELP commands for executor details.