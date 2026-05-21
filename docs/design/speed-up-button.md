<!-- LEGACY_DOCUMENT_NOTICE -->
> Legacy document notice: this is an older documentation file. Its current operational content has been migrated or superseded by: docs/agent/architecture-decisions_new.md, docs/agent/current-state_new.md, docs/modules/*/*_new.md, docs/runtime/*_new.md.
>
> Before using this file as source of truth, read: docs/agent/READ_BEFORE_WORK_new.md.
>
> Treat this file as historical or secondary unless the new documentation explicitly points back to it as an active operational reference.
<!-- /LEGACY_DOCUMENT_NOTICE -->

# Speed-Up Button Design

## Behavior

One button. One action: skip the current wait.

- If the system is waiting on the normal delay (20–70s) or a SessionRhythm pause — the button ends the wait immediately.
- The result is injected into chat right away.
- Does NOT disable the timer system — the next command gets its normal delay as usual.
- The user decides: "the response is ready, no need to wait another 40 seconds."

## Implementation
A `_skipWait` flag checked in both counting loops (normal delay and pause). If true, exit the loop and proceed.

## Status: pending implementation
