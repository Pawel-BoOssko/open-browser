# SessionRhythm Module Index

Purpose:
Describes the SessionRhythm design and its intended role in pacing OpenBrowser/OpenBridge model work and response delivery.

Depends on:
- docs/templates/document-header-template_new.md
- docs/runtime/modules-index_new.md
- docs/templates/module-docs-template_new.md
- docs/agent/current-state_new.md
- docs/modules/SessionRhythm/help_new.md
- docs/modules/SessionRhythm/rules_new.md
- docs/design/session-rhythm.md
- docs/design/pause-button.md
- docs/design/speed-up-button.md

Impacts:
- docs/modules/SessionRhythm/help_new.md
- docs/modules/SessionRhythm/rules_new.md
- docs/runtime/start-prompt_new.md
- docs/agent/current-state_new.md
- docs/agent/architecture-decisions_new.md

Update rule:
Update this file when session pacing, response delay, pause/resume behavior, speed-up behavior, long-work rhythm, implementation status, or module boundaries change.

Latest:
- Source migration from docs/design/session-rhythm.md, pause-button.md, speed-up-button.md is reflected here.
- SessionRhythm is a planned/pending design area, not confirmed as fully implemented.
- Existing normal delay is 20-70 seconds according to design sources.
- Existing cycle timeout is described as 360 seconds in session-rhythm design sources.
- SessionRhythm design adds longer, rarer pauses on top of existing delay.
- Speed-up is designed to skip both normal delay and SessionRhythm pause through a skip-wait flag.
- Pause/resume is designed to freeze pending injection after current wait finishes, not discard the pending command.

## Responsibility

SessionRhythm owns documentation for timing, pacing, delay, pause, resume, speed-up, and long-session rhythm behavior.

Current documented responsibility:
- describe existing delivery pacing assumptions;
- describe planned longer-pause behavior;
- describe pause/resume expectations;
- describe speed-up expectations;
- keep timing behavior separate from command execution result logic.

## Current source status

From design sources:
- normal delay exists;
- pause/resume is pending implementation;
- speed-up is pending implementation;
- SessionRhythm longer-pause component is pending implementation.

## Boundaries

SessionRhythm does:
- document timing around response delivery;
- support more natural session pacing;
- document pause/resume and speed-up behavior when applicable.

SessionRhythm does not:
- decide command success;
- alter command output meaning;
- own executor logic;
- replace watchdog or error handling.

## Related documents

- docs/modules/SessionRhythm/help_new.md
- docs/modules/SessionRhythm/rules_new.md
- docs/design/session-rhythm.md
- docs/design/pause-button.md
- docs/design/speed-up-button.md
- docs/runtime/start-prompt_new.md
- docs/agent/current-state_new.md