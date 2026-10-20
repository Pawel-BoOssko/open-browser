# Open Browser checkpoint: 2026-10-20 05:35 CEST, v1.72.13 wake preflight

Purpose:
Continue existing roadmap P0/Faza 5 work: fix unreliable multi-chat wake delivery, duplicate attempts, cross-chat risks, and stale drafts. No new product scope.

Depends on:
- docs/design/openbrowser-master-roadmap-v3-2026-10-19.md
- docs/openbrowser-checkpoint-2026-10-19-1717-multichat-wake.md
- docs/agent/READ_BEFORE_WORK_new.md

Impacts:
- src/BridgeBrowserAlpha0/LiteChatWakePreflight.cs
- src/BridgeBrowserAlpha0/LiteChatWakeRegistry.cs
- src/BridgeBrowserAlpha0/LiteChatWakeUi.cs
- src/BridgeBrowserAlpha0/LiteResumeSender.js
- tests/lite-resume-send-race.test.cjs
- src/BridgeBrowserAlpha0/BridgeBrowserAlpha0.csproj
- docs/agent/current-state_new.md; docs/feedback/field-notes_new.md; roadmap

Update rule:
Document each build, runtime and test tier independently. Do not mark LIVE without confirmed on-device evidence.

## Evidence before changes

PBOX1 on 2026-10-20 05:21:
- ACTIVE LIVE v1.72.12 PID20736, executable under Programs/OpenBrowserChatGPTLite_v1.72.12_20261019_1746.
- ALSO active legacy v1.64.0 PID18064, started 2026-10-20 01:32:16. Its provenance, startup task and purpose require investigation; no process was terminated.
- Log lite-events-2026-10-20.jsonl (1288 lines at inspection): 17 multi_wake_send attempts, only 4 with visible unique receipt ACK; 2 RenderProcessUnresponsive. Many `phase=form_submitted, receiptConfirmed=false`. Some attempts 20–40 seconds apart, serving two different configured schedules, not proof of a duplicate to the same actual conversation. Incoming user turn contains two Open Browser wake messages with distinct receipts; cannot yet establish why both appeared in the same chat.
- DPAPI registry had only TWO explicitly enabled chats: project suffix b74f0fc1f389, due 05:45:47, and book suffix bb4284f58c79, due 05:45:07. Their prompt texts were different and correct. The live worker navigated by URL but treated only the presence of a composer as readiness, could send while assistant generating or on a draft, and treated unhydrated form requestSubmit as if submitted.
- Current repository has long-standing staged relocation and many untracked files. DO NOT reset/clean, DO NOT do manual Git commit/push.

## SOURCE v1.72.13 implemented

1. New pure `LiteChatWakePreflight`: two observed samples for target pathname, role assistant, actual response, no generation/tools/draft/error, same SHA256 fingerprint in both samples, not same fingerprint previously attempted. Recognizes exact final output `[OPENBROWSER-PLAN-COMPLETE]` and `[BOOK-COMPLETE]`; disables completed chat, not all schedules.
2. Worker injects existing `LiteResumeProbe.js` in addition to sender script, samples at 1.5s separation. Does not mark due answer as attempted until positive read-only preflight. If unready, defers 2 min; previously-attempted same answer waits full configured interval.
3. Each DPAPI chat gains optional `LastAnswerFingerprint`, backwards-compatible for existing schema 1 files. Persisted before JS injection to fence uncertain delivery across restart. Explicit reconfiguration clears the fingerprint so operator can restart. A definitely-unsent busy/missing composer result releases the fence and defers 2m. A potentially uncertain click retains the fence.
4. `sendDirect` refuses invalid URL/receipt, generation or a pre-existing user draft. `submitDirect` checks that the same editor and injected payload are still present and that generation has not started. A late JS timer cannot submit after a blocked state.
5. Background sender uses host-managed direct submit, NOT throttled JS timers. Removes `form.requestSubmit` and CDP Enter fallback which frequently had no DIL ACK. Missing button is a documented failure, not a claim of delivery; no speculative resubmission.
6. Tests now cover busy preflight, changed owner draft, click idempotence, and explicit host management. JS send race currently 26 PASS and .NET Release build exit0 as of 05:33; final full tests and PBOX publish to follow.

## Status / next actions

- SOURCE only at this checkpoint. PBOX1 LIVE still 1.72.12 at start of turn.
- Run complete JS regression and PBOX1 staged build+selftest via existing Autosync. Keep 1.72.12 fallback.
- Validate exact scheduled task launching obsolete 1.64.0, no unilateral stop until purpose confirmed.
- Controlled live promotion only when user session is idle; do not interrupt an active model answer.
- Observe natural wakes after promotion and revise status. Never claim two natural independent wake cycles merely from a successful build.

## Update 2026-10-20 05:39 CEST

- SOURCE v1.72.13 now also acquires a Windows session-local named mutex `Local\OpenBrowserLiteChatWake_v1`. One upgraded Lite window supervises the DPAPI queue; a second upgraded window retains UI/settings but does not send. Mutex released on graceful FormClosing and abandoned ownership recoverable after crashes. Limitation: 1.72.12 and earlier do not honor this lock, so never run old+new supervisory instances concurrently.
- Fixed hidden JS timer race: `sendDirect(text,receipt,true)` disables self-scheduled timer in worker. Host explicitly calls `submitDirect`; checks for changed composer, active generating and exact injected text; no `form.requestSubmit` fallback and no CDP Enter fallback. Host breaks at terminal blocked states, avoiding a delayed click after a failed preflight.
- Test evidence SATANAMD: 16/16 JS regression suites PASS, sender 26/26 PASS, Release publish exit0, headless chat wake selftest exit0. `STAGED` output SATANAMD `%LOCALAPPDATA%\OpenBrowser\staged\OpenBrowserLite_v1.72.13_20261020`. Production PBOX1 remains v1.72.12 until safe migration.
- Investigated legacy PBOX1 v1.64 PID18064: parent `explorer.exe`. **Root cause of its re-launch:** taskbar pinned `BridgeBrowserAlpha0.lnk` targets `OpenBrowserChatGPTLite_v1.64.0_20261018`; desktop and Start shortcuts instead target v1.72.11. Windows scheduled task `OpenBrowser ChatGPT Lite` points to an even older telemetry EXE, but this process's parent was Explorer. No processes terminated.
- Existing safe deploy script `Documents\deploy-openbrowser-safe-20261019.ps1` forcibly calls `Stop-Process` on ALL user-session BridgeBrowserAlpha0 processes, including legacy 1.64, and is not safe to reuse without modification. Promotion must avoid interrupting an active answer or unsaved notes; no indiscriminate kill.
