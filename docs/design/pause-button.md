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
