# Downloads Module Rules

Purpose:
Defines rules, limitations, and update obligations for OpenBrowser automatic download handling.

Depends on:
- docs/templates/document-header-template_new.md
- docs/modules/Downloads/index_new.md
- docs/modules/Downloads/help_new.md
- docs/runtime/hst-status_new.md
- docs/agent/current-state_new.md
- test-results-v4.md

Impacts:
- docs/modules/Downloads/index_new.md
- docs/modules/Downloads/help_new.md
- docs/runtime/hst-status_new.md
- docs/runtime/hst-help_new.md
- docs/runtime/start-prompt_new.md
- docs/agent/current-state_new.md
- docs/agent/documentation-update-checklist_new.md

Update rule:
Update this file when download detection, click behavior, WebView2 download handling, downloads path, recent downloads reporting, generated-file workflow, or historical failure status changes.

Latest:
- Download handling should rely on the browser/WebView download flow, not private ChatGPT download API reconstruction.
- Generated ChatGPT sandbox files are currently auto-detected and downloaded according to latest verified current-state evidence.
- Older test-results-v4 recorded a failed sandbox download pipeline and must be treated as historical unless reproduced.
- HST_STATUS should expose recent downloads.
- Models should verify downloaded files through HST_STATUS and available executors.

## Rules

### DL-RULE-001: Prefer browser-native download flow

Use browser/WebView-native download handling for ChatGPT-generated files.
Do not rebuild ChatGPT private download API flows unless explicitly required for a separate investigation.

### DL-RULE-002: Status must expose recent downloads

Recent downloaded files should be visible through HST_STATUS when available.

### DL-RULE-003: File generation and local verification are separate steps

Generating a file in ChatGPT sandbox is not the same as verifying the local downloaded file.
Use HST_STATUS and available executors to verify local download results.

### DL-RULE-004: Avoid duplicate download activation

Download detection should guard against repeatedly activating the same file control.

### DL-RULE-005: Keep download docs model-facing

Download help should tell the model how to use generated files and recent downloads.
Implementation details belong in code or developer documentation, not in model-facing help unless they affect behavior.

### DL-RULE-006: Historical download failures must not override newer verified state

If an older test result reports download failure but later live current-state evidence verifies downloads, the older failure is historical.
If reproduced, create a new field note and update current-state.

## Documentation triggers

Update Downloads documentation when:
- generated-file detection changes;
- download activation changes;
- WebView2 download behavior changes;
- downloads directory changes;
- HST_STATUS recent download reporting changes;
- model-facing file-generation instructions change;
- new test evidence changes the verified state.