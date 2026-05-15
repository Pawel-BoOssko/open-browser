# Bridge Browser v0.01.0-alpha.11

Alpha 11 adds the first ChatGPT Conversation Trimmer integration to the existing Bridge Browser alpha line.

This is not a new browser project. It extends the existing WebView2 Bridge Browser with a hot-swappable conversation-trimmer module and host-side interception of ChatGPT conversation load responses.

## Goal

Trim large ChatGPT conversation JSON responses before they reach the ChatGPT frontend, so the visible page loads a much smaller active conversation mapping while preserving the same conversation and current node.

Confirmed Firefox POC result being ported:

```text
4893 nodes -> 173 nodes
about 9.9 MB -> about 324 KB
trimError for the target request: 0
```

Bridge Browser target parameters:

```text
keepRenderableMessages = 40
maxLoadedTurns = 100
pollIntervalMs = 600000
```

## What alpha.11 adds

1. Host-side WebView2 interception for:

```text
GET https://chatgpt.com/backend-api/conversation/{conversation_id}
```

2. Hot-swappable trimmer module:

```text
D:\temp\bridge-browser\modules\conversation-trimmer\versions\conversation-trimmer-core-v06-port-01\conversation-trimmer.js
D:\temp\bridge-browser\modules\conversation-trimmer\current\conversation-trimmer.js
```

3. Module runtime loading:

```text
Load trimmer
Promote trimmer
Trimmer status
```

4. Trim diagnostics in logs and panel:

```text
trimmer version
keepRenderableMessages
beforeMappingCount
afterMappingCount
bytesBefore
bytesAfter
trimApplied
trimPassthrough
trimError
loadedTurns
maxLoadedTurns
refreshPending
lastRefreshAt
```

5. Loaded-turns monitor without MutationObserver:

```text
maxLoadedTurns = 100
pollIntervalMs = 600000
minMsAfterReload = 600000
```

The monitor counts:

```text
[data-testid^="conversation-turn-"]
```

If turns exceed 100:

```text
idle -> reload
not idle -> refreshPending = true
cooldown -> refreshPending = true
```

## How to run

Run:

```bat
patch_install_build_run.bat
```

The BAT:

1. copies the patch to `D:\temp\bridge-browser\releases\BridgeBrowserAlpha0`;
2. copies hot-swappable modules to `D:\temp\bridge-browser\modules`;
3. builds the .NET 8 WinForms/WebView2 project;
4. starts Bridge Browser;
5. waits until it is closed;
6. writes full command output to:

```text
D:\temp\bridge-browser\logs\patch_alpha11_trimmer_install_build_run.log
```

## Acceptance test

1. Start Bridge Browser alpha.11.
2. Open a long ChatGPT conversation.
3. Watch the diagnostics panel.
4. Confirm that the trimmer reports:

```text
version = conversation-trimmer-core-v06-port-01
keepRenderableMessages = 40
beforeMappingCount > afterMappingCount
afterMappingCount much smaller than beforeMappingCount
trimApplied > 0
trimError = 0
```

5. Send a message after the trimmed conversation loads.
6. Confirm that ChatGPT appends the new response to the same conversation.
7. Confirm loaded-turns monitor fields are visible:

```text
loadedTurns
maxLoadedTurns
refreshPending
lastRefreshAt
```

## Safety

Logs may contain conversation content. Do not publish raw logs.

The GitHub-safe export remains separate and must not include:

```text
raw stream chunks
cookies
authorization headers
session tokens
sentinel/proof/verify values
WebView profile data
```

## Scope limits

Alpha 11 does not add:

```text
Bridge Core
Local Executor
multi-tab routing
ChatGPT <-> DeepSeek loop
workspace system
Firefox extension architecture
MutationObserver-based monitoring
local full conversation archive
custom old-history rendering
```


## alpha.11-fix1

This fix addresses build failures against the currently used WebView2 .NET package. It does not change the message parser or the alpha.10 transport spike result.


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
