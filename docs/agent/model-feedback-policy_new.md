# Model Feedback Policy

Purpose: Defines when and how models should record operational problems, confusing instructions, repeated failures, or documentation gaps discovered during work.

Depends on:
- docs/agent/READ_BEFORE_WORK_new.md
- docs/feedback/field-notes_new.md
- docs/agent/development-rules_new.md

Impacts:
- docs/feedback/field-notes_new.md
- final reports
- future documentation cleanup
- recurring failure analysis

Update rule: Update this file when the project changes how model observations, failed commands, repeated retries, or documentation gaps are recorded.

Latest:
- Models should not write field notes for every minor event.
- Models should record useful operational feedback after a task when it reveals a repeated issue, misleading documentation, missing help, or tool behavior that affected work.
- Feedback must be factual and short.

## When to record feedback

Record feedback when:
- a command had to be repeated because documentation or help was unclear;
- a tool behaved differently than its help described;
- a legacy document misled the model;
- a command failed because the model inferred a capability that does not exist;
- a retry pattern or repeated failure suggests missing documentation;
- the model found a gap in HST_HELP, executor help, tool README, folder policy, or commit rules;
- the model had to use a workaround that should not become normal practice.

Do not record every successful command, speculative feature wishes, broad redesign ideas not connected to the task, or complaints outside documented system purpose.

## What to record

Use concise factual entries:
- task context;
- what failed or misled the model;
- exact command or help path if relevant;
- observed result;
- suggested documentation or system correction.

## Default target

Default target:
- `docs/feedback/field-notes_new.md`
