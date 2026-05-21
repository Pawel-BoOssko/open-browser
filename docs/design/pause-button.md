<!-- LEGACY_DOCUMENT_NOTICE -->
> Legacy document notice: this is an older documentation file. Its current operational content has been migrated or superseded by: docs/agent/architecture-decisions_new.md, docs/agent/current-state_new.md, docs/modules/*/*_new.md, docs/runtime/*_new.md.
>
> Before using this file as source of truth, read: docs/agent/READ_BEFORE_WORK_new.md.
>
> Treat this file as historical or secondary unless the new documentation explicitly points back to it as an active operational reference.
<!-- /LEGACY_DOCUMENT_NOTICE -->

# Pause / Resume Button Design

## Behavior

1. User clicks **PAUSE**.
2. System waits until the current delay/timer finishes — does NOT inject the result into chat.
3. The user gets a clean chat. Can send several messages to the model, have a conversation.
4. When ready — user clicks **RESUME**.
5. The waiting result is injected into chat, and the loop continues.

## What it is not
- Not STOP — nothing is killed, nothing is cleared.
- Pending command is frozen, not discarded.
- The result waits suspended.

## One button
Starts as PAUSE, toggles to RESUME when paused.

## Status: pending implementation
