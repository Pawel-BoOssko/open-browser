# Session Rhythm Design

## Current state
- `ComputeHumanDelayMs()`: Box-Muller truncated normal, 20–70s, crypto-grade randomness
- `SendTextToChatAsync()`: delay → inject → send
- 360s cycle timeout (`SendWithDeadlineAsync`, `ExecuteAndInjectResultAsync`)
- Humanizer: text wrapping, no timing effect

## What we add
A `SessionRhythm` component that layers longer, rarer pauses on top of the existing delay. The existing delay is untouched.

## SessionRhythm logic

### When first pause
- Not before 5–10 cycles (randomized at session start)
- Not before 10–15 minutes of work

### Cooldown between pauses
- Minimum 3 normal cycles or 15 minutes since last pause ended

### Pause probability per cycle
- After conditions met: ~15% chance per cycle
- Increases with session length: after 1h → ~25%, after 2h → ~35%
- Never guaranteed — cycle can always proceed normally

### Pause duration (weighted random)
- 2–5 minutes — common (~60% of pauses)
- 5–15 minutes — less common (~30%)
- 20–30 minutes — rare (~10%)

### Placement
Same place as the existing delay — inside `SendTextToChatAsync`, as a second counting loop after the existing delay loop.

### Skipping
The speed-up button can skip both the normal delay and the pause (`_skipWait` flag).

## SessionRhythm class (new file)
- `_sessionStartedAt` — when session began
- `_cycleCount` — cycles completed
- `_lastPauseAt` — when last pause ended
- One decision method: rolls for pause, returns duration (0 = no pause)

## What is NOT touched
- Existing delay algorithm
- 360s cycle timeout
- Humanizer
- `_cycleClosed` logic

## Status: pending implementation
