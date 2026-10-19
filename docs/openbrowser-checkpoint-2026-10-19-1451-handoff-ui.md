# Open Browser Lite: checkpoint 14:51, handoff UI i pierwszy cykl DevWake

Purpose:
Rework OB3-012 and OB3-061 so the owner can explicitly rebind DevWake to the visible ChatGPT conversation without copying a URL or restarting the old chat. Preserve legacy direct send and its 25-minute cadence.

Depends on:
- docs/design/openbrowser-master-roadmap-v3-2026-10-19.md
- docs/openbrowser-checkpoint-2026-10-19-1421-chat-handoff-rebind.md
- docs/agent/READ_BEFORE_WORK_new.md

Impacts:
- src/BridgeBrowserAlpha0/LiteDevWakeState.cs
- src/BridgeBrowserAlpha0/LiteDevWakeUi.cs
- src/BridgeBrowserAlpha0/ChatGptLiteForm.cs
- src/BridgeBrowserAlpha0/Program.cs
- src/BridgeBrowserAlpha0/BridgeBrowserAlpha0.csproj
- docs/agent/current-state_new.md

Update rule:
Append actual tests and deployment evidence to this checkpoint. Do not mistake first successful wake for full two-cycle confirmation.

Latest:
- 2026-10-19 14:51 CEST, SOURCE+TEST v1.72.6. Production remains v1.72.5 until a later deployment checkpoint.

## Evidence from first real wake after handoff
- 2026-10-19 14:45:23 CEST: ChatGPT request arrived in CORRECT new conversation, and this chat resumed work on the requested roadmap. PBOX1 PID 13856, v1.72.5.0, Responding=True, pinned chat suffix b74f0fc1f389, direct_submission_requested and next due 15:10:23. This is first confirmed user-visible wake after rebind; second 25-minute cycle still pending. OB3-018 is PARTIAL LIVE, not COMPLETE.
- No native ChatGPT Tasks created, no manual Git commit/push.

## Implemented in SOURCE v1.72.6
- New operator button 'Przypnij ten czat' immediately beside Autopraca toggle in ChatGptLiteForm toolbar.
- UI click reads current WebView2 URL. Only a normal chatgpt.com/c/{id} conversation can be pinned; confirmation displays final 12 id chars and explicitly states previous conversation stops receiving wakes.
- PinCurrentConversation saves new target via existing encrypted DPAPI state, resets prior attempt/receipt ledger when the chat identity changes, and sets next wake due in 25 minutes. If a wake attempt is active, UI declines to rebind until it finishes.
- UI shows the pinned conversation ID suffix and next wake time for auditability. Exact target URL remains in local encrypted PBOX configuration, not in Git.
- New LiteDevWakeState.ArmForChat method is reused by both CLI --lite-dev-wake-arm and WinForms UI; existing CLI behavior preserved.
- Direct message insertion/sending in v1.72.5 untouched. Operator can restart and rollback using the existing PBOX1 script.

## Tests
- SATANAMD Release dotnet build: exit0, 0 errors, known WindowsBase MSB3277 warning.
- SATANAMD app --lite-dev-wake-selftest: exit0; added three new cases: rebind resets attempt history, rearm same chat preserves record, reject non-chat URL. Total recorded checks raised from 30 to 33.
- Node LiteResumeSenderRace 18 PASS, HighGuard 18 PASS, HighSlider 5 PASS, all exit0.
- Full WebView2 GUI confirmation and PBOX1 deployment: TODO, not claimed in this preliminary checkpoint.

Next:
1. Build/publish v1.72.6 from Autosync snapshot on PBOX1 in separate directory.
2. Use existing C:\Users\pboru\Documents\deploy-openbrowser-safe-20261019.ps1 with fallback to LIVE 1.72.5; do not use -SendNow.
3. Verify PID/version, new button in accessible toolbar, correct saved chat suffix and next due time; send only on normal scheduled 25-min cycle.
4. Update docs/agent/current-state_new.md and append actual evidence.
5. OB3-018: confirm second correctly routed cycle after next due; do not artificially mark it complete.

## Deployment append: 2026-10-19 14:56 CEST

- Autosync snapshot `4e218b2b12e99dca878e772a071bccef25892e96` was fetched into a separate PBOX1 Git worktree at `%LOCALAPPDATA%\OpenBrowser\build-source-v1.72.6`. No manual commit or push.
- SATANAMD Release publish v1.72.6.0 PASS; SHA256 `6B71BD1219C149E8C0248E57F842DBD8F5E48BE1D453F4A8D7B533A3FE6A4EBD`. JS promotion gate 9/9 PASS, DevWake selftest 33 checks PASS, Node sender 18 + HighGuard 18 + HighSlider 5 PASS.
- PBOX1 Release publish v1.72.6.0 PASS (SDK builds differ by timestamp). EXE SHA256 `194ECE0ED144BBACFB64174B9728EEE70B7C7AB31119A6D5A88370E17C8A7D3F`. Direct sender remains present. Selftest exit0.
- 14:52:50: ran `C:\Users\pboru\Documents\deploy-openbrowser-safe-20261019.ps1` with Candidate 1.72.6 and Fallback 1.72.5, **without** -SendNow. Old PID13856 stopped, PID21332 healthy at 14:52:56, deploy exit0. 1.72.5 remains intact as recovery candidate.
- 14:53:20: read-only Windows UIAutomation in LIVE PID21332: `Autopraca: WŁ. 25m` button, `Przypnij ten czat` button and `Czat b74f0fc1f389 | Budzenie 15:10` visible. DPAPI metadata confirms Enabled=True, correct target, due 15:10:23 CEST. No unrequested message sent.
- 14:55:35: both Desktop and Start Menu shortcuts independently verified to point to LIVE 1.72.6.0, `--lite-open-safely`.
- OB3-081 (existing Windows startup task) attempted as separate dependent housekeeping: **BLOCKED** on `Set-ScheduledTask` with AccessDenied HRESULT 0x80070005. Actual task action still points at old telemetry binary with `--chatgpt-lite`. NO retry via other tools and NO new scheduled task. Failure logged in `docs/Rejestr nieudanych komend.md`.
- OB3-012: LIVE UI visual feedback confirmed, operator action logic built/selftested, but actual click with a *different* live chat not exercised, so partial verification. OB3-061: SOURCE+TEST+LIVE UI availability, not full repeated handoff E2E. OB3-018: first correct 14:45 wake completed; second cycle due 15:10:23 pending.
- Next: await independently observed second wake (if another conversation response is completed), inspect PBOX1 process/config without changing pinned chat; choose next roadmap item. Full roadmap remains open.
