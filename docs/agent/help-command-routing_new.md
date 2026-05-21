# Help Command Routing

Purpose: Defines how OpenBridge help commands map to documentation files.

Depends on:
- docs/runtime/hst-help_new.md
- docs/runtime/modules-index_new.md
- tools/*/README.md

Impacts:
- HST_HELP
- PS_HELP
- PY_HELP
- tool help commands
- README files used as model-facing help

Update rule: Update this file when a help command is added, removed, renamed, or mapped to a different file.

Latest:
- Help commands are views over documentation files.
- Tool help commands display `tools/<tool>/README.md`.
- Help command output must say which file it displays.

## System help

- `HST_HELP`: displays `docs/runtime/hst-help_new.md`;
- `PS_HELP`: displays `docs/executors/PS/help_new.md`;
- `PY_HELP`: displays `docs/executors/PY/help_new.md`.

## Tool help

- `LINKEDIN_HELP`: displays `tools/linkedin/README.md`;
- `GITHUB_HELP`: displays `tools/github/README.md`;
- `N8N_HELP`: displays `tools/n8n/README.md`;
- `WEB_HELP`: displays `tools/web/README.md`;
- `CLAUDE_HELP`: displays `tools/claude/README.md`.

## Required output header

Every help command must start with:

```text
<COMMAND_NAME>
This is the content of:
<path>
```

Then it displays the file content.

## Rule

Do not duplicate tool manuals in HST_HELP. HST_HELP lists the help commands and their target files.
