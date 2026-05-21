# Legacy Documentation Map

Purpose: Records old documentation files that have a legacy preamble and maps each to the current documentation entrypoint or target area.

Depends on:
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/current-state_new.md
- docs/agent/development-rules_new.md
- docs/runtime/modules-index_new.md

Impacts:
- legacy documentation cleanup
- developer-agent source selection
- docs/feedback/field-notes_new.md

Update rule: Update when old documentation files are added, removed, migrated, or promoted.

Latest:
- All old tracked markdown documentation files have a legacy preamble.
- The primary entrypoint remains docs/agent/READ_BEFORE_WORK_new.md.
- Some old files remain active operational references and should be replaced later only when their runtime/config role is moved to new docs or code-backed help.

## Active old or non-new operational references

These files still exist as operational or public-facing references and should not be deleted blindly:

- README.md
- SECURITY.md
- docs/runtime/environment.md
- config/start-prompt.txt
- tools/*/README.md
- tools/*/*_WORKING_GUIDE.md

Required later replacement work:
- decide whether config/start-prompt.txt should be replaced by docs/runtime/start-prompt_new.md as the source used by the application;
- decide whether docs/runtime/environment.md should remain the model-facing runtime contract or be replaced by split new docs;
- decide whether old tool guides remain active operational references or should get new governed equivalents.

## Map

| Legacy file | Current target / active reference | Has notice |
|---|---|---|
| diagnostics/manual/openbridge_full_path_diagnostic.md | docs/agent/current-state_new.md and docs/feedback/field-notes_new.md | True |
| docs/architecture/README.md | docs/agent/current-state_new.md | True |
| docs/design/cloud-code-injection.md | docs/agent/architecture-decisions_new.md, docs/agent/current-state_new.md, docs/modules/*/*_new.md, docs/runtime/*_new.md | True |
| docs/design/help-system.md | docs/agent/architecture-decisions_new.md, docs/agent/current-state_new.md, docs/modules/*/*_new.md, docs/runtime/*_new.md | True |
| docs/design/pause-button.md | docs/agent/architecture-decisions_new.md, docs/agent/current-state_new.md, docs/modules/*/*_new.md, docs/runtime/*_new.md | True |
| docs/design/session-rhythm.md | docs/agent/architecture-decisions_new.md, docs/agent/current-state_new.md, docs/modules/*/*_new.md, docs/runtime/*_new.md | True |
| docs/design/speed-up-button.md | docs/agent/architecture-decisions_new.md, docs/agent/current-state_new.md, docs/modules/*/*_new.md, docs/runtime/*_new.md | True |
| docs/development/decisions.md | docs/agent/development-rules_new.md, docs/agent/architecture-decisions_new.md, docs/agent/coding-invariants_new.md, docs/agent/commit-rules_new.md | True |
| docs/development/journal.md | docs/agent/development-rules_new.md, docs/agent/architecture-decisions_new.md, docs/agent/coding-invariants_new.md, docs/agent/commit-rules_new.md | True |
| docs/development/README.md | docs/agent/development-rules_new.md, docs/agent/architecture-decisions_new.md, docs/agent/coding-invariants_new.md, docs/agent/commit-rules_new.md | True |
| docs/development/rules.md | docs/agent/development-rules_new.md, docs/agent/architecture-decisions_new.md, docs/agent/coding-invariants_new.md, docs/agent/commit-rules_new.md | True |
| docs/environment/folders.md | docs/agent/current-state_new.md | True |
| docs/environment/README.md | docs/agent/current-state_new.md | True |
| docs/handoff/baseline-2026-05-18.md | docs/agent/current-state_new.md, docs/modules/Downloads/index_new.md, docs/modules/PageTap/index_new.md | True |
| docs/handoff/README.md | docs/agent/current-state_new.md, docs/modules/Downloads/index_new.md, docs/modules/PageTap/index_new.md | True |
| docs/README.md | docs/agent/current-state_new.md | True |
| docs/runtime/environment.md | docs/runtime/start-prompt_new.md, docs/runtime/hst-help_new.md, docs/executors/*/help_new.md | True |
| docs/runtime/README.md | docs/agent/current-state_new.md | True |
| handoff.md | docs/agent/current-state_new.md, docs/modules/Downloads/index_new.md, docs/modules/PageTap/index_new.md | True |
| ideas.md | docs/feedback/field-notes_new.md | True |
| README.md | README.md plus docs/agent/READ_BEFORE_WORK_new.md | True |
| releases/BridgeBrowserAlpha0/CASE_NOTES_ALPHA.md | docs/agent/current-state_new.md, docs/modules/*/*_new.md, docs/agent/architecture-decisions_new.md | True |
| releases/BridgeBrowserAlpha0/CHANGELOG.md | docs/agent/current-state_new.md, docs/modules/*/*_new.md, docs/agent/architecture-decisions_new.md | True |
| releases/BridgeBrowserAlpha0/examples/README.md | docs/agent/current-state_new.md, docs/modules/*/*_new.md, docs/agent/architecture-decisions_new.md | True |
| releases/BridgeBrowserAlpha0/README.md | docs/agent/current-state_new.md, docs/modules/*/*_new.md, docs/agent/architecture-decisions_new.md | True |
| SECURITY.md | docs/agent/coding-invariants_new.md and docs/agent/current-state_new.md | True |
| test-plan-v3.md | docs/agent/current-state_new.md and docs/feedback/field-notes_new.md | True |
| test-plan-v4.md | docs/agent/current-state_new.md and docs/feedback/field-notes_new.md | True |
| test-results-v3.md | docs/agent/current-state_new.md and docs/feedback/field-notes_new.md | True |
| tool-proposals.md | docs/feedback/field-notes_new.md | True |
| tools/claude/CLAUDE_WORKING_GUIDE.md | docs/runtime/modules-index_new.md; tool README/working-guide files may remain active operational references | True |
| tools/claude/README.md | docs/runtime/modules-index_new.md; tool README/working-guide files may remain active operational references | True |
| tools/github/GITHUB_WORKING_GUIDE.md | docs/runtime/modules-index_new.md; tool README/working-guide files may remain active operational references | True |
| tools/github/README.md | docs/runtime/modules-index_new.md; tool README/working-guide files may remain active operational references | True |
| tools/linkedin/LINKEDIN_WORKING_GUIDE.md | docs/runtime/modules-index_new.md; tool README/working-guide files may remain active operational references | True |
| tools/linkedin/README.md | docs/runtime/modules-index_new.md; tool README/working-guide files may remain active operational references | True |
| tools/n8n/N8N_WORKING_GUIDE.md | docs/runtime/modules-index_new.md; tool README/working-guide files may remain active operational references | True |
| tools/n8n/README.md | docs/runtime/modules-index_new.md; tool README/working-guide files may remain active operational references | True |
| tools/README.md | docs/runtime/modules-index_new.md; tool README/working-guide files may remain active operational references | True |
| tools/web/README.md | docs/runtime/modules-index_new.md; tool README/working-guide files may remain active operational references | True |
| tools-test-plan.md | docs/agent/current-state_new.md and docs/feedback/field-notes_new.md | True |
| tools-test-results.md | docs/agent/current-state_new.md and docs/feedback/field-notes_new.md | True |
