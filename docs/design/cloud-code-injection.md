<!-- LEGACY_DOCUMENT_NOTICE -->
> Legacy document notice: this is an older documentation file. Its current operational content has been migrated or superseded by: docs/agent/architecture-decisions_new.md, docs/agent/current-state_new.md, docs/modules/*/*_new.md, docs/runtime/*_new.md.
>
> Before using this file as source of truth, read: docs/agent/READ_BEFORE_WORK_new.md.
>
> Treat this file as historical or secondary unless the new documentation explicitly points back to it as an active operational reference.
<!-- /LEGACY_DOCUMENT_NOTICE -->

# Cloud Code → OpenBrowser Chat Injection

## Flow

1. **Claude Code** detects OpenBrowser is idle (no active cycle, no pending command).
2. **CC** sends a message to OpenBrowser via named pipe.
3. **OpenBrowser** receives it and injects it into the ChatGPT chat using the existing JS injection mechanism (textarea + Send click).
4. **ChatGPT** responds. OpenBrowser captures the response via existing ResponseExtractor.
5. If the model wants to reply to Claude Code, it uses **HST_SEND_TO_CC** command with its message as payload. Host sends it back through the pipe.

## Architecture

- `HST_SEND_TO_CC` is an HST command in the Host — same level as `HST_STATUS`, `HST_HELP`.
- The model controls when to use it, same as any other command.
- CC → OB direction: direct injection via pipe, no model involvement.
- OB → CC direction: model uses HST command, Host routes through pipe.

## Transport
Named pipe between two local processes. Zero configuration.

## Status: design approved, pending implementation
