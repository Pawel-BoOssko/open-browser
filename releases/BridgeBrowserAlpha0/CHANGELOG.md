# CHANGELOG

## v0.01.0-alpha.11

Conversation Trimmer integration.

Added:

- WebView2 host-side interception for `GET https://chatgpt.com/backend-api/conversation/{conversation_id}`.
- Hot-swappable module layout under `D:\temp\bridge-browser\modules\conversation-trimmer`.
- `conversation-trimmer-core-v06-port-01` module copied from the Firefox POC core algorithm.
- `keepRenderableMessages = 40`.
- Loaded-turns monitor with `maxLoadedTurns = 100`, `pollIntervalMs = 600000`, `minMsAfterReload = 600000`.
- Buttons: `Load trimmer`, `Promote trimmer`, `Trimmer status`.
- Diagnostics panel showing module status and loaded-turns monitor status.
- Trim result logging: `beforeMappingCount`, `afterMappingCount`, `bytesBefore`, `bytesAfter`, `trimApplied`, `trimPassthrough`, `trimError`.

Not added:

- Bridge Core.
- Local Executor.
- Multi-tab orchestration.
- Firefox extension architecture.
- MutationObserver-based turn counting.

## v0.01.0-alpha.10

Closed Alpha Transport Spike.

- Confirmed ChatGPT Web in WebView2.
- Confirmed stream extraction outside DOM.
- Confirmed parser by `message.id`.
- Confirmed `answer.txt`, `messages.ndjson`, and GitHub-safe export ZIP.


## v0.01.0-alpha.11-fix1

- Fixed build compatibility with WebView2 package 1.0.2957.106.
- Removed unavailable `RemoveScriptToExecuteOnDocumentCreatedAsync` call.
- Replaced unavailable header iterator method with allow-list based `Contains` / `GetHeader` copying.
- No parser changes.


## v0.01.0-alpha.11-fix2

- Disabled host-side proxy interception for ChatGPT conversation GET.
- Reason: empirical test showed both short and long conversations failed to load before trimmer counters changed.
- Moved trimming attempt into the page fetch wrapper so ChatGPT keeps its native browser request context.
- Added page-level events: `trimmer_fetch_seen`, `trimmer_fetch_trimmed`, `trimmer_fetch_passthrough`, `trimmer_fetch_error`.
- No changes to alpha.10 answer parser.


## v0.01.0-alpha.11-fix3

- Fixed compile errors introduced in fix2 by restoring the existing `MainForm` contract:
  - `ConversationTrimmerInterceptor(CoreWebView2, LogWriter, BridgeBrowserModuleManager)`;
  - `.Install()`;
  - `.Dispose()`;
  - `PageTap.Script`.
- Host conversation interceptor remains disabled.
- Trimming remains in page fetch wrapper.
- No changes to alpha.10 answer parser.


## v0.01.0-alpha.11-fix4

- UI-only fix: trimmer controls are now visible in a two-row, wrapping top panel.
- Shortened button labels: `Load trim`, `Promote trim`, `Trim status`.
- Fixed accidental duplicate title suffix.
- No parser changes.
- No trimmer algorithm changes.
- Host interceptor remains disabled; trimming remains in page fetch wrapper.


## v0.01.0-alpha.12

[REMOVED IN LATER ALPHA] Local Helper scope.


## v0.01.0-alpha.12-fix1

- Fixed BAT label failure by removing `call :section` subroutines from installer BATs.
- No helper code changes.
- No browser code changes.


## v0.01.0-alpha.12-fix2

Fixes after first helper install test:

1. `D:\temp\bridge-browser` itself is now allowed as the workspace root, not only its subdirectories.
2. Helper command envelope NDJSON is written as UTF-8 without BOM.
3. BAT files set UTF-8 console mode with `chcp 65001 > nul`.
4. BAT files set `DOTNET_CLI_UI_LANGUAGE=en` to reduce mojibake in `dotnet` output.
5. Main install/build BAT does not auto-start the browser anymore.
6. Added explicit `run_browser.bat`.
7. Added `smoke_helper_alpha12_fix2.bat`.

No browser logic changes. No trimmer changes.


## v0.01.0-alpha.13

[REMOVED IN LATER ALPHA] Browser ↔ Local Helper command bus.
