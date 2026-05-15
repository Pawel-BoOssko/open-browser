# CASE NOTES: Bridge Browser Alpha 11

## Context

The earlier Alpha Transport Spike proved that Bridge Browser can run ChatGPT Web in WebView2, intercept response streams, reconstruct assistant messages outside the DOM, and export GitHub-safe diagnostic packages.

Alpha 11 begins the next practical line: reducing ChatGPT frontend load for very long conversations by trimming the conversation mapping response before it reaches the frontend.

## Input from Firefox POC

The Firefox POC proved that trimming the response for:

```text
GET https://chatgpt.com/backend-api/conversation/{conversation_id}
```

can reduce a large mapping while preserving usable conversation continuity.

Observed results:

```text
4893 nodes -> 173 nodes
about 9.9 MB -> about 324 KB
trimError = 0
```

## Design decision

Do not port the Firefox extension architecture.

Port only:

```text
mapping trim algorithm
current_node preservation
parent/children repair
before/after diagnostics
loaded-turns monitor
hot-swappable module model
```

## Alpha 11 implementation

The app intercepts ChatGPT conversation load requests in the WebView2 host, forwards the authenticated request using the original request headers, receives the JSON response, sends the response text into the hot-swappable JavaScript module, receives a trimmed JSON response, and returns that response to the WebView frontend.

If trimming fails, the host returns the original response body.

## Hot swap model

Stable host code:

```text
BridgeBrowserModuleManager
ConversationTrimmerInterceptor
```

Versioned module:

```text
modules/conversation-trimmer/versions/conversation-trimmer-core-v06-port-01/conversation-trimmer.js
```

Current module:

```text
modules/conversation-trimmer/current/conversation-trimmer.js
```

The UI allows loading current and promoting the latest version without restarting the browser.

## Acceptance criteria

Alpha 11 is acceptable if a long conversation load shows:

```text
trimApplied > 0
trimError = 0
beforeMappingCount > afterMappingCount
afterMappingCount is much smaller than beforeMappingCount
new message can be sent after trim
new response remains attached to the same conversation
loadedTurns monitor is active
```


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

Alpha.12 adds the minimal Local Helper as a separate .NET 8 console app.

Scope:

- `READ_FILE`
- `READ_LATEST_LOG`
- `WRITE_FILE`
- `LIST_DIR`
- `RUN_ALLOWED_BAT`
- `RUN_DOTNET_BUILD`
- `ZIP_DIR`

Every helper command writes a command envelope log from the first version:

```text
command_id
command
requested_path
resolved_path
allowed
started_at
finished_at
exit_code
stdout_len
stderr_len
```

Command envelope log:

```text
D:\temp\bridge-browser\helper\logs\helper_commands.ndjson
```

This version does not yet implement the Browser ↔ helper command bus. It provides the narrow, logged local execution layer needed for the next step.


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

Alpha.13 adds the first Browser ↔ Local Helper command bus.

Implemented:

1. Page-side JS bridge:
   ```javascript
   window.__BRIDGE_BROWSER_HELPER__.run({
     command: "LIST_DIR",
     path: "D:\\temp\\bridge-browser"
   })
   ```

2. Host-side command bus:
   - receives `helper_command` via WebView2 `WebMessageReceived`;
   - writes runtime request JSON to `D:\temp\bridge-browser\helper\requests_runtime`;
   - executes `BridgeBrowserHelper.exe --request <file>`;
   - captures stdout/stderr/exit code;
   - writes response JSON;
   - returns result to the page promise;
   - logs `helper_command_received`, `helper_command_completed`, `helper_command_failed`.

3. UI smoke button:
   ```text
   Helper smoke
   ```

4. Helper command envelope log remains the source of truth:
   ```text
   D:\temp\bridge-browser\helper\logs\helper_commands.ndjson
   ```

This version does not yet parse commands from the ChatGPT conversation text. It establishes the technical command bus needed for the next step.
