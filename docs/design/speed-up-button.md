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
