<!-- LEGACY_DOCUMENT_NOTICE -->
> Legacy document notice: this is an older documentation file. Its current operational content has been migrated or superseded by: docs/agent/architecture-decisions_new.md, docs/agent/current-state_new.md, docs/modules/*/*_new.md, docs/runtime/*_new.md.
>
> Before using this file as source of truth, read: docs/agent/READ_BEFORE_WORK_new.md.
>
> Treat this file as historical or secondary unless the new documentation explicitly points back to it as an active operational reference.
<!-- /LEGACY_DOCUMENT_NOTICE -->

# Help System Design

## Principles

- One command: `HST_HELP`. `HST_TOOLS` is removed as duplicate (same function).
- Every executor (HST, PS, PY, ...) owns and maintains its own help.
- Help must cover everything: usage, limits, timeouts, errors, encoding, base64, truncation — all of it.
- The model discovers the environment step by step: `HST_HELP` → `PS_HELP` → `PY_HELP` → ...

## HST_HELP

Lists:
- All HST commands (STATUS, HELP)
- All executors available in the system (PS, PY, ...)
- Automatic bridge behaviors: downloads (not a module — it just happens), timeout, base64 in envelopes
- Tells the model that each executor has its own help (e.g. `PS_HELP`, `PY_HELP`)

HST is just another executor — its help follows the same pattern as PS and PY.

## Per-executor help (PS_HELP, PY_HELP, ...)

Each executor's help must cover everything the model needs to know:
- How to use the command
- Limits (output size, execution time)
- Error handling — what errors look like, how to recover
- Timeout behavior
- RAW payload support (base64 internally protected)
- Encoding rules
- Truncation markers
- Anything else specific to that executor

Each executor is responsible for keeping its own help accurate and up to date.

## Discovery flow

1. Start prompt button gives model initial context
2. Model calls `HST_HELP` — learns what's available
3. Model calls `PS_HELP`, `PY_HELP`, etc. — learns specifics
4. Model builds full environment awareness on its own

## Downloads note

Downloads are automatic (WebView2), not a module. There is no `DL_HELP`.
The download behavior must be documented somewhere:
- Model can create files in ChatGPT sandbox
- OpenBrowser auto-detects download buttons and saves files to `AppPaths.Downloads`
- `HST_STATUS` lists recent downloads
- This information lives in `HST_HELP` or in a help section about automatic bridge features
