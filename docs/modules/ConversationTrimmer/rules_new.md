# ConversationTrimmer Module Rules

Purpose:
Defines rules, limitations, and update obligations for ConversationTrimmer and runtime conversation-context reduction.

Depends on:
- docs/templates/document-header-template_new.md
- docs/modules/ConversationTrimmer/index_new.md
- docs/modules/ConversationTrimmer/help_new.md
- docs/agent/current-state_new.md
- docs/agent/architecture-decisions_new.md

Impacts:
- docs/modules/ConversationTrimmer/index_new.md
- docs/modules/ConversationTrimmer/help_new.md
- docs/agent/current-state_new.md
- docs/runtime/start-prompt_new.md
- docs/agent/documentation-update-checklist_new.md

Update rule:
Update this file when trimming behavior, retained context policy, DOM-history assumptions, context-window assumptions, or model-facing memory rules change.

Latest:
- Runtime conversation trimming must not be treated as project knowledge management.
- Repository documentation is the source of truth for project state and decisions.
- DOM history is not the source of truth.
- Long-running UI history should not be required for normal runtime operation.

## Rules

### CT-RULE-001: Repository docs are project truth

Project state, architecture decisions, coding invariants, and working rules belong in repository documentation.
Conversation history must not be the only source of those rules.

### CT-RULE-002: DOM history is not canonical memory

The visible conversation DOM is a UI surface, not the canonical source of operational truth.

### CT-RULE-003: Keep only operationally useful context

ConversationTrimmer should preserve only context useful for current operation, command processing, and model interaction.
It should not preserve old history merely because it exists.

### CT-RULE-004: Trimming must not break command operation

Trimming behavior must not remove or corrupt current command flow, pending responses, or necessary short-term runtime state.

### CT-RULE-005: Documentation replaces long-memory dependency

If a fact is needed by future developer agents or models, it belongs in documentation, not only in chat history.

## Documentation triggers

Update ConversationTrimmer documentation when:
- retained message policy changes;
- trimming strategy changes;
- DOM history assumptions change;
- runtime context boundaries change;
- model-facing memory or self-discovery rules change.