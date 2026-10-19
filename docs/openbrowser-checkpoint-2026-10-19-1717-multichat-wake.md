# Open Browser Lite 1.72.8: multisession wake recovery

Purpose:
Fix 2026-10-19 15:35 missed wake caused by a different visible ChatGPT conversation; enable independently configurable per-chat wakes, and append each chat's encrypted notes when waking.

Depends on:
- docs/design/openbrowser-master-roadmap-v3-2026-10-19.md
- docs/openbrowser-checkpoint-2026-10-19-1512-second-wake.md
- docs/agent/READ_BEFORE_WORK_new.md

Impacts:
- src/BridgeBrowserAlpha0/LiteChatWakeRegistry.cs (new)
- src/BridgeBrowserAlpha0/LiteChatWakeUi.cs (new)
- src/BridgeBrowserAlpha0/ChatGptLiteForm.cs
- src/BridgeBrowserAlpha0/LiteDevWakeUi.cs
- src/BridgeBrowserAlpha0/Program.cs
- src/BridgeBrowserAlpha0/BridgeBrowserAlpha0.csproj
- docs/agent/current-state_new.md
- docs/design/openbrowser-master-roadmap-v3-2026-10-19.md

Update rule:
Log only verified state. Never equate a requested send with a delivered user message; do not report background WebView2 E2E pass until actually observed.

## Root cause of missed wake

PBOX1 LIVE v1.72.7 PID20956 continued running across 15:35. On 2026-10-19 15:36:27 CEST, local `lite-events-2026-10-19.jsonl` records `dev_wake_skipped reason=other_chat`, repeated at 15:46:27. Existing single-record scheduler compared currently visible WebView2 URL with one pinned target. User had navigated to another chat. It did not send because URL comparison intentionally protected against cross-chat delivery. No crash or sleeping timer evidence.

## SOURCE 1.72.8

1. Local encrypted DPAPI registry `chat-wakes.dpapi`: explicitly enabled individual chat URLs, interval 20–1440m, text prompt, next due, last attempt and status. Existing single-chat DPAPI settings are imported once, preserving the previous project schedule.
2. New operator UI for current conversation: `Ustaw ten czat`, `Wybudzenia`, per-chat enable/pause, schedule/prompt editor. No longer replacing previously enabled schedules. Each chat can configure its own wake through local `--lite-chat-wake-set URL minutes prompt`, pause `--lite-chat-wake-off URL`, or one-off due-for-test `--lite-chat-wake-now URL`.
3. Independent WebView2 worker, using same Windows local profile, navigates to a due allowed chat and inserts/sends via the already-working direct sender. Main user-visible tab remains where the owner navigated. A single worker serializes due chats.
4. Before each attempt, next due is stored; each chat has its own attempt state. Load chat-specific notes via `LiteChatNotes.Load` and include in the outgoing text under explicit heading. Notes stay encrypted locally, are not written to source/logs. Large notes over 24000 chars are clearly marked as truncated.
5. Legacy single-chat sender disabled when multi-registry initializes, preventing duplicate deliveries from old and new timers.

## Early verification
- SATANAMD Release compilation: PASS, zero errors (historical MSB3277). `--lite-chat-wake-selftest` exits without error, testing independent due times, prompt isolation and message composition.
- LIVE not yet upgraded; isolated and real-background send QA still pending.
- Book test chat located from local PBOX1 WebView2 history: `Napisz książkę przygodową`, URI suffix `bb4284f58c79`. Another `local-chatgpt:` URL from the browser history was deliberately NOT selected.
- No new native ChatGPT Tasks or Windows scheduled tasks. No manual commit/push; existing Autosync only.

Next:
- Publish and verify 1.72.8, use safe restart/fallback script on PBOX1.
- Configure book test via CLI, manually make its next due immediate, verify that its message appears in the book chat while foreground remains on a different chat. Confirm notes via unit and, if available, real note content.
- If background worker is unable to navigate or click (hidden WebView2), keep checkpoint OPEN and correct the root cause, without marking success.

## Verification 17:30 CEST: REAL book wake #2

- Production v1.72.9 PID7512 built/published on PBOX1, deployment recovery script exit0, fallback v1.72.8 available.
- Book chat separately enabled with 20min interval. Temporary test note added via local CLI; old note state was empty. Test command made book due once without altering project scheduling.
- Actual background worker started at 17:30:23 CEST; log at 17:30:31 CEST: `state=submission_requested`, `phase=draft_button_missing`, `visibleUserBubble=false`, `includesNotes=true`.
- Therefore note composition was verified but actual book message WAS NOT delivered. Do not mark delivery as PASS. Synthetic JS Enter was explicitly removed because it was previously logged without visible success.
- Next fix SOURCE 1.72.10: when the background WebView2 lacks a Send button after 2s, select its own composer and use WebView2 CDP `Input.insertText` to generate a native input event; try Send click, then browser-level Enter; log phase and check user bubble. Visible foreground chat remains untouched. Does not create OpenAI scheduled tasks.

## Verification 17:41–17:45 CEST: unique-marker follow-up and root cause refinement

- LIVE v1.72.11 PID20580 process remained Responding=True at 17:42:01 despite 17:39:40 `webview_process_failed RenderProcessUnresponsive` (exitCode=259), and subsequent periodic main UI heartbeats continued. Exact relation between worker render and main renderer unresponsiveness not proven.
- A newly configured test prompt with unique `[OB-BOOK-1741-UNIQUE]` was made due through app CLI at 17:41:28. At 17:41:52 PBOX1 logged `multi_wake_send {state:submission_requested, phase:direct_injected, visibleUserBubble:false, includesNotes:true}`. Thus earlier 17:38:54 `visibleUserBubble:true` was potentially a false historical match from a repeated same prompt. Do NOT certify end-to-end delivery based on that old check.
- Main technical cause found: Direct JS sender depended on `window.setTimeout` to initiate button click. Background/occluded WebView2 throttles in-document callbacks even while the C# host clock runs, explaining `phase:direct_injected` 2.8s after injection.
- SOURCE v1.72.12: `LiteResumeSender.submitDirect(receipt)` exposed for explicit immediate host-side invocation, preserving existing timer for normal foreground sending. Idempotent guard ensures that a late timer does not double-click after host flush.
- C# background worker calls `submitDirect` directly up to 7 attempts at host-clock intervals, records real click/form phase, only falls back to CDP if no valid Send action. Every attempt prefixes payload with unique `[OB-<receipt>]`, and `visibleUserBubble` checks this exact receipt instead of a reusable text snippet; `receiptConfirmed` property and persisted state distinguish true unique visible ACK vs attempted send. Notes still appended per target chat.
- JS regression suite v1.72.12: 20/20 sender PASS, HighGuard 18/18 PASS, HighSlider 5/5 PASS prior to receipt-prefix addition; .NET build after receipt-prefix change and deployment to follow.
- No new ChatGPT scheduled tasks. Book test chat and main Open Browser project are the only two locally enabled records. No manual commit/push.

## LIVE confirmation 17:47–17:49 CEST: background wake repaired in v1.72.12

- PBOX1 Autosync source snapshot `c1c7d425ec8c5b7867bd3073b596a5b86a7e666a`, standalone release built and side-by-side published as `%LOCALAPPDATA%\Programs\OpenBrowserChatGPTLite_v1.72.12_20261019_1746`. `--lite-chat-wake-selftest` PASS, manifest LIVE created. Candidate deployed with existing safe fallback script, script exit0 and old 1.72.11 retained.
- LIVE PID20736, version 1.72.12.0, Responding=True as of 17:48:54. UIAutomation shows `Autopraca: WŁ. 25m`, `Ustaw ten czat`, `Wybudzenia`, `Następne: 18:02 | Aktywne czaty: 2`. Foreground UI remains separate from worker.
- Core fix: `LiteResumeSender.js` has host-called synchronous `submitDirect(receipt)` and idempotent late-timer check. C# invokes it 7 times at host-clock intervals, not reliant on page `setTimeout` in occluded tab. Includes CDP fallback when no valid send button.
- Each wake now prefixes `[OB-<unique random 32hex receipt>]`, then prompt and any chat notes. The verification checks that unique receipt in last 6 DIL user bubbles, not a repeated prompt snippet.
- **Actual independent real-book wake at 17:47:53 CEST:** PBOX1 JSONL event `multi_wake_send`: `state=submission_requested,phase=click_invoked,visibleUserBubble=true,receiptConfirmed=true,includesNotes=true,intervalMinutes=20`. The registry independently persisted `LastState=visible_ack`, with book next due 18:07:45. This resolves prior false-positive risks from repeated prompts.
- Background chat used: `Napisz książkę przygodową` (ID suffix bb4284f58c79). Another enabled chat is primary Open Browser project (ID suffix b74f0fc1f389), with due 18:02:33. Their schedules are independent. Test book note was deliberately created by assistant and is marked `[NOTATKA TESTOWA]` about a compass.
- Existing single-chat timer does not issue duplicate sends because `_multiWakeEnabled` blocks its Poll path. No manual Git commits or push, no native OpenAI Tasks, no new Windows Task Scheduler entries.
- JS focused tests 20/20 sender, 18/18 HighGuard, 5/5 HighSlider PASS; .NET Release and PBOX chat wake selftest PASS.
- **Qualification limits:** visible user bubble with unique receipt is strong client-side send confirmation; explicit receipt check cannot by itself prove a response has been generated by the ChatGPT backend or completion of the book text. No automatic assistant reply yet independently checked. Some earlier 1.72.11 logs reported RenderProcessUnresponsive in main WebView2, so monitor 1.72.12 for recurrence.
- Next tasks: verify two independent future scheduled cycles, verify note text appears in same DIL user bubble without logging its contents, improve per-chat status/stop control, handle renderer recoveries and stale drafts, optional cleanup assistant-created book note after tests.
