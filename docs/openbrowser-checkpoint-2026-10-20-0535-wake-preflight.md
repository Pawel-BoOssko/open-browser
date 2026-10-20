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

## STAGED PBOX1 2026-10-20 05:44 CEST, no live switch

- SATANAMD existing Autosync task ran naturally at 05:40:57 (result 0), syncing source including v1.72.13 and checkpoint to branch `autosync/SATANAMD` at `b1f2ccc`; no manual Git push/commit from this chat.
- PBOX1 fetched existing Autosync branch and created an isolated detached source worktree `%LOCALAPPDATA%\OpenBrowser\build-source-v1.72.13` at `b1f2ccc`. Release publish EXIT 0 to `%LOCALAPPDATA%\Programs\OpenBrowserChatGPTLite_v1.72.13_20261020_0541`.
- STAGED manifest verified `RELEASE_IDENTITY_OK v1.72.13`, EXE SHA256 `0AE12CDCA3C5C27FF8435CCDC60A6F7416010A49CF0A50B671AA4E425BC49E52`.
- Headless PBOX1 selftests `--lite-chat-wake-selftest`, `--lite-dev-wake-selftest`, `--lite-version-selftest`, `--lite-resume-selftest`, `--lite-notes-selftest`, `--lite-transport-selftest`: EACH actual `Start-Process -Wait -PassThru` ExitCode 0. SATANAMD full 16 JS test suites 0 failures, sender 26 PASS.
- Three Windows .lnk updated and verified to point to 1.72.13 EXE with `--lite-open-safely`: pinned taskbar `BridgeBrowserAlpha0.lnk`, Desktop `Open Browser - ChatGPT Lite.lnk`, Start Menu same. This corrects old v1.64 pinned target. They cannot start a second instance while any BridgeBrowserAlpha0.exe is still running.
- 05:43:52 PBOX1 still had PID20736 v1.72.12 (multi-wake LIVE) and PID18064 v1.64 (legacy owner-launched via Explorer) both responsive. **No process was closed or force killed.** New 1.72.13 remains STAGED; no real new-version wake proved.
- Existing `Documents\deploy-openbrowser-safe-20261019.ps1` indiscriminately force-kills every BridgeBrowserAlpha0 process; expressly not used in this pass. Production switch to 1.72.13 still TODO, after active work ends or a proven safe owner-graceful switch. Staged manifest channel remains `staged`, not mislabelled `live`.
- Next: when returning, inspect currently running version, don't assume 1.72.13 is live just because shortcuts were updated. When 1.72.13 launches naturally, verify worker mutex owner, per-chat preflight statuses, independent book/project wake receipts and generation/no-draft gate, note forwarding, and absence of old `form_submitted` without ACK. Check stuck legacy pinned startup task separately if needed.

## Runtime observation after staging, 2026-10-20 05:45 CEST

- Existing LIVE 1.72.12 recorded 05:45:16 CEST `multi_wake_send phase=click_invoked, visibleUserBubble=true, receiptConfirmed=true, includesNotes=true`. This corresponds by schedule timing to the book chat's 05:45:07 due, but the JSONL record contains no chat ID, so attribution is a timing inference only. It is NOT a verification of STAGED v1.72.13.
- The second schedule was due about 05:45:47. Its outcome is not yet confirmed in this checkpoint. Do not claim both cycles succeeded.

## SOURCE 1.72.14, 2026-10-20 05:49 CEST: renderer timeout follow-up

- Actual PBOX1 LIVE 1.72.12 at 05:45:16 CEST confirmed one `multi_wake_send click_invoked,receiptConfirmed=true,includesNotes=true`; corresponding by schedule to book chat due 05:45:07. Second schedule's attempt persisted `LastState=attempting,LastAttemptUtc=05:46:07` at 05:46:42; even at 05:47:04 no send/fail event. UI heartbeats continued every 30s through 05:47:07, indicating background renderer operation likely stalled while host UI stayed responsive. This is an inferred cause, exact await location not instrumented in 1.72.12.
- SOURCE v1.72.14 adds `ExecuteWorkerScriptAsync`: strict 6500ms bound around background `CoreWebView2.ExecuteScriptAsync` calls for navigation DOM presence, two preflight observations, injected sender command, host submit flush, diagnostics and receipt check. Timeout throws a typed TimeoutException. Before durable attempt it triggers definitely-not-sent 2-minute defer; after durable attempt it logs uncertainty and keeps last-answer fingerprint fence, never blindly retries the same assistant answer.
- SATANAMD Release publish exit0, sender JS 26 PASS. Still SOURCE/STAGED SATANAMD only; no PBOX1 v1.72.14 proof at this stage. 1.72.13 STAGED on PBOX1 remains fallback. No forced restart, no additional ChatGPT tasks. To stage 1.72.14 on PBOX1 use naturally scheduled Autosync; do not manually git push.

## PBOX1 STAGED 1.72.14 verified, 2026-10-20 05:53 CEST

- SATANAMD natural scheduled Autosync ran 05:50:57, result 0; exact branch snapshot `092ce98` (v1.72.14). No manual Git commit or push.
- PBOX1 fetched `origin/autosync/SATANAMD`, created detached worktree `%LOCALAPPDATA%\OpenBrowser\build-source-v1.72.14`, published Release with exit 0 to `C:\Users\pboru\AppData\Local\Programs\OpenBrowserChatGPTLite_v1.72.14_20261020_0551`.
- Manifest created and verified as STAGED, release identity `RELEASE_IDENTITY_OK v1.72.14`. Executable SHA256 `D32C1CF5AC55C989AAC44D81F0DE9338A6FB6B94B4198F2142AD23BD8C88A5A4`.
- PBOX1 direct headless EXE tests (using Start-Process -Wait -PassThru): `--lite-chat-wake-selftest`, `--lite-dev-wake-selftest`, `--lite-resume-selftest`, `--lite-transport-selftest`: each exit code 0. SATANAMD JS full regression suites 16/16 pass, sender suite 26 tests pass.
- PBOX1 three Windows .lnk files now point to v1.72.14 and `--lite-open-safely`: pinned taskbar BridgeBrowserAlpha0.lnk, Start Menu Open Browser - ChatGPT Lite.lnk, desktop Open Browser - ChatGPT Lite.lnk. All three changes verified.
- **No LIVE promotion.** Last confirmed running instances: v1.72.12 PID20736 (main supervisor), v1.64 PID18064 (legacy, started from old taskbar shortcut before fix). Both left running without forced stop, preserving user conversation and notes. Existing deploy script force-kills all BrowserAlpha0 processes and was intentionally not run. New `--lite-open-safely` prevents opening a new process while an older one is active.
- Some live v1.72.12 send attempts still fail or hang because v1.72.14 fixes are only staged. Next developer-agent step is a controlled, non-destructive upgrade when the older app windows can be closed gracefully, followed by two independent real per-chat receipt+preflight tests, renderer timeout observation and no-duplicate verification. Roadmap is NOT complete. No ChatGPT Tasks or reminders created.
