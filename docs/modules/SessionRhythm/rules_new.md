# SessionRhythm Module Rules

Purpose:
Defines rules, limitations, and update obligations for SessionRhythm pacing, delays, pause/resume behavior, speed-up behavior, and long-session rhythm.

Depends on:
- docs/templates/document-header-template_new.md
- docs/modules/SessionRhythm/index_new.md
- docs/modules/SessionRhythm/help_new.md
- docs/agent/current-state_new.md
- docs/design/session-rhythm.md
- docs/design/pause-button.md
- docs/design/speed-up-button.md

Impacts:
- docs/modules/SessionRhythm/index_new.md
- docs/modules/SessionRhythm/help_new.md
- docs/runtime/start-prompt_new.md
- docs/agent/current-state_new.md
- docs/agent/documentation-update-checklist_new.md
- model-facing session behavior

Update rule:
Update this file when pacing, delay distribution, pause/resume behavior, speed-up behavior, loop stopping, long-session rhythm, or timing-related user controls change.

Latest:
- Source migration from SessionRhythm, pause-button, and speed-up design docs is reflected here.
- Existing normal delay is 20-70 seconds according to design sources.
- SessionRhythm longer-pause logic is pending implementation in design sources.
- Delay and pause behavior must remain separate from command success, error handling, and watchdog behavior.
- Speed-up skips the current wait only; it must not disable timing for future commands.

## Rules

### SR-RULE-001: Timing must not change meaning

SessionRhythm may delay or pace delivery, but it must not change command output, success state, error state, or technical content.

### SR-RULE-002: Operator intervention must be possible

SessionRhythm should support stopping or pausing loops so the Operator can comment, redirect, or interrupt work.

### SR-RULE-003: Avoid constant long-session rhythm

Long autonomous work should not continue for many hours at an identical short interval.
The planned design allows longer, rarer pauses after normal delays.

### SR-RULE-004: Keep pacing separate from watchdog

SessionRhythm is about pacing and delivery rhythm.
Watchdog is about detecting silence, hangs, timeouts, or missing responses.
Do not merge those responsibilities without an explicit architecture decision.

### SR-RULE-005: Keep failures clear

SessionRhythm must not hide errors, rejected commands, timeouts, or failed operations behind natural timing or wording.

### SR-RULE-006: Speed-up affects current wait only

Speed-up may skip the current normal delay or longer pause.
It must not disable timing globally for later commands.

### SR-RULE-007: Pause freezes pending injection, not command result

Pause/resume design freezes delivery of a pending result after the current wait finishes.
It must not discard the pending command result unless a separate explicit cancellation mechanism exists.

## Documentation triggers

Update SessionRhythm documentation when:
- delay rules change;
- pause/resume behavior changes;
- speed-up behavior changes;
- long-pause behavior changes;
- loop stopping behavior changes;
- model-facing timing behavior changes;
- implementation status changes;
- SessionRhythm boundaries change.