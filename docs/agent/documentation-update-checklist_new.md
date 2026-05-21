# Documentation Update Checklist

Purpose:
Defines the mandatory documentation impact check for developer agents after any change to code, behavior, commands, modules, executors, paths, logs, tests, or project rules.

Depends on:
- docs/templates/document-header-template_new.md
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/current-state_new.md
- docs/agent/coding-invariants_new.md
- docs/agent/commit-rules_new.md
- docs/runtime/modules-index_new.md
- docs/templates/module-docs-template_new.md
- docs/templates/executor-docs-template_new.md

Impacts:
- docs/agent/commit-rules_new.md
- docs/agent/current-state_new.md
- docs/agent/coding-invariants_new.md
- docs/runtime/modules-index_new.md
- docs/runtime/hst-help_new.md
- docs/runtime/hst-status_new.md
- docs/runtime/start-prompt_new.md
- docs/executors/*/index_new.md
- docs/executors/*/help_new.md
- docs/executors/*/rules_new.md
- docs/modules/*/index_new.md
- docs/modules/*/help_new.md
- docs/modules/*/rules_new.md
- docs/feedback/field-notes_new.md

Update rule:
Update this file when documentation impact rules or required documentation checks change.

Latest:
- Developer agents must check documentation impact before final reports and before recommending COMMIT.
- A code change is not complete if it changes system behavior but leaves affected docs stale.
- Module and executor documentation rules are common for all modules and executors; the module registry only lists module name, type, status and docs path.

## Required check

After any change, answer these questions:

1. Did behavior change?
2. Did any command change?
3. Did any executor or module change?
4. Did any file path, folder, log format, output format, or status output change?
5. Did any prompt, help text, self-discovery text, or user-visible instruction change?
6. Did any coding invariant appear, change, or become relevant?
7. Did any architecture decision appear, change, or become relevant?
8. Did any field note become resolved, rejected, converted to task, or require architect decision?

## Required action

If the answer to any question is yes, update the affected documentation before recommending COMMIT.

If no documentation update is needed, final response must say why.

## Module and executor documentation rule

Every documented module or executor must have:

- index_new.md
- help_new.md
- rules_new.md

The common structure is defined in:

- docs/templates/module-docs-template_new.md
- docs/templates/executor-docs-template_new.md

The registry is only a registry. It must not contain the documentation rules.