# Local Claude Code Executor Probe

Status: manual probe only — not runtime integration
Created: 2026-05-16

## Purpose

A local-only manual probe that invokes real Claude Code through the existing `OpenBridgeHost` and `ClaudeCodeExecutor`, using the git-ignored local configuration. This proves the full executor path end-to-end without connecting to the WebView runtime.

## What this is

- A standalone console project in `tools/local/ClaudeExecutorProbe/`
- A PowerShell wrapper script in `tools/local/RunClaudeExecutorProbe.ps1`
- Both are git-ignored — they exist only on the local machine
- The probe sends a single short prompt and prints the structured result
- The probe uses `System.Diagnostics.Process` through the existing `ClaudeCodeExecutor` in Process mode

## What this is NOT

- Not runtime integration — no connection to WebView, ResponseExtractor, or OpenBridgeEnvelopeObserver
- Not an automated test — will not run in CI or smoke test suites
- Not a daemon or service — runs once and exits
- Not a replacement for the dry-run smoke tests

## Prerequisites

- `config/local/claude-code-executor.local.json` must exist and be configured with `Mode: "Process"` and a valid `ExecutablePath` (e.g., `"claude"`)
- Claude Code or a compatible CLI must be installed and on PATH
- The local config must not contain secrets, API keys, or dangerous flags

## Ignored local paths

| Path | Purpose | Git status |
|---|---|---|
| `config/local/*.local.json` | ClaudeCodeExecutor options (mode, executable, args) | Ignored |
| `tools/local/` | Local probe project and scripts | Ignored |

## Committed reference files

| Path | Purpose |
|---|---|
| `config/examples/claude-code-executor.example.json` | Safe example with DryRun defaults |
| `docs/handoff/local-claude-probe.md` | This document |

## How to run

### Option 1: PowerShell wrapper (simplest)

From the project root:

```
pwsh -File tools/local/RunClaudeExecutorProbe.ps1
```

### Option 2: dotnet run directly

```
dotnet run --project tools/local/ClaudeExecutorProbe/ClaudeExecutorProbe.csproj -c Release
```

## Expected output

```
Loading config: .../config/local/claude-code-executor.local.json
Mode: Process
Executable: claude
Args template: -p "{prompt}"
Timeout: 720000ms
Max output: 50000 chars

Sending request to ClaudeCodeExecutor...
  Prompt: Say hello from OpenBridge. Return one short sentence.

--- Result ---
  Status:       Ok
  OperationId:  abc123def456
  DurationMs:   3456
  ExitCode:     0
  ErrorCode:    -
  Message:      Process completed successfully.

--- stdout preview ---
[Claude Code's response text here]
```

## Safety constraints

- The probe does not pass `--dangerously-skip-permissions`
- The probe does not run `git push`
- The probe does not install packages
- The probe works only within `D:\projects\open-browser`
- Timeout is enforced at 720000 ms (12 minutes) by default for coding-agent work
- Output is truncated at 50,000 characters

## WebView integration status

No WebView integration exists for the executor result path. Host returns structured results. The probe prints them to console. Future work: design how Host results flow back through tab runtime to the LLM conversation.
