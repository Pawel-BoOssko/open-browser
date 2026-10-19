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
