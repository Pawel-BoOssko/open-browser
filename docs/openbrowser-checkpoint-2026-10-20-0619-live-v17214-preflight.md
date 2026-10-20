# Open Browser checkpoint, 2026-10-20 06:19 CEST

Purpose:
Continue OB3-009/025/055/063, safely promote multi-chat wake preflight and resolve a real book chat preflight stall.

Depends on:
- docs/design/openbrowser-master-roadmap-v3-2026-10-19.md
- docs/openbrowser-checkpoint-2026-10-20-0535-wake-preflight.md
- docs/agent/READ_BEFORE_WORK_new.md

Impacts:
- PBOX1 LIVE browser, direct background worker and local DPAPI configuration
- src/BridgeBrowserAlpha0/LiteChatWakeUi.cs
- src/BridgeBrowserAlpha0/LiteChatWakePreflight.cs
- src/BridgeBrowserAlpha0/BridgeBrowserAlpha0.csproj
- docs/agent/current-state_new.md; docs/feedback/field-notes_new.md

Update rule:
Every stated LIVE or real-delivery claim must match an observed process, test or metadata log. Do not log conversation text or private URLs.

## Verified LIVE promotion

- PBOX1 old main Lite v1.72.12 PID20736 and separate legacy v1.64 PID18064 online at 06:13.
- Created verified recoverable **graceful-only** deployment script `C:\Users\pboru\Documents\deploy-openbrowser-graceful-20261020.ps1`. PowerShell AST 0 errors; compared exact old process path and candidate v1.72.14 SHA256, backed up DPAPI registry/settings in `Documents\OpenBrowserWakeBackup_20261020_0615`. CloseMainWindow requested only for PID20736; old app exited gracefully at 06:14:33 without Stop-Process.
- Newly launched v1.72.14 via explicit `--chatgpt-lite` and checked window responsiveness. Script result `RESULT_CANDIDATE_LIVE` 06:14:48. PBOX1 process PID18424, exact release path `OpenBrowserChatGPTLite_v1.72.14_20261020_0551`, `window_constructed` event confirms `1.72.14+build.20261020T035140Z.092ce982...`. WebView2 initialized v155.0.4283.62. Legacy v1.64 PID18064 untouched.
- Registry DPAPI survived, with **two enabled independent chats**: Open Browser project suffix b74f0fc1f389 and book suffix bb4284f58c79, each 25m. No ChatGPT native Tasks, Git manual commits/pushes, cache resets or hard-kills.
- Real new-version E2E test: intentionally triggered only the book chat with existing `--lite-chat-wake-now` at 06:15:53. Live new process initialized independent worker 06:16:01, inspected book conversation and logged `multi_wake_preflight_skipped {reason:assistant_not_complete,retryMinutes:2,definitelyNotSent:true}` at 06:16:08. Book state persisted `waiting_assistant_not_complete`, due moved to 06:20:20 by 06:18:39. No wake submitted, no accidental duplicate. Cause of assistant_not_complete not yet determined: genuinely unfinished assistant, last role user or empty DIL text are possibilities; do not guess.
- Project's last confirmed own wake remains 06:11:07 under previous v1.72.12. Next natural project due 06:36:07; new-version natural project wake not yet observed.

## SOURCE v1.72.15 diagnostic extension

- Add only metadata on failed preflight: first/second last role, response char count, number of user/assistant nodes, active generation/tool/draft/error flags and whether target pathname matches; no text/URL/cookies.
- Expanded observation schema with LastAssistantChars and UserNodeCount; backwards compatible.
- SATANAMD Release publish exit0 and all 16 JS test suites passed at 06:19:15. Next existing Autosync due about 06:20:56, then isolated PBOX build and safe graceful deploy after staged verification.
- Pending: examine real metadata, fix root cause (not simply bypass all guards), then confirm a unique book receipt, notes inclusion, two independent natural wake cycles and durable once-per-answer fencing. New version 1.72.15 SOURCE ONLY at this checkpoint.
