# Runtime Approval Manual Test

Status: manual validation guide
Date: 2026-05-16

## Purpose

This document provides manual validation steps for the DryRun-only runtime command approval UI. It describes how to verify that detected CC envelopes trigger the approval panel, that Approve and Reject work correctly, and that no real Claude Code process is launched from runtime.

## Prerequisites

- Open Bridge browser application built in Release config
- The app navigates to chatgpt.com automatically
- No local config or Process mode is required

## How to run Open Browser

```
dotnet build releases\BridgeBrowserAlpha0\src\BridgeBrowserAlpha0\BridgeBrowserAlpha0.csproj -c Release
dotnet run --project releases\BridgeBrowserAlpha0\src\BridgeBrowserAlpha0\BridgeBrowserAlpha0.csproj -c Release
```

Or run the built executable:

```
releases\BridgeBrowserAlpha0\src\BridgeBrowserAlpha0\bin\Release\net8.0-windows\BridgeBrowserAlpha0.exe
```

## Sample CC envelopes for testing

Paste these into a ChatGPT message and send them. After the response completes (stream ends), the approval panel should appear.

### Valid CC envelope (should trigger approval panel)

```
@@OPENBRIDGE_EXEC_BEGIN@@
{
  "version": "001",
  "command": "CC",
  "payload": "Say hello from the OpenBridge runtime approval panel. Return one short sentence."
}
@@OPENBRIDGE_EXEC_END@@
```

### Unsupported command envelope (should NOT trigger approval)

```
@@OPENBRIDGE_EXEC_BEGIN@@
{
  "version": "001",
  "command": "FS",
  "payload": "read README.md"
}
@@OPENBRIDGE_EXEC_END@@
```

### Invalid envelope (should NOT trigger approval)

```
@@OPENBRIDGE_EXEC_BEGIN@@
{
  "version": "001"
}
@@OPENBRIDGE_EXEC_END@@
```

## Expected UI behavior

### Valid CC envelope detected

1. The bot responds with text that includes the envelope markers
2. After the response completes (stream ends), a panel appears at the bottom of the window
3. Panel title: "Pending CC Command — Operator Approval Required"
4. Panel shows:
   - Command: CC
   - Prompt preview (truncated if >1000 chars) with original length
   - Timeout: 720000ms
   - Mode: DryRun
   - Warning: "DryRun only. No Claude Code process will be launched from runtime."
5. Two buttons: "Approve (DryRun)" (green) and "Reject"
6. "Copy details" button copies command info to clipboard without exposing any payload64 content

### Approve button (DryRun)

1. Click "Approve (DryRun)"
2. Result area shows "Executing..." briefly
3. Result area shows: `OperationId: <id> Status: Ok Duration: <ms> ExitCode: 0 Message: Dry-run completed. No Claude Code process was launched.`
4. "Copy result" button appears
5. Status bar shows "CC command OK"
6. Panel remains visible with result

### Reject button

1. Click "Reject"
2. Panel hides immediately
3. Status bar shows "CC command rejected."

### Unsupported or invalid envelope

1. No approval panel appears
2. No execution occurs

## Expected logs

Check the latest `run_*.ndjson` in `releases/BridgeBrowserAlpha0/logs/` for these events:

| Event | Description |
|---|---|
| `runtime_approval` / `pending_created` | A valid CC envelope was mapped and is awaiting approval |
| `runtime_approval` / `approval_accepted` | Operator clicked Approve |
| `runtime_approval` / `host_execution_started` | Host started DryRun execution |
| `runtime_approval` / `host_execution_finished` | Host completed with Ok/Error status |
| `runtime_approval` / `approval_rejected` | Operator clicked Reject |
| `runtime_approval` / `pending_ignored` | Second envelope ignored while first pending |
| `runtime_approval` / `map_failed` | Envelope could not be mapped to CC request |

Log entries contain `command`, `promptLength`, `mode`, `status`, `operationId`, `durationMs`, `errorCode`. They do NOT contain full prompt text or payload64 content.

## Key confirmations

- [ ] Approval panel appears for valid CC envelope after response completion
- [ ] Approval panel does NOT appear for unsupported or invalid envelopes
- [ ] Approve button executes DryRun only — result message confirms no process launched
- [ ] Reject button clears the panel and logs rejection
- [ ] Copy details copies command info without payload64 content
- [ ] Copy result copies result info with operation_id and duration
- [ ] Second pending command is rejected while first one awaits approval
- [ ] No real Claude Code process is launched from runtime
- [ ] Result is shown in the UI panel only — no WebView injection
- [ ] Log events are written to the run log

## What this is NOT testing

- Process mode from runtime (not implemented, requires separate approval)
- WebView result injection (not implemented)
- Automatic execution (not implemented, requires explicit button click)
- Cross-tab, registry, watchdog, state machine (not implemented)
