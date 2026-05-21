# ConversationTrimmer Module Index

Purpose:
Describes the ConversationTrimmer module and its role in limiting operational conversation context used by OpenBrowser/OpenBridge.

Depends on:
- docs/templates/document-header-template_new.md
- docs/runtime/modules-index_new.md
- docs/templates/module-docs-template_new.md
- docs/agent/current-state_new.md
- docs/modules/ConversationTrimmer/help_new.md
- docs/modules/ConversationTrimmer/rules_new.md

Impacts:
- docs/modules/ConversationTrimmer/help_new.md
- docs/modules/ConversationTrimmer/rules_new.md
- docs/agent/current-state_new.md
- docs/runtime/start-prompt_new.md

Update rule:
Update this file when conversation trimming behavior, context-window assumptions, retained-message policy, or module boundaries change.

Latest:
- ConversationTrimmer is documented as a runtime module for limiting operational conversation context.
- DOM history is not the source of truth.
- Repository documentation is the source of truth for project knowledge.
- Conversation trimming must not be used as a replacement for documentation governance.

## Responsibility

ConversationTrimmer owns documentation for runtime context trimming behavior.

Current documented responsibility:
- keep only operationally useful recent conversation context where needed;
- avoid treating long UI history as project memory;
- support performance and clarity in long-running conversations.

## Boundaries

ConversationTrimmer does:
- document conversation-context reduction principles;
- support the idea that only recent operational context should be needed by runtime.

ConversationTrimmer does not:
- own project documentation;
- store architecture decisions;
- replace current-state or architecture-decisions files;
- rely on full DOM conversation history as source of truth.

## Related documents

- docs/modules/ConversationTrimmer/help_new.md
- docs/modules/ConversationTrimmer/rules_new.md
- docs/agent/current-state_new.md
- docs/agent/architecture-decisions_new.md