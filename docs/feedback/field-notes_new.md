# Field Notes

Purpose:
Collects raw operational observations from the Operator, the model working inside OpenBrowser, and developer agents when they notice problems, missing capabilities, unclear instructions, failures, or useful improvement ideas.

Depends on:
- docs/templates/document-header-template_new.md
- docs/templates/field-note-template_new.md
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/documentation-update-checklist_new.md

Impacts:
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/documentation-update-checklist_new.md
- docs/agent/commit-rules_new.md
- docs/agent/current-state_new.md
- developer-agent triage and planning

Update rule:
Update this file whenever the Operator, the model working inside OpenBrowser, or a developer agent records a real observation from use, testing, development, or documentation work.

Latest:
- 2026-05-21: Test sources show unresolved PS stdout mojibake while PY UTF-8 works correctly.
- This file is a working intake queue, not polished technical documentation.
- Entries are written close to the moment of observation.
- A developer agent triages entries later into bugs, tasks, documentation updates, architecture questions, done items, or rejected items.
- Newest entries stay near the top.

## Entries

## 2026-05-21 09:25 | developer-agent | problem | new

Context:
Documentation migration reviewed linkedin-response.md and LinkedIn tool notes.

Observation:
LinkedIn notes indicate both primary and secondary client secrets were active, and local config may need cleanup to ensure the currently active secret is used. The notes also point to a clean OAuth retry with exact 127.0.0.1 redirect URI.

Impact:
LinkedIn posting may fail with invalid_client if local config uses a stale or wrong secret, hidden whitespace, or mismatched redirect host.

Expected:
Treat this as an operational LinkedIn tool cleanup item, not architecture. Before further LinkedIn posting tests, verify active secret choice, update config/local/linkedin/, avoid printing secrets, and retry OAuth cleanly.

Links or files:
- linkedin-response.md
- tools/linkedin/README.md
- tools/linkedin/LINKEDIN_WORKING_GUIDE.md

## 2026-05-21 09:20 | developer-agent | idea | new

Context:
Documentation migration reviewed ideas.md and tool-proposals.md.

Observation:
The source files contain future ideas and proposed integrations, including Gmail, Google Calendar, Google Drive, Microsoft 365, Slack, Discord, Notion, Obsidian vault, Apify, browser form automation, Supabase, and GitHub Issues/Releases.

Impact:
These items are useful backlog material, but they are not accepted current architecture and must not be added to current-state or modules-index as active tools unless the owner/architect accepts them.

Expected:
Keep these proposals as source/backlog material. Promote individual items only after explicit architect decision, then update architecture-decisions, modules-index, current-state, and relevant tool docs.

Links or files:
- ideas.md
- tool-proposals.md

## 2026-05-21 09:00 | developer-agent | problem | new

Context:
Development documentation migration reviewed test-plan and test-results source files.

Observation:
Multiple test result files report that PS stdout can corrupt Polish characters or return mojibake, while PY output and PY file UTF-8 tests pass.

Impact:
Documentation must not claim that all PowerShell output encoding problems are fully solved. The invariant remains valid for generated files and explicit UTF-8 writes, but PS stdout display may still require separate handling.

Expected:
Clarify current state: explicit UTF-8 is mandatory, PY UTF-8 works, PS generated file writes should use explicit UTF-8, and PS stdout mojibake remains an open issue unless verified fixed by a later test.

Links or files:
- test-results-v3.md
- test-results-v4.md
- tools-test-results.md

## 2026-05-21 09:00 | developer-agent | problem | triaged

Context:
Test result sources include a historical failed sandbox download pipeline in test-results-v4.md, while later live OpenBrowser checks verified automatic ChatGPT sandbox file downloads.

Observation:
Older v4 test results reported that sandbox file was not downloaded and HST_STATUS did not list it. Later current-state documentation and live checks verified openbrowser_real_sandbox_test.txt download handling.

Impact:
Old test-results-v4 failure should be treated as historical evidence, not current truth, unless the current pipeline fails again.

Expected:
Keep current-state based on latest verified live behavior, but retain this note as a warning that download behavior has had a prior failure mode.

Links or files:
- test-results-v4.md
- docs/agent/current-state_new.md
- docs/modules/Downloads/index_new.md

## Entry format

```text
## YYYY-MM-DD HH:mm | <source> | <type> | <status>

Context:
<what the Operator or model was trying to do>

Observation:
<what happened or what was noticed>

Impact:
<why it matters, what work was blocked, slowed down, or made unclear>

Expected:
<what should happen instead, if known>

Links or files:
- <optional paths, logs, downloads, screenshots, commands>
```

## Allowed values

Source:
- operator
- openbrowser-model
- developer-agent

Type:
- problem
- failure
- missing-capability
- confusing-instruction
- manual-workaround
- idea
- question

Status:
- new
- triaged
- converted-to-task
- needs-architect-decision
- done
- rejected

<!-- COMPLETE_DOC_UPDATE_FIELD_NOTES_20260521 -->
## 2026-05-21 | Model help, feedback, and text transport observations

Observed issues to keep visible:
- model operators need explicit command frame examples, not only command names;
- HST_HELP must explain OpenBridge, HST commands, tool help commands, and RAW/Base64 framing;
- tool help commands should display tools/<tool>/README.md and state the displayed path;
- executor-specific anti-patterns belong in executor help files;
- old README files and legacy documents can mislead models if not routed through help;
- text transport and encoding must be treated as an architecture issue in both directions: model to executor and executor to model;
- models need one factual place for operational observations after tasks.
<!-- /COMPLETE_DOC_UPDATE_FIELD_NOTES_20260521 -->
