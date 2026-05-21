# HST Status

Purpose:
Defines what HST_STATUS is expected to report to a model working inside OpenBrowser and to a developer agent inspecting runtime state.

Depends on:
- docs/templates/document-header-template_new.md
- docs/agent/current-state_new.md
- docs/runtime/hst-help_new.md
- docs/runtime/modules-index_new.md
- docs/runtime/environment.md
- config/start-prompt.txt

Impacts:
- docs/runtime/start-prompt_new.md
- docs/runtime/hst-help_new.md
- docs/executors/HST/help_new.md
- docs/agent/current-state_new.md
- model self-discovery inside OpenBrowser

Update rule:
Update this file when HST_STATUS output, runtime status fields, paths, recent downloads reporting, pending command reporting, available tools, or identity reporting changes.

Latest:
- Source migration from docs/runtime/environment.md and config/start-prompt.txt is reflected here.
- HST_STATUS is the primary runtime status command.
- HST_STATUS should show current working directory, build/version, conversation identity, tools, recent downloads, pending command, and last result.
- HST_STATUS is not the full help system. It reports current state, not detailed manuals.

## Expected status areas

Build and runtime:
- build number or version;
- start time;
- current time;
- working directory.

Identity:
- current OpenBridge/OpenBrowser instance ID;
- conversation identity if available.

Repository:
- short Git status summary;
- changed or untracked files if available.

Tools and executors:
- available tools;
- available executors;
- pending command if any;
- last result.

Downloads:
- recent downloaded files;
- downloads directory if available.

## Usage rule

A model working in OpenBrowser should use HST_STATUS when it needs to know where it is, what runtime is active, what files were recently downloaded, or whether a previous command is still pending.

A developer agent should use HST_STATUS only as runtime evidence, not as a replacement for repository inspection.

## Current known status evidence

Runtime/design sources state that HST_STATUS reports:
- build info;
- working directory;
- git status;
- available tools;
- pending command;
- last execution result;
- recent downloads.