# Humanizer Module Index

Purpose:
Describes the Humanizer module and its role in making OpenBrowser/OpenBridge system responses less mechanical while preserving exact meaning.

Depends on:
- docs/templates/document-header-template_new.md
- docs/runtime/modules-index_new.md
- docs/templates/module-docs-template_new.md
- docs/agent/current-state_new.md
- docs/modules/Humanizer/help_new.md
- docs/modules/Humanizer/rules_new.md

Impacts:
- docs/modules/Humanizer/help_new.md
- docs/modules/Humanizer/rules_new.md
- docs/runtime/hst-help_new.md
- docs/runtime/start-prompt_new.md
- model-facing system responses

Update rule:
Update this file when Humanizer responsibility, response wrapping behavior, wording variation rules, or module boundaries change.

Latest:
- Humanizer is documented as a runtime module for natural response wrapping and wording variation.
- Humanizer must not change command results or technical meaning.
- Humanizer is not a decision engine.
- Humanizer wording must stay neutral and operational.

## Responsibility

Humanizer owns documentation for natural-language wrapping of system responses where the runtime uses varied wording instead of repetitive mechanical phrases.

Current documented responsibility:
- vary wrapper wording around system results;
- keep equivalent meaning across variants;
- avoid adding technical facts not present in the underlying result;
- make inter-model and system-model communication less mechanical.

## Boundaries

Humanizer does:
- vary phrasing;
- make responses less repetitive;
- keep meaning stable.

Humanizer does not:
- change command output meaning;
- invent missing technical details;
- decide architecture;
- hide errors, timeouts, or failures.

## Related documents

- docs/modules/Humanizer/help_new.md
- docs/modules/Humanizer/rules_new.md
- docs/runtime/hst-help_new.md
- docs/runtime/start-prompt_new.md