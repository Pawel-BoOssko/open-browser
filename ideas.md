<!-- LEGACY_DOCUMENT_NOTICE -->
> Legacy document notice: this is an older documentation file. Its current operational content has been migrated or superseded by: docs/feedback/field-notes_new.md.
>
> Before using this file as source of truth, read: docs/agent/READ_BEFORE_WORK_new.md.
>
> Treat this file as historical or secondary unless the new documentation explicitly points back to it as an active operational reference.
<!-- /LEGACY_DOCUMENT_NOTICE -->

# Open Browser — Ideas

## Observability & diagnostics
- Download history panel — list with timestamps, filenames, sizes
- Filesystem watcher on downloads — new files visible immediately, no polling
- Conversation export — one-click JSON dump of current conversation

## Loop robustness
- Auto-retry for PS/PY commands — queue and retry once on timeout or WebView not ready
- Heartbeat watchdog — detect hung page or WebView crash, auto-reload, resume last URL
- Command chaining — multiple @@OPENBRIDGE_EXEC_BEGIN@@ blocks executed sequentially

## Quality of life
- Download type filter — whitelist/blacklist extensions (e.g. only .csv, .png)
- Clipboard bridge — HST_CLIPBOARD command to read/write Windows clipboard
- System tray minimize — keep bridge running without open window

## Multi-model / multi-session
- Tabs — multiple WebView2 instances in one window (different conversations, different providers)
- Provider abstraction — generalize PageTap for Claude, Gemini, not just ChatGPT

## New executors
- FS executor — filesystem operations
- SH executor — shell (bash/cmd)
- Cross-tab executor — inter-tab communication

## External tool integrations
- Gmail — search, read, send (Google OAuth)
- Google Calendar — read, create events, manage invitations
- Google Drive — read/write files, publish generated artifacts
- Outlook / Microsoft 365 — email, calendar, OneDrive, Teams (Microsoft Graph)
- Slack — send messages, monitor channels
- Discord — bot for messages and channel monitoring
- Notion — read and update pages/databases
- Obsidian — local markdown vault, create and search notes
- Apify — trigger actors, read datasets, manage scraping runs
- Supabase — REST queries to project databases
- GitHub Issues & Releases — create issues, comments, release notes

## UI / injection
- DOM injection — direct DOM selectors instead of textContent
- UI selector fallbacks — more robust element targeting
- Humanizer — linguistic response variants
- Browser form automation — fill forms, collect data (Playwright or WebView)
