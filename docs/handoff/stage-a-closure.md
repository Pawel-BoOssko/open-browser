# Stage A Closure: UI Shell ↔ Tab Runtime Boundary

Assessed: 2026-05-16
Commit: a47fd7c

## Files Inspected

- `releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/MainForm.cs`
- `releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/MainForm.Ui.cs`
- `releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/BrowserTabRuntime.cs`
- `releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/WebViewMessageHandler.cs`
- `releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/DiagnosticsController.cs`
- `releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/ResponseExtractor.cs`
- `releases/BridgeBrowserAlpha0/src/BridgeBrowserAlpha0/OpenBridgeProtocol/OpenBridgeEnvelopeObserver.cs`

## Verdict: Stage A is CLOSED

No code changes were required. The boundary is already clean.

## Responsibility Map

| Component | Owns | Clean? |
|---|---|---|
| `MainForm.cs` | Orchestration, dependency wiring, form lifecycle, button → delegate dispatch | Yes |
| `MainForm.Ui.cs` | All WinForms control construction, layout (`InitializeUi`), `ToggleWebVisibility`, `SetStatus`, `SetDiagnostics`, `OpenFolder` | Yes |
| `BrowserTabRuntime` | WebView2 settings, event subscriptions (Navigation*, ProcessFailed, WebMessageReceived), PageTap script injection, module initialization, `NetworkLogger` creation, `StartNewRun`, initial navigation | Yes |
| `WebViewMessageHandler` | `HandleWebMessage` — JSON parse, event dispatch, delta forwarding to `ResponseExtractor`, diagnostics refresh trigger | Yes |
| `DiagnosticsController` | `RefreshAsync` — trimmer status query, log writes, UI text update via callback | Yes |
| `ResponseExtractor` | SSE buffer, JSON delta assembly, assistant message framing, `answer.txt` / `raw.ndjson` / `messages.ndjson` output, extractor diagnostics | Yes |
| `OpenBridgeEnvelopeObserver` | Passive: calls `OpenBridgeEnvelopeParser.Parse`, logs detected envelopes, logs parse errors. No execution, no routing | Yes |

## What Was Verified

1. **MainForm.cs** contains only orchestration: it creates dependencies, wires button clicks to delegates, handles Load/FormClosing, and calls `BrowserTabRuntime.InitializeAsync()`. The `CoreWebView2Environment.CreateAsync` call is infrastructure setup required because `_webView` is a WinForms control owned by the form. The initialized `CoreWebView2` is passed to `BrowserTabRuntime` — the form does not configure it directly.

2. **MainForm.Ui.cs** owns every control declaration, `InitializeUi` layout, and UI helper methods (`SetStatus`, `SetDiagnostics`, `ToggleWebVisibility`, `OpenFolder`). No UI construction code exists in any other file.

3. **BrowserTabRuntime** owns the full WebView2 initialization sequence: settings, event handlers, PageTap injection, module init, network logger, and initial navigation. It has no reference to WinForms controls — only `CoreWebView2`.

4. **WebViewMessageHandler** handles all `WebMessageReceived` events. It extracts chunk/delta data and forwards to `ResponseExtractor`. It triggers diagnostics refresh on monitor events. No WebView message handling code exists elsewhere.

5. **DiagnosticsController** encapsulates all diagnostics refresh logic. It polls `BridgeBrowserModuleManager` for trimmer status and updates the UI via a callback. The timer that drives it lives in `MainForm.cs` as pure scheduling.

6. **ResponseExtractor** owns the full stream/delta assembly pipeline: raw buffering, SSE parsing, JSON delta extraction, assistant message framing, file output. Its `Finish()` method calls `OpenBridgeEnvelopeObserver.Observe()` — the only integration point between extraction and protocol observation.

7. **OpenBridgeEnvelopeObserver** is purely passive. It receives assembled response text, calls the parser, and logs results. It does not route, execute, or store commands.

## What Does Not Exist (Intentionally)

- No Host class, interface, or skeleton
- No command routing, executor dispatch, or executor interfaces
- No tab registry, instance registry, or conversation_id mapping
- No cross-tab communication primitives (`XTB_*`)
- No watchdog or timeout monitoring infrastructure
- No state machine for tab lifecycle
- No `BrowserTabRuntime` state/status fields beyond existing functional fields
- No event bus or pub/sub mechanism
- No `BridgeBrowserHelper` or any reference to the old helper

## Safety Grep Results

- `BridgeBrowserHelper|HelperCommandBus|__BRIDGE_BROWSER_HELPER__|helper_command|requests_runtime|HelperExe`: **zero matches**
- `watchdog|tab registry|executor router|XTB_|FS_|SH_|PY_`: only `_answer.txt` / `_messages.ndjson` filename fragments in `RedactedExport.cs` (non-runtime file path construction)

## Build & Test

- `dotnet build -c Release`: 0 errors (MSB3277 WindowsBase version conflict warning — known, harmless, from WebView2 WPF dependency)
- `dotnet run --project tests/OpenBridgeProtocolSmoke -c Release`: all 14 tests PASSED

## Why Host Is Not Next As Code

Host requires a design document before any code is written. The architecture decisions document defines Host responsibilities (routing, registry, executor communication, diagnostics, system status, controlled responses) but does not specify:

- The exact Host class/interface structure
- How `OpenBridgeEnvelopeObserver` hands off to Host
- Internal command dispatch model
- `HST_HELP` / `HST_STATUS` / `HST_CAPABILITIES` implementation details
- Error and timeout contract between Host and tab runtime
- How Host communicates results back to the conversation

These must be resolved in a Host design document (Stage B) before writing a single line of Host code.

## What Must Be Designed Before Host Code Starts

1. Host class structure and namespace location
2. Integration point: how observer parse results reach Host
3. Command dispatch table / routing model
4. `HST_HELP` implementation (loading from `docs/environment/`)
5. `HST_STATUS` initial implementation
6. `HST_CAPABILITIES` initial implementation
7. Error response format (technical result object + neutral wrapper)
8. Timeout contract between tab runtime and Host
9. Response delivery path: Host → tab runtime → WebView → LLM
10. Logging and event log integration points

## Next Recommended Step

**Stage B: Create Host design document only. No Host code yet.**

Move to `docs/architecture/host-design.md` describing the Host's internal structure, the observer→Host handoff, command dispatch, and system command implementations. All design, zero implementation.
