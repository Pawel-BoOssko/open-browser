# Downloads Module Index

Purpose:
Describes the Downloads module and its role in OpenBrowser/OpenBridge file handling.

Depends on:
- docs/templates/document-header-template_new.md
- docs/runtime/modules-index_new.md
- docs/templates/module-docs-template_new.md
- docs/agent/current-state_new.md
- docs/modules/Downloads/help_new.md
- docs/modules/Downloads/rules_new.md
- docs/test-sources-outline_new.txt
- test-results-v4.md

Impacts:
- docs/modules/Downloads/help_new.md
- docs/modules/Downloads/rules_new.md
- docs/runtime/hst-status_new.md
- docs/runtime/hst-help_new.md
- docs/runtime/start-prompt_new.md
- docs/agent/current-state_new.md

Update rule:
Update this file when automatic download behavior, downloads directory handling, recent downloads reporting, file detection, historical failure status, or documentation path changes.

Latest:
- Test source migration is reflected here.
- Downloads is the runtime module responsible for documented OpenBrowser file-download behavior.
- ChatGPT sandbox downloads are currently verified live in current-state documentation.
- Older test-results-v4 recorded a sandbox download failure; treat it as historical unless reproduced.
- Download status is exposed through HST_STATUS recent downloads.
- Download details and model-facing instructions belong in docs/modules/Downloads/help_new.md.
- Download rules and limitations belong in docs/modules/Downloads/rules_new.md.

## Responsibility

Downloads owns documentation for automatic file-download handling in OpenBrowser.

Current documented capability:
- auto-detection of ChatGPT-generated files;
- guarded activation of file controls;
- WebView2-native download handling;
- local save to the OpenBrowser downloads directory;
- recent download visibility through HST_STATUS.

## Current verification status

Current state:
- live ChatGPT sandbox file download handling is verified in docs/agent/current-state_new.md.

Historical warning:
- test-results-v4.md reported that sandbox file creation did not result in a local download and did not appear in HST_STATUS.
- Later live checks supersede that failure as current state.
- If the failure reappears, update field notes and current-state.

## Boundaries

Downloads does:
- document how generated files become local files;
- document how models should check recent downloads;
- document constraints around generated files and download reporting.

Downloads does not:
- own general filesystem operations;
- replace PS or PY file inspection;
- define all artifact-generation behavior.

## Related documents

- docs/modules/Downloads/help_new.md
- docs/modules/Downloads/rules_new.md
- docs/runtime/hst-status_new.md
- docs/agent/current-state_new.md
- docs/feedback/field-notes_new.md

## Implementation evidence

- releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/PageTap.js: detects ChatGPT download buttons, including button.behavior-btn, and dispatches click sequence.
- releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/BrowserTabRuntime.cs: subscribes to DownloadStarting, saves downloads to AppPaths.Downloads, and logs download state changes.
- releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/AppPaths.cs: defines Downloads path and EnsureAll creates it.
- releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/OpenBridgeHost/OpenBridgeRuntimeApproval.cs: HST_STATUS lists recent downloads.
- Current AppPaths.Downloads evidence resolves to releases/BridgeBrowserAlpha0/downloads/.