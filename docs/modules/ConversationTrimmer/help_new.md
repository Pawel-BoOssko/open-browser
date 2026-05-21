# ConversationTrimmer Module Help

Purpose:
Defines model-facing help for ConversationTrimmer as the runtime module responsible for limiting operational conversation context.

Depends on:
- docs/templates/document-header-template_new.md
- docs/modules/ConversationTrimmer/index_new.md
- docs/modules/ConversationTrimmer/rules_new.md
- docs/agent/current-state_new.md

Impacts:
- docs/modules/ConversationTrimmer/index_new.md
- docs/modules/ConversationTrimmer/rules_new.md
- docs/runtime/start-prompt_new.md
- model self-discovery inside OpenBrowser

Update rule:
Update this file when conversation trimming behavior, retained context policy, or model-facing context instructions change.

Latest:
- ConversationTrimmer limits operational conversation context.
- It does not replace repository documentation.
- It does not make DOM history the source of truth.
- Models should rely on documented project knowledge, not old UI history.

## What this module does

ConversationTrimmer supports keeping the working conversation context usable by reducing reliance on old conversation history.

It helps the runtime avoid treating the entire visible chat UI as operational memory.

## Model usage

A model should not assume old conversation history is complete or authoritative.
For current project truth, use repository documentation and runtime self-discovery.

For environment discovery, use:
- HST_HELP
- HST_STATUS
- executor-specific HELP commands

## Limitations

ConversationTrimmer is not a knowledge base.
It does not store architecture decisions.
It does not replace current-state, architecture-decisions, or field-notes documents.