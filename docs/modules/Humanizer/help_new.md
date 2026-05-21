# Humanizer Module Help

Purpose:
Defines model-facing help for Humanizer as the module responsible for natural wording variation around system responses.

Depends on:
- docs/templates/document-header-template_new.md
- docs/modules/Humanizer/index_new.md
- docs/modules/Humanizer/rules_new.md
- docs/agent/current-state_new.md

Impacts:
- docs/modules/Humanizer/index_new.md
- docs/modules/Humanizer/rules_new.md
- docs/runtime/hst-help_new.md
- docs/runtime/start-prompt_new.md
- model-facing system responses

Update rule:
Update this file when Humanizer behavior, wording variation rules, wrapper text, or model-facing response policy changes.

Latest:
- Humanizer may vary wording around equivalent system messages.
- Humanizer must not change technical meaning.
- Humanizer must not invent facts.
- Errors, timeouts, and failures must remain clear.

## What this module does

Humanizer makes repeated system responses less mechanical by selecting natural wording variants.

It may be used for:
- command result wrappers;
- timeout messages;
- inter-model communication wrappers;
- neutral system response wording.

## Model usage

A model normally does not call Humanizer directly.
The model sees Humanizer effects in the wording of system responses.

## Constraints

Humanizer must preserve the underlying meaning of the original result.
It must not hide errors, change command output, soften technical failures, or add unsupported information.

## Related rules

For detailed constraints, read:
- docs/modules/Humanizer/rules_new.md