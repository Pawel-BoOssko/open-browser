# SessionRhythm Module Help

Purpose:
Defines model-facing help for SessionRhythm as the design area responsible for pacing OpenBrowser/OpenBridge work.

Depends on:
- docs/templates/document-header-template_new.md
- docs/modules/SessionRhythm/index_new.md
- docs/modules/SessionRhythm/rules_new.md
- docs/agent/current-state_new.md
- docs/design/session-rhythm.md
- docs/design/pause-button.md
- docs/design/speed-up-button.md

Impacts:
- docs/modules/SessionRhythm/index_new.md
- docs/modules/SessionRhythm/rules_new.md
- docs/runtime/start-prompt_new.md
- model-facing session behavior

Update rule:
Update this file when session pacing, delay behavior, pause/resume behavior, speed-up behavior, implementation status, or model-facing rhythm instructions change.

Latest:
- SessionRhythm is documented from design sources as pending implementation.
- Existing response delivery delay is described as 20-70 seconds.
- Planned SessionRhythm adds rarer longer pauses after normal delays.
- Speed-up is designed to skip current waits, not permanently disable timing.
- Pause/resume is designed to pause pending injection, not discard the pending command.
- SessionRhythm must not change command results or hide failures.

## What this area does

SessionRhythm describes how OpenBrowser/OpenBridge should pace model work and response delivery during longer sessions.

It covers:
- existing normal delivery delay;
- planned minimum first-pause threshold;
- planned cooldown between longer pauses;
- planned weighted pause durations;
- pause/resume behavior;
- speed-up behavior;
- stopping or delaying a loop so the Operator can intervene.

## Model usage

A model normally does not call SessionRhythm directly.
The model experiences this area through timing, pause/resume behavior, speed-up behavior, and controlled delivery of responses.

## Limitations

SessionRhythm is not a command executor.
It does not decide whether a command succeeded.
It does not change command output.
It does not replace watchdog, timeout handling, or Host error handling.