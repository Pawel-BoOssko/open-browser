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
