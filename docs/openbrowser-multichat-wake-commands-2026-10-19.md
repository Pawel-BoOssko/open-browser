# Open Browser Lite: independent chat wake CLI

Purpose:
Give project chats a documented, local way to configure their own wake schedule and notes through the running Open Browser Lite installation, instead of creating a ChatGPT Task or requiring the currently visible chat to remain selected.

Depends on:
- docs/design/openbrowser-master-roadmap-v3-2026-10-19.md
- src/BridgeBrowserAlpha0/LiteChatWakeRegistry.cs
- src/BridgeBrowserAlpha0/LiteChatWakeUi.cs

Impacts:
- PBOX1 application CLI and local DPAPI configuration
- Chat operator runbooks and handoff workflows

Update rule:
Keep command forms in sync with Program.cs and publish verified changes to the latest checkpoint.

## Requirements and scope

Version: LIVE v1.72.12 (2026-10-19). The app must be running in the owner's Windows session for real periodic wake delivery. Multiple enabled conversations are allowed; one browser worker serializes due messages. Each chat is explicitly configured by a local CLI command or the app's "Wybudzenia" UI. The foreground tab may show a different conversation.

Use the installed EXE from the CURRENT verified LIVE release directory, not a hard-coded old version. On PBOX1 as of this checkpoint:
`%LOCALAPPDATA%\Programs\OpenBrowserChatGPTLite_v1.72.12_20261019_1746\BridgeBrowserAlpha0.exe`.

PowerShell examples, with `$exe` the verified LIVE EXE and `$url` the intended full ChatGPT conversation URL:

```powershell
& $exe --lite-chat-wake-set $url 25 'Continue this conversation based on its project plan and latest checkpoint.'
& $exe --lite-chat-wake-off $url
& $exe --lite-chat-wake-now $url
& $exe --lite-chat-notes-append $url 'New instructions for this conversation'
& $exe --lite-chat-wake-selftest
```

- `--lite-chat-wake-set`: creates or updates a single authorized chat, enables it and resets next due to now plus interval. Supported minutes 20 to 1440. Does not change the other chat schedules.
- `--lite-chat-wake-off`: pauses one chat without deleting its state or notes.
- `--lite-chat-wake-now`: makes a previously enabled chat due immediately, intended for explicit one-off tests. Not a new timer.
- `--lite-chat-notes-append`: preserves existing encrypted notes and appends a new line. The per-chat wake sender appends notes to each outgoing message. At most the first 24000 characters of notes are included, with explicit truncation notice.
- `--lite-chat-wake-selftest`: headless non-delivery test, safe to repeat.
- Explicitly validate the intended chat UUID before using these commands. Do not invent URLs or use `local-chatgpt:` pseudo-history URLs.
- Native ChatGPT scheduled Tasks are not used. Windows Task Scheduler is not created or modified by any of these commands.
- The next app-local scheduler pass handles the due chat. Source message has a unique `[OB-<receipt>]` prefix, allowing a new DIL user-bubble ACK to be distinguished from old messages. The persisted ledger records `visible_ack` when that client-side proof is observed.
- Client-side visible ACK confirms a new user bubble for the precise receipt but does not by itself confirm successful assistant response generation. If the book chat is complete, disable its local schedule rather than sending unnecessary new tasks.

## Verified real-world test

On PBOX1, 2026-10-19 17:47:53 CEST, the book test conversation received a wake in the background using a unique receipt. Local event `multi_wake_send` reported `phase=click_invoked`, `receiptConfirmed=true`, `includesNotes=true`; metadata-only DPAPI status became `visible_ack`. Main Open Browser project remained a separate enabled chat.

At 17:50, the book chat was reconfigured to a meaningful 25-minute novel-continuation prompt instead of the obsolete repeating test marker. Current book next due 18:15:01; main project next due 18:02:33. Book test notes include a marked `[NOTATKA TESTOWA]` instruction about a compass, retained for further note-delivery testing.
