<!-- LEGACY_DOCUMENT_NOTICE -->
> Legacy document notice: this is an older documentation file. Its current operational content has been migrated or superseded by: docs/agent/current-state_new.md, docs/modules/Downloads/index_new.md, docs/modules/PageTap/index_new.md.
>
> Before using this file as source of truth, read: docs/agent/READ_BEFORE_WORK_new.md.
>
> Treat this file as historical or secondary unless the new documentation explicitly points back to it as an active operational reference.
<!-- /LEGACY_DOCUMENT_NOTICE -->

# OpenBrowser Handoff — 2026-05-20

## Current status

- Live ChatGPT sandbox download verified end-to-end on one real generated file.
- Build passes (0 errors; MSB3277 WindowsBase warnings are pre-existing from WebView2 SDK).
- Downloads are saved to `AppPaths.Downloads` (currently resolves to `releases/BridgeBrowserAlpha0/downloads/`).
- Working tree is clean — all feature commits are already on `master` (2 commits ahead of `origin/master`).

## Key files

| File | Role |
|------|------|
| `releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/PageTap.js` | Detects ChatGPT download buttons (including `button.behavior-btn`), dispatches click sequence |
| `releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/BrowserTabRuntime.cs` | Subscribes to `DownloadStarting`, saves downloads to `AppPaths.Downloads`, logs all download state changes |
| `releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/AppPaths.cs` | Defines `Downloads` path, `EnsureAll()` creates it |
| `releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/OpenBridgeHost/OpenBridgeRuntimeApproval.cs` | `HST_STATUS` lists recent downloads |
| `test-plan-v4.md` | Test plan with live test results |
| `handoff.md` | This file |

## What not to redo

- Do not return to old API/message_id/interpreter/download/sandbox_path logic.
- Do not restore old `HandleSandboxLinksAsync` logic.
- Do not run more mockup tests unless debugging a regression.

## Next useful work

1. Run one more live regression test after app restart.
2. Confirm `HST_STATUS` lists the downloaded file.
3. Then push if tests pass — no new commits needed.

## Recommended commit message (if committing test artifacts after next run)

```
feat: add verified ChatGPT sandbox file downloads
```
