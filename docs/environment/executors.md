# Executors

## Active (isolated Host path — not runtime-connected)
- **ClaudeCodeExecutor**: Dedicated CC command executor. Two modes:
  - **DryRun** (default): Echoes the prompt, no process launched. Safe for testing.
  - **Process**: Launches a configurable local process via System.Diagnostics.Process. Available through local manual probe and ignored local config only. Not enabled from runtime.

## Planned
- HST: Host metacommands (HELP, STATUS, CAPABILITIES)
- FS: Filesystem operations (read, write, list, copy, move, mkdir, delete, stat)
- SH: Shell commands (run system command and return stdout/stderr/exit code)
- DIA: Diagnostics
- PY: Python execution (to be considered)

## Status notes

- ClaudeCodeExecutor exists and is tested with both dry-run and real Claude Code (via local manual probe only).
- Real Process mode is available only through the local manual probe (`tools/local/`) with ignored local config (`config/local/`).
- **Runtime execution of CC commands is now connected through DryRun-only approval UI.** When ResponseExtractor detects a valid CC envelope, a pending command panel appears in the WinForms UI. The operator must click Approve or Reject. Approved commands execute through OpenBridgeHost in DryRun mode only — no real Claude Code is launched.
- Process mode from runtime requires a separate, explicit approval. It has not been implemented.
- Result delivery to the LLM conversation is not implemented. Results are shown in the UI only.
